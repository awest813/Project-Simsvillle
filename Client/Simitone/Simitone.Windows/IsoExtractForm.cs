using System;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Simitone.Windows
{
    public class IsoExtractForm : Form
    {
        public bool Cancelled;
        public Exception Error;

        readonly Label _label;
        readonly ProgressBar _bar;
        readonly CancellationTokenSource _cancel = new CancellationTokenSource();

        public static void Run(IWin32Window owner, string imagePath, string destination)
        {
            var staging = destination + ".partial";
            if (Directory.Exists(staging)) Directory.Delete(staging, true);
            Directory.CreateDirectory(staging);

            using (var form = new IsoExtractForm(imagePath, staging))
            {
                form.ShowDialog(owner);
                if (form.Cancelled)
                {
                    TryDelete(staging);
                    throw new OperationCanceledException();
                }
                if (form.Error != null)
                {
                    TryDelete(staging);
                    throw form.Error;
                }
            }

            if (!Sims1Install.IsValid(staging))
            {
                TryDelete(staging);
                throw new InvalidDataException("The image was extracted, but GameData\\Behavior.iff was not in the result.");
            }

            if (Directory.Exists(destination)) Directory.Delete(destination, true);
            Directory.Move(staging, destination);
        }

        IsoExtractForm(string imagePath, string staging)
        {
            Text = "Extracting disc image";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            ControlBox = false;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(480, 140);
            Font = new Font("Segoe UI", 9f);

            _label = new Label
            {
                Text = "Reading the disc image...",
                AutoSize = false,
                Location = new Point(16, 16),
                Size = new Size(448, 36)
            };
            _bar = new ProgressBar
            {
                Location = new Point(16, 60),
                Size = new Size(448, 24),
                Minimum = 0,
                Maximum = 1000
            };
            var cancel = new Button
            {
                Text = "Cancel",
                Location = new Point(364, 96),
                Size = new Size(100, 28)
            };
            cancel.Click += (sender, args) =>
            {
                Cancelled = true;
                _cancel.Cancel();
                cancel.Enabled = false;
            };
            Controls.Add(_label);
            Controls.Add(_bar);
            Controls.Add(cancel);
            Shown += (sender, args) => Begin(imagePath, staging);
        }

        void Begin(string imagePath, string staging)
        {
            var progress = new Progress<IsoExtractProgress>(UpdateProgress);
            Task.Run(() =>
            {
                try
                {
                    Iso9660Image.ExtractSimsInstall(imagePath, staging, progress, _cancel.Token);
                }
                catch (OperationCanceledException)
                {
                    Cancelled = true;
                }
                catch (Exception ex)
                {
                    Error = ex;
                }
                try
                {
                    BeginInvoke(new Action(Close));
                }
                catch (InvalidOperationException)
                {
                }
            });
        }

        void UpdateProgress(IsoExtractProgress progress)
        {
            if (progress.BytesTotal > 0)
            {
                var value = (int)Math.Min(1000, progress.BytesCopied * 1000L / progress.BytesTotal);
                _bar.Value = Math.Max(0, value);
            }
            var name = progress.CurrentFile ?? "";
            if (name.Length > 64) name = "..." + name.Substring(name.Length - 61);
            _label.Text = name.Length == 0 ? "Finishing the extract..." : name;
        }

        static void TryDelete(string path)
        {
            try
            {
                if (Directory.Exists(path)) Directory.Delete(path, true);
            }
            catch (Exception)
            {
            }
        }
    }
}
