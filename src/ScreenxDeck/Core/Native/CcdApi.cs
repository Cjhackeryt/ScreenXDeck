using System;
using System.Runtime.InteropServices;

namespace ScreenxDeck.Core.Native;

public static class CcdApi
{
    public const uint SDC_APPLY = 0x00000080;
    public const uint SDC_NO_OPTIMIZATION = 0x00000100;
    public const uint SDC_SAVE_TO_DATABASE = 0x00000200;
    public const uint SDC_ALLOW_CHANGES = 0x00000400;

    public const uint SDC_TOPOLOGY_INTERNAL = 0x00000001;
    public const uint SDC_TOPOLOGY_CLONE = 0x00000002;
    public const uint SDC_TOPOLOGY_EXTEND = 0x00000004;
    public const uint SDC_TOPOLOGY_EXTERNAL = 0x00000008;

    public const uint QDC_ALL_PATHS = 0x00000001;
    public const uint QDC_ONLY_ACTIVE_PATHS = 0x00000002;
    public const uint QDC_DATABASE_CURRENT = 0x00000004;

    public const int ERROR_SUCCESS = 0;

    [DllImport("user32.dll")]
    public static extern int SetDisplayConfig(
        uint numPathArrayElements,
        IntPtr pathArray,
        uint numModeInfoArrayElements,
        IntPtr modeInfoArray,
        uint flags);

    [DllImport("user32.dll")]
    public static extern int QueryDisplayConfig(
        uint flags,
        ref uint numPathArrayElements,
        IntPtr pathArray,
        ref uint numModeInfoArrayElements,
        IntPtr modeInfoArray,
        IntPtr currentTopologyId);

    public enum DisplayTopology
    {
        Internal = 1,
        Clone = 2,
        Extend = 4,
        External = 8,
        Unknown = 0
    }

    /// <summary>
    /// Applies a standard display topology (Internal / PC screen only, Clone / Duplicate, Extend, External / Second screen only).
    /// </summary>
    public static bool ApplyTopology(DisplayTopology topology)
    {
        uint flag = topology switch
        {
            DisplayTopology.Internal => SDC_TOPOLOGY_INTERNAL,
            DisplayTopology.Clone => SDC_TOPOLOGY_CLONE,
            DisplayTopology.Extend => SDC_TOPOLOGY_EXTEND,
            DisplayTopology.External => SDC_TOPOLOGY_EXTERNAL,
            _ => 0
        };

        if (flag == 0) return false;

        int result = SetDisplayConfig(0, IntPtr.Zero, 0, IntPtr.Zero, SDC_APPLY | flag);
        return result == ERROR_SUCCESS;
    }
}
