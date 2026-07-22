import { DEVICES, findDevice, mapGoogleCommand } from "./devices";

export interface HomeCoordinator {
  tokenValid(token: string): Promise<boolean>;
  linked(): Promise<boolean>;
  status(): Promise<{ online: boolean; volume: number; muted: boolean; mode: string | null }>;
  execute(action: string, parameters: Record<string, boolean | number | string>, timeoutMs: number): Promise<{ success: boolean; error?: string }>;
  disconnect(token: string): Promise<void>;
}

type JsonObject = Record<string, unknown>;

export async function handleSmartHome(body: unknown, token: string, coordinator: HomeCoordinator, timeoutMs: number): Promise<JsonObject> {
  if (!await coordinator.tokenValid(token)) return { error: "unauthorized" };
  if (!body || typeof body !== "object") return { error: "invalid_request" };
  const request = body as JsonObject;
  const requestId = typeof request.requestId === "string" ? request.requestId : crypto.randomUUID();
  const inputs = Array.isArray(request.inputs) ? request.inputs : [];
  const first = inputs[0] as JsonObject | undefined;
  const intent = first?.intent;
  if (intent === "action.devices.SYNC") {
    return { requestId, payload: { agentUserId: "homepc-private-user", devices: DEVICES.map((d) => ({
      id: d.id, type: d.type, traits: d.traits, name: { name: d.name, defaultNames: [d.name] },
      willReportState: false, attributes: d.attributes, deviceInfo: { manufacturer: "HomePC", model: "Virtual Control", swVersion: "1.0.0" },
    })) } };
  }
  if (intent === "action.devices.QUERY") {
    const state = await coordinator.status();
    const payload = first?.payload as JsonObject | undefined;
    const devices = Array.isArray(payload?.devices) ? payload.devices : [];
    const response: Record<string, unknown> = {};
    for (const raw of devices) {
      const id = (raw as JsonObject).id;
      if (typeof id !== "string" || !findDevice(id)) continue;
      response[id] = { online: state.online, on: false };
    }
    return { requestId, payload: { devices: response } };
  }
  if (intent === "action.devices.DISCONNECT") {
    await coordinator.disconnect(token);
    return {};
  }
  if (intent !== "action.devices.EXECUTE") return { requestId, payload: { errorCode: "protocolError" } };
  const payload = first?.payload as JsonObject | undefined;
  const groups = Array.isArray(payload?.commands) ? payload.commands : [];
  const responses: JsonObject[] = [];
  const online = (await coordinator.status()).online;
  for (const groupRaw of groups) {
    const group = groupRaw as JsonObject;
    const executions = Array.isArray(group.execution) ? group.execution : [];
    const devices = Array.isArray(group.devices) ? group.devices : [];
    for (const executionRaw of executions) {
      const execution = executionRaw as JsonObject;
      const command = typeof execution.command === "string" ? execution.command : "";
      for (const deviceRaw of devices) {
        const id = (deviceRaw as JsonObject).id;
        if (typeof id !== "string" || !findDevice(id)) {
          responses.push({ ids: typeof id === "string" ? [id] : [], status: "ERROR", errorCode: "deviceNotFound" });
          continue;
        }
        if (!online) { responses.push({ ids: [id], status: "ERROR", errorCode: "deviceOffline" }); continue; }
        const mapped = mapGoogleCommand(id, command, execution.params);
        if (mapped === "unsupported") { responses.push({ ids: [id], status: "ERROR", errorCode: "functionNotSupported" }); continue; }
        const result = mapped.action === null ? { success: true } : await coordinator.execute(mapped.action, mapped.parameters, timeoutMs);
        responses.push(result.success
          ? { ids: [id], status: "SUCCESS", states: { online: true, on: false } }
          : { ids: [id], status: "ERROR", errorCode: result.error === "timeout" ? "timeout" : "hardError" });
      }
    }
  }
  return { requestId, payload: { commands: responses } };
}
