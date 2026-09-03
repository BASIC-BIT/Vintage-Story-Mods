import assert from "node:assert/strict";
import test from "node:test";

import { parseArgs } from "../src/args.mjs";
import { BrokerError } from "../src/contracts.mjs";

const SHA = "a".repeat(64);
const PREPARE = [
  "release", "prepare",
  "--mod-id", "42",
  "--expected-mod-identifier", "thebasics",
  "--expected-version", "5.9.1",
  "--zip", "C:\\tmp\\thebasics-v5.9.1.zip",
  "--changelog", "C:\\tmp\\changelog.txt",
  "--compatible-version", "1.21.0",
  "--expected-sha256", SHA,
];
const PUBLISH = ["release", "publish", ...PREPARE.slice(2), "--expected-file-id", "77"];
const COMMANDS = {
  "account set": ["account", "set"],
  "session status": ["session", "status"],
  "session renew": ["session", "renew", "--expected-account", "123"],
  "session import-wincred": ["session", "import-wincred", "--expected-account", "123"],
  "release prepare": PREPARE,
  "release publish": PUBLISH,
};
const FORBIDDEN = ["--password", "--cookie", "--session", "--secret-string", "--account-origin", "--moddb-origin", "--interactive"];

function rejectsOption(argv, option) {
  let error;
  assert.throws(() => parseArgs(argv), (caught) => ((error = caught), caught instanceof BrokerError));
  assert.equal(error.code, "INVALID_ARGUMENTS");
  assert.equal(error.message, `invalid option: ${option}`);
}

test("help for --help, -h, and no arguments", () => {
  for (const argv of [[], ["--help"], ["-h"], ["release", "prepare", "--help"]]) {
    assert.deepEqual(parseArgs(argv), { command: "help", options: {} });
  }
});

test("every documented command parses", () => {
  for (const [command, argv] of Object.entries(COMMANDS)) assert.equal(parseArgs(argv).command, command);
  assert.deepEqual(parseArgs(["account", "set"]).options, {});
  assert.deepEqual(parseArgs(["session", "status"]).options, {});
  assert.deepEqual(parseArgs(COMMANDS["session renew"]).options, { expectedAccount: "123" });
});

test("unknown commands are rejected without echoing them", () => {
  for (const argv of [["release"], ["release", "drop"], ["nope"], ["account", "set", "extra"]]) {
    let error;
    assert.throws(() => parseArgs(argv), (caught) => ((error = caught), caught instanceof BrokerError));
    assert.equal(error.code, "INVALID_ARGUMENTS");
    assert.equal(error.message.includes("nope"), false);
    assert.equal(error.message.includes("drop"), false);
    assert.equal(error.message.includes("extra"), false);
  }
});

test("release prepare yields typed options with a lowercased hash and repeatable compatibility", () => {
  const argv = [...PREPARE, "--compatible-version", "1.21.1"];
  argv[argv.indexOf(SHA)] = SHA.toUpperCase();
  assert.deepEqual(parseArgs(argv).options, {
    modId: 42,
    expectedModIdentifier: "thebasics",
    expectedVersion: "5.9.1",
    zip: "C:\\tmp\\thebasics-v5.9.1.zip",
    changelog: "C:\\tmp\\changelog.txt",
    compatibleVersions: ["1.21.0", "1.21.1"],
    expectedSha256: SHA,
  });
});

test("release publish requires --expected-file-id and prepare rejects it", () => {
  assert.equal(parseArgs(PUBLISH).options.expectedFileId, 77);
  rejectsOption(PUBLISH.slice(0, -2), "--expected-file-id");
  rejectsOption([...PREPARE, "--expected-file-id", "77"], "--expected-file-id");
});

test("missing required options name the option only", () => {
  for (const option of ["--mod-id", "--expected-mod-identifier", "--expected-version", "--zip", "--changelog", "--compatible-version", "--expected-sha256"]) {
    const index = PREPARE.indexOf(option);
    rejectsOption([...PREPARE.slice(0, index), ...PREPARE.slice(index + 2)], option);
  }
  rejectsOption(["session", "renew"], "--expected-account");
});

test("values are validated and the message never contains the value", () => {
  const withValue = (option, value) => {
    const argv = [...PREPARE];
    argv[argv.indexOf(option) + 1] = value;
    return argv;
  };
  const cases = [
    ["--mod-id", "0"], ["--mod-id", "-1"], ["--mod-id", "1.5"], ["--mod-id", "abc"],
    ["--expected-mod-identifier", " "], ["--expected-version", "5.9 .1"], ["--expected-version", ""],
    ["--zip", ""], ["--changelog", " "], ["--compatible-version", " "],
    ["--expected-sha256", "zz".repeat(32)], ["--expected-sha256", "ab".repeat(31)],
  ];
  for (const [option, value] of cases) rejectsOption(withValue(option, value), option);
  const argv = [...PUBLISH];
  argv[argv.length - 1] = "007-secret-value";
  let error;
  assert.throws(() => parseArgs(argv), (caught) => ((error = caught), true));
  assert.equal(error.message.includes("secret-value"), false);
});

test("missing values, duplicates, and unknown options are rejected", () => {
  rejectsOption([...PREPARE.slice(0, -1)], "--expected-sha256");
  rejectsOption([...PREPARE, "--mod-id", "43"], "--mod-id");
  rejectsOption([...PREPARE, "--nope", "x"], "--nope");
  rejectsOption(["session", "status", "--mod-id", "1"], "--mod-id");
  rejectsOption(["account", "set", "--expected-account", "1"], "--expected-account");
  rejectsOption(["session", "renew", "--expected-account", "1", "--finalize-version", "v"], "--finalize-version");
});

test("session import-wincred takes exactly one of --expected-account or --finalize-version", () => {
  assert.deepEqual(parseArgs(["session", "import-wincred", "--finalize-version", "v-1"]).options, { finalizeVersion: "v-1" });
  rejectsOption(["session", "import-wincred"], "--expected-account");
  rejectsOption(["session", "import-wincred", "--expected-account", "1", "--finalize-version", "v"], "--finalize-version");
});

test("credential and override options are rejected for every command", () => {
  for (const argv of Object.values(COMMANDS)) {
    for (const option of FORBIDDEN) rejectsOption([...argv, option, "fixture-never-print"], option);
  }
});
