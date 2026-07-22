# Portfolio notes

## What was built

A secure Google Home Cloud-to-cloud control plane for Windows: standards-based account linking, stateful edge coordination, an outbound-only .NET agent, native OS integrations, a localhost operator dashboard and reproducible setup/testing/release automation.

## Technical challenges

- Translating consumer smart-home traits into safe momentary computer actions.
- Returning truthful EXECUTE responses by correlating asynchronous WebSocket results.
- Preserving connection metadata through Durable Object hibernation.
- Keeping interactive Windows behavior separate from service-session limitations.
- Designing a useful system without introducing a general remote shell.

## Interview talking points

- Why a Durable Object is the one-PC coordination atom.
- OAuth one-time-code semantics and revocation.
- How validation, allow-listing and replay defense reduce the command surface.
- Honest Google Home limitations: QUERY works; arbitrary custom UI and CPU metrics do not.
- Operational safeguards around deployment and secret-free release artifacts.

## Resume bullets

- Built a TypeScript Cloudflare Worker/Durable Object integration implementing Google Home SYNC, QUERY, EXECUTE and DISCONNECT with OAuth 2.0 account linking.
- Engineered a .NET 8 Windows agent with authenticated outbound WebSockets, replay/expiry controls, native OS actions and an explicit no-shell allow-list.
- Added strict TypeScript/C# tests, localhost operations UI, setup automation and verified secret-safe release packaging.

## Demo video flow

Show architecture → open redacted dashboard → link `[test] HomePC` → tap Open Notepad → Notepad appears → adjust PC Media volume → trigger Gaming Mode → show Worker/agent audit entries → explain disabled shutdown control.

