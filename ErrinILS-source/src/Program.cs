using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace Errin
{
    static class Program
    {
        [DllImport("user32.dll")] static extern IntPtr FindWindow(string cls, string title);
        [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr h);
        [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr h, int cmd);
        [DllImport("user32.dll")] static extern bool SetProcessDPIAware();
        [DllImport("shcore.dll")] static extern int SetProcessDpiAwareness(int awareness);
        [DllImport("user32.dll")] static extern bool SetProcessDpiAwarenessContext(IntPtr dpiContext);

        static void EnableDpiAwareness()
        {
            try
            {
                if (Environment.OSVersion.Version.Major >= 6)
                {
                    bool set = false;
                    try { set = SetProcessDpiAwarenessContext((IntPtr)(-4)); } catch { }
                    if (!set)
                    {
                        try { SetProcessDpiAwareness(2); set = true; } catch { }
                    }
                    if (!set)
                    {
                        try { SetProcessDPIAware(); } catch { }
                    }
                }
            }
            catch { }
        }

        [STAThread]
        static void Main()
        {
            EnableDpiAwareness();
            bool created;
            using (var m = new Mutex(true, "Local\\ErrinILS.SingleInstance", out created))
            {
                if (!created)
                {
                    try { var h = FindWindow(null, "ErrinILS"); if (h != IntPtr.Zero) { ShowWindow(h, 9); SetForegroundWindow(h); } } catch { }
                    return;
                }
                Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
                Application.ThreadException += (s, e) => MessageBox.Show("Something went wrong:\n\n" + e.Exception.Message + "\n\nYour data is saved. You can keep working.", "ErrinILS", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                Application.Run(new MainForm());
            }
        }
    }
}
