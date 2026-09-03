// Decides whether the stored ModDB session is usable and, only on an
// interactive Windows desktop with the renewal capability, renews it through
// the human-assisted browser flow. Results carry the raw cookie under a
// symbol key that safeResult rejects, so only in-process callers that strip
// it deliberately can hand it to the ModDB client. Failures before promotion
// leave AWSCURRENT untouched; nothing is rolled back after promotion.
import { BrokerError } from "./contracts.mjs";
import { createModDbClient } from "./moddb-client.mjs";
import { buildSessionCandidate, getEffectiveExpiry, isExpired } from "./session-schema.mjs";
import { renewInBrowser } from "./browser-renewal.mjs";

export const SESSION_COOKIE = Symbol("sessionCookie");

const fail = (code) => new BrokerError(code, code);
const nonblank = (value) => typeof value === "string" && value.trim() !== "";

async function readCurrentOrNull(store) {
  try {
    return await store.readCurrentSession();
  } catch (error) {
    if (error?.code === "SESSION_SECRET_EMPTY") return null;
    throw error;
  }
}

const usable = (status, { session, versionId }, extra = {}) => ({
  status,
  versionId,
  ...extra,
  validatedAccount: session.validatedAccount,
  effectiveExpiry: getEffectiveExpiry(session).toISOString(),
  [SESSION_COOKIE]: session.cookieValue,
});

export async function ensureSession({
  purpose,
  expectedAccount,
  runtime,
  renewalStore,
  publisherStore,
  browserRenewal = renewInBrowser,
  modDbFactory = createModDbClient,
  clock = () => new Date(),
  onHumanActionRequired,
}) {
  const store = publisherStore ?? renewalStore;
  const current = await readCurrentOrNull(store);
  const account = expectedAccount ?? current?.session.validatedAccount;
  const validate = (cookieValue) => modDbFactory({ cookieValue }).validateAccount(account);
  // A captured cookie is unknown to ModDB until its /login bridge has run.
  const bridgeAndValidate = async (cookieValue) => {
    const modDb = modDbFactory({ cookieValue });
    await modDb.completeLoginBridge();
    await modDb.validateAccount(account);
  };

  let reason = "expired";
  if (current !== null && !isExpired(current.session, clock())) {
    try {
      await validate(current.session.cookieValue);
      return usable("valid", current);
    } catch (error) {
      if (error?.code !== "authentication-failed") throw error;
      reason = "authentication-failed";
    }
  }

  if (purpose === "status" || runtime?.interactiveWindows !== true || !renewalStore) {
    return { status: "renewal-required", reason };
  }
  if (!nonblank(account)) throw fail("MODDB_ACCOUNT_MISSING");

  const originalCurrentVersionId = current?.versionId ?? null;
  let accountLogin = null;
  let captured = null;
  let candidate = null;
  try {
    accountLogin = await renewalStore.readAccountLogin();
    captured = await browserRenewal({ accountLogin, expectedAccount: account, onHumanActionRequired });
    accountLogin = null;
    candidate = buildSessionCandidate({ ...captured, validatedAccount: account, now: clock() });
    captured = null;

    const { versionId: candidateVersionId } = await renewalStore.putPendingSession(candidate);
    await bridgeAndValidate(candidate.cookieValue);
    candidate = null;
    await renewalStore.promoteSession({ candidateVersionId, originalCurrentVersionId });

    const promoted = await renewalStore.readCurrentSession();
    if (promoted.versionId !== candidateVersionId) throw fail("SESSION_PROMOTION_CONFLICT");
    await validate(promoted.session.cookieValue);

    if (purpose === "publish") return { status: "approval-required", reason: "renewed-during-publish", versionId: candidateVersionId };
    return usable("renewed", promoted, { previousVersionId: originalCurrentVersionId });
  } finally {
    accountLogin = null;
    captured = null;
    candidate = null;
  }
}
