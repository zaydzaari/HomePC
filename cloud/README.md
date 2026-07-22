# HomePC cloud

Strict TypeScript Cloudflare Worker and SQLite Durable Object. Required secret bindings are `GOOGLE_CLIENT_ID`, `GOOGLE_CLIENT_SECRET`, `LINK_PASSWORD`, `DEVICE_TOKEN`, and `ADMIN_TOKEN`; upload them with `wrangler secret bulk generated/worker-secrets.json` from the repository root deployment script. Non-secret TTLs and the exact Google redirect URI are in `wrangler.jsonc`.

Endpoints: `GET /`, `GET /health`, `GET|POST /oauth/authorize`, `POST /oauth/token`, `POST /smarthome`, `GET /device/ws`, and bearer-protected `GET /admin/status`.

Never commit a `.dev.vars` file or generated secrets. The OAuth authorization response preserves Google's opaque `state`, and account-link POSTs use a `303` redirect to the Google callback. In Google Home Developer Console, keep HTTP Basic Auth disabled; the verified setup sends client credentials in the request body.

```powershell
npm install
npm run types
npm run typecheck
npm test
npm run dev
```
