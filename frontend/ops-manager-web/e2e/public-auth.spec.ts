import { expect, test } from "@playwright/test";

test("tenant login exposes the required credentials", async ({ page }) => {
  await page.goto("/en/login");
  await expect(page.getByRole("heading", { name: /sign in/i })).toBeVisible();
  await expect(page.getByLabel(/organization id/i)).toBeVisible();
  await expect(page.getByLabel(/email/i)).toBeVisible();
  await expect(page.getByLabel(/password/i)).toBeVisible();
});

test("Arabic routes use right-to-left document direction", async ({ page }) => {
  await page.goto("/ar/login");
  await expect(page.locator("html")).toHaveAttribute("dir", "rtl");
});
