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

        public static string Normalize(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return "";
            path = path.Trim().Trim('"').Replace('\\', '/');
            if (!path.EndsWith("/")) path += "/";
            return path;
        }
    }
}
