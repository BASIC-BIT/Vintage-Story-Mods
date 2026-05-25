# Analytics And Privacy

The BASICs can send opt-in product analytics to BASIC through a BASIC-owned relay that forwards accepted events to PostHog. Fresh installs send nothing remotely until a root admin chooses an analytics level.

## Admin Controls

Root admins can review or change consent in game:

```text
/basicsanalytics status
/basicsanalytics server
/basicsanalytics personalized
/basicsanalytics off
/basicsanalytics prompt
```

`/thebasicsanalytics` is an alias. The consent and local analytics IDs are stored in `ModConfig/the_basics_analytics.json`, separate from the main `the_basics.json` feature config.

## Consent Levels

| Choice | Config value | What it does |
|--------|--------------|--------------|
| Off | `disabled` | Sends no remote analytics and makes no analytics relay requests. |
| Anonymous server statistics | `server` | Sends server-level feature usage, config summaries, versions, coarse activity buckets, and bounded result codes. |
| Full statistics | `personalized` | Sends anonymous server statistics plus pseudonymous player session start/end events for richer usage analysis. |

The consent policy is versioned. When a release expands the data contract, previously opted-in servers are prompted again before sending under the new policy.

## Data Sent

Allowed server-statistics data is intentionally low-cardinality:

- Mod ID, mod version, and Vintage Story game version.
- A random `server_install_id` generated locally after opt-in.
- A random per-run `server_session_id`.
- Online player count bucket such as `1-5`, `6-10`, or `21-50`.
- Allowlisted event names such as `server started`, `server stopped`, `config snapshot`, `command used`, and `feature used`.
- Allowlisted command names, feature names, action names, success booleans, and bounded result codes.
- Allowlisted boolean/enum config summaries and count buckets, never raw config values.

Full statistics can also send:

- `player session started` and `player session ended` events.
- A per-server pseudonymous player ID generated with HMAC-SHA256 from the Vintage Story player UID and a local random salt.
- Bucketed session duration such as `<1m`, `1-5m`, or `30-120m`.

The pseudonymous player ID is stable only for this server install and analytics salt. The raw player UID is not sent.

## Data Not Sent

The analytics relay rejects unknown fields and does not accept:

- Chat text, whispers, OOC text, or roleplay messages.
- Command arguments.
- Player names, nicknames, character names, bios, notes, or image data.
- Headshot bytes, URLs, or hashes.
- Server IP, server name, world name, world seed, or coordinates.
- Raw `the_basics.json` or `the_basics_analytics.json` contents.
- Raw logs or stack traces.

## Transport

The mod sends batches to the Cloudflare Worker relay at `https://thebasics-analytics-relay.basic-bit-1001.workers.dev/v1/events/batch`. The PostHog project token is stored only in the relay infrastructure, not in the mod.

The relay validates event names, property names, enum values, ID formats, batch size, content type, and event age before forwarding. Events are forwarded with `$process_person_profile=false` so PostHog does not create or update person profiles for these analytics events.

Analytics are best-effort. If the relay is unavailable or rejects a batch, gameplay continues and the batch is dropped rather than retried indefinitely.
