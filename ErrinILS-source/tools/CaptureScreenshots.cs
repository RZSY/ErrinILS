using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

namespace Errin
{
    class ScreenshotGen
    {
        [STAThread]
        static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            string outDir = Path.GetFullPath(@"docs\assets");
            if (!Directory.Exists(outDir)) Directory.CreateDirectory(outDir);

            // Create temporary test store
            string tempDir = Path.Combine(Path.GetTempPath(), "ErrinScreenshotTest_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                // Create MainForm
                var form = new MainForm();
                form.Size = new Size(1120, 740);
                form.StartPosition = FormStartPosition.Manual;
                form.Location = new Point(50, 50);
                form.Show();
                Application.DoEvents();
                Thread.Sleep(300);

                // Use reflection to access private method Go(string)
                var goMethod = typeof(MainForm).GetMethod("Go", BindingFlags.NonPublic | BindingFlags.Instance);
                var unlockedField = typeof(MainForm).GetField("unlocked", BindingFlags.NonPublic | BindingFlags.Instance);

                string[] views = new string[] { "dash", "circ", "cat", "pat", "fine", "rep", "simple", "set" };
                string[] names = new string[] { 
                    "screenshot-dashboard.png",
                    "screenshot-circulation.png",
                    "screenshot-catalog.png",
                    "screenshot-students.png",
                    "screenshot-fines.png",
                    "screenshot-reports.png",
                    "screenshot-kiosk.png",
                    "screenshot-settings.png"
                };

                for (int i = 0; i < views.Length; i++)
                {
                    string v = views[i];
                    string filename = names[i];

                    if (v == "set" && unlockedField != null)
                    {
                        unlockedField.SetValue(form, true);
                    }

                    if (goMethod != null)
                    {
                        goMethod.Invoke(form, new object[] { v });
                    }
                    Application.DoEvents();
                    Thread.Sleep(200);
                    Application.DoEvents();

                    string targetPath = Path.Combine(outDir, filename);
                    using (Bitmap bmp = new Bitmap(form.ClientSize.Width, form.ClientSize.Height))
                    {
                        form.DrawToBitmap(bmp, new Rectangle(0, 0, form.ClientSize.Width, form.ClientSize.Height));
                        bmp.Save(targetPath, ImageFormat.Png);
                    }
                    Console.WriteLine("Saved: " + filename);
                }

                form.Close();
                form.Dispose();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex);
            }
            finally
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }
    }
}
