# HomePC quick start

1. Install .NET 8, Node.js LTS, and create Cloudflare and Google Home Developer Console accounts.
2. Create a Google Home **Cloud-to-cloud** project and copy its exact project ID.
3. Run:

   ```powershell
   .\tools\bootstrap.ps1 -ProjectId YOUR_GOOGLE_PROJECT_ID
   .\tools\deploy-cloudflare.ps1
   ```

4. Copy `generated/google-home-values.txt` into the Google Home integration. Keep **HTTP Basic Auth off**.
5. Save, open **Cloud-to-cloud → Test**, and click **Test**.
6. Start the Windows agent:

   ```powershell
   dotnet run --project .\src\HomePC.Agent -- --config .\generated\homepc.json
   ```

7. In Google Home, choose **Add → Device → Add a different way**, search **`[test] HomePC`**, and enter the generated link password.
8. Add the devices to a room and test **Open Notepad**.

For screenshots, detailed explanations, and troubleshooting, read [README.md](README.md).
