# Verification

Last verified: **2026-07-22** — HomePC **2.0.0**

## Automated checks

| Check | Result |
|---|---|
| TypeScript strict type-check | Pass |
| Vitest Worker tests | Pass — 16/16 |
| npm dependency audit | Pass — 0 known vulnerabilities |
| Cloudflare Worker dry-run bundle | Pass |
| .NET restore and Release build | Pass — agent, core, Windows, dashboard and tray |
| xUnit tests | Pass — 15/15 |
| DPAPI round-trip test | Pass on Windows |
| Routine validation and ordered execution | Pass |
| Generated configuration validation | Pass with DPAPI-protected live configuration |
| Local Google EXECUTE simulation | Pass |
| Self-contained Windows x64 bundle | Pass |
| Source and Windows archive inspection | Pass — generated secrets and private artifacts excluded |
| Fresh-clone install, type-check, tests and Release build | Pass |

Commands executed:

```powershell
.\tools\verify.ps1
.\tools\package-release.ps1
.\tools\build-windows-bundle.ps1
```

The bundled `HomePC.Agent.exe` also validated the live encrypted configuration successfully.

## Live verification

- HomePC v2 Worker deployed successfully to the owner's `workers.dev` endpoint.
- `/health` returned HTTP 200.
- Windows agent authenticated over outbound WSS after the v2 deployment.
- Existing Google OAuth link remained valid.
- Durable Object reported `linked: true` and `online: true`.
- Local `deviceToken` and `adminToken` were migrated to `dpapi:v1` ciphertext and still connected successfully.
- Custom routine `work-setup` was validated locally and published by the agent as `Open my work setup`.
- Durable Object stored the validated routine descriptor for future SYNC responses.
- Dashboard reported Cloud linked, agent online and configuration valid.
- Real dashboard, routine editor and Google Home screenshots were assembled into `docs/images/homepc-demo.gif`.

## Account-bound item

Report State code, service-account JWT signing, terminal-state reporting and online/offline reporting are implemented and tested at the fulfillment boundary. Live Home Graph activation is not claimed because no Google service-account JSON key was supplied. The owner can enable it with `tools/configure-report-state.ps1`; until then the live integration correctly advertises `willReportState: false` and uses QUERY.

## Honest limitations

- This remains a private single-owner integration, not a Google-certified public product.
- Numeric volume remains dashboard-only; media controls are momentary switches.
- Visible application launches require an interactive user session.
- The installer is a PowerShell/current-user installer, not a signed MSIX package.
- DPAPI CurrentUser configuration cannot be decrypted by a Windows Service running under a different identity.
- A newly exposed routine may require relinking or Request Sync before it appears in Google Home.
