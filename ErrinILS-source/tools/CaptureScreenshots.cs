using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
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

            // Create isolated temporary test directory for screenshots
            string tempDir = Path.Combine(Path.GetTempPath(), "ErrinScreenshotClean_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                // Create clean synthetic made-up demo Db with requested names
                var demoDb = new Db();
                demoDb.Set.school = "SEKOLAH MENENGAH KEBANGSAAN CONTOH";
                demoDb.Set.rate = 0.10;
                demoDb.Set.block = 5.00;
                demoDb.Set.maxLoans = 3;

                // Add sample library catalog
                var items = new List<Item>
                {
                    new Item { id = "1", ctl = "00000001", acq = "ACQ-000001", barcode = "B1001", title = "Charlotte's Web", author = "E. B. White", isbn = "9780064400558", publisher = "HarperCollins", year = "2001", call = "F WHI", subject = "Animals--Fiction", pages = "184" },
                    new Item { id = "2", ctl = "00000002", acq = "ACQ-000002", barcode = "B1002", title = "A Brief History of Time", author = "Stephen Hawking", isbn = "9780553380163", publisher = "Bantam", year = "1998", call = "523.1 HAW", subject = "Cosmology", pages = "212" },
                    new Item { id = "3", ctl = "00000003", acq = "ACQ-000003", barcode = "B1003", title = "Hatchet", author = "Gary Paulsen", isbn = "9781416936473", publisher = "Simon & Schuster", year = "2006", call = "F PAU", subject = "Wilderness survival--Fiction", pages = "195" },
                    new Item { id = "4", ctl = "00000004", acq = "ACQ-000004", barcode = "B1004", title = "The Hobbit", author = "J. R. R. Tolkien", isbn = "9780547928227", publisher = "Houghton Mifflin", year = "2012", call = "F TOL", subject = "Fantasy", pages = "300" },
                    new Item { id = "5", ctl = "00000005", acq = "ACQ-000005", barcode = "B1005", title = "Atlas of the World", author = "National Geographic", isbn = "9781426220654", publisher = "National Geographic", year = "2015", call = "912 ATL", subject = "Atlases", pages = "304" },
                    new Item { id = "6", ctl = "00000006", acq = "ACQ-000006", barcode = "B1006", title = "Matilda", author = "Roald Dahl", isbn = "9780142410370", publisher = "Puffin Books", year = "2007", call = "F DAH", subject = "Children's stories", pages = "240" },
                    new Item { id = "7", ctl = "00000007", acq = "ACQ-000007", barcode = "B1007", title = "To Kill a Mockingbird", author = "Harper Lee", isbn = "9780060935467", publisher = "Harper Perennial", year = "2002", call = "F LEE", subject = "Historical fiction", pages = "323" },
                    new Item { id = "8", ctl = "00000008", acq = "ACQ-000008", barcode = "B1008", title = "Oxford Advanced Learner's Dictionary", author = "A. S. Hornby", isbn = "9780194798488", publisher = "Oxford University Press", year = "2020", call = "423 HOR", subject = "English language--Dictionaries", pages = "1820" },
                    new Item { id = "9", ctl = "00000009", acq = "ACQ-000009", barcode = "B1009", title = "Science Encyclopedia", author = "DK Publishing", isbn = "9781465473639", publisher = "DK", year = "2018", call = "503 SCI", subject = "Science--Encyclopedias", pages = "304", weeded = "2026-10-15", weed_reason = "Damaged pages / binding loose" }
                };
                demoDb.Items.AddRange(items);

                // Add requested made-up students
                var p1 = new Patron { id = "080415-10-5521", name = "David Martinez Bin Daud Martinez", cls = "4 CEMERLANG", contact = "012-3456789", reg = "2024-01-10", last = DateTime.Today.ToString("yyyy-MM-dd") };
                var p2 = new Patron { id = "090823-14-6633", name = "Muhammad Sumbul", cls = "3 BESTARI", contact = "019-8765432", reg = "2025-01-15", last = DateTime.Today.ToString("yyyy-MM-dd") };
                demoDb.Patrons.Add(p1);
                demoDb.Patrons.Add(p2);

                // Active loans
                // David Martinez Bin Daud Martinez: Charlotte's Web (active, on-time)
                demoDb.Loans.Add(new Loan { id = "L101", iid = "1", pid = p1.id, @out = DateTime.Today.AddDays(-2).ToString("yyyy-MM-dd"), due = DateTime.Today.AddDays(5).ToString("yyyy-MM-dd"), ret = null, ren = 0 });
                // Muhammad Sumbul: The Hobbit (active, overdue by 3 days)
                demoDb.Loans.Add(new Loan { id = "L102", iid = "4", pid = p2.id, @out = DateTime.Today.AddDays(-10).ToString("yyyy-MM-dd"), due = DateTime.Today.AddDays(-3).ToString("yyyy-MM-dd"), ret = null, ren = 0 });

                // Historical completed loans (for Nilam leaderboard and 6-month borrowing trajectory)
                demoDb.Loans.Add(new Loan { id = "L001", iid = "2", pid = p1.id, @out = "2026-08-01", due = "2026-08-08", ret = "2026-08-07", ren = 0 });
                demoDb.Loans.Add(new Loan { id = "L002", iid = "3", pid = p1.id, @out = "2026-08-15", due = "2026-08-22", ret = "2026-08-20", ren = 0 });
                demoDb.Loans.Add(new Loan { id = "L003", iid = "5", pid = p1.id, @out = "2026-09-01", due = "2026-09-08", ret = "2026-09-06", ren = 0 });
                demoDb.Loans.Add(new Loan { id = "L004", iid = "6", pid = p1.id, @out = "2026-09-12", due = "2026-09-19", ret = "2026-09-18", ren = 0 });
                demoDb.Loans.Add(new Loan { id = "L005", iid = "7", pid = p1.id, @out = "2026-10-01", due = "2026-10-08", ret = "2026-10-07", ren = 0 });
                demoDb.Loans.Add(new Loan { id = "L006", iid = "8", pid = p1.id, @out = "2026-10-15", due = "2026-10-22", ret = "2026-10-21", ren = 0 });

                demoDb.Loans.Add(new Loan { id = "L007", iid = "2", pid = p2.id, @out = "2026-08-10", due = "2026-08-17", ret = "2026-08-16", ren = 0 });
                demoDb.Loans.Add(new Loan { id = "L008", iid = "3", pid = p2.id, @out = "2026-09-05", due = "2026-09-12", ret = "2026-09-11", ren = 0 });
                demoDb.Loans.Add(new Loan { id = "L009", iid = "6", pid = p2.id, @out = "2026-10-02", due = "2026-10-09", ret = "2026-10-08", ren = 0 });
                demoDb.Loans.Add(new Loan { id = "L010", iid = "7", pid = p2.id, @out = "2026-10-18", due = "2026-10-25", ret = "2026-10-24", ren = 0 });

                // Fines: Muhammad Sumbul has RM 0.30 overdue fine
                demoDb.Fines.Add(new Fine { id = "F001", pid = p2.id, iid = "4", loan = "L102", days = 3, amt = 0.30, date = DateTime.Today.ToString("yyyy-MM-dd"), paid = "" });

                // Class history cohorts
                var ch2025 = new ClassYearRecord { year = 2025, cls = "3 CEMERLANG", studentCount = 1, totalLoans = 6 };
                ch2025.students.Add(new ClassYearStudent { id = p1.id, name = p1.name, loans = 6 });
                var ch2025b = new ClassYearRecord { year = 2025, cls = "2 BESTARI", studentCount = 1, totalLoans = 4 };
                ch2025b.students.Add(new ClassYearStudent { id = p2.id, name = p2.name, loans = 4 });
                demoDb.ClassHistory.Add(ch2025);
                demoDb.ClassHistory.Add(ch2025b);

                // Save to clean test store
                var demoStore = new Store(tempDir);
                demoStore.Save(demoDb);

                // Save config with default admin123
                File.WriteAllText(demoStore.ConfigFile, "{\"admin_hash\":\"" + Auth.DefaultHash + "\",\"z_server\":false,\"z_port\":210,\"z_db\":\"errin\",\"z_remote\":false}");

                // Instantiate MainForm using the isolated demoStore
                var form = new MainForm(demoStore);
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
                    Thread.Sleep(300);
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
