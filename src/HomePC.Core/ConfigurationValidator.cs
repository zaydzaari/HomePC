namespace HomePC.Core;

public static class ConfigurationValidator
{
    private static readonly HashSet<string> KnownApps = new(StringComparer.OrdinalIgnoreCase) { "notepad", "steam", "discord", "chrome" };

    public static IReadOnlyList<ValidationIssue> Validate(HomePcConfig config, bool requireSecrets = true)
    {
        var issues = new List<ValidationIssue>();
        if (!Uri.TryCreate(config.WorkerUrl, UriKind.Absolute, out var worker) || worker.Scheme != Uri.UriSchemeHttps)
            issues.Add(new("workerUrl", "Must be an absolute https:// Worker URL."));
        if (requireSecrets && config.DeviceToken.Length < 32) issues.Add(new("deviceToken", "Must contain at least 32 characters."));
        if (requireSecrets && config.AdminToken.Length < 32) issues.Add(new("adminToken", "Must contain at least 32 characters."));
        if (config.HeartbeatSeconds is < 10 or > 300) issues.Add(new("heartbeatSeconds", "Must be between 10 and 300."));
        if (config.MaxMessageBytes is < 1024 or > 1_048_576) issues.Add(new("maxMessageBytes", "Must be between 1 KiB and 1 MiB."));
        foreach (var (name, path) in config.Applications)
        {
            if (!KnownApps.Contains(name)) issues.Add(new($"applications.{name}", "Application is not in the fixed allow-list."));
            if (!string.IsNullOrWhiteSpace(path) && !Path.IsPathFullyQualified(Environment.ExpandEnvironmentVariables(path)))
                issues.Add(new($"applications.{name}", "Executable override must be an absolute path."));
        }
        foreach (var (name, mode) in config.Modes)
        {
            if (mode.Volume is < 0 or > 100) issues.Add(new($"modes.{name}.volume", "Volume must be 0-100."));
            foreach (var app in mode.Launch.Concat(mode.Close))
                if (!KnownApps.Contains(app)) issues.Add(new($"modes.{name}", $"'{app}' is not an allow-listed application."));
            foreach (var url in mode.Urls)
                if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
                    issues.Add(new($"modes.{name}.urls", "Mode URLs must use https://."));
        }
        return issues;
    }
}
