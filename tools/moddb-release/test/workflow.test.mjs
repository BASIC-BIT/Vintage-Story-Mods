import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import test from "node:test";

import { parse } from "yaml";

const WORKFLOW_PATH = resolve(
  dirname(fileURLToPath(import.meta.url)),
  "../../../.github/workflows/moddb-release.yml",
);
const AWS_ACTION = "aws-actions/configure-aws-credentials@ec61189d14ec14c8efccab744f656cffd0e33f37";
const ROLE_ARN = "arn:aws:iam::079358094174:role/basic-vintage-story-moddb-publisher";
const INPUT_NAMES = [
  "operation",
  "release_tag",
  "asset_name",
  "expected_sha256",
  "mod_id",
  "expected_mod_identifier",
  "expected_version",
  "expected_account",
  "compatible_versions",
  "release_notes",
  "expected_file_id",
];

const source = readFileSync(WORKFLOW_PATH, "utf8");
const workflow = parse(source);
const jobs = Object.values(workflow.jobs ?? {});
const job = jobs[0] ?? {};
const steps = job.steps ?? [];
const runBodies = steps.filter((step) => typeof step.run === "string").map((step) => step.run);
const stepIndex = (predicate) => steps.findIndex(predicate);
const usesIndex = (prefix) => stepIndex((step) => typeof step.uses === "string" && step.uses.startsWith(prefix));

test("dispatches manually only", () => {
  assert.equal(workflow.name, "ModDB Release");
  assert.deepEqual(Object.keys(workflow.on), ["workflow_dispatch"]);
  assert.equal(source.includes("schedule"), false);
  assert.equal(source.includes("pull_request"), false);
});

test("declares the exact public inputs", () => {
  const inputs = workflow.on.workflow_dispatch.inputs;
  assert.deepEqual(new Set(Object.keys(inputs)), new Set(INPUT_NAMES));
  assert.equal(inputs.operation.type, "choice");
  assert.equal(inputs.operation.required, true);
  assert.deepEqual(new Set(inputs.operation.options), new Set(["prepare", "publish"]));
  for (const name of INPUT_NAMES) {
    assert.equal(inputs[name].required, name !== "expected_file_id", `${name} required flag`);
  }
});

test("runs one main-only job with minimal permissions", () => {
  assert.equal(jobs.length, 1);
  assert.equal(job["runs-on"], "ubuntu-latest");
  assert.equal(job.if, "github.ref == 'refs/heads/main'");
  assert.deepEqual(job.permissions, { contents: "read", "id-token": "write" });
  assert.ok(
    workflow.permissions === undefined || (Object.keys(workflow.permissions).length === 1 && workflow.permissions.contents === "read"),
    "top-level permissions must be absent or contents: read",
  );
  assert.equal("environment" in job, false);
  assert.equal(source.includes("continue-on-error"), false);
});

test("checks out protected main without persisted credentials", () => {
  const checkout = steps.find((step) => typeof step.uses === "string" && step.uses.startsWith("actions/checkout@"));
  assert.ok(checkout, "checkout step present");
  assert.equal(checkout.with.ref, "refs/heads/main");
  assert.equal(checkout.with["persist-credentials"], false);
});

test("installs the broker without scripts, caches, or browsers", () => {
  const setupNode = steps.find((step) => typeof step.uses === "string" && step.uses.startsWith("actions/setup-node@"));
  assert.ok(setupNode, "setup-node step present");
  assert.equal(String(setupNode.with["node-version"]), "22");
  assert.equal("cache" in setupNode.with, false);
  const install = steps.find((step) => typeof step.run === "string" && step.run.includes("npm ci --ignore-scripts"));
  assert.ok(install, "npm ci --ignore-scripts step present");
  assert.equal(install["working-directory"], "tools/moddb-release");
  for (const step of steps) {
    const uses = step.uses ?? "";
    assert.equal(uses.startsWith("actions/cache"), false);
    assert.equal(uses.includes("upload-artifact"), false);
  }
  assert.equal(runBodies.some((body) => body.includes("playwright install")), false);
});

test("validates inputs and downloads the asset before assuming the AWS role", () => {
  const aws = usesIndex(AWS_ACTION);
  assert.notEqual(aws, -1, "pinned AWS action present");
  assert.deepEqual(steps[aws].with, {
    "role-to-assume": ROLE_ARN,
    "role-session-name": "moddb-release-${{ github.run_id }}",
    "role-duration-seconds": 900,
    "aws-region": "us-east-2",
    "allowed-account-ids": "079358094174",
  });
  const validate = stepIndex((step) => step.shell === "pwsh" && typeof step.run === "string" && step.run.includes("::error::"));
  const download = stepIndex((step) => typeof step.run === "string" && step.run.includes("gh release download"));
  assert.notEqual(validate, -1, "validation step present");
  assert.notEqual(download, -1, "download step present");
  assert.ok(validate < aws, "validation runs before AWS");
  assert.ok(download < aws, "download runs before AWS");
  assert.equal(steps[download].env.GH_TOKEN, "${{ github.token }}");
  assert.ok(steps[download].run.includes("--pattern"), "downloads by exact asset pattern");
  assert.ok(/expected_sha256|EXPECTED_SHA256/i.test(steps[download].run), "download step checks the hash");
});

test("invokes the broker per the stable interface after AWS", () => {
  const aws = usesIndex(AWS_ACTION);
  const broker = stepIndex((step) => typeof step.run === "string" && step.run.includes("tools/moddb-release/src/cli.mjs"));
  assert.notEqual(broker, -1, "broker step present");
  assert.ok(broker > aws, "broker runs after AWS credentials");
  const body = steps[broker].run;
  assert.equal(steps[broker].shell, "pwsh");
  for (const flag of ["--zip", "--changelog", "--expected-sha256", "--mod-id", "--expected-mod-identifier", "--expected-version", "--compatible-version", "--expected-file-id"]) {
    assert.ok(body.includes(flag), `broker step passes ${flag}`);
  }
  assert.ok(body.includes("'release', $env:OPERATION"), "subcommand is release <validated operation>");
  assert.ok(body.includes("$env:RUNNER_TEMP"), "changelog and zip live in runner temp");
  assert.equal(body.includes("Invoke-Expression"), false);
});

test("never copies inputs inline or broker output anywhere", () => {
  for (const body of runBodies) {
    assert.equal(body.includes("${{ inputs."), false, "inputs must flow through env");
    for (const sink of ["GITHUB_OUTPUT", "GITHUB_ENV", "GITHUB_STEP_SUMMARY"]) {
      assert.equal(body.includes(sink), false, `run body writes ${sink}`);
    }
  }
  assert.equal(source.includes("secrets."), false);
});
