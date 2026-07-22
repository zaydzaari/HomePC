# Security and threat model

## Assets and boundaries

Assets are the PC control capability, OAuth tokens, device/admin tokens and local configuration. Trust boundaries exist at Google→Worker, browser→OAuth, Worker→Durable Object, WSS→agent and dashboard→localhost.

| Threat | Mitigation | Residual risk |
|---|---|---|
| Remote code execution | Fixed action enum; no shell; cloud never supplies paths/scripts | A bug in an allow-listed handler |
| Stolen OAuth code | One use, five-minute TTL, exact redirect | Compromised browser/device |
| Token guessing/timing | 256–384-bit random values, SHA-256 storage, timing-safe comparison | Endpoint traffic analysis |
| WebSocket impersonation | Separate high-entropy bearer device token over normal TLS | Token stolen from local config |
| Replay/delay | Expiry/future checks and bounded 512-ID replay cache | Replay after cache eviction is blocked by short expiry |
| Dangerous power action | Restart/shutdown/sleep local flags, disabled by default | User deliberately enables them |
| Dashboard exposure | `127.0.0.1` binding and no secret display | Malicious local process/user |
| Secret leakage | `generated/` ignored/excluded from ZIP; redacted logs | Poor local ACL/backup hygiene |

The generated agent token is stored in a user-controlled gitignored JSON file. Protect the folder with Windows ACLs. DPAPI is not used in v1 because Windows Services and interactive-user processes have different DPAPI identities; a service-specific encrypted store is a documented hardening step.

