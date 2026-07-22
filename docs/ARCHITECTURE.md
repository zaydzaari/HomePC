# Architecture

The stateless Worker validates routes and credentials. `HOMEPC.getByName("private-homepc-account")` selects one Durable Object because this repository is intentionally a one-account/private-PC coordination atom. The object stores OAuth records, linked state and a bounded audit table in SQLite. It accepts the agent with the hibernation WebSocket API and a `Bearer` device token.

EXECUTE translates Google device/trait commands to a fixed action ID, asks the Durable Object to send a command, and waits up to eight seconds for the matching result. A send alone is never success. QUERY reads the last reported volume/mute state and live socket presence. Momentary switches always query OFF.

The agent validates message size, JSON shape, expiry, future timestamps, action ID, parameters and replay IDs before invoking a focused handler. It opens no listener. The dashboard is a separate optional localhost process.

