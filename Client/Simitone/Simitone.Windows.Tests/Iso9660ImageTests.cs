using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Xunit;

namespace Simitone.Windows.Tests
{
    public class Iso9660ImageTests : IDisposable
    {
        readonly string _work = Path.Combine(Path.GetTempPath(), "simitone-iso-" + Guid.NewGuid().ToString("N"));

        public Iso9660ImageTests()
        {
            Directory.CreateDirectory(_work);
        }

        public void Dispose()
        {
            try { Directory.Delete(_work, true); } catch (IOException) { }
        }

        static byte[] Pattern(int length, int seed)
        {
            var data = new byte[length];
            for (int i = 0; i < data.Length; i++) data[i] = (byte)((i * 31 + seed) % 251);
            return data;
        }

        static readonly byte[] Behavior = Encoding.ASCII.GetBytes(string.Concat(Enumerable.Repeat("IFF FILE 2.5 behavior ", 120)));
        static readonly byte[] Large = Pattern(600_000, 7);

        static TestImage InstalledGame()
        {
            var image = new TestImage();
            image.Root.Children.Add(TestImage.Dir("THESIMS",
                TestImage.File("SIMS.EXE;1", Pattern(5000, 1)),
                TestImage.Dir("GAMEDATA",
                    TestImage.File("BEHAVIOR.IFF;1", Behavior),
                    TestImage.File("LARGE.FAR;1", Large)),
                TestImage.Dir("USERDATA",
                    TestImage.File("NEIGHBOR.IFF;1", Pattern(3000, 2)))));
            image.Root.Children.Add(TestImage.File("AUTORUN.INF;1", Pattern(40, 3)));
            return image;
        }

        string Write(string name, byte[] data)
        {
            var path = Path.Combine(_work, name);
            File.WriteAllBytes(path, data);
            return path;
        }

        string Extract(string imagePath, List<IsoExtractProgress> progress = null, CancellationToken token = default)
        {
            var dest = Path.Combine(_work, "out-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dest);
            var reporter = progress == null ? null : new ListProgress(progress);
            Iso9660Image.ExtractSimsInstall(imagePath, dest, reporter, token);
            return dest;
        }

        static void AssertFile(string path, byte[] expected)
        {
            Assert.True(File.Exists(path), path + " was not extracted.");
            Assert.Equal(expected, File.ReadAllBytes(path));
        }

        [Theory]
        [InlineData("iso")]
        [InlineData("mode1")]
        [InlineData("mode2")]
        public void ExtractsTheGameFolderFromEachLayout(string layout)
        {
            var image = InstalledGame();
            var bytes = layout == "iso" ? image.BuildIso() : image.BuildRaw(layout == "mode1" ? 16 : 24);
            var dest = Extract(Write("game." + layout, bytes));

            // Only the folder that holds GameData is copied, not the rest of the disc.
            AssertFile(Path.Combine(dest, "GAMEDATA", "BEHAVIOR.IFF"), Behavior);
            AssertFile(Path.Combine(dest, "GAMEDATA", "LARGE.FAR"), Large);
            AssertFile(Path.Combine(dest, "SIMS.EXE"), Pattern(5000, 1));
            Assert.True(Directory.Exists(Path.Combine(dest, "USERDATA")));
            Assert.False(File.Exists(Path.Combine(dest, "AUTORUN.INF")));
        }

        [Fact]
        public void SkipsTheExtendedAttributeRecordWithoutShorteningTheFile()
        {
            var image = new TestImage();
            var data = Pattern(9000, 5);
            image.Root.Children.Add(TestImage.Dir("GAMEDATA",
                TestImage.File("BEHAVIOR.IFF;1", Behavior),
                TestImage.File("XA.DAT;1", data, xaBlocks: 2)));
            var dest = Extract(Write("xa.iso", image.BuildIso()));

            AssertFile(Path.Combine(dest, "GAMEDATA", "XA.DAT"), data);
        }

        [Fact]
        public void UsesJolietNames()
        {
            var image = new TestImage { Joliet = true };
            image.Root.Children.Add(TestImage.Dir("GAMEDATA",
                new TestImage.Entry { Name = "BEHAVIOR.IFF;1", LongName = "Behavior.iff", Data = Behavior },
                new TestImage.Entry { Name = "LONGFI~1.IFF;1", LongName = "Long File Name.iff", Data = Pattern(100, 4) }));
            image.Root.Children[0].LongName = "GameData";
            var dest = Extract(Write("joliet.iso", image.BuildIso()));

            var names = Directory.GetFiles(Path.Combine(dest, "GameData")).Select(Path.GetFileName).OrderBy(x => x).ToArray();
            Assert.Equal(new[] { "Behavior.iff", "Long File Name.iff" }, names);
        }

        [Fact]
        public void UsesRockRidgeNames()
        {
            var image = new TestImage { RockRidge = true };
            image.Root.Children.Add(TestImage.Dir("GAMEDATA",
                new TestImage.Entry { Name = "BEHAVIOR.IFF;1", LongName = "Behavior.iff", Data = Behavior }));
            image.Root.Children[0].LongName = "GameData";
            var dest = Extract(Write("rr.iso", image.BuildIso()));

            AssertFile(Path.Combine(dest, "GameData", "Behavior.iff"), Behavior);
        }

        [Fact]
        public void ReportsASetupDiscInsteadOfExtractingIt()
        {
            var image = new TestImage();
            image.Root.Children.Add(TestImage.File("SETUP.EXE;1", Pattern(100, 6)));
            image.Root.Children.Add(TestImage.Dir("DATA", TestImage.File("DATA1.CAB;1", Pattern(100, 7))));

            var ex = Assert.Throws<InvalidDataException>(() => Extract(Write("setup.iso", image.BuildIso())));
            Assert.Contains("GameData", ex.Message);
            Assert.Contains("SETUP.EXE", ex.Message);
        }

        [Fact]
        public void RejectsAFileThatIsNotADiscImage()
        {
            var ex = Assert.Throws<InvalidDataException>(() => Extract(Write("notes.iso", Pattern(100_000, 8))));
            Assert.Contains("not an ISO 9660", ex.Message);
        }

        [Fact]
        public void RejectsMultiExtentFilesInsteadOfCopyingThemShort()
        {
            var image = new TestImage();
            image.Root.Children.Add(TestImage.Dir("GAMEDATA",
                TestImage.File("BEHAVIOR.IFF;1", Behavior),
                new TestImage.Entry { Name = "SPLIT.FAR;1", Data = Pattern(4000, 9), Flags = 0x80 }));

            var ex = Assert.Throws<InvalidDataException>(() => Extract(Write("split.iso", image.BuildIso())));
            Assert.Contains("multiple extents", ex.Message);
        }

        [Fact]
        public void ReplacesCharactersWindowsCannotStore()
        {
            var image = new TestImage { RockRidge = true };
            image.Root.Children.Add(TestImage.Dir("GAMEDATA",
                TestImage.File("BEHAVIOR.IFF;1", Behavior),
                new TestImage.Entry { Name = "ODD.TXT;1", LongName = "a:b?.txt", Data = Pattern(10, 10) },
                new TestImage.Entry { Name = "UP.TXT;1", LongName = "..", Data = Pattern(10, 11) }));
            var dest = Extract(Write("odd.iso", image.BuildIso()));

            AssertFile(Path.Combine(dest, "GAMEDATA", "a_b_.txt"), Pattern(10, 10));
            // A ".." name is dropped rather than written outside the destination.
            Assert.Equal(2, Directory.GetFiles(Path.Combine(dest, "GAMEDATA")).Length);
        }

        [Fact]
        public void ReportsProgressUpToTheTotal()
        {
            var progress = new List<IsoExtractProgress>();
            Extract(Write("progress.iso", InstalledGame().BuildIso()), progress);

            Assert.NotEmpty(progress);
            var last = progress[progress.Count - 1];
            Assert.Equal(last.BytesTotal, last.BytesCopied);
            Assert.Equal("", last.CurrentFile);
            Assert.True(progress.Zip(progress.Skip(1), (a, b) => b.BytesCopied >= a.BytesCopied).All(x => x));
        }

        [Fact]
        public void StopsWhenCancelled()
        {
            var cancel = new CancellationTokenSource();
            cancel.Cancel();
            Assert.ThrowsAny<OperationCanceledException>(() =>
                Extract(Write("cancel.iso", InstalledGame().BuildIso()), token: cancel.Token));
        }

        sealed class ListProgress : IProgress<IsoExtractProgress>
        {
            readonly List<IsoExtractProgress> _list;
            public ListProgress(List<IsoExtractProgress> list) { _list = list; }
            public void Report(IsoExtractProgress value) { _list.Add(value); }
        }
    }
}
