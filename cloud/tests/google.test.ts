import { describe, expect, it } from "vitest";
import { DEVICES, mapGoogleCommand } from "../src/devices";
import { handleSmartHome, type HomeCoordinator } from "../src/google";

class FakeCoordinator implements HomeCoordinator {
  valid = true; online = true; fail = false; timeout = false; disconnected = false;
  calls: Array<{ action: string; parameters: Record<string, boolean | number | string> }> = [];
  async tokenValid(): Promise<boolean> { return this.valid; }
  async linked(): Promise<boolean> { return true; }
  async status(): Promise<{ online: boolean; volume: number; muted: boolean; mode: string | null }> { return { online: this.online, volume: 42, muted: true, mode: "study" }; }
  async execute(action: string, parameters: Record<string, boolean | number | string>): Promise<{ success: boolean; error?: string }> {
    this.calls.push({ action, parameters });
    if (this.timeout) return { success: false, error: "timeout" };
    return this.fail ? { success: false, error: "action_failed" } : { success: true };
  }
  async disconnect(): Promise<void> { this.disconnected = true; }
}

const intent = (name: string, payload?: unknown) => ({ requestId: "req", inputs: [{ intent: name, ...(payload === undefined ? {} : { payload }) }] });

describe("Google Home fulfillment", () => {
  it("returns a complete SYNC schema", async () => {
    const response = await handleSmartHome(intent("action.devices.SYNC"), "token", new FakeCoordinator(), 10);
    const payload = response.payload as { devices: unknown[] };
    expect(payload.devices).toHaveLength(DEVICES.length);
    expect(JSON.stringify(response)).toContain("open-notepad");
    expect(JSON.stringify(response)).toContain("media-play-pause");
  });

  it("returns current QUERY state and momentary switches off", async () => {
    const response = await handleSmartHome(intent("action.devices.QUERY", { devices: [{ id: "open-notepad" }, { id: "media-next" }] }), "token", new FakeCoordinator(), 10);
    const devices = (response.payload as { devices: Record<string, Record<string, unknown>> }).devices;
    expect(devices["open-notepad"]).toEqual({ online: true, on: false });
    expect(devices["media-next"]).toEqual({ online: true, on: false });
  });

  it("maps Open Notepad EXECUTE and waits for result", async () => {
    const fake = new FakeCoordinator();
    const response = await handleSmartHome(intent("action.devices.EXECUTE", { commands: [{ devices: [{ id: "open-notepad" }], execution: [{ command: "action.devices.commands.OnOff", params: { on: true } }] }] }), "token", fake, 10);
    expect(fake.calls[0]?.action).toBe("open_notepad"); expect(JSON.stringify(response)).toContain("SUCCESS");
  });

  it("does not retrigger a momentary switch on OFF", async () => {
    const fake = new FakeCoordinator();
    await handleSmartHome(intent("action.devices.EXECUTE", { commands: [{ devices: [{ id: "open-notepad" }], execution: [{ command: "action.devices.commands.OnOff", params: { on: false } }] }] }), "token", fake, 10);
    expect(fake.calls).toHaveLength(0);
  });

  it("handles unsupported device, unsupported command, offline and timeout", async () => {
    const execute = (id: string, command: string) => intent("action.devices.EXECUTE", { commands: [{ devices: [{ id }], execution: [{ command, params: { on: true } }] }] });
    expect(JSON.stringify(await handleSmartHome(execute("missing", "action.devices.commands.OnOff"), "token", new FakeCoordinator(), 10))).toContain("deviceNotFound");
    expect(JSON.stringify(await handleSmartHome(execute("open-notepad", "bad"), "token", new FakeCoordinator(), 10))).toContain("functionNotSupported");
    const offline = new FakeCoordinator(); offline.online = false;
    expect(JSON.stringify(await handleSmartHome(execute("open-notepad", "action.devices.commands.OnOff"), "token", offline, 10))).toContain("deviceOffline");
    const timeout = new FakeCoordinator(); timeout.timeout = true;
    expect(JSON.stringify(await handleSmartHome(execute("open-notepad", "action.devices.commands.OnOff"), "token", timeout, 10))).toContain("timeout");
  });

  it("revokes on DISCONNECT and rejects invalid access tokens", async () => {
    const fake = new FakeCoordinator(); await handleSmartHome(intent("action.devices.DISCONNECT"), "token", fake, 10); expect(fake.disconnected).toBe(true);
    fake.valid = false; expect(await handleSmartHome(intent("action.devices.SYNC"), "bad", fake, 10)).toEqual({ error: "unauthorized" });
  });

  it("maps media switches without exposing an incomplete media-device schema", () => {
    expect(mapGoogleCommand("media-next", "action.devices.commands.OnOff", { on: true })).toEqual({ action: "next_track", parameters: {} });
    expect(mapGoogleCommand("mute-pc", "action.devices.commands.OnOff", { on: true })).toEqual({ action: "mute", parameters: { muted: true } });
  });
});
