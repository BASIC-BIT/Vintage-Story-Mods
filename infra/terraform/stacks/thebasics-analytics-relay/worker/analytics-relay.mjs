const MAX_BODY_BYTES = 64 * 1024;
const MAX_EVENTS = 50;
const MAX_DEFAULT_PROPERTIES = 24;
const MAX_CONFIG_PROPERTIES = 80;
const MAX_STRING_LENGTH = 256;
const MAX_EVENT_AGE_MS = 7 * 24 * 60 * 60 * 1000;
const MAX_EVENT_FUTURE_SKEW_MS = 24 * 60 * 60 * 1000;

const ACCEPTED_PATH = "/v1/events/batch";

const ALLOWED_ENVELOPE_KEYS = new Set([
  "batch_schema_version",
  "consent_level",
  "events",
  "game_version",
  "mod_id",
  "mod_version",
  "server_install_id",
  "source",
]);

const ALLOWED_EVENT_KEYS = new Set(["name", "properties", "timestamp"]);

const ALLOWED_EVENTS = new Set([
  "analytics consent changed",
  "command used",
  "config snapshot",
  "feature used",
  "player session ended",
  "player session started",
  "server started",
  "server stopped",
]);

const ALLOWED_CONSENT_LEVELS = new Set(["server", "personalized"]);

const ALLOWED_PROPERTIES = new Set([
  "action",
  "allow_ooc_toggle",
  "allow_player_nickname_colors",
  "allow_player_nicknames",
  "allow_player_tpa",
  "analytics_consent_level",
  "attribute_freeform_messages_to_player_name",
  "character_sheet_field_count_bucket",
  "chat_type",
  "command_name",
  "disable_nicknames",
  "disable_rp_chat",
  "enable_admin_note_ledger",
  "enable_admin_notes",
  "enable_chatter",
  "enable_character_headshots",
  "enable_character_sheets",
  "enable_distance_font_size",
  "enable_distance_obfuscation",
  "enable_global_ooc",
  "enable_language_system",
  "enable_player_notes",
  "enable_rp_character_slots",
  "enable_sleep_notifications",
  "enable_structured_admin_notes",
  "enable_typing_indicator",
  "error_telemetry_allowed",
  "event_schema_version",
  "feature_name",
  "game_version",
  "headshot_url_allowed",
  "hide_nametag_unless_targeting",
  "language_count_bucket",
  "max_rp_character_slots_bucket",
  "mod_id",
  "mod_version",
  "nametag_requires_line_of_sight",
  "new_consent_level",
  "normalize_proximity_chat_text",
  "online_player_count_bucket",
  "overhead_chat_bubble_mode",
  "performance_telemetry_allowed",
  "personalized_analytics_requested",
  "player_stat_system",
  "prevent_proximity_channel_switching",
  "pseudonymous_player_id",
  "previous_consent_level",
  "proximity_chat_as_default",
  "proximity_chat_presentation_mode",
  "remote_feature_flags_allowed",
  "require_line_of_sight_for_sign_language",
  "result",
  "server_save_announcement_as_notification",
  "server_session_id",
  "session_duration_bucket",
  "session_end_reason",
  "show_headshot_in_nametag",
  "show_nickname_in_nametag",
  "show_player_name_in_nametag",
  "success",
  "tpa_require_temporal_gear",
  "tpa_use_cooldown",
  "tpa_use_timeout",
  "typing_indicator_display_mode",
  "use_general_channel_as_proximity_chat",
  "use_custom_nametag_renderer",
]);

const ALLOWED_STRING_VALUES = new Map([
  ["action", new Set([
    "accept",
    "add",
    "admin_add",
    "admin_list",
    "admin_remove",
    "admin_set",
    "allow_incoming",
    "cancel",
    "clear",
    "clear_all",
    "clear_incoming",
    "clear_one",
    "deny",
    "disable",
    "disallow_incoming",
    "enable",
    "list",
    "place",
    "remove",
    "request",
    "request_bring",
    "request_goto",
    "send",
    "send_chat_tab",
    "send_normal",
    "send_whisper",
    "send_yell",
    "set",
    "set_durability",
    "set_normal",
    "set_whisper",
    "set_yell",
    "view_other",
    "view_own",
  ])],
  ["analytics_consent_level", new Set(["server", "personalized"])],
  ["chat_type", new Set(["chat_tab", "envhere", "gooc", "it", "me", "normal", "ooc", "whisper", "yell"])],
  ["character_sheet_field_count_bucket", new Set(["0", "1-5", "6-10", "11-20", "21-50", "51-100", "101+"])],
  ["command_name", new Set([
    "addlang",
    "adminaddlang",
    "adminlistlang",
    "adminremovelang",
    "adminsetnickname",
    "adminsetnicknamecolor",
    "chatter",
    "clearnick",
    "clearnickcolor",
    "clearstat",
    "clearstats",
    "cleartpa",
    "emotemode",
    "envhere",
    "gooc",
    "it",
    "langcolor",
    "listlang",
    "me",
    "nickname",
    "nickcolor",
    "normal",
    "ooc",
    "ooctoggle",
    "playerstats",
    "removelang",
    "rptext",
    "setdurability",
    "tpa",
    "tpaccept",
    "tpacancel",
    "tpahere",
    "tpallow",
    "tpdeny",
    "tpalist",
    "whisper",
    "yell",
  ])],
  ["language_count_bucket", new Set(["0", "1-5", "6-10", "11-20", "21-50", "51-100", "101+"])],
  ["max_rp_character_slots_bucket", new Set(["0", "1-5", "6-10", "11-20", "21-50", "51-100", "101+"])],
  ["mod_id", new Set(["thebasics"])],
  ["feature_name", new Set([
    "chat_mode",
    "chatter",
    "emote_mode",
    "environment_message",
    "global_ooc",
    "language",
    "language_colors",
    "nickname",
    "nickname_color",
    "ooc",
    "ooc_mode",
    "player_stats",
    "proximity_chat",
    "proximity_emote",
    "repair",
    "rp_text",
    "tpa",
  ])],
  ["online_player_count_bucket", new Set(["0", "1-5", "6-10", "11-20", "21-50", "51-100", "101+"])],
  ["overhead_chat_bubble_mode", new Set(["RpText", "Vanilla", "Off"])],
  ["previous_consent_level", new Set(["unknown", "disabled", "server", "personalized"])],
  ["new_consent_level", new Set(["disabled", "server", "personalized"])],
  ["proximity_chat_presentation_mode", new Set(["StandardRoleplay", "SimpleSpeech", "PlainProximity", "Prose"])],
  ["result", new Set([
    "consume_gear_failed",
    "cooldown",
    "existing_request",
    "failure",
    "missing_temporal_gear",
    "self_teleport",
    "success",
    "target_disabled",
    "target_not_found",
  ])],
  ["session_duration_bucket", new Set(["<1m", "1-5m", "5-30m", "30-120m", "120m+"])],
  ["session_end_reason", new Set(["disconnect", "server_stop"])],
  ["typing_indicator_display_mode", new Set(["Icon", "Text", "Both"])],
]);

const BOOLEAN_PROPERTIES = new Set([
  "allow_ooc_toggle",
  "allow_player_nickname_colors",
  "allow_player_nicknames",
  "allow_player_tpa",
  "attribute_freeform_messages_to_player_name",
  "disable_nicknames",
  "disable_rp_chat",
  "enable_admin_note_ledger",
  "enable_admin_notes",
  "enable_chatter",
  "enable_character_headshots",
  "enable_character_sheets",
  "enable_distance_font_size",
  "enable_distance_obfuscation",
  "enable_global_ooc",
  "enable_language_system",
  "enable_player_notes",
  "enable_rp_character_slots",
  "enable_sleep_notifications",
  "enable_structured_admin_notes",
  "enable_typing_indicator",
  "error_telemetry_allowed",
  "headshot_url_allowed",
  "hide_nametag_unless_targeting",
  "nametag_requires_line_of_sight",
  "normalize_proximity_chat_text",
  "performance_telemetry_allowed",
  "personalized_analytics_requested",
  "player_stat_system",
  "prevent_proximity_channel_switching",
  "proximity_chat_as_default",
  "remote_feature_flags_allowed",
  "require_line_of_sight_for_sign_language",
  "server_save_announcement_as_notification",
  "show_headshot_in_nametag",
  "show_nickname_in_nametag",
  "show_player_name_in_nametag",
  "success",
  "tpa_require_temporal_gear",
  "tpa_use_cooldown",
  "tpa_use_timeout",
  "use_general_channel_as_proximity_chat",
  "use_custom_nametag_renderer",
]);

const STRING_PROPERTIES = new Set([
  ...ALLOWED_STRING_VALUES.keys(),
  "game_version",
  "mod_version",
  "pseudonymous_player_id",
  "server_session_id",
]);

const BASE_PROPERTIES = new Set([
  "analytics_consent_level",
  "event_schema_version",
  "game_version",
  "mod_id",
  "mod_version",
  "online_player_count_bucket",
  "server_session_id",
]);

const CONFIG_PROPERTIES = new Set([
  ...BASE_PROPERTIES,
  "allow_ooc_toggle",
  "allow_player_nickname_colors",
  "allow_player_nicknames",
  "allow_player_tpa",
  "attribute_freeform_messages_to_player_name",
  "character_sheet_field_count_bucket",
  "disable_nicknames",
  "disable_rp_chat",
  "enable_admin_note_ledger",
  "enable_admin_notes",
  "enable_chatter",
  "enable_character_headshots",
  "enable_character_sheets",
  "enable_distance_font_size",
  "enable_distance_obfuscation",
  "enable_global_ooc",
  "enable_language_system",
  "enable_player_notes",
  "enable_rp_character_slots",
  "enable_sleep_notifications",
  "enable_structured_admin_notes",
  "enable_typing_indicator",
  "headshot_url_allowed",
  "hide_nametag_unless_targeting",
  "language_count_bucket",
  "max_rp_character_slots_bucket",
  "nametag_requires_line_of_sight",
  "normalize_proximity_chat_text",
  "overhead_chat_bubble_mode",
  "player_stat_system",
  "prevent_proximity_channel_switching",
  "proximity_chat_as_default",
  "proximity_chat_presentation_mode",
  "require_line_of_sight_for_sign_language",
  "server_save_announcement_as_notification",
  "show_headshot_in_nametag",
  "show_nickname_in_nametag",
  "show_player_name_in_nametag",
  "tpa_require_temporal_gear",
  "tpa_use_cooldown",
  "tpa_use_timeout",
  "typing_indicator_display_mode",
  "use_custom_nametag_renderer",
  "use_general_channel_as_proximity_chat",
]);

const EVENT_PROPERTIES = new Map([
  ["analytics consent changed", new Set([
    ...BASE_PROPERTIES,
    "new_consent_level",
    "personalized_analytics_requested",
    "previous_consent_level",
  ])],
  ["command used", new Set([
    ...BASE_PROPERTIES,
    "chat_type",
    "command_name",
    "result",
    "success",
  ])],
  ["config snapshot", CONFIG_PROPERTIES],
  ["feature used", new Set([
    ...BASE_PROPERTIES,
    "action",
    "chat_type",
    "feature_name",
    "result",
    "success",
  ])],
  ["player session ended", new Set([
    ...BASE_PROPERTIES,
    "pseudonymous_player_id",
    "session_duration_bucket",
    "session_end_reason",
  ])],
  ["player session started", new Set([
    ...BASE_PROPERTIES,
    "pseudonymous_player_id",
  ])],
  ["server started", new Set([
    ...BASE_PROPERTIES,
    "error_telemetry_allowed",
    "performance_telemetry_allowed",
    "personalized_analytics_requested",
    "remote_feature_flags_allowed",
  ])],
  ["server stopped", BASE_PROPERTIES],
]);

export default {
  async fetch(request, env) {
    const url = new URL(request.url);

    if (request.method === "GET" && url.pathname === "/health") {
      return json({ ok: true, service: "thebasics-analytics-relay", schema_version: 1 });
    }

    if (request.method !== "POST" || url.pathname !== ACCEPTED_PATH) {
      return json({ error: "not_found" }, 404);
    }

    const contentType = request.headers.get("content-type") || "";
    if (!/^application\/json(?:\s*;|$)/i.test(contentType)) {
      return json({ error: "unsupported_media_type" }, 415);
    }

    if (!env.POSTHOG_PROJECT_TOKEN) {
      return json({ error: "relay_not_configured" }, 503);
    }

    const contentLengthHeader = request.headers.get("content-length");
    if (!contentLengthHeader || !/^\d+$/.test(contentLengthHeader)) {
      return json({ error: "length_required" }, 411);
    }

    const contentLength = Number(contentLengthHeader);
    if (contentLength > MAX_BODY_BYTES) {
      return json({ error: "payload_too_large" }, 413);
    }

    let payload;
    try {
      const body = await request.text();
      if (new TextEncoder().encode(body).byteLength > MAX_BODY_BYTES) {
        return json({ error: "payload_too_large" }, 413);
      }

      payload = JSON.parse(body);
    } catch {
      return json({ error: "invalid_json" }, 400);
    }

    const validation = validatePayload(payload);
    if (!validation.ok) {
      return json({ error: validation.error }, 400);
    }

    const posthogHost = (env.POSTHOG_HOST || "https://us.i.posthog.com").replace(/\/+$/, "");
    const controller = new AbortController();
    const timeout = setTimeout(() => controller.abort(), 5000);
    let response;
    try {
      response = await fetch(`${posthogHost}/batch/`, {
        method: "POST",
        headers: { "content-type": "application/json" },
        body: JSON.stringify({
          api_key: env.POSTHOG_PROJECT_TOKEN,
          batch: validation.events,
        }),
        signal: controller.signal,
      });
    } catch {
      return json({ error: "upstream_failed" }, 502);
    } finally {
      clearTimeout(timeout);
    }

    if (!response.ok) {
      return json({ error: "upstream_rejected" }, 502);
    }

    return new Response(null, { status: 204 });
  },
};

function validatePayload(payload) {
  if (!isPlainObject(payload)) {
    return invalid("invalid_payload");
  }

  if (!hasOnlyKeys(payload, ALLOWED_ENVELOPE_KEYS)) {
    return invalid("unknown_envelope_key");
  }

  if (payload.source !== "thebasics" || payload.batch_schema_version !== 1 || payload.mod_id !== "thebasics") {
    return invalid("invalid_envelope");
  }

  if (!isVersionString(payload.mod_version) || !isVersionString(payload.game_version)) {
    return invalid("invalid_envelope_version");
  }

  if (!isServerInstallId(payload.server_install_id)) {
    return invalid("invalid_server_install_id");
  }

  if (!ALLOWED_CONSENT_LEVELS.has(payload.consent_level)) {
    return invalid("invalid_consent_level");
  }

  if (!Array.isArray(payload.events) || payload.events.length === 0 || payload.events.length > MAX_EVENTS) {
    return invalid("invalid_event_count");
  }

  const events = [];
  for (const event of payload.events) {
    const normalized = normalizeEvent(event, payload);
    if (!normalized.ok) {
      return normalized;
    }

    events.push(normalized.event);
  }

  return { ok: true, events };
}

function normalizeEvent(event, envelope) {
  if (!isPlainObject(event) || !ALLOWED_EVENTS.has(event.name)) {
    return invalid("invalid_event_name");
  }

  if (!hasOnlyKeys(event, ALLOWED_EVENT_KEYS)) {
    return invalid("unknown_event_key");
  }

  const timestampMs = typeof event.timestamp === "string" && event.timestamp.length <= 64
    ? Date.parse(event.timestamp)
    : NaN;
  if (Number.isNaN(timestampMs)) {
    return invalid("invalid_event_timestamp");
  }

  const now = Date.now();
  if (timestampMs < now - MAX_EVENT_AGE_MS || timestampMs > now + MAX_EVENT_FUTURE_SKEW_MS) {
    return invalid("event_timestamp_out_of_range");
  }

  if (!isPlainObject(event.properties)) {
    return invalid("invalid_event_properties");
  }

  if (!isPropertyCountAllowed(event.name, event.properties)) {
    return invalid("too_many_properties");
  }

  const properties = {
    distinct_id: envelope.server_install_id,
    server_install_id: envelope.server_install_id,
    "$process_person_profile": false,
  };

  for (const [key, value] of Object.entries(event.properties)) {
    if (!ALLOWED_PROPERTIES.has(key)) {
      return invalid("unknown_property");
    }

    if (!isPropertyAllowedForEvent(event.name, key)) {
      return invalid("property_not_allowed_for_event");
    }

    if (key === "pseudonymous_player_id" && envelope.consent_level !== "personalized") {
      return invalid("personalized_property_without_consent");
    }

    const normalized = normalizePropertyValue(key, value);
    if (!normalized.ok) {
      return invalid("invalid_property_value");
    }

    properties[key] = normalized.value;
  }

  properties.mod_id = "thebasics";
  properties.analytics_consent_level = envelope.consent_level;

  return {
    ok: true,
    event: {
      event: event.name,
      properties,
      timestamp: event.timestamp,
    },
  };
}

function normalizePropertyValue(key, value) {
  if (BOOLEAN_PROPERTIES.has(key)) {
    return typeof value === "boolean" ? { ok: true, value } : invalid("invalid_boolean");
  }

  if (key === "event_schema_version") {
    return value === 1 ? { ok: true, value } : invalid("invalid_schema_version");
  }

  if (key === "server_session_id") {
    return isHexId(value, 32) ? { ok: true, value: value.toLowerCase() } : invalid("invalid_server_session_id");
  }

  if (key === "pseudonymous_player_id") {
    return isHexId(value, 64) ? { ok: true, value: value.toLowerCase() } : invalid("invalid_pseudonymous_player_id");
  }

  if (!STRING_PROPERTIES.has(key)) {
    return invalid("invalid_property_type");
  }

  if (typeof value !== "string" || value.length > MAX_STRING_LENGTH || /[\u0000-\u001f\u007f]/.test(value)) {
    return invalid("invalid_string");
  }

  const allowedValues = ALLOWED_STRING_VALUES.get(key);
  if (allowedValues && !allowedValues.has(value)) {
    return invalid("invalid_string_value");
  }

  if ((key === "mod_version" || key === "game_version") && !/^[A-Za-z0-9 ._()+-]{1,64}$/.test(value)) {
    return invalid("invalid_version_string");
  }

  return { ok: true, value };
}

function isPlainObject(value) {
  return value !== null && typeof value === "object" && !Array.isArray(value);
}

function hasOnlyKeys(value, allowedKeys) {
  return Object.keys(value).every((key) => allowedKeys.has(key));
}

function isPropertyAllowedForEvent(eventName, key) {
  const allowedProperties = EVENT_PROPERTIES.get(eventName);
  return Boolean(allowedProperties?.has(key));
}

function isPropertyCountAllowed(eventName, properties) {
  return Object.keys(properties).length <= maxPropertyCountForEvent(eventName);
}

function maxPropertyCountForEvent(eventName) {
  return eventName === "config snapshot" ? MAX_CONFIG_PROPERTIES : MAX_DEFAULT_PROPERTIES;
}

function isServerInstallId(value) {
  return typeof value === "string" && /^[a-f0-9]{32}$/i.test(value);
}

function isVersionString(value) {
  return typeof value === "string" && /^[A-Za-z0-9 ._()+-]{1,64}$/.test(value);
}

function isHexId(value, length) {
  return typeof value === "string" && value.length === length && /^[a-f0-9]+$/i.test(value);
}

function invalid(error) {
  return { ok: false, error };
}

function json(body, status = 200) {
  return new Response(JSON.stringify(body), {
    status,
    headers: { "content-type": "application/json; charset=utf-8" },
  });
}
