import type { HomeEnv } from "./env";

const TOKEN_URL = "https://oauth2.googleapis.com/token";
const REPORT_URL = "https://homegraph.googleapis.com/v1/devices:reportStateAndNotification";
const AGENT_USER_ID = "homepc-private-user";
let cached: { token: string; expiresAt: number; email: string } | undefined;

const base64Url = (bytes: Uint8Array): string => {
  let binary = "";
  for (const byte of bytes) binary += String.fromCharCode(byte);
  return btoa(binary).replaceAll("+", "-").replaceAll("/", "_").replaceAll("=", "");
};

const encode = (value: unknown): string => base64Url(new TextEncoder().encode(JSON.stringify(value)));

async function accessToken(env: HomeEnv): Promise<string | null> {
  const email = env.HOMEGRAPH_CLIENT_EMAIL?.trim();
  const pem = env.HOMEGRAPH_PRIVATE_KEY?.replaceAll("\\n", "\n").trim();
  if (!email || !pem) return null;
  if (cached && cached.email === email && cached.expiresAt > Date.now() + 60_000) return cached.token;
  const now = Math.floor(Date.now() / 1000);
  const header = encode({ alg: "RS256", typ: "JWT" });
  const payload = encode({ iss: email, scope: "https://www.googleapis.com/auth/homegraph", aud: TOKEN_URL, iat: now, exp: now + 3600 });
  const input = `${header}.${payload}`;
  const keyBytes = Uint8Array.from(atob(pem.replace(/-----[^-]+-----/g, "").replace(/\s/g, "")), (c) => c.charCodeAt(0));
  const key = await crypto.subtle.importKey("pkcs8", keyBytes, { name: "RSASSA-PKCS1-v1_5", hash: "SHA-256" }, false, ["sign"]);
  const signature = await crypto.subtle.sign("RSASSA-PKCS1-v1_5", key, new TextEncoder().encode(input));
  const assertion = `${input}.${base64Url(new Uint8Array(signature))}`;
  const response = await fetch(TOKEN_URL, {
    method: "POST", headers: { "Content-Type": "application/x-www-form-urlencoded" },
    body: new URLSearchParams({ grant_type: "urn:ietf:params:oauth:grant-type:jwt-bearer", assertion }),
  });
  if (!response.ok) throw new Error(`homegraph_token_${response.status}`);
  const value = await response.json<{ access_token?: string; expires_in?: number }>();
  if (!value.access_token) throw new Error("homegraph_token_missing");
  cached = { token: value.access_token, expiresAt: Date.now() + (value.expires_in ?? 3600) * 1000, email };
  return value.access_token;
}

export function reportStateConfigured(env: HomeEnv): boolean {
  return !!env.HOMEGRAPH_CLIENT_EMAIL?.trim() && !!env.HOMEGRAPH_PRIVATE_KEY?.trim();
}

export async function reportStates(env: HomeEnv, states: Record<string, Record<string, unknown>>): Promise<boolean> {
  const token = await accessToken(env);
  if (!token) return false;
  const response = await fetch(REPORT_URL, {
    method: "POST",
    headers: { Authorization: `Bearer ${token}`, "Content-Type": "application/json" },
    body: JSON.stringify({ requestId: crypto.randomUUID(), agentUserId: AGENT_USER_ID, payload: { devices: { states } } }),
  });
  if (!response.ok) throw new Error(`homegraph_report_${response.status}`);
  return true;
}
