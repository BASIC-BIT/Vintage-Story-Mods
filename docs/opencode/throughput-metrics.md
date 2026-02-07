# Throughput Metrics (Low-Overhead)

Goal: make improvements measurable without turning development into bureaucracy.

This doc suggests lightweight metrics that help decide whether new tools/skills are worth keeping.

## What to measure

Cycle time:

- time from "change requested" -> "verified in-game"
- time from "code change" -> "server deployed + restarted"

Manual steps:

- number of human clicks/actions required per verification loop
- number of times the agent had to ask the user for input

Flake rate:

- how often the loop fails due to environment drift (wrong server, wrong profile, wrong version)

Durability:

- did we promote a repeated workflow into a skill/doc?
- did the next session succeed with less prompting?

## How to use metrics

- Use them to choose which automation to build next.
- Prefer improvements that reduce manual steps *and* flake rate.
- If a workflow repeats, file it into durable storage (AGENTS/skills/docs).

## Notes

- Do not optimize for token usage at the expense of reliability.
- Objective stop conditions (build/tests/log signatures) beat subjective self-report.
