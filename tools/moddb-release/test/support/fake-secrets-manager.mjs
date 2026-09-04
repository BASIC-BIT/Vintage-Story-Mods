// In-process stand-in for SecretsManagerClient. Records every command by
// class name and input; replies with whatever the test scripted for that name.

export function awsError(name, message = "aws-message-never-print") {
  const error = new Error(message);
  error.name = name;
  error.$metadata = { httpStatusCode: 400, requestId: "fixture-request-id" };
  return error;
}

export class FakeSecretsManagerClient {
  constructor() {
    this.calls = [];
    this.scripts = new Map();
  }

  // reply: a plain output object, or (input) => output (may throw).
  respond(commandName, reply) {
    this.scripts.set(commandName, reply);
    return this;
  }

  async send(command) {
    const name = command.constructor.name;
    this.calls.push({ name, input: structuredClone(command.input) });
    const reply = this.scripts.get(name);
    if (reply === undefined) throw new Error(`unscripted command: ${name}`);
    return typeof reply === "function" ? reply(command.input) : reply;
  }

  inputs(commandName) {
    return this.calls.filter((call) => call.name === commandName).map((call) => call.input);
  }

  lastInput(commandName) {
    return this.inputs(commandName).at(-1);
  }
}
