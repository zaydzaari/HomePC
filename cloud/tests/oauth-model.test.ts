import { describe, expect, it } from "vitest";

class OAuthModel {
  private codes = new Map<string, { expires: number; used: boolean; redirect: string }>();
  private refresh = new Set<string>(); private revoked = false;
  issue(code: string, redirect: string, expires: number): void { this.codes.set(code, { expires, used: false, redirect }); }
  exchange(code: string, redirect: string, now: number): boolean {
    const item = this.codes.get(code); if (!item || item.used || item.expires < now || item.redirect !== redirect) return false;
    item.used = true; this.refresh.add("refresh"); return true;
  }
  refreshToken(token: string): boolean { return !this.revoked && this.refresh.has(token); }
  disconnect(): void { this.revoked = true; }
}

describe("OAuth lifecycle contract", () => {
  it("enforces exact redirect, expiration and one-time code use", () => {
    const model = new OAuthModel(); model.issue("code", "https://exact", 10);
    expect(model.exchange("code", "https://wrong", 1)).toBe(false);
    expect(model.exchange("code", "https://exact", 11)).toBe(false);
    model.issue("fresh", "https://exact", 10); expect(model.exchange("fresh", "https://exact", 1)).toBe(true); expect(model.exchange("fresh", "https://exact", 1)).toBe(false);
  });
  it("supports refresh then revokes it on disconnect", () => {
    const model = new OAuthModel(); model.issue("code", "https://exact", 10); model.exchange("code", "https://exact", 1);
    expect(model.refreshToken("refresh")).toBe(true); model.disconnect(); expect(model.refreshToken("refresh")).toBe(false);
  });
});

