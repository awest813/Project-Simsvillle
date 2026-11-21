using System.IO;

namespace Simitone.Windows
{
    public static class Sims1Install
    {
        public static bool IsValid(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return false;
            return File.Exists(Path.Combine(path, "GameData", "Behavior.iff"));
        }

        /// <summary>
        /// Finds the install from a folder the player picked: the folder itself, the parent when
        /// GameData was picked, or one subfolder down when a folder above the install was picked.
        /// Returns null when none of them holds GameData\Behavior.iff.
        /// </summary>
        public static string FindNear(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;
            try
            {
                var full = Path.GetFullPath(path.Trim().Trim('"'));
                if (IsValid(full)) return full;

                var parent = Path.GetDirectoryName(full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                if (!string.IsNullOrEmpty(parent) && IsValid(parent)) return parent;

                if (!Directory.Exists(full)) return null;
                string found = null;
                foreach (var child in Directory.EnumerateDirectories(full))
                {
                    if (!IsValid(child)) continue;
                    // Two installs side by side is a choice for the player, not for us.
                    if (found != null) return null;
                    found = child;
                }
                return found;
            }
            catch (System.Exception)
            {
                return null;
            }
        }

        public static string Normalize(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return "";
            path = path.Trim().Trim('"').Replace('\\', '/');
            if (!path.EndsWith("/")) path += "/";
            return path;
        }
    }
}
