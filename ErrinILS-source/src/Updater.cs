// Zero-dependency update checker and updater for ErrinILS.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;

namespace Errin
{
    public class UpdateInfo
    {
        public bool HasUpdate;
        public string CurrentVersion = "";
        public string LatestVersion = "";
        public string ReleaseNotes = "";
        public string ReleaseUrl = "";
        public string DownloadUrl = "";
        public string Error = "";
    }

    public static class Updater
    {
        public const string RepoOwner = "RZSY";
        public const string RepoName = "ErrinILS";
        public const string ApiUrl = "https://api.github.com/repos/RZSY/ErrinILS/releases/latest";

        public static string GetCurrentVersion()
        {
            var v = Assembly.GetExecutingAssembly().GetName().Version;
            return string.Format("{0}.{1}.{2}", v.Major, v.Minor, v.Build);
        }

        public static UpdateInfo CheckForUpdate(string currentVersion = null)
        {
            if (string.IsNullOrEmpty(currentVersion)) currentVersion = GetCurrentVersion();
            var info = new UpdateInfo { CurrentVersion = currentVersion };

            try
            {
                // Ensure modern TLS (TLS 1.2, 1.1, 1.0)
                ServicePointManager.SecurityProtocol = (SecurityProtocolType)(192 | 768 | 3072);

                var req = (HttpWebRequest)WebRequest.Create(ApiUrl);
                req.UserAgent = "ErrinILS-Updater/" + currentVersion;
                req.Timeout = 10000;
                req.ReadWriteTimeout = 10000;

                string jsonStr;
                using (var resp = (HttpWebResponse)req.GetResponse())
                using (var reader = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                {
                    jsonStr = reader.ReadToEnd();
                }

                var root = Json.Parse(jsonStr) as Dictionary<string, object>;
                if (root == null)
                {
                    info.Error = "Invalid response from update server";
                    return info;
                }

                string tag = Json.Str(root, "tag_name").Trim();
                string cleanTag = tag.StartsWith("v", StringComparison.OrdinalIgnoreCase) ? tag.Substring(1) : tag;
                info.LatestVersion = cleanTag;
                info.ReleaseNotes = Json.Str(root, "body");
                info.ReleaseUrl = Json.Str(root, "html_url");

                // Find download URL for ErrinILS.exe or installer
                var assets = Json.Arr(root, "assets");
                foreach (var aObj in assets)
                {
                    var a = aObj as Dictionary<string, object>;
                    if (a == null) continue;
                    string name = Json.Str(a, "name").ToLowerInvariant();
                    if (name.EndsWith(".exe") || name.EndsWith(".zip"))
                    {
                        info.DownloadUrl = Json.Str(a, "browser_download_url");
                        if (name.Contains("setup") || name.Contains("errinils")) break;
                    }
                }

                info.HasUpdate = IsNewer(cleanTag, currentVersion);
            }
            catch (Exception ex)
            {
                info.Error = ex.Message;
            }

            return info;
        }

        public static bool IsNewer(string latest, string current)
        {
            try
            {
                var vLatest = ParseVersion(latest);
                var vCurrent = ParseVersion(current);
                return vLatest > vCurrent;
            }
            catch
            {
                return string.Compare(latest, current, StringComparison.OrdinalIgnoreCase) > 0;
            }
        }

        static Version ParseVersion(string v)
        {
            if (string.IsNullOrEmpty(v)) return new Version(0, 0, 0);
            v = v.Trim().TrimStart('v', 'V');
            int dash = v.IndexOf('-');
            if (dash > 0) v = v.Substring(0, dash);
            string[] parts = v.Split('.');
            int major = parts.Length > 0 ? int.Parse(parts[0]) : 0;
            int minor = parts.Length > 1 ? int.Parse(parts[1]) : 0;
            int build = parts.Length > 2 ? int.Parse(parts[2]) : 0;
            return new Version(major, minor, build);
        }

        /// <summary>Downloads update file to destination with progress updates.</summary>
        public static void DownloadFile(string url, string destPath, Action<int> onProgress)
        {
            ServicePointManager.SecurityProtocol = (SecurityProtocolType)(192 | 768 | 3072);
            var req = (HttpWebRequest)WebRequest.Create(url);
            req.UserAgent = "ErrinILS-Updater";
            req.Timeout = 30000;

            using (var resp = req.GetResponse())
            using (var inStream = resp.GetResponseStream())
            using (var outStream = File.Create(destPath))
            {
                long total = resp.ContentLength;
                byte[] buf = new byte[8192];
                long downloaded = 0;
                int read;
                while ((read = inStream.Read(buf, 0, buf.Length)) > 0)
                {
                    outStream.Write(buf, 0, read);
                    downloaded += read;
                    if (total > 0 && onProgress != null)
                    {
                        int pct = (int)((downloaded * 100) / total);
                        onProgress(pct);
                    }
                }
            }
        }

        /// <summary>Applies an update executable and restarts.</summary>
        public static void ApplyUpdateAndRestart(string newExePath)
        {
            string currentExe = Process.GetCurrentProcess().MainModule.FileName;
            string ext = Path.GetExtension(newExePath).ToLowerInvariant();

            if (newExePath.ToLowerInvariant().Contains("setup") || ext == ".msi")
            {
                // Run the setup installer directly and exit
                Process.Start(newExePath);
                Environment.Exit(0);
                return;
            }

            // Standalone executable replacement script
            string batPath = Path.Combine(Path.GetTempPath(), "errin-update-" + Guid.NewGuid().ToString("N") + ".cmd");
            string bat = string.Format(
                "@echo off\r\n" +
                "timeout /t 1 /nobreak > nul\r\n" +
                ":retry\r\n" +
                "copy /y \"{0}\" \"{1}\" > nul\r\n" +
                "if errorlevel 1 (\r\n" +
                "  timeout /t 1 /nobreak > nul\r\n" +
                "  goto retry\r\n" +
                ")\r\n" +
                "start \"\" \"{1}\"\r\n" +
                "del \"{0}\"\r\n" +
                "del \"%~f0\"\r\n",
                newExePath, currentExe);

            File.WriteAllText(batPath, bat, Encoding.ASCII);

            var psi = new ProcessStartInfo
            {
                FileName = batPath,
                CreateNoWindow = true,
                UseShellExecute = false,
                WindowStyle = ProcessWindowStyle.Hidden
            };
            Process.Start(psi);
            Environment.Exit(0);
        }
    }
}
