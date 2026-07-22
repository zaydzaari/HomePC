# Contributing to HomePC

Thanks for helping improve HomePC. Keep every change safe, testable, and easy to review.

## Development setup

1. Install .NET 8 and Node.js LTS.
2. Run `npm install` inside `cloud/`.
3. Copy `config/homepc.example.json` only through `tools/bootstrap.ps1`; never commit generated credentials.
4. Run the complete check before opening a pull request:

```powershell
.\tools\verify.ps1
```

## Safety rules

- Never add a generic shell, PowerShell, CMD, script-upload, or remote-path execution endpoint.
- New remote actions must have a fixed identifier, strict parameter validation, a local implementation, and tests.
- Power, process-closing, and other destructive actions must remain disabled until locally enabled.
- Never include `generated/`, `logs/`, `.dev.vars`, tokens, passwords, or private Worker configuration in a commit or screenshot.
- Keep the dashboard bound to loopback and redact credentials from every response.

## Pull requests

Keep a pull request focused. Explain the user-visible behavior, security impact, test coverage, and any Google Home Console migration. Update the README or relevant document when setup or behavior changes.

The planned custom-instructions work is specified in [docs/ROADMAP.md](docs/ROADMAP.md). Contributions to that feature must preserve the allow-list boundary.
