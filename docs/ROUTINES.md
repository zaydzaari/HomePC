# Custom routines

Open the localhost dashboard and use **Custom routines** to create an instruction. Each instruction has a lowercase ID, a display name, an optional Google Home switch, and 1-12 steps.

```json
[
  { "action": "open_notepad", "parameters": {} },
  { "action": "set_volume", "parameters": { "volume": 20 } }
]
```

After saving, restart HomePC from the tray. If **Expose to Google Home** is enabled, reconnecting sends only the validated routine ID and name to the Durable Object. Relink or request a sync if the switch does not appear immediately.

Supported step IDs are the same fixed actions documented in [COMMANDS.md](COMMANDS.md). `run_routine` cannot be placed inside another routine. Unknown properties, invalid parameter types, values outside their range, empty instructions, and instructions longer than 12 steps are rejected.

Power actions still require their local permission flags. No routine accepts PowerShell, CMD, shell text, uploaded scripts, executable arguments, arbitrary paths or file operations.
