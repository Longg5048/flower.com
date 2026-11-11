const test = require("node:test");
const assert = require("node:assert/strict");
const { randomUUID } = require("node:crypto");
const { createLocalAccounts } = require("../flower.com/lib/local-accounts");
const profile = require("./helpers/local-profile");

test("registering a recovery PIN does not require security questions", t => {
    const accounts = createLocalAccounts(); t.after(() => accounts.close());
    const input = profile(); delete input.questions;
    const result = accounts.register(input);
    assert.equal(result.status, 200);
    assert.equal(result.account.securityConfigured, true);
    assert.equal(accounts.login(input).status, 200);
});

test("PIN recovery requires the exact current password and revokes existing sessions", t => {
    const accounts = createLocalAccounts(); t.after(() => accounts.close());
    const input = profile(), registered = accounts.register(input);
    const body = { ...input, newPin: "6273", confirmPin: "6273" };
    const withoutPassword = { ...body, answers: Array.from({ length: 3 }, () => randomUUID()) }; delete withoutPassword.password;
    assert.throws(() => accounts.recoverPin(withoutPassword), { code: "invalid-password" });
    assert.equal(accounts.recoverPin({ ...body, password: input.password.toUpperCase() }).status, 401);
    assert.equal(accounts.recoverPin(body, registered.sessionToken).status, 200);
    assert.equal(accounts.session(registered.sessionToken), null);
    assert.equal(accounts.login(input).status, 200, "recovering the PIN does not change the password");
    const newPassword = `a${randomUUID()}`;
    assert.equal(accounts.reset({ ...input, newPassword }).status, 401);
    assert.equal(accounts.reset({ ...input, pin: body.newPin, newPassword }).status, 200);
});

test("legacy questions and recovery cooldowns do not require account re-enrollment", t => {
    const { DatabaseSync } = require("node:sqlite");
    const path = require("node:path");
    const filename = path.join(__dirname, "../flower.com/.tools/local-account-tests", `old-questions-${randomUUID()}.sqlite`);
    let now = 1_800_000_000_000;
    const input = profile(), original = createLocalAccounts({ filename, now: () => now });
    const registered = original.register(input); original.close();
    const database = new DatabaseSync(filename);
    const oldPassword = database.prepare("SELECT password_hash FROM local_users WHERE username = ?").get(input.email).password_hash;
    database.prepare("UPDATE local_users SET security_questions = ?, security_answers = ?, answer_failures = 5, answer_locked_until = ?")
        .run(JSON.stringify(["Câu hỏi cũ"]), "unused-legacy-proof", now + 900_000);
    database.close();
    const migrated = createLocalAccounts({ filename, now: () => now }); t.after(() => migrated.close());
    assert.equal(migrated.session(registered.sessionToken).securityConfigured, true);
    const recovery = { ...input, newPin: "6273", confirmPin: "6273" };
    assert.equal(migrated.recoverPin(recovery).code, "pin-recovery-locked");
    now += 900_000;
    assert.equal(migrated.recoverPin(recovery).status, 200);
    assert.equal(migrated.login(input).account.uid, registered.account.uid);
    const inspector = new DatabaseSync(filename);
    const row = inspector.prepare("SELECT password_hash, security_questions, security_answers FROM local_users WHERE username = ?").get(input.email);
    assert.deepEqual(row.password_hash, oldPassword);
    assert.equal(row.security_questions, null);
    assert.equal(row.security_answers, null);
    inspector.close();
});

test("PIN recovery password attempts retain their own cooldown after the old PIN is locked", t => {
    let now = 1_800_000_000_000;
    const accounts = createLocalAccounts({ now: () => now }); t.after(() => accounts.close());
    const input = profile(); accounts.register(input);
    for (let i = 0; i < 5; i++) accounts.reset({ ...input, pin: "0000", newPassword: `a${randomUUID()}` });
    assert.equal(accounts.login(input).account.pinResetRequired, true);
    const body = { ...input, newPin: "6273", confirmPin: "6273" };
    for (let remaining = 4; remaining >= 0; remaining--) {
        const result = accounts.recoverPin({ ...body, password: randomUUID() });
        assert.equal(result.attemptsRemaining, remaining);
        assert.equal(result.status, remaining ? 401 : 429);
    }
    assert.equal(accounts.recoverPin(body).status, 429);
    now += 15 * 60_000;
    assert.equal(accounts.recoverPin(body).status, 200);
    assert.equal(accounts.login(input).account.pinResetRequired, false);
});
