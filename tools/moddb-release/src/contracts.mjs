// Result envelopes for the broker CLI. Nothing here serializes an arbitrary
// object: success data is checked field by field, failures carry only fixed
// status/reason codes, and unknown errors collapse to a constant.

export const ExitCode = Object.freeze({
  ok: 0,
  failed: 1,
  renewalRequired: 2,
  approvalRequired: 3,
});

const SUCCESS_STATUSES = new Set(["valid", "renewed", "prepared", "published", "imported", "finalized"]);

// status -> { exitCode, reasons } where reasons null means any code-shaped reason.
const FAILURES = Object.freeze({
  "renewal-required": { exitCode: ExitCode.renewalRequired, reasons: ["expired", "authentication-failed"] },
  "approval-required": { exitCode: ExitCode.approvalRequired, reasons: ["renewed-during-publish"] },
  failed: { exitCode: ExitCode.failed, reasons: null },
});

const REASON_CODE = /^[A-Za-z][A-Za-z0-9_-]{0,63}$/;
const IDENTIFIER_KEY = /^[A-Za-z_][A-Za-z0-9_]{0,63}$/;
const FORBIDDEN_KEYS = new Set([
  "password",
  "cookie",
  "cookievalue",
  "secret",
  "secretstring",
  "token",
  "sessionkey",
  "authorization",
  "setcookie",
  "email",
]);

const forbidden = (key) => new Error(`forbidden result field: ${key}`);
const normalizeKey = (key) => key.toLowerCase().replace(/[^a-z0-9]/g, "");

function assertSafeValue(value, key) {
  if (typeof value === "function") throw forbidden(key);
  if (value === null || typeof value !== "object") return;
  if (Object.getOwnPropertySymbols(value).length > 0) throw forbidden("symbol");
  if (Array.isArray(value)) {
    for (const item of value) assertSafeValue(item, key);
    return;
  }
  const proto = Object.getPrototypeOf(value);
  if (proto !== Object.prototype && proto !== null) throw forbidden(key);
  for (const [childKey, childValue] of Object.entries(value)) {
    if (FORBIDDEN_KEYS.has(normalizeKey(childKey))) throw forbidden(childKey);
    if (!IDENTIFIER_KEY.test(childKey)) throw forbidden("non-identifier");
    assertSafeValue(childValue, childKey);
  }
}

export function safeResult(status, data) {
  if (!SUCCESS_STATUSES.has(status)) throw new Error("invalid result status");
  if (data === null || typeof data !== "object" || Array.isArray(data)) throw forbidden("data");
  assertSafeValue(data, "data");
  return { ok: true, status, data };
}

export function safeFailure(status, reason) {
  if (!Object.hasOwn(FAILURES, status)) throw new Error("invalid failure status");
  const { reasons } = FAILURES[status];
  if (typeof reason !== "string" || !REASON_CODE.test(reason) || (reasons && !reasons.includes(reason))) {
    throw new Error("invalid failure reason");
  }
  return { ok: false, status, reason };
}

// Rebuilds the envelope through the validating constructors, so a tampered or
// over-populated result can never reach the stream. Exactly one line.
export function writeResult(result, stream = process.stdout) {
  const checked =
    result?.ok === true ? safeResult(result.status, result.data) : safeFailure(result?.status, result?.reason);
  stream.write(`${JSON.stringify(checked)}\n`);
}

export class BrokerError extends Error {
  constructor(code, message, { exitCode = ExitCode.failed } = {}) {
    super(message);
    this.name = "BrokerError";
    const status = Object.keys(FAILURES).find((key) => FAILURES[key].exitCode === exitCode);
    if (status === undefined) throw new Error("invalid broker exit code");
    safeFailure(status, code); // reject a bad code at the throw site, not in the catch handler
    this.code = code;
    this.status = status;
    this.exitCode = exitCode;
  }
}

export function classifyError(error) {
  if (error instanceof BrokerError) {
    return { exitCode: error.exitCode, result: safeFailure(error.status, error.code) };
  }
  return { exitCode: ExitCode.failed, result: safeFailure("failed", "unexpected-error") };
}
