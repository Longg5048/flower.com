const test = require("node:test");
const assert = require("node:assert/strict");
const path = require("node:path");
const { createLocalAccounts } = require("../flower.com/lib/local-accounts");
const { createLocalServer } = require("../flower.com/lib/local-server");
const profile = require("./helpers/local-profile");

async function fixture(t) {
    const accounts = createLocalAccounts();
    const server = createLocalServer({ root: path.join(__dirname, "../flower.com"), localAccounts: accounts });
    await new Promise((resolve, reject) => {
        server.once("error", reject);
        server.listen(0, "127.0.0.1", resolve);
    });
    t.after(() => new Promise(resolve => {
        server.close(() => { accounts.close(); resolve(); });
        server.closeAllConnections();
    }));
    const base = `http://127.0.0.1:${server.address().port}`;
    const request = (action, body, cookie) => fetch(`${base}/api/local-auth/${action}`, {
        method: action === "session" ? "GET" : "POST",
        headers: { "Content-Type": "application/json", Origin: base, ...(cookie ? { Cookie: cookie } : {}) },
        ...(body ? { body: JSON.stringify(body) } : {})
    });
    return { base, request };
}

test("local signup needs no PIN and returns an HttpOnly session", async t => {
    const { base, request } = await fixture(t);
    const loginPage = await (await fetch(`${base}/login.html`)).text();
    assert.doesNotMatch(loginPage, /signup-security|signup-pin|signup-questions|Khôi phục tài khoản/);
    const accountPage = await (await fetch(`${base}/account.html`)).text();
    assert.doesNotMatch(accountPage, /security-form|PIN khôi phục|security-setup-questions/);
    const forgotPage = await (await fetch(`${base}/forgot-password.html`)).text();
    assert.match(forgotPage, /local-recovery-notice/);
    assert.doesNotMatch(forgotPage, /forgot-pin|pin-recovery-form|recovery-pin/);

    const registered = await request("register", profile({ email: "own@example.test", role: "owner" }));
    assert.equal(registered.status, 200);
    const cookie = registered.headers.get("set-cookie");
    assert.match(cookie, /HttpOnly/);
    assert.match(cookie, /SameSite=Strict/);
    const result = await registered.json();
    assert.equal(result.account.role, "customer");
    assert.equal(result.account.authProvider, "local");
    assert.ok(!Object.hasOwn(result, "sessionToken"));
    assert.ok(!Object.hasOwn(result.account, "securityConfigured"));
    assert.ok(!JSON.stringify(result).includes("Mat-khau-thu-123"));
    const session = await request("session", null, cookie.split(";")[0]);
    assert.equal((await session.json()).account.uid, result.account.uid);
    assert.equal((await request("session")).status, 401);
    assert.equal((await fetch(`${base}/.tools/local-auth/accounts.sqlite`)).status, 404);
});

test("local account recovery endpoints are unavailable while login remains supported", async t => {
    const { request } = await fixture(t);
    const registered = await request("register", profile());
    const cookie = registered.headers.get("set-cookie").split(";")[0];
    for (const action of ["reset", "questions", "recover-pin", "change-pin", "setup-security"]) {
        const response = await request(action, profile(), cookie);
        assert.equal(response.status, 404, action);
    }
    assert.equal((await request("login", { email: profile().email, password: profile().password })).status, 200);
});
