# Commands

| Google device | Local action | Notes |
|---|---|---|
| Open Notepad/Steam/Discord/Chrome | fixed app launch | Local path detection/override only |
| Gaming/Study/Movie Mode | configured mode | Launch, HTTPS URLs, volume; optional guarded plan/close |
| Lock PC | native lock | Enabled |
| Sleep PC | native suspend | Disabled by default |
| Restart/Shut Down PC | native Windows exit | Disabled by default; Windows privilege policy applies |
| Turn Monitor Off | monitor power message | Input can wake display |
| Mute / Play Pause / Next / Previous | momentary Google switches | Fixed media actions |
| Custom routine | generated momentary switch | Expands locally into 1-12 validated allow-listed steps; nesting is rejected |
| PC Volume | local dashboard | Validated 0–100; Google media types require Report State |

Momentary OnOff OFF requests are acknowledged as no-ops; ON executes once. QUERY always returns OFF. Unknown actions, extra fields (such as a path), invalid types, expired messages and duplicates are rejected.
