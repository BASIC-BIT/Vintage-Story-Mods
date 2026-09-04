// Strict argv grammar for the broker. Every command has an explicit option
// allowlist; anything else is rejected with a message naming only the option,
// never its value, so a credential passed by mistake cannot echo back.
import { BrokerError } from "./contracts.mjs";

const HELP_FLAGS = new Set(["--help", "-h"]);
const invalid = (option) => new BrokerError("INVALID_ARGUMENTS", `invalid option: ${option}`);

const positiveInt = (value) => (/^[1-9]\d*$/.test(value) && Number.isSafeInteger(Number(value)) ? Number(value) : undefined);
const nonblank = (value) => (value.trim() !== "" ? value : undefined);
const token = (value) => (value !== "" && !/\s/.test(value) ? value : undefined);
const hex64 = (value) => (/^[0-9a-fA-F]{64}$/.test(value) ? value.toLowerCase() : undefined);

// option -> [options key, parser, repeatable]
const OPTIONS = {
  "--expected-account": ["expectedAccount", token],
  "--finalize-version": ["finalizeVersion", token],
  "--mod-id": ["modId", positiveInt],
  "--expected-mod-identifier": ["expectedModIdentifier", nonblank],
  "--expected-version": ["expectedVersion", token],
  "--zip": ["zip", nonblank],
  "--changelog": ["changelog", nonblank],
  "--compatible-version": ["compatibleVersions", nonblank, true],
  "--expected-sha256": ["expectedSha256", hex64],
  "--expected-file-id": ["expectedFileId", positiveInt],
};

const RELEASE = ["--mod-id", "--expected-mod-identifier", "--expected-version", "--zip", "--changelog", "--compatible-version", "--expected-sha256"];

// command -> { required, optional, oneOf }
const COMMANDS = {
  "account set": { required: [] },
  "session status": { required: [] },
  "session renew": { required: ["--expected-account"] },
  "session import-wincred": { required: [], oneOf: ["--expected-account", "--finalize-version"] },
  "release prepare": { required: RELEASE, optional: ["--expected-account"] },
  "release publish": { required: [...RELEASE, "--expected-file-id"], optional: ["--expected-account"] },
};

export function parseArgs(argv) {
  if (argv.length === 0 || argv.some((arg) => HELP_FLAGS.has(arg))) return { command: "help", options: {} };
  const command = argv.slice(0, 2).join(" ");
  const spec = COMMANDS[command];
  if (spec === undefined) throw new BrokerError("INVALID_ARGUMENTS", "invalid command");
  const allowed = [...spec.required, ...(spec.optional ?? []), ...(spec.oneOf ?? [])];

  const options = {};
  const rest = argv.slice(2);
  for (let i = 0; i < rest.length; i += 2) {
    const option = rest[i];
    if (!option.startsWith("--")) throw new BrokerError("INVALID_ARGUMENTS", "unexpected argument");
    if (!allowed.includes(option)) throw invalid(option);
    const [key, parse, repeatable] = OPTIONS[option];
    const raw = rest[i + 1];
    if (raw === undefined || raw.startsWith("-")) throw invalid(option);
    const value = parse(raw);
    if (value === undefined) throw invalid(option);
    if (repeatable) (options[key] ??= []).push(value);
    else if (Object.hasOwn(options, key)) throw invalid(option);
    else options[key] = value;
  }

  for (const option of spec.required) if (!Object.hasOwn(options, OPTIONS[option][0])) throw invalid(option);
  if (spec.oneOf) {
    const present = spec.oneOf.filter((option) => Object.hasOwn(options, OPTIONS[option][0]));
    if (present.length === 0) throw invalid(spec.oneOf[0]);
    if (present.length > 1) throw invalid(present[1]);
  }
  return { command, options };
}
