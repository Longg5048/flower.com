const test = require("node:test");
const assert = require("node:assert/strict");
const fs = require("node:fs");
const os = require("node:os");
const path = require("node:path");
const { DatabaseSync } = require("node:sqlite");
const { createHash, randomBytes, randomUUID, scryptSync } = require("node:crypto");
const { createLocalAccounts } = require("../flower.com/lib/local-accounts");

const profile = (overrides = {}) => ({
    email: "ban@demo",
    password: "Mat-khau-thu-123",
    name: "Người thử",
    phone: "0900000000",
    ...overrides
});

function fixture(t) {
    let time = 1_800_000_000_000;
    const accounts = createLocalAccounts({ now: () => time });
    t.after(() => accounts.close());
    return { accounts, advance(ms) { time += ms; } };
}

test("registration needs only a valid username, strong password and profile", t => {
    const { accounts } = fixture(t);
    const user = accounts.register(profile({
        email: "other@demo",
        role: "owner",
        pin: "1234",
        confirmPin: "1234",
        questions: [{ question: "ignored", answer: "ignored" }]
    }));
    assert.equal(user.account.role, "customer");
    assert.equal(user.account.authProvider, "local");
    assert.equal("securityConfigured" in user.account, false);
    assert.equal("pinResetRequired" in user.account, false);
    assert.throws(() => accounts.register(profile({ email: "OTHER@DEMO" })), { code: "account-exists" });
    assert.throws(() => accounts.register(profile({ email: "missing-at" })), { code: "invalid-username" });
    assert.throws(() => accounts.register(profile({ password: "short" })), { code: "invalid-password" });
    assert.throws(() => accounts.register(profile({ password: "password-only" })), { code: "invalid-password" });
    assert.throws(() => accounts.register(profile({ phone: "" })), { code: "invalid-profile" });
});

test("local account service no longer exposes PIN or password recovery methods", t => {
    const { accounts } = fixture(t);
    for (const method of ["reset", "questions", "recoverPin", "changePin", "setupSecurity"]) {
        assert.equal(accounts[method], undefined, method);
    }
});

test("failed login attempts use timed lockouts and a successful login clears the failure count", t => {
    const { accounts, advance } = fixture(t);
    const input = profile();
    accounts.register(input);
    const login = (password = "wrong") => accounts.login({ email: input.email, password });

    for (let i = 0; i < 4; i++) assert.equal(login().status, 401);
    assert.equal(login().retryAfter, 15 * 60);
    advance(15 * 60_000);
    assert.equal(login().retryAfter, 30 * 60);
    advance(30 * 60_000);
    assert.equal(login(input.password).status, 200);
    for (let i = 0; i < 4; i++) assert.equal(login().status, 401);
    assert.equal(login().retryAfter, 15 * 60);
});

test("logout and session expiry invalidate local sessions", t => {
    const { accounts, advance } = fixture(t);
    const user = accounts.register(profile());
    accounts.logout(user.sessionToken);
    assert.equal(accounts.session(user.sessionToken), null);

    const loggedIn = accounts.login(profile());
    assert.ok(accounts.session(loggedIn.sessionToken));
    advance(13 * 60 * 60_000);
    assert.equal(accounts.session(loggedIn.sessionToken), null);
});

test("migration removes legacy recovery secrets but retains account and session", t => {
    let time = 1_800_000_000_000;
    const folder = fs.mkdtempSync(path.join(os.tmpdir(), "flower-local-accounts-"));
    const filename = path.join(folder, `${randomUUID()}.sqlite`);
    const input = profile();
    const salt = randomBytes(16);
    const sessionToken = randomBytes(32).toString("base64url");
    const digest = value => createHash("sha256").update(value).digest("hex");
    const passwordHash = scryptSync(input.password, salt, 32, { N: 32768, r: 8, p: 1, maxmem: 64 * 1024 * 1024 });
    const legacy = new DatabaseSync(filename);
    legacy.exec(`CREATE TABLE local_users (
        id TEXT PRIMARY KEY, username TEXT UNIQUE NOT NULL, name TEXT NOT NULL, phone TEXT NOT NULL,
        password_hash BLOB NOT NULL, salt BLOB NOT NULL, recovery_hash TEXT NOT NULL,
        failures INTEGER NOT NULL DEFAULT 0, locked_until INTEGER NOT NULL DEFAULT 0,
        reset_required INTEGER NOT NULL DEFAULT 0, recovery_failures INTEGER NOT NULL DEFAULT 0,
        recovery_locked_until INTEGER NOT NULL DEFAULT 0, pin_hash BLOB, pin_salt BLOB,
        security_questions TEXT, security_answers TEXT, answer_failures INTEGER NOT NULL DEFAULT 0,
        answer_locked_until INTEGER NOT NULL DEFAULT 0, pin_failures INTEGER NOT NULL DEFAULT 0,
        pin_reset_required INTEGER NOT NULL DEFAULT 0, pin_format_version INTEGER NOT NULL DEFAULT 2);
        CREATE TABLE local_sessions (token_hash TEXT PRIMARY KEY, user_id TEXT NOT NULL, expires_at INTEGER NOT NULL);`);
    legacy.prepare(`INSERT INTO local_users (id, username, name, phone, password_hash, salt, recovery_hash,
        reset_required, pin_hash, pin_salt, security_questions, security_answers)
        VALUES (?, ?, ?, ?, ?, ?, ?, 1, ?, ?, ?, ?)`)
        .run("legacy-user", input.email, input.name, input.phone, passwordHash, salt, "legacy-recovery",
            Buffer.from("old-pin-hash"), salt, '["old question"]', '[{"hash":"old answer hash"}]');
    legacy.prepare("INSERT INTO local_sessions VALUES (?, ?, ?)")
        .run(digest(sessionToken), "legacy-user", time + 60_000);
    legacy.close();

    const accounts = createLocalAccounts({ filename, now: () => time });
    t.after(() => {
        accounts.close();
        fs.rmSync(folder, { recursive: true, force: true });
    });
    assert.equal(accounts.session(sessionToken).email, input.email);
    assert.equal(accounts.login(input).retryAfter, 4 * 60 * 60);

    const migrated = new DatabaseSync(filename);
    const row = migrated.prepare(`SELECT recovery_hash, pin_hash, pin_salt, security_questions, security_answers,
        recovery_failures, answer_failures, pin_failures, pin_reset_required, reset_required
        FROM local_users WHERE id = ?`).get("legacy-user");
    assert.equal(row.recovery_hash, "");
    for (const key of ["pin_hash", "pin_salt", "security_questions", "security_answers"]) assert.equal(row[key], null);
    for (const key of ["recovery_failures", "answer_failures", "pin_failures", "pin_reset_required", "reset_required"]) {
        assert.equal(row[key], 0);
    }
    migrated.close();

    time += 4 * 60 * 60_000;
    assert.equal(accounts.login(input).status, 200);
});
