import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import test from "node:test";

import worker, {
  CONTRACT_REVISION,
  validatePayload,
} from "./analytics-relay.mjs";

const serverInstallId = "a".repeat(32);

function baseProperties(modVersion, consentLevel, serverSessionId) {
  const properties = {
    event_schema_version: 1,
    mod_id: "thebasics",
    mod_version: modVersion,
    game_version: "1.22.6",
    analytics_consent_level: consentLevel,
    online_player_count_bucket: "0",
  };

  if (serverSessionId) {
    properties.server_session_id = serverSessionId;
  }

  return properties;
}

function currentConfigSnapshotProperties() {
  const source = readFileSync(
    new URL(
      "../../../../../mods-dll/thebasics/src/ModSystems/Analytics/AnalyticsService.cs",
      import.meta.url,
    ),
    "utf8",
  );
  const snapshot = source.match(
    /Track\("config snapshot", new Dictionary<string, object>\s*\{([\s\S]*?)\n\s*\}\);/,
  );

  assert.ok(snapshot, "missing TrackConfigSnapshot properties");
  const keys = [...snapshot[1].matchAll(/\["([^"]+)"\]\s*=/g)].map(
    (match) => match[1],
  );
  assert.ok(keys.length > 0, "empty TrackConfigSnapshot properties");

  return Object.fromEntries(keys.map((key) => [key, configValue(key)]));
}

function configValue(key) {
  if (key.endsWith("_bucket")) {
    return "0";
  }

  return {
    overhead_chat_bubble_mode: "RpText",
    proximity_chat_presentation_mode: "StandardRoleplay",
    typing_indicator_display_mode: "Both",
  }[key] ?? false;
}

function payload(modVersion, consentLevel, serverSessionId, configSnapshot) {
  const commonProperties = baseProperties(
    modVersion,
    consentLevel,
    serverSessionId,
  );
  const timestamp = new Date().toISOString();
  const events = [
    {
      name: "server started",
      timestamp,
      properties: {
        ...commonProperties,
        remote_feature_flags_allowed: false,
        error_telemetry_allowed: true,
        performance_telemetry_allowed: false,
        personalized_analytics_requested: consentLevel === "personalized",
      },
    },
  ];

  if (configSnapshot) {
    events.push({
      name: "config snapshot",
      timestamp,
      properties: { ...commonProperties, ...configSnapshot },
    });
  }

  return {
    source: "thebasics",
    batch_schema_version: 1,
    server_install_id: serverInstallId,
    consent_level: consentLevel,
    mod_id: "thebasics",
    mod_version: modVersion,
    game_version: "1.22.6",
    events,
  };
}

test("relay accepts phase-one 5.6 and current 5.9 payloads", () => {
  const phaseOne = validatePayload(payload("5.6.0", "server"));
  assert.equal(phaseOne.ok, true, phaseOne.error);

  const current = validatePayload(
    payload(
      "5.9.0",
      "personalized",
      "b".repeat(32),
      currentConfigSnapshotProperties(),
    ),
  );
  assert.equal(current.ok, true, current.error);
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
