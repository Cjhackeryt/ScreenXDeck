using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace ScreenXDeckPlugin;

public sealed class DisplayModeService
{
    private readonly ILogger _logger;

    public DisplayModeService(ILogger logger) => _logger = logger;

    public async Task SetModeAsync(string mode, CancellationToken cancellationToken)
    {
        if (mode is not ("internal" or "clone" or "extend" or "external"))
            throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unsupported display mode.");

        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = "DisplaySwitch.exe",
            Arguments = "/" + mode,
            UseShellExecute = false,
            CreateNoWindow = true
        }) ?? throw new InvalidOperationException("Windows DisplaySwitch.exe could not be started.");

        await process.WaitForExitAsync(cancellationToken);
        if (process.ExitCode != 0)
            throw new InvalidOperationException($"Windows display switch failed with exit code {process.ExitCode}.");

        _logger.LogInformation("Windows display mode changed to {Mode}.", mode);
    }

    public static bool IsSupportedMode(string mode) =>
        mode is "internal" or "clone" or "extend" or "external";

    public string GetCurrentMode()
    {
        var status = DisplayTopology.Query();
        return status switch
        {
            DisplayTopologyStatus.Clone => "clone",
            DisplayTopologyStatus.Extend => "extend",
            DisplayTopologyStatus.Internal => "internal",
            DisplayTopologyStatus.External => "external",
            _ => "extend"
        };
    }

    private enum DisplayTopologyStatus
    {
        Unknown,
        Internal,
        External,
        Clone,
        Extend
    }

    private static class DisplayTopology
    {
        private const uint QdcOnlyActivePaths = 0x00000002;
        private const uint ErrorInsufficientBuffer = 122;
        private const uint DisplayConfigPathActive = 0x00000001;
        private const uint OutputTechnologyInternal = 0x80000000;

        public static DisplayTopologyStatus Query()
        {
            if (GetDisplayConfigBufferSizes(QdcOnlyActivePaths, out var pathCount, out var modeCount) != 0)
                return DisplayTopologyStatus.Unknown;

            var paths = new DISPLAYCONFIG_PATH_INFO[pathCount];
            var modes = new DISPLAYCONFIG_MODE_INFO[modeCount];
            var result = QueryDisplayConfig(
                QdcOnlyActivePaths,
                ref pathCount,
                paths,
                ref modeCount,
                modes,
                IntPtr.Zero);
            if (result != 0 && result != ErrorInsufficientBuffer)
                return DisplayTopologyStatus.Unknown;

            var activePaths = paths.Take((int)pathCount)
                .Where(path => (path.flags & DisplayConfigPathActive) != 0)
                .ToArray();
            if (activePaths.Length == 0)
                return DisplayTopologyStatus.Unknown;

            var sourceCount = activePaths.Select(path => path.sourceInfo.id).Distinct().Count();
            if (sourceCount == 1 && activePaths.Length > 1)
                return DisplayTopologyStatus.Clone;
            if (sourceCount > 1)
                return DisplayTopologyStatus.Extend;

            return activePaths.Any(path =>
                (uint)path.targetInfo.outputTechnology == OutputTechnologyInternal)
                ? DisplayTopologyStatus.Internal
                : DisplayTopologyStatus.External;
        }

        [DllImport("user32.dll")]
        private static extern int GetDisplayConfigBufferSizes(
            uint flags,
            out uint numPathArrayElements,
            out uint numModeInfoArrayElements);

        [DllImport("user32.dll")]
        private static extern int QueryDisplayConfig(
            uint flags,
            ref uint numPathArrayElements,
            [Out] DISPLAYCONFIG_PATH_INFO[] pathInfoArray,
            ref uint numModeInfoArrayElements,
            [Out] DISPLAYCONFIG_MODE_INFO[] modeInfoArray,
            IntPtr currentTopologyId);

        [StructLayout(LayoutKind.Sequential)]
        private struct DISPLAYCONFIG_PATH_INFO
        {
            public DISPLAYCONFIG_PATH_SOURCE_INFO sourceInfo;
            public DISPLAYCONFIG_PATH_TARGET_INFO targetInfo;
            public uint flags;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DISPLAYCONFIG_PATH_SOURCE_INFO
        {
            public Luid adapterId;
            public uint id;
            public uint modeInfoIdx;
            public uint statusFlags;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DISPLAYCONFIG_PATH_TARGET_INFO
        {
            public Luid adapterId;
            public uint id;
            public uint modeInfoIdx;
            public DISPLAYCONFIG_VIDEO_OUTPUT_TECHNOLOGY outputTechnology;
            public DISPLAYCONFIG_ROTATION rotation;
            public DISPLAYCONFIG_SCALING scaling;
            public DISPLAYCONFIG_RATIONAL refreshRate;
            public DISPLAYCONFIG_SCANLINE_ORDERING scanLineOrdering;
            public bool targetAvailable;
            public uint statusFlags;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DISPLAYCONFIG_MODE_INFO
        {
            public DISPLAYCONFIG_MODE_INFO_TYPE infoType;
            public uint id;
            public Luid adapterId;
            public DISPLAYCONFIG_MODE_INFO_UNION modeInfo;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct DISPLAYCONFIG_MODE_INFO_UNION
        {
            [FieldOffset(0)]
            public DISPLAYCONFIG_TARGET_MODE targetMode;
            [FieldOffset(0)]
            public DISPLAYCONFIG_SOURCE_MODE sourceMode;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DISPLAYCONFIG_TARGET_MODE
        {
            public DISPLAYCONFIG_VIDEO_SIGNAL_INFO targetVideoSignalInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DISPLAYCONFIG_SOURCE_MODE
        {
            public uint width;
            public uint height;
            public DISPLAYCONFIG_PIXELFORMAT pixelFormat;
            public POINTL position;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DISPLAYCONFIG_VIDEO_SIGNAL_INFO
        {
            public ulong pixelRate;
            public DISPLAYCONFIG_RATIONAL hSyncFreq;
            public DISPLAYCONFIG_RATIONAL vSyncFreq;
            public DISPLAYCONFIG_2DREGION activeSize;
            public DISPLAYCONFIG_2DREGION totalSize;
            public uint videoStandard;
            public DISPLAYCONFIG_SCANLINE_ORDERING scanLineOrdering;
            public DISPLAYCONFIG_VIDEO_SIGNAL_INFO_FLAGS additionalSignalInfo;
            public uint videoTimings;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DISPLAYCONFIG_RATIONAL
        {
            public uint Numerator;
            public uint Denominator;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DISPLAYCONFIG_2DREGION
        {
            public uint cx;
            public uint cy;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINTL
        {
            public int x;
            public int y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct Luid
        {
            public uint LowPart;
            public int HighPart;
        }

        private enum DISPLAYCONFIG_MODE_INFO_TYPE : uint
        {
            Source = 1,
            Target = 2
        }

        private enum DISPLAYCONFIG_VIDEO_OUTPUT_TECHNOLOGY : uint
        {
            Other = 0,
            Hd15 = 1,
            Dvi = 2,
            Hdmi = 5,
            DisplayPortExternal = 10,
            DisplayPortEmbedded = 11,
            Internal = OutputTechnologyInternal
        }

        private enum DISPLAYCONFIG_ROTATION : uint { Identity = 1 }
        private enum DISPLAYCONFIG_SCALING : uint { Identity = 1 }
        private enum DISPLAYCONFIG_SCANLINE_ORDERING : uint { Unspecified = 0 }
        private enum DISPLAYCONFIG_PIXELFORMAT : uint { EightBpp = 1 }
        [Flags]
        private enum DISPLAYCONFIG_VIDEO_SIGNAL_INFO_FLAGS : uint { None = 0 }
    }
}
