using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Extensions.Logging;

namespace ScreenXDeckPlugin;

public sealed class RefreshRateService
{
    private readonly ILogger _logger;

    public RefreshRateService(ILogger logger) => _logger = logger;

    public int? GetCurrent(int monitorIndex)
    {
        var screen = GetScreen(monitorIndex);
        if (screen is null)
            return null;

        var mode = DevMode.Create();
        return EnumDisplaySettings(screen.DeviceName, ENUM_CURRENT_SETTINGS, ref mode)
            ? mode.dmDisplayFrequency
            : null;
    }

    public IReadOnlyList<int> GetAvailable(int monitorIndex)
    {
        var screen = GetScreen(monitorIndex);
        if (screen is null)
            return [];

        var rates = new HashSet<int>();
        for (var modeIndex = 0; ; modeIndex++)
        {
            var mode = DevMode.Create();
            if (!EnumDisplaySettings(screen.DeviceName, modeIndex, ref mode))
                break;

            if (mode.dmPelsWidth == screen.Bounds.Width &&
                mode.dmPelsHeight == screen.Bounds.Height &&
                mode.dmDisplayFrequency > 1)
            {
                rates.Add(mode.dmDisplayFrequency);
            }
        }

        return rates.OrderBy(rate => rate).ToArray();
    }

    public void Set(int monitorIndex, int refreshRate)
    {
        var screen = GetScreen(monitorIndex)
            ?? throw new InvalidOperationException($"Monitor {monitorIndex + 1} is not connected.");
        var available = GetAvailable(monitorIndex);
        if (!available.Contains(refreshRate))
            throw new InvalidOperationException(
                $"Refresh rate {refreshRate} Hz is not supported by {screen.DeviceName} at its current resolution.");

        var mode = DevMode.Create();
        if (!EnumDisplaySettings(screen.DeviceName, ENUM_CURRENT_SETTINGS, ref mode))
            throw new InvalidOperationException("Unable to read the current display mode.");

        mode.dmDisplayFrequency = (short)refreshRate;
        mode.dmFields = DM_DISPLAYFREQUENCY;
        var test = ChangeDisplaySettingsEx(screen.DeviceName, ref mode, IntPtr.Zero, CDS_TEST, IntPtr.Zero);
        if (test != DISP_CHANGE_SUCCESSFUL)
            throw new InvalidOperationException($"Windows rejected refresh rate {refreshRate} Hz (code {test}).");

        var result = ChangeDisplaySettingsEx(screen.DeviceName, ref mode, IntPtr.Zero, 0, IntPtr.Zero);
        if (result != DISP_CHANGE_SUCCESSFUL)
            throw new InvalidOperationException($"Windows could not apply refresh rate {refreshRate} Hz (code {result}).");

        _logger.LogInformation("Set {Monitor} refresh rate to {RefreshRate} Hz.", screen.DeviceName, refreshRate);
    }

    public void Step(int monitorIndex, int direction)
    {
        var rates = GetAvailable(monitorIndex);
        var current = GetCurrent(monitorIndex);
        if (rates.Count == 0 || current is null)
            throw new InvalidOperationException($"No refresh rates are available for monitor {monitorIndex + 1}.");

        var currentPosition = Array.IndexOf(rates.ToArray(), current.Value);
        var nextPosition = Math.Clamp(currentPosition + Math.Sign(direction), 0, rates.Count - 1);
        Set(monitorIndex, rates[nextPosition]);
    }

    private static Screen? GetScreen(int index) =>
        index >= 0 && index < Screen.AllScreens.Length ? Screen.AllScreens[index] : null;

    private const int ENUM_CURRENT_SETTINGS = -1;
    private const int DM_DISPLAYFREQUENCY = 0x00400000;
    private const uint CDS_TEST = 0x00000002;
    private const int DISP_CHANGE_SUCCESSFUL = 0;

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool EnumDisplaySettings(
        string? deviceName,
        int modeNum,
        ref DevMode devMode);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int ChangeDisplaySettingsEx(
        string deviceName,
        ref DevMode devMode,
        IntPtr hwnd,
        uint flags,
        IntPtr lParam);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DevMode
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string dmDeviceName;
        public short dmSpecVersion;
        public short dmDriverVersion;
        public short dmSize;
        public short dmDriverExtra;
        public int dmFields;
        public int dmPositionX;
        public int dmPositionY;
        public int dmDisplayOrientation;
        public int dmDisplayFixedOutput;
        public short dmColor;
        public short dmDuplex;
        public short dmYResolution;
        public short dmTTOption;
        public short dmCollate;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string dmFormName;
        public short dmLogPixels;
        public short dmBitsPerPel;
        public int dmPelsWidth;
        public int dmPelsHeight;
        public int dmDisplayFlags;
        public int dmDisplayFrequency;
        public int dmICMMethod;
        public int dmICMIntent;
        public int dmMediaType;
        public int dmDitherType;
        public int dmReserved1;
        public int dmReserved2;
        public int dmPanningWidth;
        public int dmPanningHeight;

        public static DevMode Create() => new()
        {
            dmDeviceName = string.Empty,
            dmFormName = string.Empty,
            dmSize = (short)Marshal.SizeOf<DevMode>()
        };
    }
}
