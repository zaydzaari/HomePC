import { DurableObject } from "cloudflare:workers";
import type { HomeEnv } from "./env";
import type { ActionResult, CommandEnvelope } from "./protocol";
import { isActionResult, MAX_MESSAGE_BYTES } from "./protocol";
import { constantTimeEqual, randomToken, sha256 } from "./security";

interface PendingCommand {
  resolve: (value: { success: boolean; error?: string }) => void;
  timer: ReturnType<typeof setTimeout>;
}

interface StatusSnapshot { online: boolean; volume: number; muted: boolean; mode: string | null; lastSeen: number | null; lastAction: string | null }

export class HomePcObject extends DurableObject<HomeEnv> {
  private readonly pending = new Map<string, PendingCommand>();

  constructor(ctx: DurableObjectState, env: HomeEnv) {
    super(ctx, env);
    ctx.blockConcurrencyWhile(async () => this.migrate());
    ctx.setWebSocketAutoResponse(new WebSocketRequestResponsePair("ping", "pong"));
  }

  private migrate(): void {
    this.ctx.storage.sql.exec(`
      CREATE TABLE IF NOT EXISTS oauth_codes(code_hash TEXT PRIMARY KEY, redirect_uri TEXT NOT NULL, expires_at INTEGER NOT NULL, used INTEGER NOT NULL DEFAULT 0);
      CREATE TABLE IF NOT EXISTS oauth_states(state_hash TEXT PRIMARY KEY, expires_at INTEGER NOT NULL, used INTEGER NOT NULL DEFAULT 0);
      CREATE TABLE IF NOT EXISTS access_tokens(token_hash TEXT PRIMARY KEY, expires_at INTEGER NOT NULL, revoked INTEGER NOT NULL DEFAULT 0);
      CREATE TABLE IF NOT EXISTS refresh_tokens(token_hash TEXT PRIMARY KEY, revoked INTEGER NOT NULL DEFAULT 0);
      CREATE TABLE IF NOT EXISTS account_state(key TEXT PRIMARY KEY, value TEXT NOT NULL);
      CREATE TABLE IF NOT EXISTS audit(id INTEGER PRIMARY KEY AUTOINCREMENT, at INTEGER NOT NULL, event TEXT NOT NULL, detail TEXT NOT NULL);
      CREATE INDEX IF NOT EXISTS idx_audit_at ON audit(at DESC);
    `);
  }

  async storeOAuthState(state: string, ttlSeconds = 600): Promise<void> {
    this.ctx.storage.sql.exec("DELETE FROM oauth_states WHERE expires_at<? OR used=1", Date.now());
    this.ctx.storage.sql.exec("INSERT OR REPLACE INTO oauth_states(state_hash,expires_at,used) VALUES(?,?,0)", await sha256(state), Date.now() + ttlSeconds * 1000);
  }

  async consumeOAuthState(state: string): Promise<boolean> {
    const hash = await sha256(state);
    const row = this.ctx.storage.sql.exec<{ expires_at: number; used: number }>("SELECT expires_at,used FROM oauth_states WHERE state_hash=?", hash).toArray()[0];
    if (!row || row.used !== 0 || row.expires_at < Date.now()) return false;
    this.ctx.storage.sql.exec("UPDATE oauth_states SET used=1 WHERE state_hash=?", hash);
    return true;
  }

  private audit(event: string, detail: string): void {
    this.ctx.storage.sql.exec("INSERT INTO audit(at,event,detail) VALUES(?,?,?)", Date.now(), event, detail.slice(0, 256));
    this.ctx.storage.sql.exec("DELETE FROM audit WHERE id NOT IN (SELECT id FROM audit ORDER BY id DESC LIMIT 100)");
  }

  async authorizeCode(redirectUri: string, ttlSeconds: number): Promise<string> {
    const code = randomToken();
    this.ctx.storage.sql.exec("INSERT INTO oauth_codes(code_hash,redirect_uri,expires_at) VALUES(?,?,?)", await sha256(code), redirectUri, Date.now() + ttlSeconds * 1000);
    this.ctx.storage.sql.exec("INSERT INTO account_state(key,value) VALUES('linked','true') ON CONFLICT(key) DO UPDATE SET value='true'");
    this.audit("oauth_authorized", "authorization code issued");
    return code;
  }

  async exchangeCode(code: string, redirectUri: string, accessTtlSeconds: number): Promise<{ accessToken: string; refreshToken: string } | null> {
    const hash = await sha256(code);
    const row = this.ctx.storage.sql.exec<{ redirect_uri: string; expires_at: number; used: number }>(
      "SELECT redirect_uri,expires_at,used FROM oauth_codes WHERE code_hash=?", hash).toArray()[0];
    if (!row || row.used !== 0 || row.expires_at < Date.now() || row.redirect_uri !== redirectUri) return null;
    this.ctx.storage.sql.exec("UPDATE oauth_codes SET used=1 WHERE code_hash=?", hash);
    const accessToken = randomToken();
    const refreshToken = randomToken();
    this.ctx.storage.sql.exec("INSERT INTO access_tokens(token_hash,expires_at) VALUES(?,?)", await sha256(accessToken), Date.now() + accessTtlSeconds * 1000);
    this.ctx.storage.sql.exec("INSERT INTO refresh_tokens(token_hash) VALUES(?)", await sha256(refreshToken));
    this.audit("oauth_token", "authorization code exchanged");
    return { accessToken, refreshToken };
  }

  async refresh(refreshToken: string, accessTtlSeconds: number): Promise<string | null> {
    const hash = await sha256(refreshToken);
    const row = this.ctx.storage.sql.exec<{ revoked: number }>("SELECT revoked FROM refresh_tokens WHERE token_hash=?", hash).toArray()[0];
    if (!row || row.revoked !== 0) return null;
    const accessToken = randomToken();
    this.ctx.storage.sql.exec("INSERT INTO access_tokens(token_hash,expires_at) VALUES(?,?)", await sha256(accessToken), Date.now() + accessTtlSeconds * 1000);
    this.audit("oauth_refresh", "access token refreshed");
    return accessToken;
  }

  async tokenValid(token: string): Promise<boolean> {
    const row = this.ctx.storage.sql.exec<{ expires_at: number; revoked: number }>(
      "SELECT expires_at,revoked FROM access_tokens WHERE token_hash=?", await sha256(token)).toArray()[0];
    return !!row && row.revoked === 0 && row.expires_at >= Date.now();
  }

  async disconnect(token: string): Promise<void> {
    this.ctx.storage.sql.exec("UPDATE access_tokens SET revoked=1 WHERE token_hash=?", await sha256(token));
    this.ctx.storage.sql.exec("UPDATE refresh_tokens SET revoked=1");
    this.ctx.storage.sql.exec("INSERT INTO account_state(key,value) VALUES('linked','false') ON CONFLICT(key) DO UPDATE SET value='false'");
    this.audit("oauth_disconnect", "Google account unlinked");
  }

  async linked(): Promise<boolean> {
    return this.ctx.storage.sql.exec<{ value: string }>("SELECT value FROM account_state WHERE key='linked'").toArray()[0]?.value === "true";
  }

  async status(): Promise<StatusSnapshot> {
    const sockets = this.ctx.getWebSockets("agent");
    const state = (await this.ctx.storage.get<Omit<StatusSnapshot, "online">>("device_state")) ?? {
      volume: 0, muted: false, mode: null, lastSeen: null, lastAction: null,
    };
    return { ...state, online: sockets.some((socket) => socket.readyState === WebSocket.OPEN) };
  }

  async execute(action: string, parameters: Record<string, boolean | number | string>, timeoutMs: number): Promise<{ success: boolean; error?: string }> {
    const socket = this.ctx.getWebSockets("agent").find((item) => item.readyState === WebSocket.OPEN);
    if (!socket) return { success: false, error: "offline" };
    const now = Date.now();
    const envelope: CommandEnvelope = {
      type: "command", commandId: crypto.randomUUID(), action: action as CommandEnvelope["action"], parameters,
      issuedAt: now, expiresAt: now + timeoutMs,
    };
    socket.send(JSON.stringify(envelope));
    this.audit("command_sent", `${envelope.commandId}:${action}`);
    return await new Promise((resolve) => {
      const timer = setTimeout(() => {
        this.pending.delete(envelope.commandId);
        this.audit("command_timeout", envelope.commandId);
        resolve({ success: false, error: "timeout" });
      }, timeoutMs);
      this.pending.set(envelope.commandId, { resolve, timer });
    });
  }

  async recentAudit(): Promise<Array<{ at: number; event: string; detail: string }>> {
    return this.ctx.storage.sql.exec<{ at: number; event: string; detail: string }>("SELECT at,event,detail FROM audit ORDER BY id DESC LIMIT 25").toArray();
  }

  override async fetch(request: Request): Promise<Response> {
    if (request.headers.get("Upgrade")?.toLowerCase() !== "websocket") return new Response("Upgrade required", { status: 426 });
    const token = request.headers.get("Authorization")?.replace(/^Bearer /, "") ?? "";
    if (!await constantTimeEqual(token, this.env.DEVICE_TOKEN)) return new Response("Unauthorized", { status: 401 });
    const pair = new WebSocketPair();
    const client = pair[0];
    const server = pair[1];
    this.ctx.acceptWebSocket(server, ["agent"]);
    server.serializeAttachment({ connectedAt: Date.now() });
    this.audit("agent_connected", "authenticated websocket");
    return new Response(null, { status: 101, webSocket: client });
  }

  override async webSocketMessage(socket: WebSocket, message: string | ArrayBuffer): Promise<void> {
    const bytes = typeof message === "string" ? new TextEncoder().encode(message).byteLength : message.byteLength;
    if (bytes > MAX_MESSAGE_BYTES || typeof message !== "string") { socket.close(1009, "Message too large or binary"); return; }
    let value: unknown;
    try { value = JSON.parse(message); } catch { socket.send(JSON.stringify({ type: "error", error: "malformed_json" })); return; }
    if (value && typeof value === "object" && (value as Record<string, unknown>).type === "hello") {
      await this.ctx.storage.put("device_state", { volume: 0, muted: false, mode: null, lastSeen: Date.now(), lastAction: null });
      socket.send(JSON.stringify({ type: "hello_ack", serverTime: Date.now() }));
      return;
    }
    if (!isActionResult(value)) { socket.send(JSON.stringify({ type: "error", error: "invalid_message" })); return; }
    const result: ActionResult = value;
    const pending = this.pending.get(result.commandId);
    if (pending) {
      clearTimeout(pending.timer); this.pending.delete(result.commandId);
      pending.resolve(result.success ? { success: true } : { success: false, error: result.error ?? "action_failed" });
    }
    const previous = await this.status();
    await this.ctx.storage.put("device_state", {
      volume: result.state?.volume ?? previous.volume, muted: result.state?.muted ?? previous.muted,
      mode: result.state?.mode ?? previous.mode, lastSeen: Date.now(), lastAction: result.commandId,
    });
    this.audit(result.success ? "command_success" : "command_failure", `${result.commandId}:${result.error ?? "ok"}`);
  }

  override async webSocketClose(_socket: WebSocket, _code: number, _reason: string, _wasClean: boolean): Promise<void> {
    this.audit("agent_disconnected", "websocket closed");
  }
}
