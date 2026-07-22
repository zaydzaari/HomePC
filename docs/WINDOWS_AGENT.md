# Windows agent

Console mode is preferred for the first Notepad demo because it runs in the signed-in interactive desktop session. Ctrl+C performs graceful cancellation. The Windows Service scripts require Administrator rights and provide explicit missing-build/config errors.

HomePC v2's recommended interactive host is `HomePC.Tray`. Run `scripts/install-user.ps1` to publish the tray, agent and dashboard into LocalAppData, create Start Menu and Startup shortcuts, and copy the DPAPI-protected configuration. Double-click the tray icon for the dashboard or use its menu to restart after editing routines.

Windows services run in session 0; modern Windows intentionally isolates them from the interactive desktop. A service can maintain cloud connectivity and perform machine actions, but GUI launches may not appear for the signed-in user. For an always-on interactive deployment, start the console agent at user logon with Task Scheduler; do not enable “Interact with desktop.”

Application detection uses local overrides, the registry and standard install locations. Discord supports its `Update.exe --processStart Discord.exe` mechanism. Monitor-off uses the Windows monitor-power message; keyboard/mouse activity may wake it. Media and volume use Windows virtual media keys. Volume is normalized by sending volume-down then stepping up; another mixer may alter the effective level afterward.
