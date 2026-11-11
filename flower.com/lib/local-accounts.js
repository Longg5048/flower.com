const { DatabaseSync } = require("node:sqlite");
const { randomBytes, randomUUID, scryptSync, timingSafeEqual, createHash } = require("node:crypto");
const fs = require("node:fs");
const path = require("node:path");
const { restriction, LOCK_MINUTES } = require("./login-protection");
const { isStrongPassword, message: passwordPolicyMessage } = require("../js/core/password-policy");
const { SESSION_MS } = require("./local-session");

const PASSWORD_OPTIONS = { N: 32768, r: 8, p: 1, maxmem: 64 * 1024 * 1024 };
const digest = value => createHash("sha256").update(String(value)).digest("hex");
const passwordHash = (value, salt) => scryptSync(value, salt, 32, PASSWORD_OPTIONS);
const same = (a, b) => a.length === b.length && timingSafeEqual(Buffer.from(a), Buffer.from(b));
function invalid(code, message, status = 400) { throw Object.assign(new Error(message), { code, status }); }
function username(value) {
    const result = typeof value === "string" ? value.trim().toLowerCase() : "";
    if (result.length > 254 || !/^[^\s@]+@[^\s@]+$/.test(result)) invalid("invalid-username", "Tên đăng nhập cần có @, ví dụ ban@demo.");
    return result;
}
function password(value, minimum = 8) {
    if (typeof value !== "string" || value.length < minimum || value.length > 4096) {
        invalid("invalid-password", minimum === 8 ? passwordPolicyMessage : "Vui lòng nhập mật khẩu.");
    }
    if (minimum === 8 && !isStrongPassword(value)) invalid("invalid-password", passwordPolicyMessage);
    return value;
}
function profile(input) {
    const name = typeof input.name === "string" ? input.name.trim() : "";
    const phone = typeof input.phone === "string" ? input.phone.trim() : "";
    if (!name || name.length > 120 || !phone || phone.length > 30) invalid("invalid-profile", "Vui lòng nhập đầy đủ họ tên và số điện thoại (tối đa 120 và 30 ký tự).");
    return { name, phone };
}
function publicAccount(row) {
    return { uid: row.id, email: row.username, name: row.name || "Khách hàng", phone: row.phone,
        role: "customer", authProvider: "local", sessionExpiresAt: Number(row.session_expires_at) };
}
function localFailureState(state, time) {
    const failures = Math.min(state.failures + 1, LOCK_MINUTES.length + 5);
    const lockIndex = Math.min(failures - 5, LOCK_MINUTES.length - 1);
    return {
        failures,
        lockedUntil: failures >= 5 ? time + LOCK_MINUTES[Math.max(0, lockIndex)] * 60_000 : 0,
        resetRequired: false
    };
}
function createLocalAccounts({ filename = ":memory:", now = Date.now } = {}) {
    if (filename !== ":memory:") fs.mkdirSync(path.dirname(filename), { recursive: true, mode: 0o700 });
    const db = new DatabaseSync(filename);
    db.exec(`PRAGMA foreign_keys = ON; PRAGMA busy_timeout = 5000; PRAGMA journal_mode = WAL;
        CREATE TABLE IF NOT EXISTS local_users (
            id TEXT PRIMARY KEY, username TEXT UNIQUE NOT NULL, name TEXT NOT NULL, phone TEXT NOT NULL,
            password_hash BLOB NOT NULL, salt BLOB NOT NULL, failures INTEGER NOT NULL DEFAULT 0,
            locked_until INTEGER NOT NULL DEFAULT 0
        );
        CREATE TABLE IF NOT EXISTS local_sessions (
            token_hash TEXT PRIMARY KEY, user_id TEXT NOT NULL REFERENCES local_users(id) ON DELETE CASCADE,
            expires_at INTEGER NOT NULL
        );`);
    const userColumns = new Set(db.prepare("PRAGMA table_info(local_users)").all().map(row => row.name));
    const cleanup = ["pin_hash", "pin_salt", "security_questions", "security_answers"]
        .filter(column => userColumns.has(column)).map(column => `${column} = NULL`);
    if (userColumns.has("recovery_hash")) cleanup.push("recovery_hash = ''");
    for (const column of ["recovery_failures", "recovery_locked_until", "answer_failures", "answer_locked_until", "pin_failures", "pin_reset_required"]) {
        if (userColumns.has(column)) cleanup.push(`${column} = 0`);
    }
    if (userColumns.has("reset_required")) {
        cleanup.push("failures = CASE WHEN reset_required = 1 THEN 10 ELSE failures END");
        cleanup.push(`locked_until = CASE WHEN reset_required = 1 THEN MAX(locked_until, ${Number(now()) + LOCK_MINUTES.at(-1) * 60_000}) ELSE locked_until END`);
        cleanup.push("reset_required = 0");
    }
    if (cleanup.length) db.prepare(`UPDATE local_users SET ${cleanup.join(", ")}`).run();

    function transaction(action) {
        db.exec("BEGIN IMMEDIATE");
        try { const result = action(); db.exec("COMMIT"); return result; }
        catch (error) { db.exec("ROLLBACK"); throw error; }
    }
    const userByName = db.prepare("SELECT * FROM local_users WHERE username = ?");
    const dummySalt = randomBytes(16), dummyHash = passwordHash(randomBytes(32).toString("hex"), dummySalt);
    function removePresentedSessions(token) {
        for (const value of new Set(Array.isArray(token) ? token : [token])) {
            if (typeof value === "string" && /^[A-Za-z0-9_-]{43}$/.test(value)) {
                db.prepare("DELETE FROM local_sessions WHERE token_hash = ?").run(digest(value));
            }
        }
    }
    function newSession(user, time, previousToken) {
        const token = randomBytes(32).toString("base64url");
        db.prepare("DELETE FROM local_sessions WHERE expires_at <= ?").run(time);
        removePresentedSessions(previousToken);
        db.prepare("INSERT INTO local_sessions VALUES (?, ?, ?)").run(digest(token), user.id, time + SESSION_MS);
        return token;
    }
    function authenticated(user, previousToken) {
        const time = now();
        return { status: 200, account: publicAccount({ ...user, session_expires_at: time + SESSION_MS }),
            sessionToken: newSession(user, time, previousToken) };
    }
    function sessionUser(token) {
        if (typeof token !== "string" || !/^[A-Za-z0-9_-]{43}$/.test(token)) return null;
        return db.prepare(`SELECT u.*, s.expires_at AS session_expires_at FROM local_sessions s JOIN local_users u ON u.id = s.user_id
            WHERE s.token_hash = ? AND s.expires_at > ?`).get(digest(token), now());
    }
    function register(input = {}, previousToken) {
        const email = username(input.email), secret = password(input.password), details = profile(input);
        if (userByName.get(email)) invalid("account-exists", "Tên đăng nhập này đã được sử dụng.", 409);
        return transaction(() => {
            if (userByName.get(email)) invalid("account-exists", "Tên đăng nhập này đã được sử dụng.", 409);
            const salt = randomBytes(16), id = `local-${randomUUID()}`;
            const columns = ["id", "username", "name", "phone", "password_hash", "salt"];
            const values = [id, email, details.name, details.phone, passwordHash(secret, salt), salt];
            if (userColumns.has("recovery_hash")) {
                columns.push("recovery_hash");
                values.push("");
            }
            db.prepare(`INSERT INTO local_users (${columns.join(", ")}) VALUES (${columns.map(() => "?").join(", ")})`).run(...values);
            return authenticated(userByName.get(email), previousToken);
        });
    }
    function login(input = {}, previousToken) {
        const email = username(input.email), secret = password(input.password, 1);
        return transaction(() => {
            const user = userByName.get(email);
            if (!user) { same(passwordHash(secret, dummySalt), dummyHash); return { status: 401, code: "invalid-credential" }; }
            const time = now();
            const state = { failures: user.failures, lockedUntil: user.locked_until, resetRequired: false };
            const stop = restriction(state, time);
            if (stop) return stop;
            if (!same(passwordHash(secret, user.salt), user.password_hash)) {
                const next = localFailureState(state, time);
                db.prepare("UPDATE local_users SET failures = ?, locked_until = ? WHERE id = ?")
                    .run(next.failures, next.lockedUntil, user.id);
                return restriction(next, time) || { status: 401, code: "invalid-credential" };
            }
            db.prepare("UPDATE local_users SET failures = 0, locked_until = 0 WHERE id = ?").run(user.id);
            return authenticated(user, previousToken);
        });
    }
    function session(token) { const user = sessionUser(token); return user ? publicAccount(user) : null; }
    function logout(token) { transaction(() => removePresentedSessions(token)); }
    return { register, login, session, logout, close: () => db.close() };
}
module.exports = { createLocalAccounts };
