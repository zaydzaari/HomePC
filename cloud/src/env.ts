export interface SecretBindings {
  GOOGLE_CLIENT_ID: string;
  GOOGLE_CLIENT_SECRET: string;
  GOOGLE_REDIRECT_URI: string;
  LINK_PASSWORD: string;
  DEVICE_TOKEN: string;
  ADMIN_TOKEN: string;
  HOMEGRAPH_CLIENT_EMAIL?: string;
  HOMEGRAPH_PRIVATE_KEY?: string;
}

export type HomeEnv = Cloudflare.Env & SecretBindings;
