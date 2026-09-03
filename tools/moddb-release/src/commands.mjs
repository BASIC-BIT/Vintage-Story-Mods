// The six broker commands, composed from the capability modules. Every
// dependency is injected so tests run against fakes; cli.mjs supplies the
// production set. Each command returns a safe result envelope built field by
// field, or throws a BrokerError. Raw cookies live only in local variables
// that are nulled on the way out.
import { ACCOUNT_SECRET_ID, SESSION_COOKIE_NAME } from "./config.mjs";
import { BrokerError, safeFailure, safeResult } from "./contracts.mjs";
import { createAccountAdminStore, createPublisherStore, createRenewalStore } from "./secret-store.mjs";
import { buildSessionCandidate, getEffectiveExpiry } from "./session-schema.mjs";
import { SESSION_COOKIE, ensureSession } from "./session-service.mjs";

const RECAPTCHA_LINE = "Complete the reCAPTCHA in the Chrome window.\n";
const fail = (code, message = code) => new BrokerError(code, message);
const CTRL_C = String.fromCharCode(3);
const DEL = String.fromCharCode(127);
const EM_DASH = String.fromCharCode(0x2014); // U+2014, banned from public release copy

// Raw-mode line reader: renders only the prompt and a final newline, never
// the typed characters. Ctrl-C rejects. Terminal state is restored on every
// exit path.
export function readMaskedLine(prompt, { stdin, stdout }) {
  return new Promise((resolve, reject) => {
    const raw = typeof stdin.setRawMode === "function" && stdin.isTTY;
    const chars = [];
    const finish = (settle, value) => {
      stdin.off("data", onData);
      if (raw) stdin.setRawMode(false);
      stdin.pause();
      stdout.write("\n");
      chars.length = 0;
      settle(value);
    };
    const onData = (chunk) => {
      for (const ch of String(chunk)) {
        if (ch === CTRL_C) return finish(reject, fail("PROMPT_CANCELLED"));
        if (ch === "\r" || ch === "\n") return finish(resolve, chars.join(""));
        if (ch === DEL || ch === "\b") chars.pop();
        else chars.push(ch);
      }
    };
    stdout.write(prompt);
    if (raw) stdin.setRawMode(true);
    stdin.setEncoding?.("utf8");
    stdin.on("data", onData);
    stdin.resume();
  });
}

async function readCurrentOrNull(store) {
  try {
    return await store.readCurrentSession();
  } catch (error) {
    if (error?.code === "SESSION_SECRET_EMPTY") return null;
    throw error;
  }
}

// ensureSession outcome -> envelope (cookie stripped) or null when usable.
function sessionEnvelope(outcome) {
  if (outcome.status === "renewal-required") return safeFailure("renewal-required", outcome.reason);
  if (outcome.status === "approval-required") return safeFailure("approval-required", outcome.reason);
  const data = { versionId: outcome.versionId };
  if (outcome.status === "renewed") data.previousVersionId = outcome.previousVersionId;
  data.validatedAccount = outcome.validatedAccount;
  data.effectiveExpiry = outcome.effectiveExpiry;
  return safeResult(outcome.status, data);
}

export function createCommands(deps) {
  const { secretsClient, stdin, stdout, stderr, modDbFactory, clock } = deps;
  const runtime = () => ({ interactiveWindows: deps.platform === "win32" && deps.isTTY === true && deps.env.GITHUB_ACTIONS !== "true" });
  const prompt = (text) => deps.readMaskedLine(text, { stdin, stdout });
  const onHumanActionRequired = () => stderr.write(RECAPTCHA_LINE);

  const acquire = (purpose, expectedAccount) => {
    const rt = runtime();
    return ensureSession({
      purpose,
      expectedAccount,
      runtime: rt,
      renewalStore: rt.interactiveWindows ? createRenewalStore(secretsClient) : undefined,
      publisherStore: createPublisherStore(secretsClient),
      browserRenewal: deps.browserRenewal,
      modDbFactory,
      clock,
      onHumanActionRequired,
    });
  };

  // Local evidence first: nothing here touches AWS or ModDB.
  function inspectLocal({ zip, changelog, expectedModIdentifier, expectedVersion, expectedSha256 }) {
    let changelogHtml;
    try {
      changelogHtml = deps.readFile(changelog, "utf8");
    } catch {
      throw fail("CHANGELOG_NOT_FOUND", "changelog file could not be read");
    }
    if (typeof changelogHtml !== "string" || changelogHtml.trim() === "" || changelogHtml.includes(EM_DASH)) {
      throw fail("CHANGELOG_INVALID", "changelog must be nonblank and free of U+2014");
    }
    const artifact = deps.inspectArtifact(zip, { modIdentifier: expectedModIdentifier, version: expectedVersion, sha256: expectedSha256 });
    return { changelogHtml, artifact };
  }

  return {
    async accountSet() {
      if (deps.isTTY !== true) throw fail("TTY_REQUIRED", "account set needs an interactive terminal");
      const email = await prompt("ModDB account email: ");
      let password = await prompt("Password: ");
      let confirmation = await prompt("Confirm password: ");
      try {
        if (password !== confirmation) throw fail("PASSWORD_MISMATCH", "passwords did not match");
        const { versionId } = await createAccountAdminStore(secretsClient).putAccountLogin({ email, password });
        return safeResult("imported", { secretId: ACCOUNT_SECRET_ID, versionId });
      } finally {
        password = null;
        confirmation = null;
      }
    },

    async sessionStatus() {
      const outcome = await ensureSession({ purpose: "status", publisherStore: createPublisherStore(secretsClient), modDbFactory, clock });
      return sessionEnvelope(outcome);
    },

    async sessionRenew({ expectedAccount }) {
      if (!runtime().interactiveWindows) throw fail("INTERACTIVE_WINDOWS_REQUIRED", "renewal needs an interactive Windows terminal outside GitHub Actions");
      return sessionEnvelope(await acquire("renew", expectedAccount));
    },

    async sessionImportWincred({ expectedAccount, finalizeVersion }) {
      if (deps.platform !== "win32") throw fail("WINCRED_UNSUPPORTED_PLATFORM", "wincred requires windows");
      const store = createRenewalStore(secretsClient);

      if (finalizeVersion !== undefined) {
        const { session, versionId } = await store.readCurrentSession();
        if (versionId !== finalizeVersion) throw fail("SESSION_VERSION_MISMATCH", "AWSCURRENT is not the version being finalized");
        await modDbFactory({ cookieValue: session.cookieValue }).validateAccount(session.validatedAccount);
        await deps.deleteWinCred();
        return safeResult("finalized", { versionId, validatedAccount: session.validatedAccount, winCredDeleted: true });
      }

      const current = await readCurrentOrNull(store);
      const originalCurrentVersionId = current?.versionId ?? null;
      let cookieValue = await deps.readWinCred();
      try {
        const candidate = buildSessionCandidate({ cookieName: SESSION_COOKIE_NAME, cookieValue, observedCookieExpiresAt: null, validatedAccount: expectedAccount, now: clock() });
        const { versionId } = await store.putPendingSession(candidate);
        const modDb = modDbFactory({ cookieValue });
        await modDb.completeLoginBridge(); // harmless when ModDB already knows the cookie
        await modDb.validateAccount(expectedAccount);
        await store.promoteSession({ candidateVersionId: versionId, originalCurrentVersionId });
        const promoted = await store.readCurrentSession();
        if (promoted.versionId !== versionId) throw fail("SESSION_PROMOTION_CONFLICT");
        return safeResult("imported", {
          versionId,
          previousVersionId: originalCurrentVersionId,
          validatedAccount: expectedAccount,
          effectiveExpiry: getEffectiveExpiry(promoted.session).toISOString(),
        });
      } finally {
        cookieValue = null;
      }
    },

    async releasePrepare(options) {
      const { artifact } = inspectLocal(options);
      const session = await acquire("prepare");
      if (session.status !== "valid" && session.status !== "renewed") return sessionEnvelope(session);
      const { modId, expectedModIdentifier, expectedVersion, compatibleVersions } = options;
      const staged = await modDbFactory({ cookieValue: session[SESSION_COOKIE] }).prepareRelease({ modId, artifact, expectedModIdentifier, expectedVersion });
      return safeResult("prepared", {
        fileId: staged.fileId,
        modIdentifier: staged.modIdentifier,
        version: staged.version,
        fileName: artifact.fileName,
        byteSize: artifact.byteSize,
        entryCount: artifact.entryCount,
        sha256: artifact.sha256,
        sessionVersionId: session.versionId,
        sessionStatus: session.status,
        compatibleVersions: [...compatibleVersions],
      });
    },

    async releasePublish(options) {
      const { artifact, changelogHtml } = inspectLocal(options);
      const session = await acquire("publish");
      if (session.status !== "valid" && session.status !== "renewed") return sessionEnvelope(session);
      const { modId, expectedModIdentifier, expectedVersion, expectedFileId, compatibleVersions } = options;
      const modDb = modDbFactory({ cookieValue: session[SESSION_COOKIE] });

      let saved = { assetId: null, releaseUrl: null };
      try {
        saved = await modDb.publishRelease({ modId, artifact, expectedModIdentifier, expectedVersion, expectedFileId, changelogHtml, compatibleVersions });
      } catch (error) {
        // The save may have landed; only continue when ModDB already serves the version publicly.
        if (error?.code !== "MODDB_PUBLISH_INDETERMINATE") throw error;
        const { published } = await modDb.readPublicState({ modId, expectedVersion });
        if (!published) throw error;
      }
      const verified = await modDb.verifyPublishedArtifact({ modId, expectedModIdentifier, expectedVersion, expectedSha256: artifact.sha256, compatibleVersions });
      return safeResult("published", {
        fileId: expectedFileId,
        assetId: saved.assetId,
        releaseUrl: saved.releaseUrl,
        modIdentifier: expectedModIdentifier,
        version: expectedVersion,
        sha256: artifact.sha256,
        verifiedSha256: verified.sha256,
        downloadUrl: verified.downloadUrl,
        compatibleVersions: [...compatibleVersions],
        sessionVersionId: session.versionId,
      });
    },
  };
}
