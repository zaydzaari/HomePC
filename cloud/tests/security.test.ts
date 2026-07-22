import { describe, expect, it } from "vitest";
import { constantTimeEqual, randomToken, sha256 } from "../src/security";

describe("OAuth and device security primitives", () => {
  it("validates OAuth client and device tokens without direct comparison", async () => {
    expect(await constantTimeEqual("client", "client")).toBe(true);
    expect(await constantTimeEqual("client", "wrong")).toBe(false);
  });
  it("generates high entropy URL-safe tokens", () => {
    const one = randomToken(); const two = randomToken();
    expect(one.length).toBeGreaterThanOrEqual(43); expect(one).not.toBe(two); expect(one).toMatch(/^[A-Za-z0-9_-]+$/);
  });
  it("hashes stored token material", async () => { expect(await sha256("secret")).toHaveLength(64); expect(await sha256("secret")).not.toContain("secret"); });
});

