# HomePC

[![CI](https://github.com/zaydzaari/HomePC/actions/workflows/ci.yml/badge.svg)](https://github.com/zaydzaari/HomePC/actions/workflows/ci.yml)
[![Release](https://img.shields.io/github/v/release/zaydzaari/HomePC)](https://github.com/zaydzaari/HomePC/releases)
[![License: MIT](https://img.shields.io/badge/license-MIT-3b82f6.svg)](LICENSE)
[![.NET 8](https://img.shields.io/badge/.NET-8.0-512bd4.svg)](https://dotnet.microsoft.com/download/dotnet/8.0)

HomePC is an open-source bridge for controlling a Windows PC from Google Home without opening an inbound port or exposing a remote shell.

HomePC publishes a fixed set of safe Windows actions as Google Home switches. A .NET agent keeps one authenticated outbound WebSocket connection to a Cloudflare Durable Object, while Google Home communicates with a standards-based Cloud-to-cloud fulfillment service.

> **Status:** v2.0.0 is released. Account linking, device discovery, custom routines, the live Cloudflare deployment, DPAPI-protected credentials, and the Windows agent have been exercised end to end. Report State remains opt-in because Google requires a project-specific service-account key.

<p align="center">
  <img src="docs/images/google-home-devices.png" alt="HomePC devices inside Google Home" width="300">
</p>

## Verification at a glance

| Area | Current evidence |
|---|---|
| Automated tests | 16 Cloudflare/Google protocol tests and 15 .NET tests |
| Static checks | TypeScript type-check, Worker dry-run, release build, and npm audit |
| Release pipeline | GitHub Actions builds the self-contained Windows x64 bundle from a tag |
| Local secrets | Device and admin tokens are encrypted with Windows DPAPI for the current user |
| Network boundary | The agent makes one authenticated outbound WebSocket connection; the PC accepts no inbound internet connection |
| Command boundary | Cloud messages select fixed action identifiers; shell text, scripts, remote paths, and arbitrary executables are rejected |

Run `./tools/verify.ps1` to reproduce the project checks. Recorded results and the exact environment are in [VERIFICATION.md](VERIFICATION.md).

## Features

- Native controls inside Google Home for apps, media, display, modes, and guarded PC power actions.
- OAuth 2.0 authorization-code account linking with expiring codes, refresh tokens, and revocation.
- Cloudflare Worker plus SQLite-backed Durable Object and WebSocket hibernation.
- .NET 8 Windows agent with replay protection, strict message validation, and a fixed action registry.
- Localhost-only Windows-style dashboard for status, application detection, safe tests, and protected permissions.
- Custom user routines that compose 1-12 validated allow-listed actions and can appear as Google Home switches.
- Windows tray application plus a current-user installer and startup shortcut.
- Current-user DPAPI encryption for the local device and admin tokens.
- Optional proactive Google Home Report State using service-account credentials stored only as Worker secrets.
- No inbound PC port, arbitrary command endpoint, uploaded script, or remote executable path.
- Automated TypeScript and .NET tests, release packaging, and GitHub Actions CI.

## Architecture

```mermaid
flowchart LR
  G["Google Home / Assistant"] -->|"OAuth + SYNC / QUERY / EXECUTE"| W["Cloudflare Worker"]
  W --> D["Durable Object\nSQLite + live coordination"]
  A["Windows .NET agent"] -->|"outbound authenticated WSS"| D
  A --> R["Fixed action registry"]
  R --> X["Allow-listed apps and Windows APIs"]
  B["Local dashboard\n127.0.0.1 only"] --> R
```

## Supported controls

| Category | Google Home devices |
|---|---|
| Applications | Open Notepad, Steam, Discord, Chrome |
| Modes | Gaming, Study, Movie |
| Media | Mute, Play/Pause, Next, Previous |
| Windows | Lock PC, Monitor Off |
| Protected power | Sleep, Restart, Shutdown — disabled by default |
| Local dashboard | Numeric PC volume and manual safe tests |

See [docs/COMMANDS.md](docs/COMMANDS.md) for behavior and safety rules.

## Prerequisites

- Windows 10 or 11
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Node.js LTS](https://nodejs.org/) and npm
- A free [Cloudflare account](https://dash.cloudflare.com/)
- A [Google Home Developer Console](https://console.home.google.com/) project
- Google Home on a phone using the same Google account as the developer project

## Setup

### 1. Create the Google Home project

1. Open the [Google Home Developer Console](https://console.home.google.com/).
2. Create a project and add a **Cloud-to-cloud** integration.
3. Copy the immutable project ID, not only the display name.

Do not choose Matter for this release. Matter uses commissioning QR codes; HomePC uses Cloud-to-cloud account linking.

### 2. Generate private configuration

From the repository root:

```powershell
.\tools\bootstrap.ps1 -ProjectId YOUR_GOOGLE_PROJECT_ID
```

This creates three gitignored files under `generated/`:

- `worker-secrets.json` — Cloudflare Worker secrets
- `homepc.json` — Windows agent configuration with its local device/admin tokens encrypted by Windows DPAPI
- `google-home-values.txt` — values to paste into Google Home Developer Console

The script will not silently overwrite existing secrets.

### 3. Deploy the Cloudflare backend

For a new Cloudflare account, open **Workers & Pages** once and initialize the account's `workers.dev` subdomain. Then run:

```powershell
.\tools\deploy-cloudflare.ps1
```

The deployment script installs dependencies, generates Worker types, type-checks, tests, uploads secrets, deploys, and writes the final Worker URL back into the generated configuration.

Confirm the health endpoint:

```powershell
curl.exe https://YOUR_WORKER.workers.dev/health
```

Expected response:

```json
{"ok":true,"service":"homepc-cloud"}
```

### 4. Configure Google Home

Open `generated/google-home-values.txt` and copy its values into the Cloud-to-cloud integration.

| Console field | Value |
|---|---|
| Integration name | `HomePC` |
| Device type | `Switch` |
| OAuth Client ID / secret | Generated values |
| Authorization URL | `https://YOUR_WORKER.workers.dev/oauth/authorize` |
| Token URL | `https://YOUR_WORKER.workers.dev/oauth/token` |
| Fulfillment URL | `https://YOUR_WORKER.workers.dev/smarthome` |
| Scope | `homepc.control` |
| HTTP Basic Auth | **Off** — use Google's default request-body credentials |
| Icon | Rename `assets/PROJECT_ID.png` to the exact project ID plus `.png` |

Save the integration. Go to **Cloud-to-cloud → Test** and click **Test** once so the unpublished integration becomes visible to the project owner.

### 5. Install the tray application

The recommended v2 setup installs the agent, dashboard and tray application for the current Windows user:

```powershell
.\scripts\install-user.ps1
```

HomePC starts with Windows. Double-click its tray icon to open the dashboard; use the menu to restart after editing routines. To remove the application binaries while retaining recoverable encrypted configuration, run `scripts/uninstall-user.ps1`.

For console development instead:

```powershell
dotnet publish .\src\HomePC.Agent -c Release -r win-x64 --self-contained false
dotnet run --project .\src\HomePC.Agent -- --config .\generated\homepc.json
```

For desktop actions such as opening Notepad, run the agent in the signed-in user's session. A Windows Service runs in session 0 and cannot reliably display GUI apps. See [docs/WINDOWS_AGENT.md](docs/WINDOWS_AGENT.md).

### 6. Link Google Home

1. Open Google Home with the developer project's Google account.
2. Tap **Add → Device**.
3. If the QR scanner appears, choose **Add a different way**.
4. Search for **`[test] HomePC`**.
5. Enter the generated link password.
6. Select the HomePC devices and add them to a room.
7. Test **Open Notepad** first.

The password page is used only for the initial OAuth account-linking step. After linking, the controls live directly inside Google Home.

### 7. Run the local dashboard

```powershell
dotnet run --project .\src\HomePC.Dashboard -- --config .\generated\homepc.json
```

Open [http://127.0.0.1:5187](http://127.0.0.1:5187). The dashboard binds only to localhost and never renders the device or admin tokens.

## Custom routines

The dashboard's **Custom routines** editor creates instructions such as **Open my work setup**. A routine is an ordered JSON list of existing action IDs. Save it, restart from the tray, and optionally expose it as a Google Home switch.

Every step passes through the same parameter validation and permission gates as a direct command. Nested routines, arbitrary shell text, PowerShell, CMD, scripts, executable paths and remote paths are rejected. See [docs/ROUTINES.md](docs/ROUTINES.md).

## Optional Report State

HomePC implements Google Home Graph Report State, but Google requires a service-account key from the matching project. After enabling HomeGraph API and downloading that key, activate it with:

```powershell
.\tools\configure-report-state.ps1 -ServiceAccountJson C:\secure\homepc-service-account.json
```

The key is uploaded directly to Cloudflare secrets and is never copied into Git. Until configured, `willReportState` is false and QUERY remains authoritative. See [docs/REPORT_STATE.md](docs/REPORT_STATE.md).

## Configuration

Start with [config/homepc.example.json](config/homepc.example.json). Application overrides and mode URLs remain local to the PC. Unknown actions, extra command fields, expired messages, duplicates, and unapproved power operations are rejected.

## Validation

Run the complete verification suite:

```powershell
.\tools\verify.ps1
```

Current recorded results are in [VERIFICATION.md](VERIFICATION.md). The repository also includes GitHub Actions CI in `.github/workflows/ci.yml`.

## Security model

- The agent initiates the connection; the PC does not accept inbound internet traffic.
- Cloud requests contain action identifiers, not shell commands or executable paths.
- Sleep, restart, shutdown, process closing, and power-plan changes require local opt-in.
- Tokens and generated configuration are excluded from Git.
- The dashboard is localhost-only and redacts secrets.

Read the full [security model](docs/SECURITY.md) before enabling protected actions.

## Troubleshooting

The most common setup failures and their fixes are documented in [docs/TROUBLESHOOTING.md](docs/TROUBLESHOOTING.md). In particular:

- A looping Android account-link page requires the current Worker release, which allows Google's callback origin and uses a `303` post-login redirect.
- Keep **HTTP Basic Auth off** in Google Home Developer Console unless you intentionally change the server configuration.
- If the integration is missing, click **Test** in Developer Console using the same Google account as the phone.

## Releases

Tags matching `v*` run the full verification suite, build a self-contained Windows x64 bundle, and publish both source and Windows ZIPs to [GitHub Releases](https://github.com/zaydzaari/HomePC/releases). See [docs/ROADMAP.md](docs/ROADMAP.md) for future work.

Inside the Windows bundle, run `.\install.ps1 -ConfigPath C:\path\to\homepc.json` to install the already-built application without requiring the source tree.

## Repository guide

| Path | Purpose |
|---|---|
| `cloud/` | Cloudflare Worker, Durable Object, OAuth, Google intents |
| `src/HomePC.Agent/` | Windows WebSocket agent |
| `src/HomePC.Core/` | Models, validation, action registry |
| `src/HomePC.Windows/` | Allow-listed Windows integrations |
| `src/HomePC.Dashboard/` | Local status and safe test UI |
| `src/HomePC.Tray/` | Windows notification-area launcher |
| `tests/` | .NET tests |
| `tools/` | Bootstrap, deploy, Report State, verify, and release scripts |
| `docs/` | Architecture, setup, security, commands, and roadmap |

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md). Security-sensitive changes should preserve the fixed action boundary and include tests.

## License

[MIT](LICENSE)
