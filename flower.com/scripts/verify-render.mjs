// Read-only checks for the deployed frontend and ASP.NET host. No credentials required.
import assert from "node:assert/strict";

const origin = new URL(process.argv[2] || "https://flower-com.onrender.com");
assert.equal(origin.protocol, "https:", "Deployment verification requires HTTPS");
assert.equal(origin.username + origin.password + origin.search + origin.hash, "");
assert.equal(origin.pathname, "/", "Pass the deployment origin without a path");
const checks = [];
for (const [path, status, type] of [
  ["/health", 200, "application/json"],
  ["/", 200, "text/html"],
  ["/api/products", 200, "application/json"],
  ["/api/local-auth/session", 404, null]
]) {
  const response = await fetch(new URL(path, origin), {
    redirect: "error", signal: AbortSignal.timeout(90_000)
  });
  assert.equal(response.status, status, `${path}: unexpected status`);
  if (type) {
    assert.ok(response.headers.get("content-type")?.startsWith(type), `${path}: content type`);
    assert.ok(response.headers.get("content-security-policy"), `${path}: missing CSP`);
    assert.match(response.headers.get("strict-transport-security") || "", /max-age=[1-9]\d*/);
  }
  if (path === "/health") {
    const health = await response.json();
    assert.equal(health.status, "ok");
    assert.equal(health.service, "flowerflow-api");
  } else if (path === "/api/products") {
    assert.ok(Array.isArray((await response.json()).products), "Catalog must contain products array");
  } else {
    await response.arrayBuffer();
  }
  checks.push({ path, status: response.status });
}
console.log(JSON.stringify({ origin: origin.origin, checkedAt: new Date().toISOString(), checks }, null, 2));
