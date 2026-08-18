import assert from "node:assert/strict";
import { readFileSync, readdirSync } from "node:fs";
import test from "node:test";

import worker, {
  CONTRACT_REVISION,
  validatePayload,
} from "./analytics-relay.mjs";

const serverInstallId = "a".repeat(32);
const sourceRoot = new URL(
  "../../../../../mods-dll/thebasics/src/",
  import.meta.url,
);

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

function payloadForEvent(name, properties) {
  return {
    source: "thebasics",
    batch_schema_version: 1,
    server_install_id: serverInstallId,
    consent_level: "personalized",
    mod_id: "thebasics",
    mod_version: "5.9.0",
    game_version: "1.22.6",
    events: [{
      name,
      timestamp: new Date().toISOString(),
      properties: {
        ...baseProperties("5.9.0", "personalized", "b".repeat(32)),
        ...properties,
      },
    }],
  };
}

function readSources(directory = sourceRoot) {
  return readdirSync(directory, { withFileTypes: true }).flatMap((entry) => {
    const child = new URL(entry.name + (entry.isDirectory() ? "/" : ""), directory);
    if (entry.isDirectory()) {
      return readSources(child);
    }

    return entry.name.endsWith(".cs") ? [readFileSync(child, "utf8")] : [];
  });
}

function callArguments(source, methodName) {
  const calls = [];
  const needle = `${methodName}(`;
  let searchFrom = 0;

  while ((searchFrom = source.indexOf(needle, searchFrom)) >= 0) {
    const args = [];
    let start = searchFrom + needle.length;
    let depth = 1;
    let inString = false;

    for (let index = start; index < source.length; index += 1) {
      const character = source[index];
      if (character === '"' && source[index - 1] !== "\\") {
        inString = !inString;
      } else if (!inString && "([{<".includes(character)) {
        depth += 1;
      } else if (!inString && ")]}>".includes(character)) {
        depth -= 1;
        if (depth === 0) {
          args.push(source.slice(start, index));
          searchFrom = index + 1;
          break;
        }
      } else if (!inString && character === "," && depth === 1) {
        args.push(source.slice(start, index));
        start = index + 1;
      }
    }

    calls.push(args);
  }

  return calls;
}

function argument(args, index, name) {
  const named = args.find((value) => new RegExp(`^\\s*${name}\\s*:`).test(value));
  return (named ?? args[index])?.replace(new RegExp(`^\\s*${name}\\s*:`), "").trim();
}

function addLiteralValues(target, expression) {
  if (!expression) {
    return;
  }

  const exact = expression.match(/^"([^"]+)"$/s);
  if (exact) {
    target.add(exact[1]);
    return;
  }

  if (expression.includes("?") && !expression.includes("+")) {
    for (const match of expression.matchAll(/"([^"]+)"/g)) {
      target.add(match[1]);
    }
  }
}

function currentProducerContracts() {
  const contracts = {
    action: new Set([
      "back_warmup_start", "home_warmup_start", "request_bring", "request_goto",
      "send_chat_tab", "send_normal", "send_whisper", "send_yell", "set_emote",
      "set_globalooc", "set_normal", "set_ooc", "set_whisper", "set_yell",
      "spawn_warmup_start", "stuck_warmup_start", "top_warmup_start",
    ]),
    command_name: new Set(["tpa", "tpahere"]),
    feature_name: new Set(),
    result: new Set([
      "back_expired", "back_not_set", "failure", "home-name-invalid",
      "home-name-required", "home-name-too-long", "player-required", "success",
      "teleport-unavailable", "teleport-warmup-active", "thebasics:chat-gooc-disabled",
      "thebasics:chat-ooc-disabled", "thebasics:chat-ooc-mode-no-privilege",
      "thebasics:chat-override-rp-disabled", "thebasics:chat-type-rptext-disabled",
      "unknown", "warmup_cancelled_cancelled", "warmup_cancelled_cleared",
      "warmup_cancelled_damage", "warmup_cancelled_death", "warmup_cancelled_denied",
      "warmup_cancelled_disconnect", "warmup_cancelled_interaction",
      "warmup_cancelled_movement", "warmup_cancelled_playerrejoin",
      "warmup_cancelled_timeout", "warmup_failed",
    ]),
  };
  const specs = [
    ["AnalyticsService.TrackCommandUsed", ["command_name", 0, "commandName"], ["result", 2, "result"]],
    ["AnalyticsService.TrackFeatureUsed", ["feature_name", 0, "featureName"], ["action", 1, "action"], ["result", 3, "result"]],
    ["AnalyticsService.TrackFailure", ["result", 3, "result"]],
    ["TrackHomeSpawnFailure", ["command_name", 0, "commandName"], ["action", 1, "featureAction"], ["result", 2, "result"]],
    ["TrackTpaFailure", ["command_name", 0, "commandName"], ["action", 1, "action"], ["result", 2, "result"]],
    ["SendThroughPipeline", ["command_name", 1, "surface"], ["feature_name", 2, "featureName"], ["action", 3, "featureAction"]],
  ];

  for (const source of readSources()) {
    for (const [methodName, ...fields] of specs) {
      for (const args of callArguments(source, methodName)) {
        for (const [field, index, name] of fields) {
          addLiteralValues(contracts[field], argument(args, index, name));
        }
      }
    }

    for (const match of source.matchAll(/public const string \w+ = "([^"]+)";/g)) {
      if (source.includes("class HeadshotErrorCodes")) {
        contracts.result.add(match[1]);
      }
    }
  }

  return contracts;
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

test("relay accepts current production event contracts", () => {
  const contracts = currentProducerContracts();
  const fixtures = {
    action: (value) => payloadForEvent("feature used", {
      action: value,
      feature_name: "tpa",
      result: "success",
      success: true,
    }),
    command_name: (value) => payloadForEvent("command used", {
      command_name: value,
      result: "success",
      success: true,
    }),
    feature_name: (value) => payloadForEvent("feature used", {
      action: "accept",
      feature_name: value,
      result: "success",
      success: true,
    }),
    result: (value) => payloadForEvent("command used", {
      command_name: "tpa",
      result: value,
      success: false,
    }),
  };

  for (const [field, values] of Object.entries(contracts)) {
    for (const value of values) {
      const result = validatePayload(fixtures[field](value));
      assert.equal(result.ok, true, `${field}=${value}: ${result.error}`);
    }
  }

  const warmup = validatePayload(payloadForEvent("feature used", {
    action: "accept_warmup_start",
    feature_name: "tpa",
    result: "success",
    success: true,
    warmup_seconds_bucket: "1-5",
  }));
  assert.equal(warmup.ok, true, warmup.error);
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
