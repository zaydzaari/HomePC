using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using HomePC.Core;

namespace HomePC.Windows;

public sealed class NativeWindowsController : IWindowsController
{
    private const int WmSyscommand = 0x0112, ScMonitorpower = 0xF170;
    private const byte KeyVolumeMute = 0xAD, KeyVolumeDown = 0xAE, KeyVolumeUp = 0xAF;
    private const byte KeyMediaNext = 0xB0, KeyMediaPrevious = 0xB1, KeyMediaPlayPause = 0xB3, KeyUp = 0x02;
    private const uint EwxShutdown = 0x00000001, EwxReboot = 0x00000002, EwxForceIfHung = 0x00000010;
    private readonly IReadOnlyDictionary<string, string?> _apps;
    private AgentState _state = new(50, false, null);

    public NativeWindowsController(HomePcConfig config) => _apps = new ApplicationLocator(config.Applications).DetectAll();
    public AgentState State => _state;
    public IReadOnlyDictionary<string, string?> DetectedApplications => _apps;

    public Task LaunchAsync(string app, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_apps.TryGetValue(app, out var path) || path is null) throw new FileNotFoundException($"application_not_found:{app}");
        var info = new ProcessStartInfo(path) { UseShellExecute = false };
        if (app.Equals("discord", StringComparison.OrdinalIgnoreCase) && Path.GetFileName(path).Equals("Update.exe", StringComparison.OrdinalIgnoreCase))
        { info.ArgumentList.Add("--processStart"); info.ArgumentList.Add("Discord.exe"); }
        else foreach (var argument in arguments) info.ArgumentList.Add(argument);
        Process.Start(info)?.Dispose();
        return Task.CompletedTask;
    }

    public Task LockAsync(CancellationToken cancellationToken) { CheckWindows(cancellationToken); if (!LockWorkStation()) ThrowWin32(); return Task.CompletedTask; }
    public Task SleepAsync(CancellationToken cancellationToken) { CheckWindows(cancellationToken); if (!SetSuspendState(false, false, false)) ThrowWin32(); return Task.CompletedTask; }
    public Task RestartAsync(CancellationToken cancellationToken) { CheckWindows(cancellationToken); if (!ExitWindowsEx(EwxReboot | EwxForceIfHung, 0)) ThrowWin32(); return Task.CompletedTask; }
    public Task ShutdownAsync(CancellationToken cancellationToken) { CheckWindows(cancellationToken); if (!ExitWindowsEx(EwxShutdown | EwxForceIfHung, 0)) ThrowWin32(); return Task.CompletedTask; }

    public Task SetVolumeAsync(int volume, CancellationToken cancellationToken)
    {
        CheckWindows(cancellationToken); volume = Math.Clamp(volume, 0, 100);
        for (var i = 0; i < 50; i++) Press(KeyVolumeDown);
        for (var i = 0; i < (int)Math.Round(volume / 2d); i++) Press(KeyVolumeUp);
        _state = _state with { Volume = volume, Muted = false }; return Task.CompletedTask;
    }

    public Task SetMutedAsync(bool muted, CancellationToken cancellationToken)
    {
        CheckWindows(cancellationToken); if (_state.Muted != muted) Press(KeyVolumeMute);
        _state = _state with { Muted = muted }; return Task.CompletedTask;
    }

    public Task MediaKeyAsync(ActionId action, CancellationToken cancellationToken)
    {
        CheckWindows(cancellationToken);
        Press(action switch { ActionId.NextTrack => KeyMediaNext, ActionId.PreviousTrack => KeyMediaPrevious, ActionId.PlayPause => KeyMediaPlayPause, _ => throw new InvalidOperationException("unsupported_media_key") });
        return Task.CompletedTask;
    }

    public Task MonitorOffAsync(CancellationToken cancellationToken) { CheckWindows(cancellationToken); _ = SendMessage(new IntPtr(0xffff), WmSyscommand, new IntPtr(ScMonitorpower), new IntPtr(2)); return Task.CompletedTask; }

    public Task SetPowerPlanAsync(string plan, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var scheme = plan.ToLowerInvariant() switch { "high-performance" => "SCHEME_MIN", "balanced" => "SCHEME_BALANCED", "power-saver" => "SCHEME_MAX", _ => throw new InvalidOperationException("invalid_power_plan") };
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "powercfg.exe");
        using var process = Process.Start(new ProcessStartInfo(path) { UseShellExecute = false, ArgumentList = { "/setactive", scheme }, CreateNoWindow = true }) ?? throw new InvalidOperationException("powercfg_start_failed");
        process.WaitForExit(); if (process.ExitCode != 0) throw new InvalidOperationException("powercfg_failed"); return Task.CompletedTask;
    }

    public Task CloseAllowListedAsync(string app, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_apps.TryGetValue(app, out var path) || path is null) throw new FileNotFoundException($"application_not_found:{app}");
        var expected = Path.GetFullPath(path);
        foreach (var process in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(path)))
        {
            using (process) { try { if (string.Equals(process.MainModule?.FileName, expected, StringComparison.OrdinalIgnoreCase)) process.CloseMainWindow(); } catch (Win32Exception) { } }
        }
        return Task.CompletedTask;
    }

    private static void Press(byte key) { keybd_event(key, 0, 0, UIntPtr.Zero); keybd_event(key, 0, KeyUp, UIntPtr.Zero); }
    private static void CheckWindows(CancellationToken token) { token.ThrowIfCancellationRequested(); if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("windows_required"); }
    private static void ThrowWin32() => throw new Win32Exception(Marshal.GetLastWin32Error());

    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool LockWorkStation();
    [DllImport("powrprof.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetSuspendState(bool hibernate, bool forceCritical, bool disableWakeEvent);
    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool ExitWindowsEx(uint flags, uint reason);
    [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr hWnd, int message, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] private static extern void keybd_event(byte virtualKey, byte scanCode, uint flags, UIntPtr extraInfo);
}

