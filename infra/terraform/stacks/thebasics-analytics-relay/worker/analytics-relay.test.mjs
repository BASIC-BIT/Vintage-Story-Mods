import assert from "node:assert/strict";
import test from "node:test";
import worker, { validatePayload } from "./analytics-relay.mjs";

const now = () => new Date().toISOString();

function basePayload(overrides = {}) {
  return {
    source: "thebasics",
    batch_schema_version: 1,
    server_install_id: "0123456789abcdef0123456789abcdef",
    consent_level: "server",
    mod_id: "thebasics",
    mod_version: "5.6.0",
    game_version: "1.20.12",
    events: [
      {
        name: "server started",
        timestamp: now(),
        properties: {
          event_schema_version: 1,
          mod_id: "thebasics",
          mod_version: "5.6.0",
          game_version: "1.20.12",
          analytics_consent_level: "server",
          online_player_count_bucket: "1-5",
          server_session_id: "11111111111111111111111111111111",
          remote_feature_flags_allowed: false,
          error_telemetry_allowed: true,
          performance_telemetry_allowed: false,
          personalized_analytics_requested: false,
        },
      },
    ],
    ...overrides,
  };
}

function requestFor(payload, headers = {}) {
  const body = JSON.stringify(payload);
  return new Request("https://relay.test/v1/events/batch", {
    method: "POST",
    headers: {
      "content-type": "application/json",
      "content-length": String(new TextEncoder().encode(body).byteLength),
      ...headers,
    },
    body,
  });
}

test("valid server payload normalizes for PostHog", () => {
  const validation = validatePayload(basePayload());

  assert.equal(validation.ok, true);
  assert.equal(validation.events.length, 1);
  assert.equal(validation.events[0].event, "server started");
  assert.equal(validation.events[0].properties.distinct_id, "0123456789abcdef0123456789abcdef");
  assert.equal(validation.events[0].properties.server_install_id, "0123456789abcdef0123456789abcdef");
  assert.equal(validation.events[0].properties.$process_person_profile, false);
});

test("rejects unknown envelope keys", () => {
  const validation = validatePayload(basePayload({ world_name: "secret" }));

  assert.deepEqual(validation, { ok: false, error: "unknown_envelope_key" });
});

test("rejects properties not allowed for an event", () => {
  const payload = basePayload();
  payload.events[0].properties.command_name = "tpa";

  const validation = validatePayload(payload);

  assert.deepEqual(validation, { ok: false, error: "property_not_allowed_for_event" });
});

test("rejects player session events without personalized consent", () => {
  const payload = basePayload({
    events: [
      {
        name: "player session started",
        timestamp: now(),
        properties: {
          event_schema_version: 1,
          mod_id: "thebasics",
          mod_version: "5.6.0",
          game_version: "1.20.12",
          analytics_consent_level: "server",
          online_player_count_bucket: "1-5",
          server_session_id: "11111111111111111111111111111111",
        },
      },
    ],
  });

  const validation = validatePayload(payload);

  assert.deepEqual(validation, { ok: false, error: "personalized_event_without_consent" });
});

test("accepts personalized player session payloads", () => {
  const payload = basePayload({
    consent_level: "personalized",
    events: [
      {
        name: "player session ended",
        timestamp: now(),
        properties: {
          event_schema_version: 1,
          mod_id: "thebasics",
          mod_version: "5.6.0",
          game_version: "1.20.12",
          analytics_consent_level: "personalized",
          online_player_count_bucket: "1-5",
          server_session_id: "11111111111111111111111111111111",
          pseudonymous_player_id: "a".repeat(64),
          session_duration_bucket: "5-30m",
          session_end_reason: "disconnect",
        },
      },
    ],
  });

  const validation = validatePayload(payload);

  assert.equal(validation.ok, true);
  assert.equal(validation.events[0].properties.pseudonymous_player_id, "a".repeat(64));
});

test("fetch forwards accepted batches to PostHog", async () => {
  const originalFetch = globalThis.fetch;
  let forwardedUrl;
  let forwardedBody;
  globalThis.fetch = async (url, options) => {
    forwardedUrl = String(url);
    forwardedBody = JSON.parse(options.body);
    return new Response(null, { status: 200 });
  };

  try {
    const response = await worker.fetch(requestFor(basePayload()), {
      POSTHOG_PROJECT_TOKEN: "ph_test_token",
      POSTHOG_HOST: "https://us.i.posthog.com",
    });

    assert.equal(response.status, 204);
    assert.equal(forwardedUrl, "https://us.i.posthog.com/batch/");
    assert.equal(forwardedBody.api_key, "ph_test_token");
    assert.equal(forwardedBody.batch.length, 1);
  } finally {
    globalThis.fetch = originalFetch;
  }
});
