import { test as base, expect } from "@playwright/test";
import { randomUUID } from "node:crypto";
import { createLocalAccountWorker } from "../lib/local-account-worker";
import { createLocalServer } from "../lib/local-server";

const test = base.extend({ baseURL: async ({}, use) => {
  const accounts = createLocalAccountWorker();
  const server = createLocalServer({ root: process.cwd(), localAccounts: accounts });
  await new Promise(resolve => server.listen(0, "127.0.0.1", resolve));
  try { await use(`http://127.0.0.1:${server.address().port}`); }
  finally { await new Promise(resolve => { server.close(resolve); server.closeAllConnections(); }); await accounts.close(); }
} });

test.beforeEach(async ({ page }) => {
  await page.route("https://www.gstatic.com/firebasejs/**", route => route.abort());
});

test("local signup needs no PIN or security questions and session login still works", async ({ page }) => {
  const email = `ban-${randomUUID()}@demo`;
  const password = "Tai-khoan-tu-tao-123";
  await page.goto("/login.html");
  await expect(page.locator("[data-google-auth]").first()).toBeHidden();
  await expect(page.locator("#signup-security")).toHaveCount(0);
  await page.locator("#signup").click();
  for (const [id, value] of Object.entries({
    name: "Khách",
    lastName: "thử nghiệm",
    email,
    phone: "0900000000",
    password,
    checkpassword: password
  })) await page.locator(`#${id}`).fill(value);
  await page.locator("#signUp .submit").click();
  await expect(page).toHaveURL(/product\.html/, { timeout: 15_000 });

  await page.evaluate(() => {
    const account = JSON.parse(localStorage.getItem("loggedUser"));
    localStorage.setItem("loggedUser", JSON.stringify({ ...account, role: "owner", name: "Forged" }));
  });
  await page.goto("/account.html");
  await expect(page.locator("#account-name")).toHaveText("Khách thử nghiệm");
  await expect(page.locator("#account-email")).toHaveText(email);
  await expect(page.locator("#account-phone")).toHaveText("0900000000");
  await expect(page.locator("#security-form")).toHaveCount(0);
  expect(await page.evaluate(() => JSON.parse(localStorage.getItem("loggedUser")).role)).toBe("customer");
  await page.locator("#logout-button").click();
  await expect(page).toHaveURL(/login\.html/);
  await page.locator("#email1").fill(email);
  await page.locator("#password1").fill(password);
  await page.locator("#signIn .submit").click();
  await expect(page).toHaveURL(/product\.html/);
});

test("local accounts see that self-service password recovery is unavailable", async ({ page }) => {
  await page.goto("/forgot-password.html");
  await expect(page.locator("#local-recovery-notice")).toBeVisible();
  await expect(page.locator("#local-recovery-signup")).toBeVisible();
  await expect(page.locator("#recovery-form")).toBeHidden();
  await expect(page.locator("#pin-recovery")).toHaveCount(0);
  await page.locator("#local-recovery-signup").click();
  await expect(page).toHaveURL(/login\.html#signup$/);
  await expect(page.locator("#email")).toBeVisible();
});
