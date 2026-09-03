#!/usr/bin/env node
// Entry point. Parses argv, wires production dependencies, runs one command,
// and writes exactly one safe JSON line to stdout. Errors never print stack
// traces: BrokerErrors become their code, anything else becomes a constant.
import { readFileSync } from "node:fs";
import { pathToFileURL } from "node:url";

import { SecretsManagerClient } from "@aws-sdk/client-secrets-manager";

import { parseArgs } from "./args.mjs";
import { inspectArtifact } from "./artifact.mjs";
import { renewInBrowser } from "./browser-renewal.mjs";
import { createCommands, readMaskedLine } from "./commands.mjs";
import { AWS_REGION } from "./config.mjs";
import { ExitCode, classifyError, exitCodeFor, writeResult } from "./contracts.mjs";
import { createModDbClient } from "./moddb-client.mjs";
import { deleteWinCredSession, readWinCredSession } from "./wincred.mjs";

const USAGE = `Usage: node tools/moddb-release/src/cli.mjs <command> [options]

  account set
  session status
  session renew --expected-account <moddb-username>
  session import-wincred --expected-account <moddb-username>
  session import-wincred --finalize-version <aws-version-id>
  release prepare --mod-id <number> --expected-mod-identifier <id> --expected-version <semver> --zip <path> --changelog <path> --compatible-version <semver> --expected-sha256 <hex> [--expected-account <moddb-username>]
  release publish --mod-id <number> --expected-mod-identifier <id> --expected-version <semver> --zip <path> --changelog <path> --compatible-version <semver> --expected-sha256 <hex> --expected-file-id <number> [--expected-account <moddb-username>]

--compatible-version is repeatable. <moddb-username> is the name shown in the ModDB account menu;
release commands default it to the account the stored session was validated for.
Each command writes one JSON line to stdout.
Exit codes: 0 ok, 1 failed, 2 renewal-required, 3 approval-required.
`;

const HANDLER = {
  "account set": "accountSet",
  "session status": "sessionStatus",
  "session renew": "sessionRenew",
  "session import-wincred": "sessionImportWincred",
  "release prepare": "releasePrepare",
  "release publish": "releasePublish",
};

const productionDeps = () => ({
  secretsClient: new SecretsManagerClient({ region: AWS_REGION }),
  stdin: process.stdin,
  stdout: process.stdout,
  stderr: process.stderr,
  isTTY: process.stdin.isTTY === true,
  platform: process.platform,
  env: process.env,
  readMaskedLine,
  readWinCred: () => readWinCredSession(),
  deleteWinCred: () => deleteWinCredSession(),
  browserRenewal: renewInBrowser,
  modDbFactory: createModDbClient,
  inspectArtifact,
  readFile: readFileSync,
  clock: () => new Date(),
});

export async function main(argv, deps = {}) {
  const stdout = deps.stdout ?? process.stdout;
  try {
    const { command, options } = parseArgs(argv);
    if (command === "help") {
      stdout.write(USAGE);
      return ExitCode.ok;
    }
    // Tests inject a complete dependency set; production fills anything missing.
    const commands = createCommands(deps.secretsClient ? deps : { ...productionDeps(), ...deps });
    const result = await commands[HANDLER[command]](options);
    writeResult(result, stdout);
    return exitCodeFor(result);
  } catch (error) {
    const { exitCode, result } = classifyError(error);
    writeResult(result, stdout);
    return exitCode;
  }
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  main(process.argv.slice(2)).then((code) => {
    process.exitCode = code;
  });
}
