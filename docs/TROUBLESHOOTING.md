# Troubleshooting

## Google Home linking

| Symptom | Check / fix |
|---|---|
| Only a QR scanner appears | Cloud-to-cloud is not Matter. Tap **Add a different way**, then search for **`[test] HomePC`**. |
| `[test] HomePC` is missing | In Developer Console, open **Cloud-to-cloud → Test** and click **Test** with the same Google account used by the phone. |
| Password page says `invalid_request` | Deploy the latest Worker. It preserves Google's opaque OAuth state instead of consuming it as a server token. Start a fresh link attempt. |
| Password page loops or loads forever | Deploy the latest Worker. Its CSP allows `oauth-redirect.googleusercontent.com` and the form uses a `303` callback redirect. Fully close the old web view before retrying. |
| OAuth `invalid_client` | Re-copy the generated client ID and secret, upload Worker secrets again, and save the integration. |
| Token exchange fails | Keep **HTTP Basic Auth off** so Google sends credentials in the documented request body. HomePC supports both modes, but body mode is the verified configuration. |
| Redirect mismatch | The exact redirect must be `https://oauth-redirect.googleusercontent.com/r/PROJECT_ID`, using the immutable project ID. |
| Devices do not appear after linking | Confirm the fulfillment URL, inspect Worker audit events, and relink with the developer-project Google account. |
| New custom routine is missing | Enable **Expose to Google Home**, restart from the tray, then relink or request a sync. |
| Report State stays disabled | Enable HomeGraph API and run `tools/configure-report-state.ps1` with a service-account JSON key from the same project. |

Useful live checks:

```powershell
cd cloud
npx wrangler tail homepc-google-home --format pretty
```

```powershell
curl.exe https://YOUR_WORKER.workers.dev/health
```

## Cloudflare

| Symptom | Check / fix |
|---|---|
| `workers.dev` subdomain missing | Open Cloudflare **Workers & Pages** once and initialize the account subdomain. |
| `npm ETIMEDOUT` | Check proxy/DNS, then run `npm config set registry https://registry.npmjs.org/`. |
| Wrong npm registry | Run `npm config get registry`; the deploy script repairs obvious internal lock-file URLs. |
| Deployment fails | Run `npx wrangler whoami`, read the first Wrangler error, and rerun `tools/deploy-cloudflare.ps1`. |

## Windows agent

| Symptom | Check / fix |
|---|---|
| Google reports device offline | Start the agent, verify the Worker URL/device token, and check the system clock. |
| WebSocket will not connect | Corporate proxies may block WSS. Test another network and inspect TLS/proxy policy. |
| Notepad works but another app does not | Check application detection in the dashboard and set an absolute local override in `generated/homepc.json`. |
| Service is running but GUI apps do not appear | Windows Services run in session 0. Use the per-user interactive agent for visible app launches. |
| Power action does nothing | Sleep/restart/shutdown are disabled by default. Enable the exact permission locally and restart the agent. |

## Dashboard

| Symptom | Check / fix |
|---|---|
| Dashboard unavailable | Start `HomePC.Dashboard` and open `http://127.0.0.1:5187`. |
| Cloud shows unavailable | Verify the admin token and Worker URL in the generated config, then check `/health`. |
| Permission change is not active | Restart both the agent and dashboard after changing protected permissions. |
| DPAPI decryption fails | Run the tray or agent as the Windows user that ran bootstrap. A service identity requires its own protected configuration. |
