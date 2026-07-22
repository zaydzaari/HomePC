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
| Secret leakage | `generated/` ignored/excluded from ZIP; redacted logs; local agent tokens encrypted with current-user DPAPI | A process already running as the same Windows user can request DPAPI decryption |
| Unsafe custom routine | Versioned schema, 12-step cap, fixed action IDs and parameter validation on every step | A user can deliberately enable and compose protected actions |
| Service-account theft | HomeGraph key is uploaded only as a Worker secret and never copied into repository configuration | Cloudflare account compromise |

Bootstrap encrypts the device and admin tokens with Windows DPAPI for the current user. This matches the tray and interactive-agent installation. A service running as another identity cannot decrypt that file; use a configuration protected under the service identity for service-mode deployments.
