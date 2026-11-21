using System;
using System.IO;
using Xunit;

namespace Simitone.Windows.Tests
{
    public class Sims1InstallTests : IDisposable
    {
        readonly string _work = Path.Combine(Path.GetTempPath(), "simitone-install-" + Guid.NewGuid().ToString("N"));

        public Sims1InstallTests()
        {
            Directory.CreateDirectory(_work);
        }

        public void Dispose()
        {
            try { Directory.Delete(_work, true); } catch (IOException) { }
        }

        string MakeInstall(params string[] parts)
        {
            var root = Path.Combine(_work, Path.Combine(parts));
            Directory.CreateDirectory(Path.Combine(root, "GameData"));
            File.WriteAllText(Path.Combine(root, "GameData", "Behavior.iff"), "IFF");
            return root;
        }

        [Fact]
        public void FindsTheInstallFolderItself()
        {
            var install = MakeInstall("The Sims");
            Assert.Equal(install, Sims1Install.FindNear(install));
        }

        [Fact]
        public void FindsTheInstallWhenGameDataIsPicked()
        {
            var install = MakeInstall("The Sims");
            Assert.Equal(install, Sims1Install.FindNear(Path.Combine(install, "GameData")));
        }

        [Fact]
        public void FindsTheInstallOneFolderDown()
        {
            var install = MakeInstall("Maxis", "The Sims");
            Directory.CreateDirectory(Path.Combine(_work, "Maxis", "Other"));
            Assert.Equal(install, Sims1Install.FindNear(Path.Combine(_work, "Maxis")));
        }

        [Fact]
        public void LeavesTwoInstallsForThePlayerToChoose()
        {
            MakeInstall("Games", "The Sims");
            MakeInstall("Games", "The Sims Copy");
            Assert.Null(Sims1Install.FindNear(Path.Combine(_work, "Games")));
        }

        [Fact]
        public void ReturnsNullWhenThereIsNoInstall()
        {
            Directory.CreateDirectory(Path.Combine(_work, "Empty"));
            Assert.Null(Sims1Install.FindNear(Path.Combine(_work, "Empty")));
            Assert.Null(Sims1Install.FindNear(Path.Combine(_work, "Missing")));
            Assert.Null(Sims1Install.FindNear(""));
        }

        [Theory]
        [InlineData(@"C:\Games\The Sims", "C:/Games/The Sims/")]
        [InlineData("\"C:\\Games\\The Sims\\\"", "C:/Games/The Sims/")]
        [InlineData("  /home/me/sims/  ", "/home/me/sims/")]
        [InlineData("", "")]
        public void NormalizesToForwardSlashesWithATrailingSlash(string input, string expected)
        {
            Assert.Equal(expected, Sims1Install.Normalize(input));
        }
    }
}
