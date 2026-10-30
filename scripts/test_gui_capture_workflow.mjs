import assert from 'node:assert/strict';
import {readFileSync} from 'node:fs';
import {createRequire} from 'node:module';
import test from 'node:test';

const workflow = readFileSync(new URL('../.github/workflows/gui-capture-report.yml', import.meta.url), 'utf8').split(/\r?\n/);
const AsyncFunction = Object.getPrototypeOf(async function () {}).constructor;
function script(name) {
    const start = workflow.findIndex(line => line.trim() === `- name: ${name}`);
    assert.ok(start >= 0, `Missing workflow step ${name}`);
    const offset = workflow.findIndex((line, index) => index > start && line.trim() === 'script: |');
    const body = [];
    for (const line of workflow.slice(offset + 1)) {
        if (!line.startsWith('            ')) break;
        body.push(line.slice(12));
    }
    const compiled = new AsyncFunction('github', 'context', 'core', 'require', body.join('\n'));
    return (...args) => compiled(...args, createRequire(import.meta.url));
}

test('automatic evidence selection rejects unrelated or stale PR runs before publishing', async () => {
    const select = script('Select immutable evidence and bind automatic runs to the current PR');
    const sha = 'a'.repeat(40), base = 'b'.repeat(40);
    const run = {id: 123, event: 'pull_request', name: 'GUI Captures', repository: {full_name: 'owner/repo'}, head_sha: sha,
        pull_requests: [{number: 7, head: {sha}, base: {sha: base}}]};
    const pr = {number: 7, state: 'open', head: {sha}};
    const context = {eventName: 'workflow_run', repo: {owner: 'owner', repo: 'repo'}, payload: {workflow_run: {id: 123}}};
    const github = {rest: {actions: {getWorkflowRun: async () => ({data: run})}, pulls: {get: async () => ({data: pr})}}};
    const outputs = {};
    const core = {setOutput: (key, value) => outputs[key] = value};
    await select(github, context, core);
    assert.equal(outputs.source_ref, sha);
    assert.equal(outputs.baseline_ref, base);
    assert.equal(outputs.reviewed, 'false');
    assert.equal(outputs.publish, 'true');
    pr.head.sha = 'c'.repeat(40);
    await assert.rejects(select(github, context, core), /no longer the open PR head/);
    pr.head.sha = sha;
    run.name = 'Other workflow';
    await assert.rejects(select(github, context, core), /workflow identity/);
});

test('sticky publication rechecks the exact head immediately before any write', async () => {
    const publish = script('Upsert explicitly selected PR sticky report');
    process.env.PR_NUMBER = '7';
    process.env.EXPECTED_HEAD = 'a'.repeat(40);
    let writes = 0;
    const github = {rest: {pulls: {get: async () => ({data: {state: 'open', head: {sha: 'b'.repeat(40)}}})},
        issues: {createComment: async () => writes++, updateComment: async () => writes++}}};
    await assert.rejects(publish(github, {repo: {owner: 'owner', repo: 'repo'}}, {}), /PR head changed/);
    assert.equal(writes, 0);
});
