using FSO.Client;
using FSO.Common;
using FSO.LotView;
using Simitone.Client;
using Simitone.Windows.GameLocator;
using Simitone.Windows.Utils;
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace Simitone.Windows
{
#if WINDOWS || LINUX
    /// <summary>
    /// The main class.
    /// </summary>
    public static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            Directory.SetCurrentDirectory(baseDir);
            AppDomain.CurrentDomain.AssemblyResolve += OnAssemblyResolve;

            OperatingSystem os = Environment.OSVersion;
            PlatformID pid = os.Platform;

            ILocator gameLocator;
            bool linux = pid == PlatformID.MacOSX || pid == PlatformID.Unix;
            if (linux && Directory.Exists("/Users"))
                gameLocator = new MacOSLocator();
            else if (linux)
                gameLocator = new LinuxLocator();
            else
                gameLocator = new WindowsLocator();

            FSOEnvironment.Enable3D = false;
            bool ide = false;
            bool aa = false;
            bool jit = false;
            bool nolauncher = false;
            bool? directXArg = null;
            bool enable3dArg = false;
            byte? langArg = null;
            int? hzArg = null;
            string pathArg = null;
            #region User resolution parmeters

            FSOEnvironment.Args = string.Join(" ", args);

            foreach (var arg in args)
            {
                if (arg.Length == 0 || arg[0] != '-') continue;
                var cmd = arg.Substring(1);
                if (cmd.StartsWith("lang"))
                {
                    langArg = byte.Parse(cmd.Substring(4));
                }
                else if (cmd.StartsWith("hz")) hzArg = int.Parse(cmd.Substring(2));
                else
                {
                    //normal style param
                    switch (cmd)
                    {
                        case "ide":
                            ide = true;
                            break;
                        case "3d":
                            enable3dArg = true;
                            break;
                        case "aa":
                            aa = true;
                            break;
                        case "jit":
                            jit = true;
                            break;
                        case "dx":
                        case "dx11":
                            directXArg = true;
                            break;
                        case "gl":
                        case "ogl":
                            directXArg = false;
                            break;
                        case "touch":
                            FSOEnvironment.SoftwareKeyboard = true;
                            break;
                        case "nosound":
                            FSOEnvironment.NoSound = true;
                            break;
                        case "nolauncher":
                            nolauncher = true;
                            break;
                        case string s when s.StartsWith("path"): //The Sims path
                            pathArg = s.Length > 4 ? s.Substring(4) : null;
                            break;
                    }
                }
            }
            #endregion

            // Settings are stored under Documents/Simitone. Set that before the first GlobalSettings read,
            // or config.ini is created in the working directory.
            FSOEnvironment.UserDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Simitone/").Replace('\\', '/');
            Directory.CreateDirectory(FSOEnvironment.UserDir);

            var prefs = LauncherPreferences.Load(FSOEnvironment.UserDir);
            var settings = GlobalSettings.Default;
            var path = FirstValidSimsPath(pathArg, settings.StartupPath, gameLocator.FindTheSims1());
            var useDX = directXArg ?? prefs.DirectX;
            if (linux) useDX = false;

            var choice = new LauncherChoice
            {
                SimsPath = path,
                LanguageCode = langArg ?? (settings.LanguageCode == 0 ? (byte)1 : settings.LanguageCode),
                Width = settings.GraphicsWidth > 0 ? settings.GraphicsWidth : 1024,
                Height = settings.GraphicsHeight > 0 ? settings.GraphicsHeight : 768,
                Windowed = settings.Windowed,
                Enable3D = enable3dArg || prefs.Enable3D,
                AntiAlias = aa || settings.AntiAlias > 0,
                DirectX = useDX
            };

            if (!nolauncher || !Sims1Install.IsValid(choice.SimsPath))
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                using (var launcher = new LauncherForm(choice, linux))
                {
                    if (launcher.ShowDialog() != DialogResult.OK) return;
                    choice = launcher.Choice;
                }
            }

            if (!Sims1Install.IsValid(choice.SimsPath)) return;

            useDX = choice.DirectX && !linux;
            FSOEnvironment.Enable3D = choice.Enable3D;
            prefs.DirectX = useDX;
            prefs.Enable3D = choice.Enable3D;
            prefs.Save(FSOEnvironment.UserDir);

            useDX = MonogameLinker.Link(useDX);

            FSO.Files.ImageLoaderHelpers.BitmapFunction = BitmapReader;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            FSOEnvironment.SoftwareDepth = false;
            FSOEnvironment.UseMRT = true;

            FSOEnvironment.ContentDir = "Content/";
            FSOEnvironment.GFXContentDir = "Content/" + (useDX ? "DX/" : "OGL/");
            FSOEnvironment.Linux = false;
            FSOEnvironment.DirectX = useDX;
            FSOEnvironment.GameThread = Thread.CurrentThread;

            settings.LanguageCode = choice.LanguageCode == 0 ? (byte)1 : choice.LanguageCode;
            FSO.Files.Formats.IFF.Chunks.STR.DefaultLangCode = (FSO.Files.Formats.IFF.Chunks.STRLangCode)settings.LanguageCode;

            settings.StartupPath = choice.SimsPath;
            settings.TS1HybridEnable = true;
            settings.TS1HybridPath = choice.SimsPath;
            settings.ClientVersion = "0";
            settings.LightingMode = 3;
            settings.AntiAlias = choice.AntiAlias ? 1 : 0;
            settings.ComplexShaders = true;
            settings.EnableTransitions = true;
            settings.Windowed = choice.Windowed;
            settings.GraphicsWidth = Math.Max(1, choice.Width);
            settings.GraphicsHeight = Math.Max(1, choice.Height);
            if (hzArg != null) settings.TargetRefreshRate = hzArg.Value;
            settings.Save();

            if (ide) new FSO.IDE.VolcanicStartProxy().InitVolcanic(args);

            var assemblies = new FSO.SimAntics.JIT.Runtime.AssemblyStore();
            //var globals = new TS1.Scripts.Dummy(); //make sure scripts assembly is loaded
            if (jit) assemblies.InitAOT();
            FSO.SimAntics.Engine.VMTranslator.INSTANCE = new FSO.SimAntics.JIT.Runtime.VMAOTTranslator(assemblies);

            var start = new GameStartProxy();
            start.Start(useDX);
        }

        private static string FirstValidSimsPath(string pathArg, string savedPath, string locatedPath)
        {
            var fromArg = Sims1Install.Normalize(pathArg);
            if (!string.IsNullOrEmpty(fromArg)) return fromArg;
            var saved = Sims1Install.Normalize(savedPath);
            if (Sims1Install.IsValid(saved)) return saved;
            var located = Sims1Install.Normalize(locatedPath);
            if (Sims1Install.IsValid(located)) return located;
            if (!string.IsNullOrEmpty(fromArg)) return fromArg;
            if (!string.IsNullOrEmpty(saved)) return saved;
            return located;
        }

        private static System.Reflection.Assembly OnAssemblyResolve(object sender, ResolveEventArgs args)
        {
            try
            {
                var name = args.Name;
                if (name.StartsWith("FSO.Scripts"))
                {
                    return AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(x => x.FullName == name);
                }
                else
                {
                    var assemblyPath = Path.Combine(MonogameLinker.AssemblyDir, args.Name.Substring(0, name.IndexOf(',')) + ".dll");
                    var assembly = Assembly.LoadFrom(assemblyPath);
                    return assembly;
                }
            }
            catch (Exception e)
            {
                return null;
            }
        }

        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var exception = e.ExceptionObject;
            if (exception is OutOfMemoryException)
            {
                MessageBox.Show(e.ExceptionObject.ToString(), "Out of Memory! FreeSO needs to close.");
            }
            else
            {
                MessageBox.Show(e.ExceptionObject.ToString(), "A fatal error occured! Screenshot this dialog and post it on Discord.");
            }
        }

        public static Tuple<byte[], int, int> BitmapReader(Stream str)
        {
            Bitmap image = (Bitmap)Bitmap.FromStream(str);
            try
            {
                // Fix up the Image to match the expected format
                image = (Bitmap)image.RGBToBGR();

                var width = image.Width;
                var height = image.Height;
                var data = new byte[width * height * 4];

                BitmapData bitmapData = image.LockBits(new System.Drawing.Rectangle(0, 0, width, height),
                    ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                var rowBytes = width * 4;
                if (bitmapData.Stride == rowBytes)
                    Marshal.Copy(bitmapData.Scan0, data, 0, data.Length);
                else
                {
                    for (int y = 0; y < height; y++)
                        Marshal.Copy(bitmapData.Scan0 + y * bitmapData.Stride, data, y * rowBytes, rowBytes);
                }
                image.UnlockBits(bitmapData);

                return new Tuple<byte[], int, int>(data, image.Width, image.Height);
            }
            finally
            {
                image.Dispose();
            }
        }

        // RGB to BGR convert Matrix
        private static float[][] rgbtobgr = new float[][]
          {
             new float[] {0, 0, 1, 0, 0},
             new float[] {0, 1, 0, 0, 0},
             new float[] {1, 0, 0, 0, 0},
             new float[] {0, 0, 0, 1, 0},
             new float[] {0, 0, 0, 0, 1}
          };


        internal static Image RGBToBGR(this Image bmp)
        {
            Image newBmp;
            if ((bmp.PixelFormat & System.Drawing.Imaging.PixelFormat.Indexed) != 0)
            {
                newBmp = new Bitmap(bmp.Width, bmp.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            }
            else
            {
                // Need to clone so the call to Clear() below doesn't clear the source before trying to draw it to the target.
                newBmp = (Image)bmp.Clone();
            }

            try
            {
                System.Drawing.Imaging.ImageAttributes ia = new System.Drawing.Imaging.ImageAttributes();
                System.Drawing.Imaging.ColorMatrix cm = new System.Drawing.Imaging.ColorMatrix(rgbtobgr);

                ia.SetColorMatrix(cm);
                using (System.Drawing.Graphics g = System.Drawing.Graphics.FromImage(newBmp))
                {
                    g.Clear(Color.Transparent);
                    g.DrawImage(bmp, new System.Drawing.Rectangle(0, 0, bmp.Width, bmp.Height), 0, 0, bmp.Width, bmp.Height, System.Drawing.GraphicsUnit.Pixel, ia);
                }
            }
            finally
            {
                if (newBmp != bmp)
                {
                    bmp.Dispose();
                }
            }

            return newBmp;
        }
    }
#endif
}
