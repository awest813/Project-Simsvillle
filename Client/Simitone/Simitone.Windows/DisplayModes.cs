using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace Simitone.Windows
{
    public struct DisplayMode
    {
        public int Width;
        public int Height;
        public int RefreshRate;

        public DisplayMode(int width, int height, int refreshRate)
        {
            Width = width;
            Height = height;
            RefreshRate = refreshRate;
        }
    }

    public class ResolutionOption
    {
        public int Width;
        public int Height;
        public bool Desktop;

        public override string ToString()
        {
            return Width + " x " + Height + (Desktop ? "  (desktop)" : "");
        }
    }

    public class RefreshOption
    {
        public int Rate;
        public bool Display;

        public override string ToString()
        {
            return Rate + " fps" + (Display ? "  (display)" : "");
        }
    }

    /// <summary>
    /// Builds the launcher's resolution and frame rate lists from what the primary display reports.
    /// The lists are built from plain data so they can be tested without a display.
    /// </summary>
    public static class DisplayModes
    {
        public const int MinWidth = 1024;
        public const int MinHeight = 768;

        static readonly DisplayMode[] FallbackModes =
        {
            new DisplayMode(1024, 768, 60),
            new DisplayMode(1280, 720, 60),
            new DisplayMode(1366, 768, 60),
            new DisplayMode(1600, 900, 60),
            new DisplayMode(1920, 1080, 60),
            new DisplayMode(2560, 1440, 60),
            new DisplayMode(3840, 2160, 60)
        };

        static readonly int[] CommonRates = { 30, 60, 75, 90, 100, 120, 144, 165, 240 };

        /// <summary>
        /// Sizes the player can pick, smallest first. The desktop size is marked, the saved size is
        /// kept even when the display does not list it, and nothing smaller than 1024 x 768 is offered,
        /// since the interface does not fit below that.
        /// </summary>
        public static List<ResolutionOption> Resolutions(IEnumerable<DisplayMode> modes, DisplayMode? desktop, int savedWidth, int savedHeight)
        {
            var sizes = (modes ?? Enumerable.Empty<DisplayMode>())
                .Where(m => m.Width >= MinWidth && m.Height >= MinHeight)
                .Select(m => (m.Width, m.Height))
                .ToList();
            if (sizes.Count == 0)
                sizes = FallbackModes.Select(m => (m.Width, m.Height)).ToList();
            if (desktop != null && desktop.Value.Width >= MinWidth && desktop.Value.Height >= MinHeight)
                sizes.Add((desktop.Value.Width, desktop.Value.Height));
            if (savedWidth > 0 && savedHeight > 0)
                sizes.Add((savedWidth, savedHeight));

            return sizes
                .Distinct()
                .OrderBy(s => s.Width).ThenBy(s => s.Height)
                .Select(s => new ResolutionOption
                {
                    Width = s.Width,
                    Height = s.Height,
                    Desktop = desktop != null && desktop.Value.Width == s.Width && desktop.Value.Height == s.Height
                })
                .ToList();
        }

        /// <summary>
        /// Frame rates the player can pick: common rates up to the display's current refresh rate
        /// (or the fastest mode it reports, when the current rate is unknown), that rate itself,
        /// and the saved rate. Frames past the display's rate are never shown, so they are not offered.
        /// </summary>
        public static List<RefreshOption> RefreshRates(IEnumerable<DisplayMode> modes, DisplayMode? desktop, int savedRate)
        {
            var reported = (modes ?? Enumerable.Empty<DisplayMode>())
                .Select(m => m.RefreshRate)
                .Where(r => r > 1)
                .ToList();
            var displayRate = desktop?.RefreshRate > 1 ? desktop.Value.RefreshRate : 0;
            if (displayRate > 0) reported.Add(displayRate);
            var max = Math.Max(60, displayRate > 0 ? displayRate : (reported.Count == 0 ? 60 : reported.Max()));

            var rates = CommonRates.Where(r => r <= max).ToList();
            if (displayRate > 0) rates.Add(displayRate);
            if (savedRate > 0) rates.Add(savedRate);

            return rates
                .Distinct()
                .OrderBy(r => r)
                .Select(r => new RefreshOption { Rate = r, Display = r == displayRate })
                .ToList();
        }

        /// <summary>
        /// The primary display's modes, in physical pixels. Empty when the display cannot be queried.
        /// </summary>
        public static List<DisplayMode> Query(out DisplayMode? desktop)
        {
            desktop = null;
            var modes = new List<DisplayMode>();
            try
            {
                var mode = NewDevMode();
                if (EnumDisplaySettings(null, ENUM_CURRENT_SETTINGS, ref mode))
                    desktop = new DisplayMode(mode.dmPelsWidth, mode.dmPelsHeight, mode.dmDisplayFrequency);

                for (int i = 0; i < 1000; i++)
                {
                    mode = NewDevMode();
                    if (!EnumDisplaySettings(null, i, ref mode)) break;
                    if (mode.dmBitsPerPel < 32) continue;
                    modes.Add(new DisplayMode(mode.dmPelsWidth, mode.dmPelsHeight, mode.dmDisplayFrequency));
                }
            }
            catch (Exception)
            {
                // Not on Windows, or the call is unavailable. The fallback list covers it.
            }
            return modes;
        }

        const int ENUM_CURRENT_SETTINGS = -1;

        static DEVMODE NewDevMode()
        {
            var mode = new DEVMODE();
            mode.dmSize = (short)Marshal.SizeOf(typeof(DEVMODE));
            return mode;
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "EnumDisplaySettingsW")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool EnumDisplaySettings(string deviceName, int modeNum, ref DEVMODE devMode);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct DEVMODE
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
    }
}
