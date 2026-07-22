# Verification

Last verified: **2026-07-22**

## Automated checks

| Check | Result |
|---|---|
| TypeScript type-check | Pass |
| Vitest Worker tests | Pass — 13/13 |
| npm dependency audit | Pass — 0 known vulnerabilities |
| .NET restore and Release build | Pass |
| xUnit tests | Pass — 12/12 |
| Cloudflare Worker bundle | Pass |
| Generated configuration validation | Pass |
| Secret sentinel and package inspection | Pass |
| Local EXECUTE simulation | Pass |
| Secret-safe release archive | Pass |
| Fresh-clone install, build, and tests | Pass |

Run the same suite with:

```powershell
.\tools\verify.ps1
```

Additional release checks executed:

```powershell
cd cloud
npm audit
npm run deploy:dry
cd ..
.\tools\package-release.ps1
```

The committed repository was also cloned into a new gitignored directory, then verified with `npm ci`, TypeScript tests, `dotnet restore`, Release build, and all .NET tests.

## Live verification

The following account-bound checks were completed against the owner's private Google Home project and Cloudflare account:

- Cloudflare Worker deployed successfully to `workers.dev`.
- `/health` returned HTTP 200.
- All required Worker secrets were uploaded.
- Windows agent established an authenticated outbound WebSocket connection.
- OAuth authorization returned Google's callback redirect.
- Authorization-code exchange returned HTTP 200 and issued access and refresh tokens.
- Android Google Home account linking completed.
- Google Home SYNC discovered the HomePC virtual devices.
- Google Home displayed the devices for room assignment.
- Local dashboard reported Cloud linked, Windows agent online, and configuration valid.

Evidence is included in:

- `docs/images/google-home-devices.png`
- `docs/images/dashboard.png`

## Known limitations

- This is a private, single-owner proof of concept rather than a certified public Google Home integration.
- Report State is not implemented; QUERY is authoritative.
- Media transport controls are momentary switches. Numeric volume remains dashboard-only.
- Windows Services run in session 0, so visible desktop app launches require an interactive per-user agent.
- Volume state can drift when another application changes it.
- Local generated secrets rely on Windows filesystem permissions rather than DPAPI.
- Protected power actions are intentionally disabled until enabled locally.
