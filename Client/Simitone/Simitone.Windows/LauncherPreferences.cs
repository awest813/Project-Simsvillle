using System.IO;

namespace Simitone.Windows
{
    /// <summary>
    /// Renderer choices that GlobalSettings does not store. The rest of the launcher
    /// writes through GlobalSettings, which the game already reads at startup.
    /// </summary>
    public class LauncherPreferences
    {
        public bool DirectX { get; set; } = true;
        public bool Enable3D { get; set; }

        public static LauncherPreferences Load(string userDir)
        {
            var prefs = new LauncherPreferences();
            var path = Path.Combine(userDir, "launcher.ini");
            if (!File.Exists(path)) return prefs;

            foreach (var line in File.ReadAllLines(path))
            {
                var clean = line.Trim();
                var split = clean.IndexOf('=');
                if (split <= 0) continue;
                var key = clean.Substring(0, split).Trim();
                var value = clean.Substring(split + 1).Trim();
                if (key == "DirectX" && bool.TryParse(value, out var dx)) prefs.DirectX = dx;
                if (key == "Enable3D" && bool.TryParse(value, out var enable3d)) prefs.Enable3D = enable3d;
            }
            return prefs;
        }

        public void Save(string userDir)
        {
            Directory.CreateDirectory(userDir);
            File.WriteAllLines(Path.Combine(userDir, "launcher.ini"), new[]
            {
                "DirectX=" + DirectX,
                "Enable3D=" + Enable3D
            });
        }
    }
}
