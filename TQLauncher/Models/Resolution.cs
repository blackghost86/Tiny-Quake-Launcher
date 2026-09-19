using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace TinyQuakeLauncher;

public sealed class Resolution
{
    private const int ENUM_CURRENT_SETTINGS = -1;

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool EnumDisplaySettings(
        string? deviceName,
        int modeNum,
        ref DevMode devMode);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DevMode
    {
        private const int CCHDEVICENAME = 32;
        private const int CCHFORMNAME = 32;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CCHDEVICENAME)]
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

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CCHFORMNAME)]
        public string dmFormName;

        public short dmLogPixels;
        public int dmBitsPerPel;
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
    }

    public int Width { get; }

    public int Height { get; }

    public bool IsDefault { get; }

    public string DisplayName =>
        IsDefault
            ? "Default"
            : $"{Width}x{Height}";

    public Resolution(
        int width,
        int height,
        bool isDefault = false)
    {
        if (!isDefault &&
            (width <= 0 || height <= 0))
        {
            throw new ArgumentOutOfRangeException(
                nameof(width),
                "Resolution dimensions must be greater than zero.");
        }

        Width = width;
        Height = height;
        IsDefault = isDefault;
    }

    public static Resolution Default =>
        new Resolution(0, 0, true);

    public static List<Resolution> GetAvailableResolutions()
    {
        var resolutions =
            new Dictionary<string, Resolution>(
                StringComparer.OrdinalIgnoreCase);

        DevMode mode = new DevMode();
        int modeIndex = 0;

        while (EnumDisplaySettings(
            null,
            modeIndex++,
            ref mode))
        {
            if (mode.dmPelsWidth <= 0 ||
                mode.dmPelsHeight <= 0)
            {
                continue;
            }

            string key =
                $"{mode.dmPelsWidth}x{mode.dmPelsHeight}";

            if (!resolutions.ContainsKey(key))
            {
                resolutions.Add(
                    key,
                    new Resolution(
                        mode.dmPelsWidth,
                        mode.dmPelsHeight));
            }
        }

        return resolutions.Values
            .OrderByDescending(
                resolution => resolution.Width)
            .ThenByDescending(
                resolution => resolution.Height)
            .ToList();
    }

    public override string ToString()
    {
        return DisplayName;
    }
}
