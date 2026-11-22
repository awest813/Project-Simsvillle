using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace Simitone.Windows
{
    public class LauncherChoice
    {
        public string SimsPath;
        public byte LanguageCode;
        public int Width;
        public int Height;
        public int RefreshRate;
        public bool Windowed;
        public bool Enable3D;
        public bool AntiAlias;
        public bool DirectX;
    }

    public class LauncherForm : Form
    {
        public LauncherChoice Choice { get; private set; }

        readonly TextBox _path;
        readonly Label _status;
        readonly ComboBox _resolution;
        readonly ComboBox _refresh;
        readonly ComboBox _language;
        readonly CheckBox _windowed;
        readonly CheckBox _enable3d;
        readonly CheckBox _antialias;
        readonly RadioButton _directX;
        readonly RadioButton _openGL;
        readonly Button _play;
        readonly Label _displayHint;
        DisplayMode? _desktop;

        // The folder Play will use. It can differ from the text box when the player
        // typed the folder above the install, or the GameData folder inside it.
        string _resolvedPath;

        static readonly string[] ImageExtensions = { ".iso", ".bin", ".img" };

        static readonly (byte code, string name)[] Languages =
        {
            (1, "English (US)"),
            (2, "English (UK)"),
            (3, "French"),
            (4, "German"),
            (5, "Italian"),
            (6, "Spanish"),
            (7, "Dutch"),
            (8, "Danish"),
            (9, "Swedish"),
            (10, "Norwegian"),
            (11, "Finnish"),
            (12, "Hebrew"),
            (13, "Russian"),
            (14, "Portuguese"),
            (15, "Japanese"),
            (16, "Polish"),
            (17, "Simplified Chinese"),
            (18, "Traditional Chinese"),
            (19, "Thai"),
            (20, "Korean")
        };

        public LauncherForm(LauncherChoice initial, bool linux)
        {
            Choice = initial;
            Text = "Simitone";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(560, 526);
            Font = new Font("Segoe UI", 9f);
            AutoScaleMode = AutoScaleMode.Font;
            AllowDrop = true;
            DragEnter += OnDragEnter;
            DragDrop += OnDragDrop;
            try
            {
                Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            }
            catch (Exception)
            {
            }

            var title = new Label
            {
                Text = "Simitone",
                Font = new Font("Segoe UI", 18f, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(16, 12)
            };
            var subtitle = new Label
            {
                Text = "Choose an installed The Sims folder, or extract a disc image of that install. You can also drop either one on this window.",
                AutoSize = false,
                Size = new Size(528, 36),
                Location = new Point(16, 48)
            };

            var pathLabel = new Label
            {
                Text = "The Sims &folder",
                AutoSize = true,
                Location = new Point(16, 96)
            };
            _path = new TextBox
            {
                Location = new Point(16, 118),
                Size = new Size(338, 24),
                Text = initial.SimsPath ?? ""
            };
            var browse = new Button
            {
                Text = "&Browse...",
                Location = new Point(362, 116),
                Size = new Size(88, 28)
            };
            browse.Click += Browse;
            var iso = new Button
            {
                Text = "&ISO...",
                Location = new Point(456, 116),
                Size = new Size(88, 28)
            };
            iso.Click += ChooseImage;
            _path.TextChanged += (sender, args) => RefreshStatus();
            _status = new Label
            {
                AutoSize = false,
                Location = new Point(16, 152),
                Size = new Size(528, 40)
            };

            var display = new GroupBox
            {
                Text = "Display",
                Location = new Point(16, 198),
                Size = new Size(528, 176)
            };
            var resolutionLabel = new Label
            {
                Text = "&Resolution",
                AutoSize = true,
                Location = new Point(12, 28)
            };
            _resolution = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(90, 24),
                Size = new Size(170, 24)
            };
            var refreshLabel = new Label
            {
                Text = "Frame ra&te",
                AutoSize = true,
                Location = new Point(282, 28)
            };
            _refresh = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(360, 24),
                Size = new Size(150, 24)
            };
            FillDisplayLists(initial);
            _windowed = new CheckBox
            {
                Text = "&Windowed",
                AutoSize = true,
                Location = new Point(12, 64),
                Checked = initial.Windowed
            };
            _enable3d = new CheckBox
            {
                Text = "&3D lot view",
                AutoSize = true,
                Location = new Point(150, 64),
                Checked = initial.Enable3D
            };
            _antialias = new CheckBox
            {
                Text = "&Anti-aliasing",
                AutoSize = true,
                Location = new Point(282, 64),
                Checked = initial.AntiAlias
            };
            _directX = new RadioButton
            {
                Text = "&DirectX",
                AutoSize = true,
                Location = new Point(12, 102),
                Checked = initial.DirectX && !linux,
                Enabled = !linux
            };
            _openGL = new RadioButton
            {
                Text = "&OpenGL",
                AutoSize = true,
                Location = new Point(150, 102),
                Checked = !initial.DirectX || linux,
                Enabled = !linux
            };
            display.Controls.Add(resolutionLabel);
            display.Controls.Add(_resolution);
            display.Controls.Add(refreshLabel);
            display.Controls.Add(_refresh);
            display.Controls.Add(_windowed);
            display.Controls.Add(_enable3d);
            display.Controls.Add(_antialias);
            display.Controls.Add(_directX);
            display.Controls.Add(_openGL);
            _displayHint = new Label
            {
                AutoSize = false,
                Location = new Point(12, 134),
                Size = new Size(504, 34),
                ForeColor = Color.FromArgb(160, 90, 0)
            };
            display.Controls.Add(_displayHint);
            _resolution.SelectedIndexChanged += (sender, args) => RefreshDisplayHint();
            _windowed.CheckedChanged += (sender, args) => RefreshDisplayHint();
            display.Controls.Add(new Label
            {
                Text = linux ? "This system uses OpenGL." : "F12 in a lot switches 3D and hybrid.",
                AutoSize = true,
                ForeColor = SystemColors.GrayText,
                Location = new Point(282, 104)
            });

            var languageBox = new GroupBox
            {
                Text = "Language",
                Location = new Point(16, 384),
                Size = new Size(528, 68)
            };
            _language = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(12, 26),
                Size = new Size(220, 24)
            };
            var langIndex = 0;
            for (int i = 0; i < Languages.Length; i++)
            {
                _language.Items.Add(Languages[i].name);
                if (Languages[i].code == initial.LanguageCode) langIndex = i;
            }
            _language.SelectedIndex = langIndex;
            languageBox.Controls.Add(_language);
            languageBox.Controls.Add(new Label
            {
                Text = "Applies when you press Play.",
                AutoSize = true,
                ForeColor = SystemColors.GrayText,
                Location = new Point(248, 30)
            });

            var userFolder = new LinkLabel
            {
                Text = "Open the Simitone folder",
                AutoSize = true,
                Location = new Point(16, 478)
            };
            userFolder.LinkClicked += (sender, args) => OpenUserFolder();

            _play = new Button
            {
                Text = "&Play",
                Location = new Point(348, 470),
                Size = new Size(100, 32)
            };
            _play.Click += Play;
            var quit = new Button
            {
                Text = "Quit",
                Location = new Point(456, 470),
                Size = new Size(88, 32),
                DialogResult = DialogResult.Cancel
            };
            AcceptButton = _play;
            CancelButton = quit;

            // Added in tab order.
            Controls.Add(title);
            Controls.Add(subtitle);
            Controls.Add(pathLabel);
            Controls.Add(_path);
            Controls.Add(browse);
            Controls.Add(iso);
            Controls.Add(_status);
            Controls.Add(display);
            Controls.Add(languageBox);
            Controls.Add(_play);
            Controls.Add(quit);
            Controls.Add(userFolder);
            RefreshStatus();
            RefreshDisplayHint();
        }

        static string UserFolder =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Simitone");

        void FillDisplayLists(LauncherChoice initial)
        {
            var modes = DisplayModes.Query(out var desktop);
            _desktop = desktop;

            var resolutions = DisplayModes.Resolutions(modes, desktop, initial.Width, initial.Height);
            foreach (var option in resolutions) _resolution.Items.Add(option);
            var selected = resolutions.FindIndex(r => r.Width == initial.Width && r.Height == initial.Height);
            if (selected < 0) selected = resolutions.FindIndex(r => r.Desktop);
            _resolution.SelectedIndex = Math.Max(0, selected);

            var rates = DisplayModes.RefreshRates(modes, desktop, initial.RefreshRate);
            foreach (var option in rates) _refresh.Items.Add(option);
            var rate = rates.FindIndex(r => r.Rate == initial.RefreshRate);
            if (rate < 0) rate = rates.FindIndex(r => r.Rate == 60);
            _refresh.SelectedIndex = Math.Max(0, rate);
        }

        void RefreshDisplayHint()
        {
            var text = "";
            if (_windowed.Checked && _resolution.SelectedItem is ResolutionOption size)
            {
                var screen = Screen.PrimaryScreen;
                var work = screen?.WorkingArea ?? Rectangle.Empty;
                if (!DisplayModes.WindowFits(size.Width, size.Height, work.Width, work.Height))
                {
                    var scaling = DisplayModes.ScalingPercent(_desktop?.Width ?? 0, screen?.Bounds.Width ?? 0);
                    text = "A " + size.Width + " x " + size.Height + " window is larger than this screen" +
                        (scaling > 100 ? " at " + scaling + "% display scaling" : "") +
                        ". Choose a smaller size, or turn off Windowed.";
                }
            }
            _displayHint.Text = text;
        }

        void OnDragEnter(object sender, DragEventArgs e)
        {
            // Nothing is accepted while an extract is running.
            var dropped = Enabled ? DroppedPath(e) : null;
            e.Effect = dropped != null && (Directory.Exists(dropped) || IsImage(dropped))
                ? DragDropEffects.Copy
                : DragDropEffects.None;
        }

        void OnDragDrop(object sender, DragEventArgs e)
        {
            var dropped = Enabled ? DroppedPath(e) : null;
            if (dropped == null) return;
            if (Directory.Exists(dropped))
                _path.Text = Sims1Install.FindNear(dropped) ?? dropped;
            else if (IsImage(dropped))
                // Let the drop finish before the dialogs open, or Explorer waits on us.
                BeginInvoke(new Action(() => ExtractImage(dropped)));
        }

        static string DroppedPath(DragEventArgs e)
        {
            if (!(e.Data?.GetData(DataFormats.FileDrop) is string[] files) || files.Length != 1) return null;
            return files[0];
        }

        static bool IsImage(string path)
        {
            return File.Exists(path) && ImageExtensions.Contains(Path.GetExtension(path).ToLowerInvariant());
        }

        void ChooseImage(object sender, EventArgs e)
        {
            using (var dialog = new OpenFileDialog())
            {
                dialog.Title = "Choose a The Sims disc image";
                dialog.Filter = "Disc image (*.iso;*.bin;*.img)|*.iso;*.bin;*.img|All files (*.*)|*.*";
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                ExtractImage(dialog.FileName);
            }
        }

        void ExtractImage(string imagePath)
        {
            var name = Path.GetFileNameWithoutExtension(imagePath);
            foreach (var invalid in Path.GetInvalidFileNameChars())
                name = name.Replace(invalid, '_');
            name = name.Trim();
            if (name.Length == 0) name = "The Sims";
            var destination = Path.Combine(UserFolder, "Extracted", name);

            if (Directory.Exists(destination) && Directory.EnumerateFileSystemEntries(destination).Any())
            {
                var replace = MessageBox.Show(this,
                    "A previous extract is already here:\n\n" + destination + "\n\nExtract again and replace those files?\n\nNo keeps the folder that is already there.",
                    "Simitone",
                    MessageBoxButtons.YesNoCancel,
                    MessageBoxIcon.Question);
                if (replace == DialogResult.Cancel) return;
                if (replace == DialogResult.No)
                {
                    _path.Text = destination;
                    return;
                }
            }

            try
            {
                Enabled = false;
                IsoExtractForm.Run(this, imagePath, destination);
                _path.Text = destination;
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Simitone", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally
            {
                Enabled = true;
                RefreshStatus();
            }
        }

        void Browse(object sender, EventArgs e)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Select the The Sims folder";
                dialog.UseDescriptionForTitle = true;
                var current = _path.Text.Trim().Trim('"').TrimEnd('/', '\\');
                if (Directory.Exists(current)) dialog.SelectedPath = current;
                if (dialog.ShowDialog(this) == DialogResult.OK)
                    _path.Text = Sims1Install.FindNear(dialog.SelectedPath) ?? dialog.SelectedPath;
            }
        }

        void OpenUserFolder()
        {
            try
            {
                Directory.CreateDirectory(UserFolder);
                Process.Start(new ProcessStartInfo { FileName = UserFolder, UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Simitone", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        void RefreshStatus()
        {
            var text = _path.Text;
            // A half-typed server name can block for seconds, so network paths are only
            // checked when the player presses Play.
            var network = IsNetworkPath(text);
            var valid = !network && Sims1Install.IsValid(text);
            _resolvedPath = valid ? text : (network ? null : Sims1Install.FindNear(text));

            if (valid)
            {
                _status.Text = "Found GameData\\Behavior.iff. This install can be played.";
                _status.ForeColor = Color.FromArgb(20, 110, 50);
            }
            else if (_resolvedPath != null)
            {
                _status.Text = "The game is in " + _resolvedPath + ". Play uses that folder.";
                _status.ForeColor = Color.FromArgb(20, 110, 50);
            }
            else if (network)
            {
                _status.Text = "Network folder. It is checked when you press Play.";
                _status.ForeColor = SystemColors.GrayText;
            }
            else if (string.IsNullOrWhiteSpace(text))
            {
                _status.Text = "Choose the installed game folder, or use ISO to extract a disc image of that install.";
                _status.ForeColor = SystemColors.GrayText;
            }
            else
            {
                _status.Text = "That folder does not contain GameData\\Behavior.iff. Choose the install, or use ISO for a disc image of it.";
                _status.ForeColor = Color.FromArgb(160, 40, 40);
            }
            _play.Enabled = _resolvedPath != null || network;
        }

        static bool IsNetworkPath(string text)
        {
            var trimmed = (text ?? "").TrimStart().TrimStart('"');
            return trimmed.StartsWith(@"\\") || trimmed.StartsWith("//");
        }

        void Play(object sender, EventArgs e)
        {
            RefreshStatus();
            if (_resolvedPath == null && IsNetworkPath(_path.Text))
            {
                Cursor = Cursors.WaitCursor;
                try
                {
                    _resolvedPath = Sims1Install.FindNear(_path.Text);
                }
                finally
                {
                    Cursor = Cursors.Default;
                }
                if (_resolvedPath == null)
                {
                    _status.Text = "That network folder does not contain GameData\\Behavior.iff, or it could not be reached.";
                    _status.ForeColor = Color.FromArgb(160, 40, 40);
                    return;
                }
            }
            if (_resolvedPath == null) return;

            var resolution = _resolution.SelectedItem as ResolutionOption;
            var refresh = _refresh.SelectedItem as RefreshOption;
            Choice = new LauncherChoice
            {
                SimsPath = Sims1Install.Normalize(_resolvedPath),
                LanguageCode = Languages[Math.Max(0, _language.SelectedIndex)].code,
                Width = resolution?.Width ?? 1024,
                Height = resolution?.Height ?? 768,
                RefreshRate = refresh?.Rate ?? 60,
                Windowed = _windowed.Checked,
                Enable3D = _enable3d.Checked,
                AntiAlias = _antialias.Checked,
                DirectX = _directX.Checked
            };
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
