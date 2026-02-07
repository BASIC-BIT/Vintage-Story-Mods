---
name: playwright-exploration
description: Safely explore a website with Playwright, then turn findings into a deterministic automation playbook (e.g., ModDB uploads).
compatibility: opencode
metadata:
  audience: maintainers
  domain: ui-automation
---

## Goal
Use Playwright in two phases:
1. Exploratory pass (read-only intent) to learn the UI.
2. Deterministic pass (scripted steps) to reliably do the task later.

## Phase 1: Exploratory pass (read-only intent)
Do:
- Navigate and snapshot frequently.
- Identify stable anchors:
  - ARIA roles and accessible names
  - visible button text
  - form labels / placeholders
  - page headings
- Record expected page-state checks after each action ("I should now see X").

Avoid:
- Clicking destructive actions (delete/publish) in exploration.
- Relying on brittle selectors (deep CSS, auto-generated ids).

Outputs to capture
- A short step list: navigation path + what to click.
- A list of selectors/anchors in plain English.
- Error states (rate limit, auth expired, missing permissions) and how to recover.

## Phase 2: Deterministic playbook
Convert exploration notes into a reproducible playbook:
- Prereqs (auth, required files, versions).
- Exact navigation steps.
- Explicit checks after each step.
- Failure handling and rollback.

## Authentication pattern
For sites requiring login (e.g., ModDB):
- Prefer a human-authenticated session.
- Have the user log in interactively.
- Then automation continues from a known "logged-in" page.

Never store credentials in the repo.

## Safety rules
- Treat anything that publishes/releases as destructive.
- Default to "draft" states when available.
- Prefer verifying version strings before uploading.
