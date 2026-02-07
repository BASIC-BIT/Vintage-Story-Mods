# Pterodactyl Orchestration (Application API)

Purpose: spin up disposable test servers quickly and safely ("templated servers"), using Pterodactyl's Application API.

This is the foundation for parallel test matrices (config permutations, version targets, feature flags).

## Prerequisites

- `PTERO_BASE_URL` set
- `PTERO_TOKEN_APPLICATION` set (ptla_...)
- `PTERO_TOKEN` set (ptlc_...) for client-side operations if needed

Guards:

- Application API writes require `PTERO_APP_ALLOW_WRITE=1` and `confirm=true`

## Terminology

- Application API uses numeric server IDs (e.g. `123`).
- Client API uses server identifiers (short IDs like `8982de16`).

## Suggested Workflow (Clone Based)

Cloning is usually safer than constructing a server from scratch because it preserves:

- egg choice
- startup command
- docker image
- limits/feature limits
- environment variables

### Step 1: Identify a template server

Use the application API to find the template (the known-good test server):

- `ptero_app_servers_list filterName="Test Vintage Story" include="node,allocations"`

Record:

- source server numeric id
- node id
- current allocation id(s)

### Step 2: Pick a free allocation on the same node

List allocations:

- `ptero_app_node_allocations_list nodeId=<nodeId>`

Pick an allocation where `assigned=false`.

### Step 3: Clone the server

- Enable writes: set `PTERO_APP_ALLOW_WRITE=1`
- Call clone:
  - `ptero_app_server_clone sourceServerId=<id> newName="VS Test - <tag>" allocationDefaultId=<allocId> copyEnvironment=true confirm=true`

### Step 4: Install/Start and deploy

This depends on the egg. Common follow-ups:

- reinstall the server if required
- set startup vars (e.g. VS version) via the server startup endpoint
- upload the mod zip to `data/Mods`
- restart

## Safety Notes

- Always use a naming convention that makes disposable servers obvious.
- Prefer cloning within a dedicated location/node to avoid resource contention.
- Never run destructive operations without the write gate.
