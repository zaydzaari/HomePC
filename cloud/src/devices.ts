import type { ActionId } from "./protocol";

export interface DeviceDefinition {
  id: string;
  name: string;
  type: string;
  traits: string[];
  attributes: Record<string, unknown>;
  action?: ActionId;
  dangerous?: boolean;
}

export interface RoutineDescriptor { id: string; name: string }

const momentary = (id: string, name: string, action: ActionId, dangerous = false): DeviceDefinition => ({
  id, name, action, dangerous,
  type: "action.devices.types.SWITCH",
  traits: ["action.devices.traits.OnOff"],
  attributes: { commandOnlyOnOff: false, queryOnlyOnOff: false },
});

export const DEVICES: readonly DeviceDefinition[] = [
  momentary("open-notepad", "Open Notepad", "open_notepad"),
  momentary("open-steam", "Open Steam", "open_steam"),
  momentary("open-discord", "Open Discord", "open_discord"),
  momentary("open-chrome", "Open Chrome", "open_chrome"),
  momentary("gaming-mode", "Gaming Mode", "gaming_mode"),
  momentary("study-mode", "Study Mode", "study_mode"),
  momentary("movie-mode", "Movie Mode", "movie_mode"),
  momentary("lock-pc", "Lock PC", "lock_pc"),
  momentary("sleep-pc", "Sleep PC", "sleep_pc", true),
  momentary("restart-pc", "Restart PC", "restart_pc", true),
  momentary("shutdown-pc", "Shut Down PC", "shutdown_pc", true),
  momentary("monitor-off", "Turn Monitor Off", "monitor_off"),
  momentary("mute-pc", "Mute PC", "mute"),
  momentary("media-play-pause", "Media Play Pause", "play_pause"),
  momentary("media-next", "Media Next", "next_track"),
  momentary("media-previous", "Media Previous", "previous_track"),
] as const;

export function allDevices(routines: readonly RoutineDescriptor[] = []): readonly DeviceDefinition[] {
  const custom = routines.map((routine) => momentary(`routine-${routine.id}`, routine.name, "run_routine"));
  return [...DEVICES, ...custom];
}

export function findDevice(id: string, devices: readonly DeviceDefinition[] = DEVICES): DeviceDefinition | undefined {
  return devices.find((device) => device.id === id);
}

export interface MappedCommand { action: ActionId | null; parameters: Record<string, boolean | number | string> }

export function mapGoogleCommand(deviceId: string, command: string, params: unknown, devices: readonly DeviceDefinition[] = DEVICES): MappedCommand | "unsupported" {
  const device = findDevice(deviceId, devices);
  if (!device) return "unsupported";
  const p = params && typeof params === "object" ? params as Record<string, unknown> : {};
  if (device.action && command === "action.devices.commands.OnOff" && p.on === true) {
    if (device.action === "run_routine") return { action: device.action, parameters: { routineId: device.id.slice("routine-".length) } };
    return { action: device.action, parameters: device.action === "mute" ? { muted: true } : {} };
  }
  if (device.action && command === "action.devices.commands.OnOff" && p.on === false) return { action: null, parameters: {} };
  return "unsupported";
}
