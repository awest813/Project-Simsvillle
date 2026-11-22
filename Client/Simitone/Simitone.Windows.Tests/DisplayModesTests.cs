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
        public void StopsAtTheRateTheDesktopRunsAtNotTheFastestMode()
        {
            // A 144 Hz monitor left at 60 Hz cannot show more than 60 frames.
            var list = DisplayModes.RefreshRates(Monitor, new DisplayMode(2560, 1440, 60), 60);

            Assert.Equal(new[] { 30, 60 }, list.Select(x => x.Rate));
        }

        [Fact]
        public void UsesTheFastestModeWhenTheDesktopRateIsUnknown()
        {
            var list = DisplayModes.RefreshRates(Monitor, null, 0);

            Assert.Equal(144, list.Last().Rate);
            Assert.DoesNotContain(list, r => r.Display);
        }

        [Theory]
        // 1920 x 1080 desktop at 100%: work area about 1920 x 1040 with the taskbar.
        [InlineData(1600, 900, 1920, 1040, true)]
        [InlineData(1920, 1080, 1920, 1040, false)]
        // Same monitor at 150%: the work area is reported as 1280 x 693.
        [InlineData(1024, 768, 1280, 693, false)]
        [InlineData(1024, 600, 1280, 693, true)]
        // Unknown work area: do not warn.
        [InlineData(3840, 2160, 0, 0, true)]
        public void ChecksWhetherAWindowFitsTheWorkArea(int width, int height, int workWidth, int workHeight, bool fits)
        {
            Assert.Equal(fits, DisplayModes.WindowFits(width, height, workWidth, workHeight));
        }

        [Theory]
        // Already fits: unchanged.
        [InlineData(1280, 720, 1920, 1040, 1280, 720)]
        // Desktop-sized full screen on a 1080p screen: shrinks to fit 1904 x 1000, keeping 16:9.
        [InlineData(1920, 1080, 1920, 1040, 1777, 1000)]
        // 1080p at 150%: the work area is 1280 x 693.
        [InlineData(1920, 1080, 1280, 693, 1160, 653)]
        // Unknown work area: unchanged.
        [InlineData(2560, 1440, 0, 0, 2560, 1440)]
        public void ShrinksAWindowToFitKeepingItsShape(int width, int height, int workWidth, int workHeight, int fitWidth, int fitHeight)
        {
            Assert.Equal((fitWidth, fitHeight), DisplayModes.FitWindow(width, height, workWidth, workHeight));
        }

        [Theory]
        [InlineData(1280, 720, 0, 0, 1920, 1040, 312, 140)]
        // A second monitor or a taskbar on the left moves the work area's origin.
        [InlineData(1280, 720, 100, 0, 1920, 1040, 412, 140)]
        // Larger than the work area: pinned to its top left so the title bar can still be grabbed.
        [InlineData(2560, 1440, 0, 0, 1920, 1040, 0, 0)]
        public void CentersAWindowInTheWorkArea(int width, int height, int workX, int workY, int workWidth, int workHeight, int x, int y)
        {
            Assert.Equal((x, y), DisplayModes.CenterWindow(width, height, workX, workY, workWidth, workHeight));
        }

        [Fact]
        public void DoesNotPlaceAWindowOnAnUnknownScreen()
        {
            Assert.Null(DisplayModes.CenterWindow(1280, 720, 0, 0, 0, 0));
        }

        [Theory]
        [InlineData(1920, 1080, 2560, 1440, false)]
        [InlineData(1366, 768, 1920, 1080, false)]
        [InlineData(1024, 768, 1920, 1080, true)]
        [InlineData(1920, 1080, 1920, 1200, true)]
        [InlineData(1024, 768, 0, 0, false)]
        public void SpotsSizesFullScreenWillStretch(int width, int height, int desktopWidth, int desktopHeight, bool stretches)
        {
            Assert.Equal(stretches, DisplayModes.StretchesInFullScreen(width, height, desktopWidth, desktopHeight));
        }

        [Theory]
        [InlineData(1920, 1920, 100)]
        [InlineData(1920, 1536, 125)]
        [InlineData(2560, 1707, 150)]
        [InlineData(3840, 1920, 200)]
        [InlineData(0, 1920, 100)]
        public void WorksOutTheDisplayScaling(int physical, int logical, int percent)
        {
            Assert.Equal(percent, DisplayModes.ScalingPercent(physical, logical));
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
