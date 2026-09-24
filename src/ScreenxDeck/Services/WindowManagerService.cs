using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using ScreenxDeck.Core.Models;
using ScreenxDeck.Core.Native;
using Serilog;

namespace ScreenxDeck.Services;

public sealed class WindowManagerService : IDisposable
{
    private readonly ILogger _logger;
    private readonly DisplayManagerService _displayManager;
    private readonly SettingsService _settingsService;
    // Set of HWNDs currently pinned by the plugin
    private readonly ConcurrentDictionary<IntPtr, string> _pinnedWindows = new();
    private readonly Timer _autoPinTimer;

    public WindowManagerService(ILogger logger, DisplayManagerService displayManager, SettingsService settingsService)
    {
        _logger = logger.ForContext<WindowManagerService>();
        _displayManager = displayManager;
        _settingsService = settingsService;

        // Auto-pin check timer every 2 seconds
        _autoPinTimer = new Timer(CheckAutoPinWindows, null, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2));
    }

    public bool IsForegroundWindowTopmost()
    {
        if (!OperatingSystem.IsWindows()) return false;

        IntPtr hWnd = User32.GetForegroundWindow();
        if (hWnd == IntPtr.Zero) return false;

        IntPtr exStyle = User32.GetWindowLongPtr(hWnd, User32.GWL_EXSTYLE);
        return (exStyle.ToInt64() & User32.WS_EX_TOPMOST) != 0;
    }

    public bool ToggleAlwaysOnTopActiveWindow()
    {
        IntPtr hWnd = User32.GetForegroundWindow();
        if (hWnd == IntPtr.Zero)
        {
            _logger.Warning("No foreground window found to toggle Always On Top");
            return false;
        }

        bool isCurrentlyTopmost = IsForegroundWindowTopmost();
        IntPtr targetHwnd = isCurrentlyTopmost ? User32.HWND_NOTOPMOST : User32.HWND_TOPMOST;

        bool success = User32.SetWindowPos(
            hWnd,
            targetHwnd,
            0, 0, 0, 0,
            User32.SWP_NOMOVE | User32.SWP_NOSIZE | User32.SWP_SHOWWINDOW);

        if (success)
        {
            string title = GetWindowTitle(hWnd);
            if (!isCurrentlyTopmost)
            {
                _pinnedWindows[hWnd] = title;
                _logger.Information("Window pinned Always on Top: '{Title}'", title);
            }
            else
            {
                _pinnedWindows.TryRemove(hWnd, out _);
                _logger.Information("Window unpinned: '{Title}'", title);
            }
            return true;
        }

        _logger.Error("Failed to toggle Always on Top for HWND 0x{Hwnd:X}", hWnd.ToInt64());
        return false;
    }

    public bool SetWindowAlwaysOnTop(IntPtr hWnd, bool alwaysOnTop)
    {
        if (hWnd == IntPtr.Zero) return false;

        IntPtr targetHwnd = alwaysOnTop ? User32.HWND_TOPMOST : User32.HWND_NOTOPMOST;
        bool success = User32.SetWindowPos(
            hWnd,
            targetHwnd,
            0, 0, 0, 0,
            User32.SWP_NOMOVE | User32.SWP_NOSIZE | User32.SWP_SHOWWINDOW);

        if (success)
        {
            string title = GetWindowTitle(hWnd);
            if (alwaysOnTop)
            {
                _pinnedWindows[hWnd] = title;
            }
            else
            {
                _pinnedWindows.TryRemove(hWnd, out _);
            }
        }

        return success;
    }

    public void UnpinAllWindows()
    {
        int count = 0;
        foreach (var hWnd in _pinnedWindows.Keys.ToList())
        {
            if (SetWindowAlwaysOnTop(hWnd, false))
            {
                count++;
            }
        }
        _pinnedWindows.Clear();
        _logger.Information("Unpinned {Count} windows", count);
    }

    public bool MoveActiveWindowToDisplay(int displayIndex)
    {
        IntPtr hWnd = User32.GetForegroundWindow();
        if (hWnd == IntPtr.Zero) return false;

        var targetDisplay = _displayManager.GetDisplay(displayIndex);
        if (targetDisplay == null)
        {
            _logger.Warning("Target Display {Index} not found", displayIndex);
            return false;
        }

        if (!User32.GetWindowRect(hWnd, out var windowRect))
        {
            return false;
        }

        int width = windowRect.Width;
        int height = windowRect.Height;

        // Position window centered in target display work area
        int targetX = targetDisplay.WorkArea.Left + Math.Max(0, (targetDisplay.WorkArea.Width - width) / 2);
        int targetY = targetDisplay.WorkArea.Top + Math.Max(0, (targetDisplay.WorkArea.Height - height) / 2);

        bool success = User32.MoveWindow(hWnd, targetX, targetY, width, height, true);
        if (success)
        {
            _logger.Information("Moved active window to Display {Index} at ({X}, {Y})", displayIndex, targetX, targetY);
        }
        return success;
    }

    public bool MoveActiveWindowNextPrevious(bool next)
    {
        IntPtr hWnd = User32.GetForegroundWindow();
        if (hWnd == IntPtr.Zero) return false;

        if (!User32.GetWindowRect(hWnd, out var windowRect))
        {
            return false;
        }

        var monitors = _displayManager.ConnectedMonitors;
        if (monitors.Count <= 1) return true;

        int centerX = windowRect.Left + windowRect.Width / 2;
        int centerY = windowRect.Top + windowRect.Height / 2;

        // Find which monitor currently holds the center of the window
        var currentMonitor = monitors.FirstOrDefault(m =>
            centerX >= m.Bounds.Left && centerX <= m.Bounds.Right &&
            centerY >= m.Bounds.Top && centerY <= m.Bounds.Bottom) ?? monitors[0];

        int currentIndex = 0;
        for (int i = 0; i < monitors.Count; i++)
        {
            if (monitors[i].DisplayIndex == currentMonitor.DisplayIndex)
            {
                currentIndex = i;
                break;
            }
        }
        int targetIndex = next
            ? (currentIndex + 1) % monitors.Count
            : (currentIndex - 1 + monitors.Count) % monitors.Count;

        var targetMonitor = monitors[targetIndex];
        return MoveActiveWindowToDisplay(targetMonitor.DisplayIndex);
    }

    public bool MaximizeActiveWindowOnDisplay(int displayIndex)
    {
        IntPtr hWnd = User32.GetForegroundWindow();
        if (hWnd == IntPtr.Zero) return false;

        var targetDisplay = _displayManager.GetDisplay(displayIndex);
        if (targetDisplay == null) return false;

        // First restore to moveable state
        User32.ShowWindow(hWnd, User32.SW_RESTORE);

        // Move to target display
        User32.MoveWindow(
            hWnd,
            targetDisplay.WorkArea.Left,
            targetDisplay.WorkArea.Top,
            targetDisplay.WorkArea.Width / 2,
            targetDisplay.WorkArea.Height / 2,
            true);

        // Maximize
        User32.ShowWindow(hWnd, User32.SW_MAXIMIZE);
        _logger.Information("Maximized active window on Display {Index}", displayIndex);
        return true;
    }

    public bool MoveCursorToDisplay(int displayIndex)
    {
        var targetDisplay = _displayManager.GetDisplay(displayIndex);
        if (targetDisplay == null) return false;

        int centerX = targetDisplay.Bounds.Left + targetDisplay.Bounds.Width / 2;
        int centerY = targetDisplay.Bounds.Top + targetDisplay.Bounds.Height / 2;

        bool success = User32.SetCursorPos(centerX, centerY);
        if (success)
        {
            _logger.Information("Moved cursor to center of Display {Index} ({X}, {Y})", displayIndex, centerX, centerY);
        }
        return success;
    }

    private void CheckAutoPinWindows(object? state)
    {
        try
        {
            var autoPinList = _settingsService.Current.AutoPinApplications;
            if (autoPinList == null || autoPinList.Count == 0) return;

            IntPtr hWnd = User32.GetForegroundWindow();
            if (hWnd == IntPtr.Zero) return;

            User32.GetWindowThreadProcessId(hWnd, out uint processId);
            if (processId == 0) return;

            using var process = Process.GetProcessById((int)processId);
            string processName = process.ProcessName;
            string windowTitle = GetWindowTitle(hWnd);

            bool matches = autoPinList.Any(target =>
                string.Equals(target, processName, StringComparison.OrdinalIgnoreCase) ||
                windowTitle.Contains(target, StringComparison.OrdinalIgnoreCase));

            if (matches && !IsForegroundWindowTopmost())
            {
                SetWindowAlwaysOnTop(hWnd, true);
                _logger.Information("Auto-pinned matching application '{ProcessName}' (HWND: 0x{Hwnd:X})", processName, hWnd.ToInt64());
            }
        }
        catch
        {
            // Ignore intermittent process lookup errors
        }
    }

    public (string title, string processName, int displayIndex) GetActiveWindowInfo()
    {
        if (!OperatingSystem.IsWindows()) return ("Desktop", string.Empty, 0);

        IntPtr hWnd = User32.GetForegroundWindow();
        if (hWnd == IntPtr.Zero) return (string.Empty, string.Empty, 0);

        string title = GetWindowTitle(hWnd);
        string processName = string.Empty;

        try
        {
            User32.GetWindowThreadProcessId(hWnd, out uint processId);
            if (processId != 0)
            {
                using var proc = Process.GetProcessById((int)processId);
                processName = proc.ProcessName;
            }
        }
        catch
        {
            // Ignore process lookup failure
        }

        int displayIndex = 1;
        if (User32.GetWindowRect(hWnd, out RECT rect))
        {
            int midX = rect.Left + rect.Width / 2;
            int midY = rect.Top + rect.Height / 2;
            var mon = _displayManager.ConnectedMonitors.FirstOrDefault(m =>
                midX >= m.Bounds.Left && midX < m.Bounds.Right &&
                midY >= m.Bounds.Top && midY < m.Bounds.Bottom);
            if (mon != null) displayIndex = mon.DisplayIndex;
        }

        return (title, processName, displayIndex);
    }

    private static string GetWindowTitle(IntPtr hWnd)
    {
        var sb = new StringBuilder(256);
        User32.GetWindowText(hWnd, sb, 256);
        return sb.ToString();
    }

    public void Dispose()
    {
        _autoPinTimer.Dispose();
        _pinnedWindows.Clear();
    }
}
