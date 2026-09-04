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

export async function readCurrentOrNull(store) {
  try {
    return await store.readCurrentSession();
  } catch (error) {
    if (error?.code === "SESSION_SECRET_EMPTY") return null;
    throw error;
  }
}

// Writes the candidate as AWSPENDING and moves AWSCURRENT onto it. On a true
// bootstrap AWS labels the first version AWSCURRENT itself, so the move is
// skipped when the re-read already shows the candidate current. The caller
// still validates the promoted session live.
export async function stageAndPromote(store, candidate, originalCurrentVersionId) {
  const { versionId: candidateVersionId } = await store.putPendingSession(candidate);
  const alreadyCurrent = originalCurrentVersionId === null && (await readCurrentOrNull(store))?.versionId === candidateVersionId;
  if (!alreadyCurrent) await store.promoteSession({ candidateVersionId, originalCurrentVersionId });
  const promoted = await store.readCurrentSession();
  if (promoted.versionId !== candidateVersionId) throw fail("SESSION_PROMOTION_CONFLICT");
  return { candidateVersionId, promoted };
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
    try {
      accountLogin = await renewalStore.readAccountLogin();
    } catch (error) {
      // Interactive, but this identity cannot read the login (publisher-only profile): same answer as the cloud.
      if (error?.code === "SECRET_READ_FAILED" || error?.code === "ACCOUNT_SECRET_EMPTY") return { status: "renewal-required", reason };
      throw error;
    }
    captured = await browserRenewal({ accountLogin, onHumanActionRequired });
    accountLogin = null;
    candidate = buildSessionCandidate({ ...captured, validatedAccount: account, now: clock() });
    captured = null;

    // Validate before AWS sees the candidate: the first version of an empty
    // secret becomes AWSCURRENT regardless of the requested stages.
    await bridgeAndValidate(candidate.cookieValue);
    const { candidateVersionId, promoted } = await stageAndPromote(renewalStore, candidate, originalCurrentVersionId);
    candidate = null;
    await validate(promoted.session.cookieValue);

    if (purpose === "publish") return { status: "approval-required", reason: "renewed-during-publish", versionId: candidateVersionId };
    return usable("renewed", promoted, { previousVersionId: originalCurrentVersionId });
  } finally {
    accountLogin = null;
    captured = null;
    candidate = null;
  }
}
