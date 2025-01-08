using System;
using System.Runtime.InteropServices;
using Rainmeter;

public enum AccentState 
{
    DISABLED = 0,
    BLURBEHIND = 3,
    ACRYLIC = 4
}

[StructLayout(LayoutKind.Sequential)]
public struct ACCENTPOLICY
{
    public int nAccentState;
    public int nFlags;
    public uint nColor;
    public int nAnimationId;
}

[StructLayout(LayoutKind.Sequential)]
public struct WINCOMPATTRDATA
{
    public int nAttribute;
    public IntPtr pData;
    public uint ulDataSize;
}

public class Measure
{
    private IntPtr skinHandle = IntPtr.Zero;
    private AccentState previousAccentState = AccentState.DISABLED;
    private int previousCornerType = 0;

    private static IntPtr user32 = IntPtr.Zero;
    private static IntPtr dwmApi = IntPtr.Zero;

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate bool SetWindowCompositionAttributeDelegate(IntPtr hwnd, ref WINCOMPATTRDATA data);
    private static SetWindowCompositionAttributeDelegate SetWindowCompositionAttribute;

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int DwmSetWindowAttributeDelegate(IntPtr hwnd, int attribute, ref int value, int size);
    private static DwmSetWindowAttributeDelegate DwmSetWindowAttribute;

    static Measure()
    {
        user32 = LoadLibrary("user32.dll");
        if (user32 != IntPtr.Zero)
        {
            IntPtr proc = GetProcAddress(user32, "SetWindowCompositionAttribute");
            if (proc != IntPtr.Zero)
            {
                SetWindowCompositionAttribute = (SetWindowCompositionAttributeDelegate)Marshal.GetDelegateForFunctionPointer(proc, typeof(SetWindowCompositionAttributeDelegate));
            }
        }

        dwmApi = LoadLibrary("dwmapi.dll");
        if (dwmApi != IntPtr.Zero)
        {
            IntPtr proc = GetProcAddress(dwmApi, "DwmSetWindowAttribute");
            if (proc != IntPtr.Zero)
            {
                DwmSetWindowAttribute = (DwmSetWindowAttributeDelegate)Marshal.GetDelegateForFunctionPointer(proc, typeof(DwmSetWindowAttributeDelegate));
            }
        }
    }

    private static IntPtr LoadLibrary(string libraryName) => NativeMethods.LoadLibrary(libraryName);
    private static IntPtr GetProcAddress(IntPtr hModule, string procedureName) => NativeMethods.GetProcAddress(hModule, procedureName);

    private static bool SetAccentState(IntPtr hwnd, AccentState state)
    {
        if (SetWindowCompositionAttribute != null)
        {
            var policy = new ACCENTPOLICY
            {
                nAccentState = (int)state,
                nFlags = 0,
                nColor = 0x01000000,
                nAnimationId = 1
            };

            var data = new WINCOMPATTRDATA
            {
                nAttribute = 19,
                pData = Marshal.AllocHGlobal(Marshal.SizeOf(policy)),
                ulDataSize = (uint)Marshal.SizeOf(policy)
            };

            bool result = false;
            try
            {
                Marshal.StructureToPtr(policy, data.pData, false);
                result = SetWindowCompositionAttribute(hwnd, ref data);
            }
            finally
            {
                if (data.pData != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(data.pData);
                }
            }

            return result;
        }

        return false;
    }

    private static bool SetCornerType(IntPtr hwnd, int cornerType)
    {
        if (DwmSetWindowAttribute != null)
        {
            int dwmCornerType = cornerType switch
            {
                1 => 2, // Rounded
                2 => 1, // RoundedSmall
                _ => 0  // Default
            };
            return DwmSetWindowAttribute(hwnd, 33, ref dwmCornerType, sizeof(int)) == 0;
        }

        return false;
    }

    public void Reload(API api)
    {
        skinHandle = api.GetSkinWindow();

        string type = api.ReadString("Type", "None").ToLowerInvariant();
        AccentState accentState = type switch
        {
            "blur" => AccentState.BLURBEHIND,
            "acrylic" => AccentState.ACRYLIC,
            _ => AccentState.DISABLED
        };

        if (accentState != previousAccentState)
        {
            SetAccentState(skinHandle, AccentState.DISABLED);
            SetAccentState(skinHandle, accentState);
            previousAccentState = accentState;
        }

        string corner = api.ReadString("Corner", "None").ToLowerInvariant();
        int cornerType = corner switch
        {
            "round" => 1,
            "roundsmall" => 2,
            _ => 0
        };

        if (cornerType != previousCornerType)
        {
            SetCornerType(skinHandle, cornerType);
            previousCornerType = cornerType;
        }
    }

    public void Finalize()
    {
        SetAccentState(skinHandle, AccentState.DISABLED);
        if (user32 != IntPtr.Zero)
        {
            NativeMethods.FreeLibrary(user32);
            user32 = IntPtr.Zero;
        }
        if (dwmApi != IntPtr.Zero)
        {
            NativeMethods.FreeLibrary(dwmApi);
            dwmApi = IntPtr.Zero;
        }
    }
}

public static class Plugin
{
    [DllExport]
    public static void Initialize(ref IntPtr data, IntPtr rm)
    {
        data = GCHandle.ToIntPtr(GCHandle.Alloc(new Measure()));
    }

    [DllExport]
    public static void Reload(IntPtr data, IntPtr rm, ref double maxValue)
    {
        var measure = (Measure)GCHandle.FromIntPtr(data).Target;
        measure.Reload(new API(rm));
    }

    [DllExport]
    public static double Update(IntPtr data) => 0.0;

    [DllExport]
    public static void Finalize(IntPtr data)
    {
        var measure = (Measure)GCHandle.FromIntPtr(data).Target;
        measure.Finalize();
        GCHandle.FromIntPtr(data).Free();
    }
}

internal static class NativeMethods
{
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern IntPtr LoadLibrary(string lpFileName);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool FreeLibrary(IntPtr hModule);
}