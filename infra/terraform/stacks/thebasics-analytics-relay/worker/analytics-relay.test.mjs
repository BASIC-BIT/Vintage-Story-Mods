import assert from "node:assert/strict";
import { readFileSync, readdirSync } from "node:fs";
import test, { afterEach } from "node:test";

import worker, {
  CONTRACT_REVISION,
  validatePayload,
} from "./analytics-relay.mjs";

const serverInstallId = "a".repeat(32);
const playerPseudonym = "c".repeat(64);
const originalFetch = globalThis.fetch;
const originalConsoleLog = console.log;
const sourceRoot = new URL(
  "../../../../../mods-dll/thebasics/src/",
  import.meta.url,
);
const knownDynamicExpressions = new Set([
  '"send_" + modeName',
  '"enter_" + mode.ToString().ToLowerInvariant()',
  '"set_" + mode.ToString().ToLowerInvariant()',
  '"tp" + commandName',
  '"warmup_cancelled_" + reason',
  'commandName + "_warmup_start"',
  'refusalLangKey ?? "unknown"',
  "action",
  "commandName",
  "featureAction",
  "featureName",
  "modeName",
  "nameError.ErrorCode",
  "normalizedResultCode",
  "result",
  "result.ErrorCode ?? \"warmup_failed\"",
  "surface",
]);

let upstreamCalls = [];
let logLines = [];

afterEach(() => {
  globalThis.fetch = originalFetch;
  console.log = originalConsoleLog;
  upstreamCalls = [];
  logLines = [];
});

function assertAccepted(validation, context) {
  assert.equal(validation.ok, true, `${context}: ${validation.error}`);
  assert.deepEqual(validation.rejected, [], `${context}: ${JSON.stringify(validation.rejected)}`);
  assert.ok(validation.events.length > 0, `${context}: no normalized events`);
}

function baseProperties(modVersion, consentLevel, serverSessionId) {
  const properties = {
    event_schema_version: 1,
    mod_id: "thebasics",
    mod_version: modVersion,
    game_version: "1.22.6",
    analytics_consent_level: consentLevel,
    online_player_count: 0,
  };

  if (serverSessionId) {
    properties.server_session_id = serverSessionId;
  }

  return properties;
}

function legacyBaseProperties(modVersion, consentLevel, serverSessionId) {
  const properties = baseProperties(modVersion, consentLevel, serverSessionId);
  delete properties.online_player_count;
  properties.online_player_count_bucket = "0";
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

function payload(modVersion, consentLevel, serverSessionId, configSnapshot, legacyPlayerCount = false) {
  const commonProperties = legacyPlayerCount
    ? legacyBaseProperties(modVersion, consentLevel, serverSessionId)
    : baseProperties(modVersion, consentLevel, serverSessionId);
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
    const lineStart = source.lastIndexOf("\n", searchFrom) + 1;
    const linePrefix = source.slice(lineStart, searchFrom);
    if (/\b(?:public|private|protected|internal)\b/.test(linePrefix)) {
      searchFrom += needle.length;
      continue;
    }

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

  const normalizedExpression = expression.replace(/\s+/g, " ").trim();

  const exact = normalizedExpression.match(/^"([^"]+)"$/s);
  if (exact) {
    target.add(exact[1]);
    return;
  }

  if (normalizedExpression.includes("?") && !normalizedExpression.includes("+")) {
    for (const match of normalizedExpression.matchAll(/"([^"]+)"/g)) {
      target.add(match[1]);
    }
    return;
  }

  if (knownDynamicExpressions.has(normalizedExpression)) {
    return;
  }

  throw new Error(`Unsupported analytics contract expression: ${normalizedExpression}`);
}

function currentProducerContracts() {
  const contracts = {
    action: new Set([
      "back_warmup_start", "home_warmup_start", "request_bring", "request_goto",
      "send_chat_tab", "send_normal", "send_whisper", "send_yell", "set_emote",
      "set_globalooc", "set_normal", "set_ooc", "set_whisper", "set_yell",
      "spawn_warmup_start", "stuck_warmup_start", "top_warmup_start",
    ]),
    area: new Set(),
    command_name: new Set(["tpa", "tpahere"]),
    feature_name: new Set(),
    operation: new Set([
      "enter_emote", "enter_globalooc", "enter_normal", "enter_ooc",
      "enter_whisper", "enter_yell",
    ]),
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
    severity: new Set(),
  };
  const specs = [
    ["AnalyticsService.TrackCommandUsed", ["command_name", 0, "commandName"], ["result", 2, "result"]],
    ["AnalyticsService.TrackFeatureUsed", ["feature_name", 0, "featureName"], ["action", 1, "action"], ["result", 3, "result"]],
    ["AnalyticsService.TrackFailure", ["area", 0, "area"], ["operation", 1, "operation"], ["severity", 2, "severity"], ["result", 3, "result"]],
    ["TrackConfigEditorFailure", ["area", 0, "featureName"], ["operation", 1, "action"]],
    ["TrackHomeSpawnFailure", ["command_name", 0, "commandName"], ["action", 1, "featureAction"], ["result", 2, "result"]],
    ["TrackTpaFailure", ["command_name", 0, "commandName"], ["action", 1, "action"], ["result", 2, "result"]],
    ["SendThroughPipeline", ["command_name", 1, "surface"], ["feature_name", 2, "featureName"], ["action", 3, "featureAction"], ["area", 2, "featureName"], ["operation", 1, "surface"]],
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
  const phaseOne = validatePayload(payload("5.6.0", "server", undefined, undefined, true));
  assertAccepted(phaseOne, "phase-one payload");

  const deployed = validatePayload(payload("5.9.0", "personalized", "b".repeat(32), undefined, true));
  assertAccepted(deployed, "deployed payload");

  const current = validatePayload(
    payload(
      "5.9.0",
      "personalized",
      "b".repeat(32),
      currentConfigSnapshotProperties(),
    ),
  );
  assertAccepted(current, "current payload");
});

test("producer discovery fails closed on unknown contract expressions", () => {
  assert.throws(
    () => addLiteralValues(new Set(), "BuildUnexpectedAnalyticsLabel()"),
    /Unsupported analytics contract expression/,
  );
});

test("relay accepts bounded exact online player counts and legacy buckets", () => {
  for (const onlinePlayerCount of [0, 1, 100, 10_000]) {
    const current = payloadForEvent("feature used", {
      action: "send_normal",
      feature_name: "proximity_chat",
      online_player_count: onlinePlayerCount,
      result: "success",
      success: true,
    });
    assertAccepted(validatePayload(current), `online_player_count=${onlinePlayerCount}`);
  }

  const legacy = payloadForEvent("feature used", {
    action: "send_normal",
    feature_name: "proximity_chat",
    result: "success",
    success: true,
  });
  delete legacy.events[0].properties.online_player_count;
  legacy.events[0].properties.online_player_count_bucket = "21-50";
  assertAccepted(validatePayload(legacy), "legacy online_player_count_bucket");

  for (const onlinePlayerCount of [-1, 10_001, 1.5, "5"]) {
    const invalidCount = payloadForEvent("feature used", {
      action: "send_normal",
      feature_name: "proximity_chat",
      online_player_count: onlinePlayerCount,
      result: "success",
      success: true,
    });
    assert.deepEqual(validatePayload(invalidCount).rejected, ["invalid_online_player_count"]);
  }
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
    area: (value) => payloadForEvent("mod failure", {
      area: value,
      operation: "load",
      severity: "error",
      result: "failure",
      recovered: true,
      success: false,
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
    operation: (value) => payloadForEvent("mod failure", {
      area: "config",
      operation: value,
      severity: "error",
      result: "failure",
      recovered: true,
      success: false,
    }),
    result: (value) => payloadForEvent("command used", {
      command_name: "tpa",
      result: value,
      success: false,
    }),
    severity: (value) => payloadForEvent("mod failure", {
      area: "config",
      operation: "load",
      severity: value,
      result: "failure",
      recovered: true,
      success: false,
    }),
  };

  const failures = [];
  for (const [field, values] of Object.entries(contracts)) {
    for (const value of values) {
      const result = validatePayload(fixtures[field](value));
      if (!result.ok || result.rejected.length > 0 || result.events.length === 0) {
        failures.push(`${field}=${value}: ${result.error ?? JSON.stringify(result.rejected)}`);
      }
    }
  }
  assert.deepEqual(failures, []);

  const warmup = validatePayload(payloadForEvent("feature used", {
    action: "accept_warmup_start",
    feature_name: "tpa",
    result: "success",
    success: true,
    warmup_seconds_bucket: "1-5",
  }));
  assertAccepted(warmup, "warmup_seconds_bucket");
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
  assert.match(source, /\["online_player_count"\]\s*=\s*Math\.Clamp\(GetOnlinePlayerCount\(\), 0, MaxOnlinePlayerCount\)/);
  assert.doesNotMatch(source, /\["online_player_count_bucket"\]/);

  const response = await worker.fetch(
    new Request("https://relay.example/health"),
    {},
  );
  const health = await response.json();

  assert.equal(health.ok, true);
  assert.equal(health.contract_revision, CONTRACT_REVISION);
});

test("relay forwards every event family and registered semantic labels", async () => {
  stubUpstream();
  captureLogs();

  const common = runtimeBaseProperties();
  const timestamp = new Date().toISOString();
  const events = [
    {
      name: "analytics consent changed",
      properties: {
        ...common,
        previous_consent_level: "server",
        new_consent_level: "personalized",
        personalized_analytics_requested: true,
      },
      timestamp,
    },
    {
      name: "mod failure",
      properties: {
        ...common,
        area: "analytics",
        operation: "startup_sentinel",
        severity: "warning",
        result: "update_failed",
        success: false,
        recovered: true,
        exception_type: "InvalidOperationException",
      },
      timestamp,
    },
    commandEvent("sethome"),
    {
      name: "config snapshot",
      properties: {
        ...common,
        enable_global_ooc: true,
        typing_indicator_display_mode: "Both",
        language_count_bucket: "1-5",
        home_warmup_seconds_bucket: "1-5",
        register_home_commands: true,
      },
      timestamp,
    },
    featureEvent("home-spawn", "set_home"),
    {
      name: "player session ended",
      properties: {
        ...common,
        pseudonymous_player_id: playerPseudonym,
        session_duration_bucket: "30-120m",
        session_end_reason: "disconnect",
      },
      timestamp,
    },
    {
      name: "player session started",
      properties: {
        ...common,
        pseudonymous_player_id: playerPseudonym,
      },
      timestamp,
    },
    {
      name: "server started",
      properties: {
        ...common,
        remote_feature_flags_allowed: false,
        error_telemetry_allowed: true,
        performance_telemetry_allowed: false,
        personalized_analytics_requested: true,
      },
      timestamp,
    },
    {
      name: "server stopped",
      properties: common,
      timestamp,
    },
  ];

  const response = await postBatch(runtimeEnvelope(events));

  assert.equal(response.status, 204);
  assert.equal(upstreamCalls.length, 1);
  const forwarded = upstreamBody();
  assert.deepEqual(forwarded.batch.map((event) => event.event), events.map((event) => event.name));
  for (const event of forwarded.batch) {
    assert.equal(event.properties.distinct_id, serverInstallId);
    assert.equal(event.properties.online_player_count, 0);
    assert.equal(event.properties.$geoip_disable, true);
    assert.equal(event.properties.$process_person_profile, false);
  }

  const log = lastLog();
  assert.equal(log.outcome, "accepted");
  assert.equal(log.accepted_event_count, events.length);
  assert.equal(log.rejected_event_count, 0);
  assert.equal(log.forwarded_event_count, events.length);
  assert.equal(log.upstream_status, 200);
});

test("relay rejects identifying label-shaped values and unknown properties without logging them", async () => {
  stubUpstream();
  captureLogs();

  const response = await postBatch(runtimeEnvelope([
    featureEvent("proximity_chat", "alice"),
    featureEvent("alice", "send_normal"),
    commandEvent("123456789"),
    failureEvent({ area: "alice" }),
    failureEvent({ operation: "alice" }),
    failureEvent({ severity: "alice" }),
    failureEvent({ result: "alice" }),
    featureEvent("proximity_chat", "send_normal", { chat_text: "private message content" }),
    featureEvent("proximity_chat", "send_normal", { $geoip_disable: false }),
    featureEvent("proximity_chat", "send_normal", { $process_person_profile: true }),
  ]));

  assert.equal(response.status, 400);
  assert.equal(upstreamCalls.length, 0);
  const body = await response.json();
  assert.equal(body.error, "no_valid_events");
  assert.equal(body.rejected_event_count, 10);
  assert.deepEqual(body.rejection_reasons, { invalid_string_value: 7, unknown_property: 3 });
  assert.doesNotMatch(logLines.join("\n"), /alice|123456789|private message content/);
  assert.doesNotMatch(logLines.join("\n"), new RegExp(serverInstallId));
});

test("relay rejects personalized properties without personalized consent", async () => {
  stubUpstream();
  captureLogs();

  const event = {
    name: "player session started",
    properties: {
      ...runtimeBaseProperties("server"),
      pseudonymous_player_id: playerPseudonym,
    },
    timestamp: new Date().toISOString(),
  };
  const response = await postBatch(runtimeEnvelope([event], "server"));

  assert.equal(response.status, 400);
  assert.equal(upstreamCalls.length, 0);
  assert.deepEqual(lastLog().rejection_reasons, { personalized_property_without_consent: 1 });
});

test("relay forwards valid events from a mixed batch and reports partial acceptance", async () => {
  stubUpstream();
  captureLogs();

  const response = await postBatch(runtimeEnvelope([
    featureEvent("home-spawn", "set_home"),
    featureEvent("proximity_chat", "send_normal", { player_name: "must not pass" }),
  ]));

  assert.equal(response.status, 202);
  assert.deepEqual(await response.json(), {
    ok: true,
    accepted_event_count: 1,
    rejected_event_count: 1,
    rejection_reasons: { unknown_property: 1 },
  });
  assert.equal(upstreamCalls.length, 1);
  assert.equal(upstreamBody().batch.length, 1);
  assert.equal(upstreamBody().batch[0].properties.feature_name, "home-spawn");

  const log = lastLog();
  assert.equal(log.outcome, "partially_accepted");
  assert.equal(log.accepted_event_count, 1);
  assert.equal(log.rejected_event_count, 1);
  assert.deepEqual(log.rejection_reasons, { unknown_property: 1 });
  assert.doesNotMatch(logLines.join("\n"), /must not pass/);
});

test("relay preserves request-level consent and batch-size limits", async () => {
  stubUpstream();
  captureLogs();

  const invalidConsent = await postBatch(runtimeEnvelope([
    featureEvent("proximity_chat", "send_normal"),
  ], "disabled"));
  assert.equal(invalidConsent.status, 400);
  assert.deepEqual(await invalidConsent.json(), { error: "invalid_consent_level" });

  const oversized = await postBatch(runtimeEnvelope(
    Array.from({ length: 51 }, () => featureEvent("proximity_chat", "send_normal")),
  ));
  assert.equal(oversized.status, 400);
  assert.deepEqual(await oversized.json(), { error: "invalid_event_count" });
  assert.equal(upstreamCalls.length, 0);
});

test("relay reports PostHog rejection without exposing the upstream body", async () => {
  stubUpstream(500);
  captureLogs();

  const response = await postBatch(runtimeEnvelope([
    featureEvent("proximity_chat", "send_normal", { chat_type: "normal" }),
  ]));

  assert.equal(response.status, 502);
  assert.deepEqual(await response.json(), { error: "upstream_rejected" });
  assert.equal(lastLog().outcome, "upstream_failed");
  assert.equal(lastLog().upstream_status, 500);
  assert.equal(lastLog().forwarded_event_count, 1);
});

test("relay reports PostHog connection failure without logging exception text", async () => {
  globalThis.fetch = async () => {
    throw new TypeError("connection failed with private details");
  };
  captureLogs();

  const response = await postBatch(runtimeEnvelope([
    featureEvent("proximity_chat", "send_normal", { chat_type: "normal" }),
  ]));

  assert.equal(response.status, 502);
  assert.deepEqual(await response.json(), { error: "upstream_failed" });
  assert.equal(lastLog().outcome, "upstream_failed");
  assert.equal(lastLog().request_error, "upstream_failed");
  assert.equal(lastLog().upstream_status, null);
  assert.doesNotMatch(logLines.join("\n"), /connection failed|private details/);
});

function runtimeBaseProperties(consentLevel = "personalized") {
  return baseProperties("5.9.0", consentLevel, "b".repeat(32));
}

function featureEvent(featureName, action, extraProperties = {}) {
  return {
    name: "feature used",
    properties: {
      ...runtimeBaseProperties(),
      feature_name: featureName,
      action,
      success: true,
      result: "success",
      ...extraProperties,
    },
    timestamp: new Date().toISOString(),
  };
}

function commandEvent(commandName) {
  return {
    name: "command used",
    properties: {
      ...runtimeBaseProperties(),
      command_name: commandName,
      success: true,
      result: "success",
    },
    timestamp: new Date().toISOString(),
  };
}

function failureEvent(overrides = {}) {
  return {
    name: "mod failure",
    properties: {
      ...runtimeBaseProperties(),
      area: "config",
      operation: "load",
      severity: "error",
      result: "failure",
      success: false,
      recovered: true,
      ...overrides,
    },
    timestamp: new Date().toISOString(),
  };
}

function runtimeEnvelope(events, consentLevel = "personalized") {
  return {
    source: "thebasics",
    batch_schema_version: 1,
    server_install_id: serverInstallId,
    consent_level: consentLevel,
    mod_id: "thebasics",
    mod_version: "5.9.0",
    game_version: "1.22.6",
    events,
  };
}

async function postBatch(batchPayload) {
  const body = JSON.stringify(batchPayload);
  const request = new Request("https://relay.test/v1/events/batch", {
    method: "POST",
    headers: {
      "content-type": "application/json",
      "content-length": String(new TextEncoder().encode(body).byteLength),
    },
    body,
  });

  return worker.fetch(request, {
    POSTHOG_HOST: "https://posthog.test",
    POSTHOG_PROJECT_TOKEN: "test-token",
  });
}

function stubUpstream(status = 200) {
  globalThis.fetch = async (...args) => {
    upstreamCalls.push(args);
    return new Response(null, { status });
  };
}

function upstreamBody() {
  return JSON.parse(upstreamCalls[0][1].body);
}

function captureLogs() {
  console.log = (line) => logLines.push(String(line));
}

function lastLog() {
  return JSON.parse(logLines.at(-1));
}
