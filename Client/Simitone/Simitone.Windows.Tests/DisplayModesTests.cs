using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Xunit;

namespace Simitone.Windows.Tests
{
    public class DisplayModesTests
    {
        static readonly DisplayMode[] Monitor =
        {
            new DisplayMode(800, 600, 60),
            new DisplayMode(1024, 768, 60),
            new DisplayMode(1920, 1080, 60),
            new DisplayMode(1920, 1080, 144),
            new DisplayMode(2560, 1440, 60),
            new DisplayMode(2560, 1440, 144)
        };

        [Fact]
        public void DevModeMatchesTheWin32Layout()
        {
            // EnumDisplaySettingsW rejects a DEVMODEW whose dmSize is not 220 bytes, and the
            // launcher would silently fall back to the fixed list.
            var devMode = typeof(DisplayModes).GetNestedType("DEVMODE", BindingFlags.NonPublic);
            Assert.Equal(220, Marshal.SizeOf(devMode));
        }

        [Fact]
        public void ListsTheDisplaySizesOnceEachAndMarksTheDesktop()
        {
            var list = DisplayModes.Resolutions(Monitor, new DisplayMode(2560, 1440, 144), 1920, 1080);

            Assert.Equal(new[] { "1024 x 768", "1920 x 1080", "2560 x 1440  (desktop)" }, list.Select(x => x.ToString()));
        }

        [Fact]
        public void KeepsASavedSizeTheDisplayDoesNotList()
        {
            var list = DisplayModes.Resolutions(Monitor, new DisplayMode(1920, 1080, 60), 1700, 950);

            Assert.Contains(list, r => r.Width == 1700 && r.Height == 950);
        }

        [Fact]
        public void FallsBackToCommonSizesWhenTheDisplayCannotBeRead()
        {
            var list = DisplayModes.Resolutions(new DisplayMode[0], null, 0, 0);

            Assert.Equal(1024, list[0].Width);
            Assert.Contains(list, r => r.Width == 1920 && r.Height == 1080);
            Assert.DoesNotContain(list, r => r.Desktop);
        }

        [Fact]
        public void OffersRatesUpToTheFastestTheDisplaySupports()
        {
            var list = DisplayModes.RefreshRates(Monitor, new DisplayMode(2560, 1440, 144), 60);

            Assert.Equal(new[] { 30, 60, 75, 90, 100, 120, 144 }, list.Select(x => x.Rate));
            Assert.Equal("144 fps  (display)", list.Last().ToString());
        }

        [Fact]
        public void IncludesAnUnusualDisplayRateAndTheSavedRate()
        {
            var list = DisplayModes.RefreshRates(new[] { new DisplayMode(1920, 1080, 59) }, new DisplayMode(1920, 1080, 59), 200);

            Assert.Equal(new[] { 30, 59, 60, 200 }, list.Select(x => x.Rate));
            Assert.True(list.Single(x => x.Rate == 59).Display);
        }
    }
}
