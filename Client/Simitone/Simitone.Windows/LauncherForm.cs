using System;
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
        readonly ComboBox _language;
        readonly CheckBox _windowed;
        readonly CheckBox _enable3d;
        readonly CheckBox _antialias;
        readonly RadioButton _directX;
        readonly RadioButton _openGL;
        readonly Button _play;

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

        static readonly (int width, int height)[] Resolutions =
        {
            (1024, 768),
            (1280, 720),
            (1366, 768),
            (1600, 900),
            (1920, 1080),
            (2560, 1440),
            (3840, 2160)
        };

        public LauncherForm(LauncherChoice initial, bool linux)
        {
            Choice = initial;
            Text = "Simitone";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(560, 492);
            Font = new Font("Segoe UI", 9f);
            AutoScaleMode = AutoScaleMode.Font;

            var title = new Label
            {
                Text = "Simitone",
                Font = new Font("Segoe UI", 18f, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(16, 12)
            };
            var subtitle = new Label
            {
                Text = "Choose an installed The Sims folder, or extract a disc image of that install.",
                AutoSize = false,
                Size = new Size(528, 36),
                Location = new Point(16, 48)
            };

            var pathLabel = new Label
            {
                Text = "The Sims folder",
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
                Text = "Browse...",
                Location = new Point(362, 116),
                Size = new Size(88, 28)
            };
            browse.Click += Browse;
            var iso = new Button
            {
                Text = "ISO...",
                Location = new Point(456, 116),
                Size = new Size(88, 28)
            };
            iso.Click += ExtractIso;
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
                Size = new Size(528, 150)
            };
            display.Controls.Add(new Label
            {
                Text = "Resolution",
                AutoSize = true,
                Location = new Point(12, 28)
            });
            _resolution = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(100, 24),
                Size = new Size(160, 24)
            };
            FillResolutions(initial.Width, initial.Height);
            _windowed = new CheckBox
            {
                Text = "Windowed",
                AutoSize = true,
                Location = new Point(280, 26),
                Checked = initial.Windowed
            };
            _enable3d = new CheckBox
            {
                Text = "3D lot view",
                AutoSize = true,
                Location = new Point(12, 62),
                Checked = initial.Enable3D
            };
            _antialias = new CheckBox
            {
                Text = "Anti-aliasing",
                AutoSize = true,
                Location = new Point(150, 62),
                Checked = initial.AntiAlias
            };
            _directX = new RadioButton
            {
                Text = "DirectX",
                AutoSize = true,
                Location = new Point(12, 100),
                Checked = initial.DirectX && !linux,
                Enabled = !linux
            };
            _openGL = new RadioButton
            {
                Text = "OpenGL",
                AutoSize = true,
                Location = new Point(150, 100),
                Checked = !initial.DirectX || linux,
                Enabled = !linux
            };
            display.Controls.Add(_resolution);
            display.Controls.Add(_windowed);
            display.Controls.Add(_enable3d);
            display.Controls.Add(_antialias);
            display.Controls.Add(_directX);
            display.Controls.Add(_openGL);
            display.Controls.Add(new Label
            {
                Text = linux ? "This system uses OpenGL." : "F12 in a lot switches 3D and hybrid.",
                AutoSize = true,
                ForeColor = SystemColors.GrayText,
                Location = new Point(250, 102)
            });

            var languageBox = new GroupBox
            {
                Text = "Language",
                Location = new Point(16, 358),
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

            _play = new Button
            {
                Text = "Play",
                Location = new Point(348, 444),
                Size = new Size(100, 32)
            };
            _play.Click += Play;
            var quit = new Button
            {
                Text = "Quit",
                Location = new Point(456, 444),
                Size = new Size(88, 32),
                DialogResult = DialogResult.Cancel
            };
            AcceptButton = _play;
            CancelButton = quit;

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
            RefreshStatus();
        }

        void FillResolutions(int width, int height)
        {
            var found = false;
            foreach (var res in Resolutions)
            {
                _resolution.Items.Add(res.width + " x " + res.height);
                if (res.width == width && res.height == height)
                {
                    _resolution.SelectedIndex = _resolution.Items.Count - 1;
                    found = true;
                }
            }
            if (!found)
            {
                if (width < 1) width = 1024;
                if (height < 1) height = 768;
                _resolution.Items.Insert(0, width + " x " + height);
                _resolution.SelectedIndex = 0;
            }
        }

        void ExtractIso(object sender, EventArgs e)
        {
            using (var dialog = new OpenFileDialog())
            {
                dialog.Title = "Choose a The Sims disc image";
                dialog.Filter = "Disc image (*.iso;*.bin;*.img)|*.iso;*.bin;*.img|All files (*.*)|*.*";
                if (dialog.ShowDialog(this) != DialogResult.OK) return;

                var name = Path.GetFileNameWithoutExtension(dialog.FileName);
                foreach (var invalid in Path.GetInvalidFileNameChars())
                    name = name.Replace(invalid, '_');
                name = name.Trim();
                if (name.Length == 0) name = "The Sims";
                var destination = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "Simitone",
                    "Extracted",
                    name);

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
                    IsoExtractForm.Run(this, dialog.FileName, destination);
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
        }

        void Browse(object sender, EventArgs e)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Select the The Sims folder";
                dialog.UseDescriptionForTitle = true;
                var current = _path.Text.Trim().TrimEnd('/', '\\');
                if (Directory.Exists(current)) dialog.SelectedPath = current;
                if (dialog.ShowDialog(this) == DialogResult.OK)
                    _path.Text = dialog.SelectedPath;
            }
        }

        void RefreshStatus()
        {
            if (Sims1Install.IsValid(_path.Text))
            {
                _status.Text = "Found GameData\\Behavior.iff. This install can be played.";
                _status.ForeColor = Color.FromArgb(20, 110, 50);
                _play.Enabled = true;
            }
            else if (string.IsNullOrWhiteSpace(_path.Text))
            {
                _status.Text = "Choose the installed game folder, or use ISO to extract a disc image of that install.";
                _status.ForeColor = SystemColors.GrayText;
                _play.Enabled = false;
            }
            else
            {
                _status.Text = "That folder does not contain GameData\\Behavior.iff. Choose the install, or use ISO for a disc image of it.";
                _status.ForeColor = Color.FromArgb(160, 40, 40);
                _play.Enabled = false;
            }
        }

        void Play(object sender, EventArgs e)
        {
            if (!Sims1Install.IsValid(_path.Text))
            {
                RefreshStatus();
                return;
            }

            ParseResolution(out var width, out var height);
            Choice = new LauncherChoice
            {
                SimsPath = Sims1Install.Normalize(_path.Text),
                LanguageCode = Languages[Math.Max(0, _language.SelectedIndex)].code,
                Width = width,
                Height = height,
                Windowed = _windowed.Checked,
                Enable3D = _enable3d.Checked,
                AntiAlias = _antialias.Checked,
                DirectX = _directX.Checked
            };
            DialogResult = DialogResult.OK;
            Close();
        }

        void ParseResolution(out int width, out int height)
        {
            width = 1024;
            height = 768;
            var text = _resolution.SelectedItem as string;
            if (string.IsNullOrEmpty(text)) return;
            var parts = text.Split('x');
            if (parts.Length != 2) return;
            int.TryParse(parts[0].Trim(), out width);
            int.TryParse(parts[1].Trim(), out height);
            if (width < 1) width = 1024;
            if (height < 1) height = 768;
        }
    }
}
