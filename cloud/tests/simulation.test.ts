import { expect, it } from "vitest";
import { handleSmartHome, type HomeCoordinator } from "../src/google";

it("simulates Google EXECUTE through a mocked agent result", async () => {
  let opened = false;
  const coordinator: HomeCoordinator = {
    tokenValid: async () => true, linked: async () => true,
    status: async () => ({ online: true, volume: 50, muted: false, mode: null }),
    execute: async (action) => { opened = action === "open_notepad"; return { success: opened }; },
    disconnect: async () => undefined,
    routines: async () => [], willReportState: async () => false, reportState: async () => false,
  };
  const request = { requestId: "e2e", inputs: [{ intent: "action.devices.EXECUTE", payload: { commands: [{ devices: [{ id: "open-notepad" }], execution: [{ command: "action.devices.commands.OnOff", params: { on: true } }] }] } }] };
  const response = await handleSmartHome(request, "access-token", coordinator, 8_000);
  expect(opened).toBe(true); expect(JSON.stringify(response)).toContain("SUCCESS");
});
