using System.Diagnostics;
using System.Drawing;

namespace HomePC.Tray;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        var config = Argument(args, "--config") ?? Path.Combine(AppContext.BaseDirectory, "config", "homepc.json");
        Application.Run(new HomePcTrayContext(Path.GetFullPath(config)));
    }

    private static string? Argument(string[] args, string name)
    {
        var index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }
}

internal sealed class HomePcTrayContext : ApplicationContext
{
    private readonly string _configPath;
    private readonly NotifyIcon _icon;
    private Process? _agent;
    private Process? _dashboard;

    public HomePcTrayContext(string configPath)
    {
        _configPath = configPath;
        if (!File.Exists(_configPath)) throw new FileNotFoundException("HomePC configuration was not found.", _configPath);
        var menu = new ContextMenuStrip();
        menu.Items.Add("Open dashboard", null, (_, _) => OpenDashboard());
        menu.Items.Add("Restart HomePC", null, (_, _) => Restart());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => ExitThread());
        _icon = new NotifyIcon { Icon = SystemIcons.Application, Text = "HomePC", Visible = true, ContextMenuStrip = menu };
        _icon.DoubleClick += (_, _) => OpenDashboard();
        StartChildren();
    }

    private void StartChildren()
    {
        _agent = StartSibling("HomePC.Agent.exe");
        _dashboard = StartSibling("HomePC.Dashboard.exe");
        _icon.ShowBalloonTip(1800, "HomePC", "Agent and local dashboard started.", ToolTipIcon.Info);
    }

    private Process StartSibling(string executable)
    {
        var path = Path.Combine(AppContext.BaseDirectory, executable);
        if (!File.Exists(path)) throw new FileNotFoundException($"Installed HomePC component is missing: {executable}", path);
        return Process.Start(new ProcessStartInfo(path) { UseShellExecute = false, CreateNoWindow = true, ArgumentList = { "--config", _configPath } })
            ?? throw new InvalidOperationException($"Unable to start {executable}.");
    }

    private static void OpenDashboard() => Process.Start(new ProcessStartInfo("http://127.0.0.1:5187") { UseShellExecute = true })?.Dispose();

    private void Restart()
    {
        StopChildren();
        StartChildren();
    }

    private void StopChildren()
    {
        foreach (var process in new[] { _dashboard, _agent })
        {
            if (process is null) continue;
            try { if (!process.HasExited) process.Kill(true); } catch (InvalidOperationException) { }
            process.Dispose();
        }
        _agent = null;
        _dashboard = null;
    }

    protected override void ExitThreadCore()
    {
        _icon.Visible = false;
        _icon.Dispose();
        StopChildren();
        base.ExitThreadCore();
    }
}
