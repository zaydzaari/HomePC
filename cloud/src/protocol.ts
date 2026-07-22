export const MAX_MESSAGE_BYTES = 32_768;

export type ActionId =
  | "open_notepad" | "open_steam" | "open_discord" | "open_chrome"
  | "gaming_mode" | "study_mode" | "movie_mode" | "lock_pc" | "sleep_pc"
  | "restart_pc" | "shutdown_pc" | "set_volume" | "mute" | "play_pause"
  | "next_track" | "previous_track" | "monitor_off";

export interface CommandEnvelope {
  type: "command";
  commandId: string;
  action: ActionId;
  parameters: Record<string, boolean | number | string>;
  issuedAt: number;
  expiresAt: number;
}

export interface ActionResult {
  type: "result";
  commandId: string;
  success: boolean;
  error?: string;
  state?: { volume?: number; muted?: boolean; mode?: string };
  completedAt: number;
}

export function isActionResult(value: unknown): value is ActionResult {
  if (!value || typeof value !== "object") return false;
  const v = value as Record<string, unknown>;
  return v.type === "result" && typeof v.commandId === "string" &&
    typeof v.success === "boolean" && typeof v.completedAt === "number";
}

