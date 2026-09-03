import { MODDB_SESSION_DAYS } from "./config.mjs";

// Error messages name the offending field only. Values and raw JSON never
// appear because both secrets carry credentials.

const ISO_TIMESTAMP = /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d{1,3})?(?:Z|[+-]\d{2}:\d{2})$/;
const DAY_MS = 86_400_000;

const isNonblank = (value) => typeof value === "string" && value.trim() !== "";
const isTimestamp = (value) => typeof value === "string" && ISO_TIMESTAMP.test(value) && !Number.isNaN(Date.parse(value));
const isNullOrTimestamp = (value) => value === null || isTimestamp(value);

const ACCOUNT_LOGIN_FIELDS = { email: isNonblank, password: isNonblank };
const SESSION_FIELDS = {
  cookieName: isNonblank,
  cookieValue: isNonblank,
  capturedAt: isTimestamp,
  observedCookieExpiresAt: isNullOrTimestamp,
  modDbValidUntilEstimate: isTimestamp,
  validatedAt: isTimestamp,
  validatedAccount: isNonblank,
};

function parseShape(kind, fields, json) {
  let input = json;
  if (typeof json === "string") {
    try {
      input = JSON.parse(json);
    } catch {
      throw new Error(`invalid ${kind} json`);
    }
  }
  if (input === null || typeof input !== "object") throw new Error(`invalid ${kind} json`);
  if (input.schemaVersion !== 1) throw new Error(`invalid ${kind} field: schemaVersion`);
  const parsed = { schemaVersion: 1 };
  for (const [name, isValid] of Object.entries(fields)) {
    if (!isValid(input[name])) throw new Error(`invalid ${kind} field: ${name}`);
    parsed[name] = input[name];
  }
  return parsed;
}

export const parseAccountLogin = (json) => parseShape("account-login", ACCOUNT_LOGIN_FIELDS, json);
export const parseSession = (json) => parseShape("session", SESSION_FIELDS, json);

export function getEffectiveExpiry(session) {
  const estimate = new Date(session.modDbValidUntilEstimate);
  if (session.observedCookieExpiresAt === null) return estimate;
  const observed = new Date(session.observedCookieExpiresAt);
  return observed < estimate ? observed : estimate;
}

export function isExpired(session, now = new Date()) {
  return getEffectiveExpiry(session).getTime() <= now.getTime();
}

export function buildSessionCandidate({ cookieName, cookieValue, observedCookieExpiresAt, validatedAccount, now = new Date() }) {
  const nowIso = now.toISOString();
  return parseSession({
    schemaVersion: 1,
    cookieName,
    cookieValue,
    capturedAt: nowIso,
    observedCookieExpiresAt,
    modDbValidUntilEstimate: new Date(now.getTime() + MODDB_SESSION_DAYS * DAY_MS).toISOString(),
    validatedAt: nowIso,
    validatedAccount,
  });
}
