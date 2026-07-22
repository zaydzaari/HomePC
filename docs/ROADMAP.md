# Roadmap: custom instructions

The next HomePC update will let the owner create personal routines such as **Focus mode**, **Open my work setup**, or **Movie night**. A routine will compose existing approved actions; it will not execute arbitrary text as code.

## Proposed experience

The localhost dashboard will provide a routine editor with:

- a name and optional Google Home display name;
- an ordered list of allow-listed actions;
- validated parameters selected through controls, not a command text box;
- a **Dry run** preview and a local **Test** button;
- explicit confirmation for actions requiring protected permissions;
- an audit entry showing which routine and step ran.

Example configuration:

```json
{
  "schemaVersion": 2,
  "routines": [
    {
      "id": "focus-mode",
      "name": "Focus mode",
      "steps": [
        { "action": "open_notepad" },
        { "action": "set_volume", "parameters": { "level": 25 } }
      ]
    }
  ]
}
```

## Security boundary

Custom means **user-composed**, not unrestricted. The implementation will:

- resolve every step through the existing fixed action registry;
- reject unknown fields, unknown action IDs, invalid types, and out-of-range values;
- reject PowerShell, CMD, shell strings, uploaded scripts, executable arguments, and remote paths;
- enforce the existing local permission gates for sleep, restart, shutdown, process closing, and power-plan changes;
- cap routine length and execution time;
- stop safely on failure and record the failed step;
- keep routine definitions local unless the user explicitly publishes their names as Google Home devices.

## Delivery plan

1. Add a versioned routine schema and strict parser in `HomePC.Core`.
2. Add registry composition, cancellation, timeout, and audit support.
3. Build the localhost routine editor with dry-run validation.
4. Add optional Google Home SYNC exposure for selected routines.
5. Add migration from schema version 1, unit tests, integration tests, and updated screenshots.

## Definition of done

- Existing configurations continue to work unchanged.
- Invalid routines cannot start and produce a useful local error.
- A routine cannot bypass an action's permission check.
- SYNC, QUERY, EXECUTE, relinking, and agent reconnection remain covered by tests.
- Documentation clearly distinguishes routine composition from arbitrary remote command execution.
