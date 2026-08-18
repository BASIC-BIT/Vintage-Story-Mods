import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import test from "node:test";

import worker, {
  CONTRACT_REVISION,
  validatePayload,
} from "./analytics-relay.mjs";

const serverInstallId = "a".repeat(32);

function payload(modVersion, consentLevel, serverSessionId) {
  const properties = {
    event_schema_version: 1,
    mod_id: "thebasics",
    mod_version: modVersion,
    game_version: "1.22.6",
    analytics_consent_level: consentLevel,
    remote_feature_flags_allowed: false,
    error_telemetry_allowed: true,
    performance_telemetry_allowed: false,
    personalized_analytics_requested: consentLevel === "personalized",
    online_player_count_bucket: "0",
  };

  if (serverSessionId) {
    properties.server_session_id = serverSessionId;
  }

  return {
    source: "thebasics",
    batch_schema_version: 1,
    server_install_id: serverInstallId,
    consent_level: consentLevel,
    mod_id: "thebasics",
    mod_version: modVersion,
    game_version: "1.22.6",
    events: [
      {
        name: "server started",
        timestamp: new Date().toISOString(),
        properties,
      },
    ],
  };
}

test("relay accepts phase-one 5.6 and current 5.9 payloads", () => {
  assert.equal(validatePayload(payload("5.6.0", "server")).ok, true);
  assert.equal(
    validatePayload(payload("5.9.0", "personalized", "b".repeat(32))).ok,
    true,
  );
});

test("health exposes the relay contract required by the mod", async () => {
  const source = readFileSync(
    new URL(
      "../../../../../mods-dll/thebasics/src/ModSystems/Analytics/RelayAnalyticsSink.cs",
      import.meta.url,
    ),
    "utf8",
  );
  const requiredRevision = source.match(
    /RequiredRelayContractRevision\s*=\s*(\d+)/,
  );

  assert.ok(requiredRevision, "missing RequiredRelayContractRevision");
  assert.equal(Number(requiredRevision[1]), CONTRACT_REVISION);

  const response = await worker.fetch(
    new Request("https://relay.example/health"),
    {},
  );
  const health = await response.json();

  assert.equal(health.ok, true);
  assert.equal(health.contract_revision, CONTRACT_REVISION);
});
