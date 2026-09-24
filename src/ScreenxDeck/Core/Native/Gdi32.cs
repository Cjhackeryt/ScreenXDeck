using System;
using System.Runtime.InteropServices;

namespace ScreenxDeck.Core.Native;

public static class Gdi32
{
    [DllImport("gdi32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    public static extern IntPtr CreateDC(string? lpszDriver, string? lpszDevice, string? lpszOutput, IntPtr lpInitData);

    [DllImport("gdi32.dll", SetLastError = true)]
    public static extern bool DeleteDC(IntPtr hdc);

    [DllImport("gdi32.dll", SetLastError = true)]
    public static extern bool GetDeviceGammaRamp(IntPtr hdc, ref RAMP lpRamp);

    [DllImport("gdi32.dll", SetLastError = true)]
    public static extern bool SetDeviceGammaRamp(IntPtr hdc, ref RAMP lpRamp);

    /// <summary>
    /// Gets a valid device context for a display device, trying DISPLAY driver first, then device name, then default.
    /// </summary>
    public static IntPtr GetDisplayDC(string? deviceName)
    {
        IntPtr hdc = IntPtr.Zero;
        if (!string.IsNullOrEmpty(deviceName))
        {
            hdc = CreateDC("DISPLAY", deviceName, null, IntPtr.Zero);
            if (hdc == IntPtr.Zero)
            {
                hdc = CreateDC(null, deviceName, null, IntPtr.Zero);
            }
        }
        if (hdc == IntPtr.Zero)
        {
            hdc = CreateDC("DISPLAY", null, null, IntPtr.Zero);
        }
        return hdc;
    }

    /// <summary>
    /// Computes a safe, driver-compliant GDI gamma ramp corresponding to the specified brightness percentage (0 to 100).
    /// Uses a power-law gamma curve (gamma 1.0 at 100% to 4.2 at 0%) which maintains full dynamic endpoints (0 and 65535)
    /// while smoothly darkening midtones and shadows. This completely avoids Windows graphics driver safety heuristic
    /// rejections that cause linear ramps to fail below 50%.
    /// </summary>
    public static RAMP CreateGammaRampForBrightness(int brightnessPercent)
    {
        brightnessPercent = Math.Clamp(brightnessPercent, 0, 100);
        // Exponent scales smoothly: 1.0 (neutral 100%) to 4.2 (dark floor at 0%)
        double gamma = 1.0 + Math.Pow((100.0 - brightnessPercent) / 100.0, 1.15) * 3.2;
        return CreateGammaRampWithExponent(gamma);
    }

    /// <summary>
    /// Computes a GDI gamma ramp for an exact gamma exponent.
    /// </summary>
    public static RAMP CreateGammaRampWithExponent(double gamma)
    {
        var ramp = new RAMP();
        ramp.Init();

        for (int i = 0; i < 256; i++)
        {
            double normalized = i / 255.0;
            double output = Math.Pow(normalized, gamma);
            int value = (int)Math.Round(output * 65535.0);
            ushort clamped = (ushort)Math.Clamp(value, 0, 65535);

            ramp.Red[i] = clamped;
            ramp.Green[i] = clamped;
            ramp.Blue[i] = clamped;
        }

        return ramp;
    }
}
