// Capability-shaped Secrets Manager access. Each factory returns only the
// operations its IAM identity may perform. Every failure becomes a BrokerError
// carrying a fixed code and nothing else: no AWS message, input, output, or
// secret JSON is ever attached, because both secrets hold credentials.

import { randomUUID } from "node:crypto";

import {
  GetSecretValueCommand,
  PutSecretValueCommand,
  UpdateSecretVersionStageCommand,
} from "@aws-sdk/client-secrets-manager";

import { ACCOUNT_SECRET_ID, SESSION_SECRET_ID } from "./config.mjs";
import { BrokerError } from "./contracts.mjs";
import { parseAccountLogin, parseSession } from "./session-schema.mjs";

const fail = (code) => new BrokerError(code, code);
const isNonblank = (value) => typeof value === "string" && value.trim() !== "";

// Schema errors name a field only (see session-schema.mjs); that name is
// folded into the code, so the diagnostic survives the fixed-code policy.
const SCHEMA_FIELD = /^invalid (?:session|account-login) field: ([A-Za-z]+)$/;

function validate(parse, value, code) {
  try {
    return parse(value);
  } catch (error) {
    const field = SCHEMA_FIELD.exec(String(error?.message ?? ""))?.[1];
    throw fail(field ? `${code}_${field}` : code);
  }
}

async function readCurrent(client, secretId, parse, emptyCode) {
  let output;
  try {
    output = await client.send(new GetSecretValueCommand({ SecretId: secretId, VersionStage: "AWSCURRENT" }));
  } catch (error) {
    throw fail(error?.name === "ResourceNotFoundException" ? emptyCode : "SECRET_READ_FAILED");
  }
  if (output?.SecretBinary !== undefined || typeof output?.SecretString !== "string") throw fail("SECRET_MISSING");
  if (!isNonblank(output.VersionId)) throw fail("SECRET_MALFORMED");
  return { value: validate(parse, output.SecretString, "SECRET_MALFORMED"), versionId: output.VersionId };
}

async function putVersion(client, uuid, secretId, value, stage, failCode) {
  const input = {
    SecretId: secretId,
    ClientRequestToken: uuid(),
    SecretString: JSON.stringify(value),
    VersionStages: [stage],
  };
  try {
    const output = await client.send(new PutSecretValueCommand(input));
    return { versionId: output?.VersionId };
  } catch {
    throw fail(failCode);
  }
}

const readSession = (client) => readCurrent(client, SESSION_SECRET_ID, parseSession, "SESSION_SECRET_EMPTY");

// How Secrets Manager reports a stage move whose RemoveFromVersionId no
// longer carries AWSCURRENT, i.e. someone else promoted first.
const PROMOTION_CONFLICTS = new Set(["InvalidParameterException", "InvalidRequestException", "ResourceExistsException"]);

export function createAccountAdminStore(client, { uuid = randomUUID } = {}) {
  return {
    async putAccountLogin({ email, password }) {
      const login = validate(parseAccountLogin, { schemaVersion: 1, email, password }, "ACCOUNT_INVALID");
      return putVersion(client, uuid, ACCOUNT_SECRET_ID, login, "AWSCURRENT", "ACCOUNT_WRITE_FAILED");
    },
  };
}

export function createPublisherStore(client) {
  return {
    async readCurrentSession() {
      const { value, versionId } = await readSession(client);
      return { session: value, versionId };
    },
  };
}

export function createRenewalStore(client, { uuid = randomUUID } = {}) {
  return {
    async readAccountLogin() {
      const { value } = await readCurrent(client, ACCOUNT_SECRET_ID, parseAccountLogin, "ACCOUNT_SECRET_EMPTY");
      return value;
    },
    readCurrentSession: createPublisherStore(client).readCurrentSession,
    async putPendingSession(candidate) {
      const session = validate(parseSession, candidate, "SESSION_CANDIDATE_INVALID");
      return putVersion(client, uuid, SESSION_SECRET_ID, session, "AWSPENDING", "SESSION_WRITE_FAILED");
    },
    // Fails closed: AWS rejects the move when AWSCURRENT no longer sits on
    // originalCurrentVersionId. null means the container had no version yet.
    async promoteSession({ candidateVersionId, originalCurrentVersionId }) {
      const bootstrap = originalCurrentVersionId === null;
      if (!isNonblank(candidateVersionId) || (!bootstrap && !isNonblank(originalCurrentVersionId))) {
        throw fail("SESSION_VERSION_INVALID");
      }
      const input = { SecretId: SESSION_SECRET_ID, VersionStage: "AWSCURRENT", MoveToVersionId: candidateVersionId };
      if (!bootstrap) input.RemoveFromVersionId = originalCurrentVersionId;
      try {
        await client.send(new UpdateSecretVersionStageCommand(input));
      } catch (error) {
        throw fail(PROMOTION_CONFLICTS.has(error?.name) ? "SESSION_PROMOTION_CONFLICT" : "SESSION_PROMOTION_FAILED");
      }
    },
  };
}
