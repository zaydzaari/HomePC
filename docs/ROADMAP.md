# Roadmap

## Shipped in v2.0

- Custom allow-listed routines with strict schema validation.
- Local routine editor, test and delete controls.
- Optional custom routine switches in Google Home SYNC.
- Windows tray application and current-user installer.
- Current-user DPAPI encryption for local device and admin tokens.
- Optional Google Home Report State with service-account authentication.
- Expanded cloud and .NET test coverage.
- Automated Windows bundles and GitHub Releases.

## Candidates for v2.1

- Signed MSIX packaging and automatic update checks.
- A visual drag-and-drop routine editor in place of the JSON step editor.
- DPAPI `LocalMachine` support for dedicated Windows Service installations.
- Home Graph accuracy telemetry in the local dashboard.
- Multiple-PC accounts with explicit device ownership and revocation.

The no-shell boundary remains permanent: HomePC routines compose approved actions and will never become arbitrary remote command execution.
