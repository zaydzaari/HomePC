# Google Home Report State

HomePC v2 implements the Home Graph `devices:reportStateAndNotification` endpoint using an RS256 service-account assertion. It reports complete OnOff state after successful commands and reports all virtual devices online or offline when the agent connects or disconnects.

Activation requires the owner of the matching Google Cloud project:

1. Enable the **HomeGraph API** in Google Cloud Console.
2. Create a service account in the same project and download a JSON key.
3. Run:

```powershell
.\tools\configure-report-state.ps1 -ServiceAccountJson C:\secure\homepc-service-account.json
```

The script verifies that the key's `project_id` matches the bootstrap project, uploads the email and private key directly as encrypted Cloudflare Worker secrets, and redeploys. It does not copy the key into the repository or generated configuration.

Verify state with Google's Home Graph Viewer. Delete or rotate test keys after use. HomePC stops reporting after DISCONNECT. Without these two Worker secrets, `willReportState` remains false and QUERY continues to be authoritative.
