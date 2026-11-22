using System;

namespace Simitone.Client.Utils
{
    /// <summary>
    /// Fits a game window to the screen. The client cannot see the screen's work area itself,
    /// so the platform project sets <see cref="Fit"/> at startup. Without one, sizes pass through.
    /// </summary>
    public static class WindowFit
    {
        /// <summary>
        /// Takes a window's client size and returns the size to use so the window, with its frame,
        /// fits the screen.
        /// </summary>
        public static Func<int, int, (int Width, int Height)> Fit = (width, height) => (width, height);

        /// <summary>
        /// Takes a window's client size and returns where its top left corner goes to sit in the
        /// middle of the screen, or null when the screen is unknown.
        /// </summary>
        public static Func<int, int, (int X, int Y)?> Center = (width, height) => null;

        public static (int X, int Y)? CenterOf(int width, int height)
        {
            try
            {
                return Center(width, height);
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static (int Width, int Height) Apply(int width, int height)
        {
            try
            {
                var fitted = Fit(width, height);
                if (fitted.Width < 1 || fitted.Height < 1) return (width, height);
                return fitted;
            }
            catch (Exception)
            {
                return (width, height);
            }
        }
    }
}
