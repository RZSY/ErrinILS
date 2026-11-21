// Zero-dependency Cloud Sync Manager for ErrinILS.
// Manages:
// 1. Direct Cloud User Records Sync (Patrons, Loans, Holds, Fines, Settings) for tamanria.sk@gmail.com
//    - Directly connects to Google Drive cloud over HTTPS without installing any Google Drive apps or 3rd-party software.
//    - Supports Google Apps Script Web App direct cloud endpoint and Google Drive REST API v3 (OAuth / Bearer token).
// 2. Books and Catalog Data Sync via GitHub repository (pustaka-terbuka/sk-taman-ria).
// Supports multi-PC synchronization so that multiple PCs operate identically with unified records.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace Errin
{
    public class SyncConfig
    {
        // Google Cloud Direct Sync
        public string googleAccount = "tamanria.sk@gmail.com";
        public string googleCloudEndpoint = "";
        public string googleClientId = "";
        public string googleClientSecret = "";
        public string googleRefreshToken = "";
        public string googleAccessToken = "";
        public string googleFolderId = "";
        public string googleFileId = "";

        // Optional local folder fallback
        public string gdriveFolder = "";

        // GitHub Catalog Sync
        public string githubRepo = "pustaka-terbuka/sk-taman-ria";
        public string githubToken = "";
        public bool autoSync = true;
        public bool syncOnStartup = true;
        public bool syncOnSave = false; // "Instant On-Save" is disabled by default
        public bool syncOnExit = true; // Sync on application exit
        public string lastSyncTime = "";
        public string lastSyncStatus = "";

        public bool IsGoogleCloudConfigured
        {
            get
            {
                return !string.IsNullOrEmpty(googleCloudEndpoint) ||
                       !string.IsNullOrEmpty(googleRefreshToken) ||
                       !string.IsNullOrEmpty(googleAccessToken);
            }
        }

        public Dictionary<string, object> ToJ()
        {
            var d = new Dictionary<string, object>();
            d["googleAccount"] = googleAccount ?? "tamanria.sk@gmail.com";
            d["googleCloudEndpoint"] = googleCloudEndpoint ?? "";
            d["googleClientId"] = googleClientId ?? "";
            d["googleClientSecret"] = googleClientSecret ?? "";
            d["googleRefreshToken"] = googleRefreshToken ?? "";
            d["googleAccessToken"] = googleAccessToken ?? "";
            d["googleFolderId"] = googleFolderId ?? "";
            d["googleFileId"] = googleFileId ?? "";
            d["gdriveFolder"] = gdriveFolder ?? "";
            d["githubRepo"] = githubRepo ?? "pustaka-terbuka/sk-taman-ria";
            d["githubToken"] = githubToken ?? "";
            d["autoSync"] = autoSync;
            d["syncOnStartup"] = syncOnStartup;
            d["syncOnSave"] = syncOnSave;
            d["syncOnExit"] = syncOnExit;
            d["lastSyncTime"] = lastSyncTime ?? "";
            d["lastSyncStatus"] = lastSyncStatus ?? "";
            return d;
        }

        public static SyncConfig FromJ(Dictionary<string, object> d)
        {
            var c = new SyncConfig();
            if (d == null) return c;
            c.googleAccount = Json.Str(d, "googleAccount");
            if (string.IsNullOrEmpty(c.googleAccount)) c.googleAccount = "tamanria.sk@gmail.com";
            c.googleCloudEndpoint = Json.Str(d, "googleCloudEndpoint");
            c.googleClientId = Json.Str(d, "googleClientId");
            c.googleClientSecret = Json.Str(d, "googleClientSecret");
            c.googleRefreshToken = Json.Str(d, "googleRefreshToken");
            c.googleAccessToken = Json.Str(d, "googleAccessToken");
            c.googleFolderId = Json.Str(d, "googleFolderId");
            c.googleFileId = Json.Str(d, "googleFileId");
            c.gdriveFolder = Json.Str(d, "gdriveFolder");
            c.githubRepo = Json.Str(d, "githubRepo");
            if (string.IsNullOrEmpty(c.githubRepo)) c.githubRepo = "pustaka-terbuka/sk-taman-ria";
            c.githubToken = Json.Str(d, "githubToken");
            if (d.ContainsKey("autoSync")) c.autoSync = d["autoSync"] is bool && (bool)d["autoSync"];
            if (d.ContainsKey("syncOnStartup")) c.syncOnStartup = d["syncOnStartup"] is bool && (bool)d["syncOnStartup"];
            else c.syncOnStartup = c.autoSync;
            if (d.ContainsKey("syncOnSave")) c.syncOnSave = d["syncOnSave"] is bool && (bool)d["syncOnSave"];
            else c.syncOnSave = false;
            if (d.ContainsKey("syncOnExit")) c.syncOnExit = d["syncOnExit"] is bool && (bool)d["syncOnExit"];
            else c.syncOnExit = true;
            c.lastSyncTime = Json.Str(d, "lastSyncTime");
            c.lastSyncStatus = Json.Str(d, "lastSyncStatus");
            return c;
        }
    }

    public class SyncResult
    {
        public bool Success;
        public string Message = "";
        public int ItemsAdded;
        public int ItemsUpdated;
        public int PatronsAdded;
        public int PatronsUpdated;
        public int LoansUpdated;
        public int FinesUpdated;

        public override string ToString()
        {
            return Message;
        }
    }

    /// <summary>
    /// Direct Cloud Google Drive Synchronization for tamanria.sk@gmail.com.
    /// Operates directly over HTTPS without requiring any installed Google Drive applications or 3rd-party software.
    /// </summary>
    public static class GoogleDriveCloudSync
    {
        public const string DefaultTargetAccount = "tamanria.sk@gmail.com";
        public const string DefaultCloudFolderName = "ErrinILS - SK Taman Ria";
        public const string UsersJsonName = "users_data.json";

        /// <summary>
        /// Generates a Google Apps Script tailored dynamically for any school's name and dedicated Gmail account.
        /// </summary>
        public static string GenerateAppsScript(string schoolName, string googleAccount)
        {
            if (string.IsNullOrEmpty(schoolName)) schoolName = "School Library";
            if (string.IsNullOrEmpty(googleAccount)) googleAccount = "school.library@gmail.com";
            string folderName = "ErrinILS - " + schoolName.Trim();

            return string.Format(
@"// ============================================================
// Google Apps Script: ErrinILS Direct Cloud Sync
// Target Google Account: {0}
// School / Library: {1}
// ============================================================
// INSTRUCTIONS:
// 1. Sign in to your Google Account ({0}).
// 2. Open https://script.google.com and click 'New project'.
// 3. Paste this entire code into Code.gs.
// 4. Click 'Deploy' -> 'New deployment'.
//    - Select type: 'Web app'.
//    - Description: ErrinILS Cloud Sync - {1}
//    - Execute as: Me ({0})
//    - Who has access: Anyone
// 5. Click 'Deploy', authorize access, and copy the Web App URL!
// 6. Paste the Web App URL into ErrinILS Settings -> Cloud Sync.
// ============================================================

function doPost(e) {{
  try {{
    var data = JSON.parse(e.postData.contents);
    var folderName = ""{2}"";
    var folders = DriveApp.getFoldersByName(folderName);
    var folder = folders.hasNext() ? folders.next() : DriveApp.createFolder(folderName);

    var fileName = ""users_data.json"";
    var files = folder.getFilesByName(fileName);
    var content = JSON.stringify(data, null, 2);

    if (files.hasNext()) {{
      files.next().setContent(content);
    }} else {{
      folder.createFile(fileName, content, MimeType.PLAIN_TEXT);
    }}

    return ContentService.createTextOutput(JSON.stringify({{ success: true, message: ""Users synced to Google Drive ({0})"" }}))
      .setMimeType(ContentService.MimeType.JSON);
  }} catch (err) {{
    return ContentService.createTextOutput(JSON.stringify({{ success: false, error: err.toString() }}))
      .setMimeType(ContentService.MimeType.JSON);
  }}
}}

function doGet(e) {{
  try {{
    var folderName = ""{2}"";
    var folders = DriveApp.getFoldersByName(folderName);
    if (!folders.hasNext()) {{
      return ContentService.createTextOutput(JSON.stringify({{ patrons: [], loans: [], fines: [], holds: [] }}))
        .setMimeType(ContentService.MimeType.JSON);
    }}
    var folder = folders.next();
    var files = folder.getFilesByName(""users_data.json"");
    if (!files.hasNext()) {{
      return ContentService.createTextOutput(JSON.stringify({{ patrons: [], loans: [], fines: [], holds: [] }}))
        .setMimeType(ContentService.MimeType.JSON);
    }}
    var content = files.next().getBlob().getDataAsString();
    return ContentService.createTextOutput(content)
      .setMimeType(ContentService.MimeType.JSON);
  }} catch (err) {{
    return ContentService.createTextOutput(JSON.stringify({{ error: err.toString() }}))
      .setMimeType(ContentService.MimeType.JSON);
  }}
}}
", googleAccount, schoolName, folderName.Replace("\"", "\\\""));
        }

        public static string AppsScriptTemplate
        {
            get { return GenerateAppsScript("SK Taman Ria", "tamanria.sk@gmail.com"); }
        }

        /// <summary>
        /// Serializes the entire user records dataset into clean, formatted JSON.
        /// </summary>
        public static string BuildUsersJson(Db db, string targetAccount = null)
        {
            var root = new Dictionary<string, object>();
            string sName = string.IsNullOrEmpty(db.Set.school) ? "SK Taman Ria" : db.Set.school;
            string acct = !string.IsNullOrEmpty(targetAccount) ? targetAccount :
                (CloudSyncManager.Instance != null && CloudSyncManager.Instance.Config != null && !string.IsNullOrEmpty(CloudSyncManager.Instance.Config.googleAccount) ? CloudSyncManager.Instance.Config.googleAccount : DefaultTargetAccount);
            var meta = new Dictionary<string, object>
            {
                { "school", sName },
                { "account", acct },
                { "exportedAt", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") },
                { "machine", Environment.MachineName },
                { "version", 1 }
            };
            root["metadata"] = meta;
            root["patrons"] = db.Patrons.Select(p => (object)p.ToJ()).ToList();
            root["loans"] = db.Loans.Select(l => (object)new Dictionary<string, object>
            {
                { "id", l.id }, { "iid", l.iid }, { "pid", l.pid },
                { "out", l.@out }, { "due", l.due }, { "ret", l.ret },
                { "ren", (double)l.ren }
            }).ToList();
            root["holds"] = db.Holds.Select(h => (object)new Dictionary<string, object>
            {
                { "id", h.id }, { "iid", h.iid }, { "pid", h.pid },
                { "date", h.date }, { "st", h.st }
            }).ToList();
            root["fines"] = db.Fines.Select(f => (object)new Dictionary<string, object>
            {
                { "id", f.id }, { "pid", f.pid }, { "iid", f.iid },
                { "loan", f.loan }, { "days", (double)f.days }, { "amt", f.amt },
                { "date", f.date }, { "paid", f.paid }
            }).ToList();

            if (db.ClassHistory != null && db.ClassHistory.Count > 0)
            {
                root["classHistory"] = db.ClassHistory.Select(ch => (object)new Dictionary<string, object>
                {
                    { "year", (double)ch.year },
                    { "cls", ch.cls },
                    { "studentCount", (double)ch.studentCount },
                    { "totalLoans", (double)ch.totalLoans },
                    { "students", ch.students.Select(st => (object)new Dictionary<string, object> { { "id", st.id }, { "name", st.name }, { "loans", (double)st.loans } }).ToList() }
                }).ToList();
            }

            if (db.ArchivedPatrons != null && db.ArchivedPatrons.Count > 0)
            {
                root["archivedPatrons"] = db.ArchivedPatrons.Select(ap => (object)ap.ToJ()).ToList();
            }

            var s = new Dictionary<string, object>
            {
                { "school", db.Set.school },
                { "rate", db.Set.rate },
                { "maxLoans", (double)db.Set.maxLoans },
                { "block", db.Set.block },
                { "opacLogin", db.Set.opacLogin }
            };
            if (db.Set.requiredPatronFields != null)
                s["requiredPatronFields"] = db.Set.requiredPatronFields.Select(x => (object)x).ToList();
            root["settings"] = s;

            return Json.WritePretty(root);
        }

        /// <summary>
        /// Merges user records from a JSON string into local database.
        /// Non-destructive: preserves existing records while incorporating remote additions and updates.
        /// </summary>
        public static SyncResult MergeUsersJson(Db db, string json)
        {
            var res = new SyncResult();
            if (db == null || string.IsNullOrEmpty(json))
            {
                res.Success = false;
                res.Message = "Empty data received";
                return res;
            }

            try
            {
                var root = Json.Parse(json) as Dictionary<string, object>;
                if (root == null)
                {
                    res.Success = false;
                    res.Message = "Invalid JSON structure";
                    return res;
                }

                // Merge Patrons
                var patronMap = new Dictionary<string, Patron>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < db.Patrons.Count; i++)
                {
                    var p = db.Patrons[i];
                    if (!string.IsNullOrEmpty(p.id)) patronMap[p.id] = p;
                    if (!string.IsNullOrEmpty(p.no_pengenalan)) patronMap[p.no_pengenalan] = p;
                }

                foreach (var o in Json.Arr(root, "patrons"))
                {
                    var d = o as Dictionary<string, object>;
                    if (d == null) continue;
                    var remote = Patron.FromJ(d);
                    if (string.IsNullOrEmpty(remote.id)) continue;

                    Patron local;
                    if (patronMap.TryGetValue(remote.id, out local) || (!string.IsNullOrEmpty(remote.no_pengenalan) && patronMap.TryGetValue(remote.no_pengenalan, out local)))
                    {
                        bool changed = false;
                        foreach (var k in Patron.Keys)
                        {
                            string rv = remote.Get(k);
                            string lv = local.Get(k);
                            if (!string.IsNullOrEmpty(rv) && rv != lv)
                            {
                                local.Set(k, rv);
                                changed = true;
                            }
                        }
                        if (changed) res.PatronsUpdated++;
                    }
                    else
                    {
                        db.Patrons.Add(remote);
                        patronMap[remote.id] = remote;
                        if (!string.IsNullOrEmpty(remote.no_pengenalan)) patronMap[remote.no_pengenalan] = remote;
                        res.PatronsAdded++;
                    }
                }

                // Merge Loans
                var loanMap = new Dictionary<string, Loan>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < db.Loans.Count; i++)
                {
                    var l = db.Loans[i];
                    if (!string.IsNullOrEmpty(l.id)) loanMap[l.id] = l;
                }

                foreach (var o in Json.Arr(root, "loans"))
                {
                    var d = o as Dictionary<string, object>;
                    if (d == null) continue;
                    string lid = Json.Str(d, "id");
                    if (string.IsNullOrEmpty(lid)) continue;

                    string rRet = Json.Str(d, "ret");
                    string rDue = Json.Str(d, "due");
                    int rRen = (int)Json.Num(d, "ren");

                    Loan local;
                    if (loanMap.TryGetValue(lid, out local))
                    {
                        bool loanChanged = false;
                        if (!string.IsNullOrEmpty(rRet) && string.IsNullOrEmpty(local.ret))
                        {
                            local.ret = rRet;
                            loanChanged = true;
                        }
                        if (rRen > local.ren)
                        {
                            local.ren = rRen;
                            local.due = rDue;
                            loanChanged = true;
                        }
                        if (loanChanged) res.LoansUpdated++;
                    }
                    else
                    {
                        db.Loans.Add(new Loan
                        {
                            id = lid,
                            iid = Json.Str(d, "iid"),
                            pid = Json.Str(d, "pid"),
                            @out = Json.Str(d, "out"),
                            due = rDue,
                            ret = string.IsNullOrEmpty(rRet) ? null : rRet,
                            ren = rRen
                        });
                        res.LoansUpdated++;
                    }
                }

                // Merge Holds
                var holdMap = new Dictionary<string, Hold>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < db.Holds.Count; i++)
                {
                    var h = db.Holds[i];
                    if (!string.IsNullOrEmpty(h.id)) holdMap[h.id] = h;
                }

                foreach (var o in Json.Arr(root, "holds"))
                {
                    var d = o as Dictionary<string, object>;
                    if (d == null) continue;
                    string hid = Json.Str(d, "id");
                    if (string.IsNullOrEmpty(hid)) continue;

                    Hold local;
                    if (holdMap.TryGetValue(hid, out local))
                    {
                        string rSt = Json.Str(d, "st");
                        if (!string.IsNullOrEmpty(rSt)) local.st = rSt;
                    }
                    else
                    {
                        db.Holds.Add(new Hold
                        {
                            id = hid,
                            iid = Json.Str(d, "iid"),
                            pid = Json.Str(d, "pid"),
                            date = Json.Str(d, "date"),
                            st = Json.Str(d, "st")
                        });
                    }
                }

                // Merge Fines
                var fineMap = new Dictionary<string, Fine>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < db.Fines.Count; i++)
                {
                    var f = db.Fines[i];
                    if (!string.IsNullOrEmpty(f.id)) fineMap[f.id] = f;
                }

                foreach (var o in Json.Arr(root, "fines"))
                {
                    var d = o as Dictionary<string, object>;
                    if (d == null) continue;
                    string fid = Json.Str(d, "id");
                    if (string.IsNullOrEmpty(fid)) continue;

                    Fine local;
                    if (fineMap.TryGetValue(fid, out local))
                    {
                        string rPaid = Json.Str(d, "paid");
                        if (!string.IsNullOrEmpty(rPaid) && string.IsNullOrEmpty(local.paid))
                        {
                            local.paid = rPaid;
                            res.FinesUpdated++;
                        }
                    }
                    else
                    {
                        db.Fines.Add(new Fine
                        {
                            id = fid,
                            pid = Json.Str(d, "pid"),
                            iid = Json.Str(d, "iid"),
                            loan = Json.Str(d, "loan"),
                            days = (int)Json.Num(d, "days"),
                            amt = Json.Num(d, "amt"),
                            date = Json.Str(d, "date"),
                            paid = Json.Str(d, "paid")
                        });
                        res.FinesUpdated++;
                    }
                }

                // Merge Class History
                var classHistMap = new Dictionary<string, ClassYearRecord>();
                for (int i = 0; i < db.ClassHistory.Count; i++)
                {
                    var ch = db.ClassHistory[i];
                    classHistMap[ch.year + ":" + ch.cls] = ch;
                }

                foreach (var o in Json.Arr(root, "classHistory"))
                {
                    var d = o as Dictionary<string, object>;
                    if (d == null) continue;
                    int y = (int)Json.Num(d, "year");
                    string c = Json.Str(d, "cls");
                    string key = y + ":" + c;

                    if (!classHistMap.ContainsKey(key))
                    {
                        var rec = new ClassYearRecord
                        {
                            year = y,
                            cls = c,
                            studentCount = (int)Json.Num(d, "studentCount"),
                            totalLoans = (int)Json.Num(d, "totalLoans")
                        };
                        foreach (var so in Json.Arr(d, "students"))
                        {
                            var sd = so as Dictionary<string, object>;
                            if (sd == null) continue;
                            rec.students.Add(new ClassYearStudent
                            {
                                id = Json.Str(sd, "id"),
                                name = Json.Str(sd, "name"),
                                loans = (int)Json.Num(sd, "loans")
                            });
                        }
                        db.ClassHistory.Add(rec);
                        classHistMap[key] = rec;
                    }
                }

                // Merge Archived Patrons
                var archMap = new Dictionary<string, ArchivedPatron>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < db.ArchivedPatrons.Count; i++)
                {
                    var ap = db.ArchivedPatrons[i];
                    if (!string.IsNullOrEmpty(ap.id)) archMap[ap.id] = ap;
                }

                foreach (var o in Json.Arr(root, "archivedPatrons"))
                {
                    var d = o as Dictionary<string, object>;
                    if (d == null) continue;
                    var ap = ArchivedPatron.FromJ(d);
                    if (!string.IsNullOrEmpty(ap.id) && !archMap.ContainsKey(ap.id))
                    {
                        db.ArchivedPatrons.Add(ap);
                        archMap[ap.id] = ap;
                    }
                }

                // Merge Settings
                var s = Json.Obj(root, "settings");
                if (s.ContainsKey("school") && string.IsNullOrEmpty(db.Set.school))
                    db.Set.school = Json.Str(s, "school");

                res.Success = true;
                res.Message = string.Format("Merged cloud records: {0} new students, {1} updated, {2} loans.",
                    res.PatronsAdded, res.PatronsUpdated, res.LoansUpdated);
                return res;
            }
            catch (Exception ex)
            {
                res.Success = false;
                res.Message = "JSON merge error: " + ex.Message;
                return res;
            }
        }

        /// <summary>
        /// HTTP POST to Google Apps Script Web App handling 302 redirects automatically.
        /// </summary>
        public static string PostToGoogleScript(string url, string jsonBody)
        {
            byte[] data = Encoding.UTF8.GetBytes(jsonBody);
            var req = (HttpWebRequest)WebRequest.Create(url);
            req.Method = "POST";
            req.ContentType = "application/json; charset=utf-8";
            req.ContentLength = data.Length;
            req.UserAgent = "ErrinILS/1.3";
            req.AllowAutoRedirect = false;
            req.Timeout = 30000;

            using (var st = req.GetRequestStream()) st.Write(data, 0, data.Length);

            try
            {
                using (var resp = (HttpWebResponse)req.GetResponse())
                {
                    int code = (int)resp.StatusCode;
                    if (code == 301 || code == 302 || code == 307)
                    {
                        string redirectUrl = resp.Headers["Location"];
                        if (!string.IsNullOrEmpty(redirectUrl))
                        {
                            var redReq = (HttpWebRequest)WebRequest.Create(redirectUrl);
                            redReq.Method = "GET";
                            redReq.UserAgent = "ErrinILS/1.3";
                            redReq.Timeout = 30000;
                            using (var redResp = (HttpWebResponse)redReq.GetResponse())
                            using (var rdr = new StreamReader(redResp.GetResponseStream(), Encoding.UTF8))
                            {
                                return rdr.ReadToEnd();
                            }
                        }
                    }

                    using (var rdr = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                    {
                        return rdr.ReadToEnd();
                    }
                }
            }
            catch (WebException wex)
            {
                if (wex.Response != null)
                {
                    var httpResp = wex.Response as HttpWebResponse;
                    if (httpResp != null && ((int)httpResp.StatusCode == 302 || (int)httpResp.StatusCode == 301 || (int)httpResp.StatusCode == 307))
                    {
                        string redirectUrl = httpResp.Headers["Location"];
                        if (!string.IsNullOrEmpty(redirectUrl))
                        {
                            var redReq = (HttpWebRequest)WebRequest.Create(redirectUrl);
                            redReq.Method = "GET";
                            redReq.UserAgent = "ErrinILS/1.3";
                            redReq.Timeout = 30000;
                            using (var redResp = (HttpWebResponse)redReq.GetResponse())
                            using (var rdr = new StreamReader(redResp.GetResponseStream(), Encoding.UTF8))
                            {
                                return rdr.ReadToEnd();
                            }
                        }
                    }
                }
                throw;
            }
        }

        /// <summary>
        /// HTTP GET from Google Apps Script Web App.
        /// </summary>
        public static string GetFromGoogleScript(string url)
        {
            var req = (HttpWebRequest)WebRequest.Create(url);
            req.Method = "GET";
            req.UserAgent = "ErrinILS/1.3";
            req.AllowAutoRedirect = true;
            req.Timeout = 30000;
            using (var resp = (HttpWebResponse)req.GetResponse())
            using (var rdr = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
            {
                return rdr.ReadToEnd();
            }
        }

        /// <summary>
        /// Synchronizes user records directly with Google Drive cloud via Apps Script Web App endpoint.
        /// </summary>
        public static SyncResult SyncViaCloudEndpoint(Db db, SyncConfig cfg)
        {
            var res = new SyncResult();
            if (string.IsNullOrEmpty(cfg.googleCloudEndpoint))
            {
                res.Success = false;
                res.Message = "No Cloud Endpoint URL configured";
                return res;
            }

            try
            {
                // 1. Download existing records from Google Drive cloud
                string getResp = null;
                try { getResp = GetFromGoogleScript(cfg.googleCloudEndpoint); }
                catch (Exception)
                {
                    // If endpoint is not reachable or first run, log warning
                    getResp = null;
                }

                if (!string.IsNullOrEmpty(getResp) && getResp.IndexOf("\"patrons\"", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    MergeUsersJson(db, getResp);
                }

                // 2. Build unified dataset and upload directly to Google Drive cloud
                string uploadJson = BuildUsersJson(db);
                string postResp = PostToGoogleScript(cfg.googleCloudEndpoint, uploadJson);

                res.Success = true;
                res.Message = string.Format("Direct cloud sync to Google Drive ({0}) complete! ({1} students, {2} loans)",
                    cfg.googleAccount, db.Patrons.Count, db.Loans.Count);
                return res;
            }
            catch (Exception ex)
            {
                res.Success = false;
                res.Message = "Google Drive cloud sync error: " + ex.Message;
                return res;
            }
        }

        /// <summary>
        /// Direct Google Drive REST API v3 Client methods (for OAuth 2.0 / Bearer tokens).
        /// </summary>
        public static class RestApi
        {
            public const string DriveApiBase = "https://www.googleapis.com/drive/v3/files";
            public const string DriveUploadBase = "https://www.googleapis.com/upload/drive/v3/files";
            public const string TokenEndpoint = "https://oauth2.googleapis.com/token";

            public static string RefreshToken(SyncConfig cfg, out string err)
            {
                err = null;
                if (string.IsNullOrEmpty(cfg.googleRefreshToken))
                {
                    err = "No refresh token";
                    return null;
                }
                try
                {
                    var req = (HttpWebRequest)WebRequest.Create(TokenEndpoint);
                    req.Method = "POST";
                    req.ContentType = "application/x-www-form-urlencoded";
                    req.UserAgent = "ErrinILS/1.3";
                    string data = string.Format("client_id={0}&client_secret={1}&refresh_token={2}&grant_type=refresh_token",
                        Uri.EscapeDataString(cfg.googleClientId ?? ""),
                        Uri.EscapeDataString(cfg.googleClientSecret ?? ""),
                        Uri.EscapeDataString(cfg.googleRefreshToken));
                    byte[] bytes = Encoding.UTF8.GetBytes(data);
                    req.ContentLength = bytes.Length;
                    using (var st = req.GetRequestStream()) st.Write(bytes, 0, bytes.Length);
                    using (var resp = (HttpWebResponse)req.GetResponse())
                    using (var rdr = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                    {
                        var d = Json.Parse(rdr.ReadToEnd()) as Dictionary<string, object>;
                        if (d != null && d.ContainsKey("access_token"))
                        {
                            cfg.googleAccessToken = Json.Str(d, "access_token");
                            return cfg.googleAccessToken;
                        }
                    }
                }
                catch (Exception ex) { err = ex.Message; }
                return null;
            }

            public static string FindOrCreateFolder(string folderName, string token)
            {
                string q = Uri.EscapeDataString(string.Format("name='{0}' and mimeType='application/vnd.google-apps.folder' and trashed=false", folderName));
                string url = DriveApiBase + "?q=" + q + "&fields=files(id,name)";
                var req = (HttpWebRequest)WebRequest.Create(url);
                req.Method = "GET";
                req.Headers["Authorization"] = "Bearer " + token;
                req.UserAgent = "ErrinILS/1.3";
                using (var resp = (HttpWebResponse)req.GetResponse())
                using (var rdr = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                {
                    var root = Json.Parse(rdr.ReadToEnd()) as Dictionary<string, object>;
                    var files = Json.Arr(root, "files");
                    if (files.Count > 0)
                    {
                        var f = files[0] as Dictionary<string, object>;
                        return Json.Str(f, "id");
                    }
                }

                // Create folder
                var crReq = (HttpWebRequest)WebRequest.Create(DriveApiBase);
                crReq.Method = "POST";
                crReq.Headers["Authorization"] = "Bearer " + token;
                crReq.ContentType = "application/json; charset=utf-8";
                crReq.UserAgent = "ErrinILS/1.3";
                string body = string.Format("{{\"name\":\"{0}\",\"mimeType\":\"application/vnd.google-apps.folder\"}}", folderName);
                byte[] bytes = Encoding.UTF8.GetBytes(body);
                crReq.ContentLength = bytes.Length;
                using (var st = crReq.GetRequestStream()) st.Write(bytes, 0, bytes.Length);
                using (var resp = (HttpWebResponse)crReq.GetResponse())
                using (var rdr = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                {
                    var root = Json.Parse(rdr.ReadToEnd()) as Dictionary<string, object>;
                    return Json.Str(root, "id");
                }
            }

            public static string FindFile(string folderId, string fileName, string token)
            {
                string q = Uri.EscapeDataString(string.Format("'{0}' in parents and name='{1}' and trashed=false", folderId, fileName));
                string url = DriveApiBase + "?q=" + q + "&fields=files(id,name)";
                var req = (HttpWebRequest)WebRequest.Create(url);
                req.Method = "GET";
                req.Headers["Authorization"] = "Bearer " + token;
                req.UserAgent = "ErrinILS/1.3";
                using (var resp = (HttpWebResponse)req.GetResponse())
                using (var rdr = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                {
                    var root = Json.Parse(rdr.ReadToEnd()) as Dictionary<string, object>;
                    var files = Json.Arr(root, "files");
                    if (files.Count > 0)
                    {
                        var f = files[0] as Dictionary<string, object>;
                        return Json.Str(f, "id");
                    }
                }
                return null;
            }

            public static string DownloadFile(string fileId, string token)
            {
                string url = DriveApiBase + "/" + fileId + "?alt=media";
                var req = (HttpWebRequest)WebRequest.Create(url);
                req.Method = "GET";
                req.Headers["Authorization"] = "Bearer " + token;
                req.UserAgent = "ErrinILS/1.3";
                using (var resp = (HttpWebResponse)req.GetResponse())
                using (var rdr = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                {
                    return rdr.ReadToEnd();
                }
            }

            public static bool UploadFile(string folderId, string fileName, string content, string existingFileId, string token, out string newFileId)
            {
                newFileId = existingFileId;
                byte[] bytes = Encoding.UTF8.GetBytes(content);

                if (!string.IsNullOrEmpty(existingFileId))
                {
                    string url = DriveUploadBase + "/" + existingFileId + "?uploadType=media";
                    var req = (HttpWebRequest)WebRequest.Create(url);
                    req.Method = "PATCH";
                    req.Headers["Authorization"] = "Bearer " + token;
                    req.ContentType = "application/json; charset=utf-8";
                    req.ContentLength = bytes.Length;
                    req.UserAgent = "ErrinILS/1.3";
                    using (var st = req.GetRequestStream()) st.Write(bytes, 0, bytes.Length);
                    using (var resp = (HttpWebResponse)req.GetResponse()) { return resp.StatusCode == HttpStatusCode.OK; }
                }
                else
                {
                    var req = (HttpWebRequest)WebRequest.Create(DriveApiBase);
                    req.Method = "POST";
                    req.Headers["Authorization"] = "Bearer " + token;
                    req.ContentType = "application/json; charset=utf-8";
                    req.UserAgent = "ErrinILS/1.3";
                    string meta = string.Format("{{\"name\":\"{0}\",\"parents\":[\"{1}\"]}}", fileName, folderId);
                    byte[] metaBytes = Encoding.UTF8.GetBytes(meta);
                    req.ContentLength = metaBytes.Length;
                    using (var st = req.GetRequestStream()) st.Write(metaBytes, 0, metaBytes.Length);
                    using (var resp = (HttpWebResponse)req.GetResponse())
                    using (var rdr = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                    {
                        var root = Json.Parse(rdr.ReadToEnd()) as Dictionary<string, object>;
                        newFileId = Json.Str(root, "id");
                    }

                    if (!string.IsNullOrEmpty(newFileId))
                    {
                        string url = DriveUploadBase + "/" + newFileId + "?uploadType=media";
                        var upReq = (HttpWebRequest)WebRequest.Create(url);
                        upReq.Method = "PATCH";
                        upReq.Headers["Authorization"] = "Bearer " + token;
                        upReq.ContentType = "application/json; charset=utf-8";
                        upReq.ContentLength = bytes.Length;
                        upReq.UserAgent = "ErrinILS/1.3";
                        using (var st = upReq.GetRequestStream()) st.Write(bytes, 0, bytes.Length);
                        using (var resp = (HttpWebResponse)upReq.GetResponse()) { return resp.StatusCode == HttpStatusCode.OK; }
                    }
                }
                return false;
            }
        }

        /// <summary>
        /// Synchronizes user records directly with Google Drive cloud via REST API v3 (OAuth / Token).
        /// </summary>
        public static SyncResult SyncViaRestApi(Db db, SyncConfig cfg)
        {
            var res = new SyncResult();
            string token = cfg.googleAccessToken;

            // Refresh token if needed
            if (string.IsNullOrEmpty(token) && !string.IsNullOrEmpty(cfg.googleRefreshToken))
            {
                string rErr;
                token = RestApi.RefreshToken(cfg, out rErr);
            }

            if (string.IsNullOrEmpty(token))
            {
                res.Success = false;
                res.Message = "No valid Google Drive access token for " + cfg.googleAccount;
                return res;
            }

            try
            {
                // 1. Find or create cloud folder
                string folderId = cfg.googleFolderId;
                if (string.IsNullOrEmpty(folderId))
                {
                    folderId = RestApi.FindOrCreateFolder(DefaultCloudFolderName, token);
                    cfg.googleFolderId = folderId;
                }

                if (string.IsNullOrEmpty(folderId))
                {
                    res.Success = false;
                    res.Message = "Could not find or create Google Drive cloud folder: " + DefaultCloudFolderName;
                    return res;
                }

                // 2. Find and download users_data.json
                string fileId = cfg.googleFileId;
                if (string.IsNullOrEmpty(fileId))
                {
                    fileId = RestApi.FindFile(folderId, UsersJsonName, token);
                    cfg.googleFileId = fileId;
                }

                if (!string.IsNullOrEmpty(fileId))
                {
                    try
                    {
                        string json = RestApi.DownloadFile(fileId, token);
                        if (!string.IsNullOrEmpty(json)) MergeUsersJson(db, json);
                    }
                    catch { }
                }

                // 3. Upload unified JSON
                string content = BuildUsersJson(db);
                string newId;
                bool ok = RestApi.UploadFile(folderId, UsersJsonName, content, fileId, token, out newId);
                cfg.googleFileId = newId;

                res.Success = ok;
                res.Message = ok ? string.Format("Direct cloud sync to Google Drive ({0}) active: {1} students, {2} loans.", cfg.googleAccount, db.Patrons.Count, db.Loans.Count)
                                : "Upload to Google Drive cloud failed";
                return res;
            }
            catch (Exception ex)
            {
                res.Success = false;
                res.Message = "REST API error: " + ex.Message;
                return res;
            }
        }

        /// <summary>
        /// High-level sync entry point for user records:
        /// Directly synchronizes with Google Drive cloud over HTTPS.
        /// By default, every Google Drive sync is automatically followed by GitHub catalog sync.
        /// </summary>
        public static SyncResult Sync(Db db, SyncConfig cfg)
        {
            return Sync(db, cfg, true);
        }

        public static SyncResult Sync(Db db, SyncConfig cfg, bool followWithGitHub)
        {
            if (cfg == null) cfg = new SyncConfig();
            SyncResult res;

            // 1. Check Google Apps Script Web App direct endpoint (Preferred zero-dependency direct cloud method)
            if (!string.IsNullOrEmpty(cfg.googleCloudEndpoint))
            {
                res = SyncViaCloudEndpoint(db, cfg);
            }
            // 2. Check direct REST API v3 with OAuth / Token
            else if (!string.IsNullOrEmpty(cfg.googleRefreshToken) || !string.IsNullOrEmpty(cfg.googleAccessToken))
            {
                res = SyncViaRestApi(db, cfg);
            }
            // 3. Fallback to local drive sync if configured
            else if (!string.IsNullOrEmpty(cfg.gdriveFolder) && Directory.Exists(cfg.gdriveFolder))
            {
                res = GoogleDriveSync.Sync(db, cfg.gdriveFolder);
            }
            // 4. If nothing configured yet, return clear setup instructions
            else
            {
                res = new SyncResult();
                res.Success = false;
                res.Message = string.Format("Google Drive Cloud ({0}) needs setup. In Settings, click 'Configure Google Cloud' to connect.", cfg.googleAccount);
                return res;
            }

            // Every Google Drive sync is also followed by GitHub sync
            if (followWithGitHub && cfg != null && !string.IsNullOrEmpty(cfg.githubRepo))
            {
                try
                {
                    string schoolName = db != null && db.Set != null ? db.Set.school : null;
                    var catRes = GitHubCatalogSync.Sync(db, cfg.githubRepo, cfg.githubToken, schoolName);
                    if (catRes != null)
                    {
                        res.ItemsAdded = catRes.ItemsAdded;
                        res.ItemsUpdated = catRes.ItemsUpdated;
                        if (catRes.Success)
                        {
                            res.Message += " + GitHub synced";
                        }
                    }
                }
                catch { }
            }

            return res;
        }
    }

    /// <summary>
    /// File-based Google Drive sync helper for offline caching or local folders.
    /// </summary>
    public static class GoogleDriveSync
    {
        public const string DefaultSubDir = "ErrinILS\\sk-taman-ria";
        public const string UsersJsonName = "users_data.json";
        public const string UsersDbName = "users_data.db";

        public static string DetectGoogleDriveRoot()
        {
            string[] driveLetters = new[] { "G:\\", "H:\\", "I:\\", "D:\\", "F:\\", "E:\\" };
            for (int i = 0; i < driveLetters.Length; i++)
            {
                string dl = driveLetters[i];
                try
                {
                    string myDrive = Path.Combine(dl, "My Drive");
                    if (Directory.Exists(myDrive)) return myDrive;
                    if (Directory.Exists(dl) && (Directory.Exists(Path.Combine(dl, "Shared drives")) || File.Exists(Path.Combine(dl, ".tmp.drive"))))
                        return dl;
                }
                catch { }
            }

            try
            {
                string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                string p1 = Path.Combine(userProfile, "Google Drive\\My Drive");
                if (Directory.Exists(p1)) return p1;
                string p2 = Path.Combine(userProfile, "Google Drive");
                if (Directory.Exists(p2)) return p2;
            }
            catch { }

            return null;
        }

        public static string GetSyncFolder(string customPath = null)
        {
            if (!string.IsNullOrEmpty(customPath) && Directory.Exists(customPath))
            {
                if (File.Exists(Path.Combine(customPath, UsersJsonName)) ||
                    customPath.IndexOf("ErrinILS", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    customPath.IndexOf("sk-taman-ria", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return customPath;
                }
                string customSub = Path.Combine(customPath, DefaultSubDir);
                try { Directory.CreateDirectory(customSub); } catch { }
                return customSub;
            }

            string root = DetectGoogleDriveRoot();
            if (string.IsNullOrEmpty(root)) return null;

            string full = Path.Combine(root, DefaultSubDir);
            try { Directory.CreateDirectory(full); } catch { }
            return full;
        }

        public static bool ExportUsers(Db db, string folder, out string err)
        {
            err = null;
            if (db == null || string.IsNullOrEmpty(folder)) { err = "No database or target folder"; return false; }

            try
            {
                Directory.CreateDirectory(folder);
                string jsonFile = Path.Combine(folder, UsersJsonName);
                string tmpFile = jsonFile + ".tmp";
                string jsonContent = GoogleDriveCloudSync.BuildUsersJson(db);
                File.WriteAllText(tmpFile, jsonContent, new UTF8Encoding(false));
                if (File.Exists(jsonFile))
                {
                    try { File.Copy(jsonFile, jsonFile + ".bak", true); } catch { }
                    File.Delete(jsonFile);
                }
                File.Move(tmpFile, jsonFile);

                if (SqliteNative.IsAvailable)
                {
                    string dbFile = Path.Combine(folder, UsersDbName);
                    try
                    {
                        var usersOnlyDb = new Db();
                        usersOnlyDb.Patrons = new List<Patron>(db.Patrons);
                        usersOnlyDb.Loans = new List<Loan>(db.Loans);
                        usersOnlyDb.Holds = new List<Hold>(db.Holds);
                        usersOnlyDb.Fines = new List<Fine>(db.Fines);
                        usersOnlyDb.ClassHistory = new List<ClassYearRecord>(db.ClassHistory);
                        usersOnlyDb.ArchivedPatrons = new List<ArchivedPatron>(db.ArchivedPatrons);
                        usersOnlyDb.Set = db.Set;
                        SqliteStore.Save(dbFile, usersOnlyDb);
                    }
                    catch { }
                }

                return true;
            }
            catch (Exception ex)
            {
                err = ex.Message;
                return false;
            }
        }

        public static SyncResult ImportUsers(Db db, string folder)
        {
            var res = new SyncResult();
            if (db == null || string.IsNullOrEmpty(folder))
            {
                res.Success = false;
                res.Message = "Target folder not found";
                return res;
            }

            string jsonFile = Path.Combine(folder, UsersJsonName);
            if (!File.Exists(jsonFile))
            {
                res.Success = true;
                res.Message = "No remote user records found yet";
                return res;
            }

            try
            {
                string json = File.ReadAllText(jsonFile, Encoding.UTF8);
                return GoogleDriveCloudSync.MergeUsersJson(db, json);
            }
            catch (Exception ex)
            {
                res.Success = false;
                res.Message = "Error reading users: " + ex.Message;
                return res;
            }
        }

        public static SyncResult Sync(Db db, string customFolder = null)
        {
            string folder = GetSyncFolder(customFolder);
            if (string.IsNullOrEmpty(folder))
            {
                var r = new SyncResult();
                r.Success = false;
                r.Message = "Google Drive folder not found";
                return r;
            }

            var res = ImportUsers(db, folder);
            if (!res.Success) return res;

            string err;
            if (!ExportUsers(db, folder, out err))
            {
                res.Success = false;
                res.Message = "Failed to write user records: " + err;
                return res;
            }

            res.Success = true;
            res.Message = string.Format("Google Drive in sync ({0} students, {1} loans, {2} fines).",
                db.Patrons.Count, db.Loans.Count, db.Fines.Count);
            return res;
        }
    }

    /// <summary>
    /// Synchronizes books and catalog data via GitHub repository (pustaka-terbuka/sk-taman-ria).
    /// Uses native Git CLI if available, with pure .NET 4.0 HTTP REST API fallback.
    /// </summary>
    public static class GitHubCatalogSync
    {
        public const string DefaultOrg = "pustaka-terbuka";
        public const string DefaultRepo = "sk-taman-ria";
        public const string DefaultRemoteUrl = "https://github.com/pustaka-terbuka/sk-taman-ria.git";

        public static string GetRepoDir(string repo)
        {
            if (string.IsNullOrEmpty(repo)) repo = DefaultRepo;
            string safeName = repo;
            int idx = safeName.LastIndexOf('/');
            if (idx >= 0) safeName = safeName.Substring(idx + 1);
            return Path.Combine(Store.ActiveReposDir, safeName);
        }

        public static string GetDefaultRepoDir()
        {
            return GetRepoDir(DefaultRepo);
        }

        public static string FindGitExe()
        {
            string pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
            string[] paths = pathEnv.Split(';');
            for (int i = 0; i < paths.Length; i++)
            {
                string p = paths[i].Trim();
                if (string.IsNullOrEmpty(p)) continue;
                string exe = Path.Combine(p, "git.exe");
                if (File.Exists(exe)) return exe;
            }

            string[] standardPaths = new[]
            {
                @"C:\Program Files\Git\cmd\git.exe",
                @"C:\Program Files\Git\bin\git.exe",
                @"C:\Program Files (x86)\Git\cmd\git.exe",
                @"C:\Program Files (x86)\Git\bin\git.exe",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Programs\Git\cmd\git.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Programs\Git\bin\git.exe")
            };

            for (int i = 0; i < standardPaths.Length; i++)
            {
                if (File.Exists(standardPaths[i])) return standardPaths[i];
            }

            return null;
        }

        public static bool HasGit()
        {
            return FindGitExe() != null;
        }

        public static string RunGit(string args, string workingDir, out int exitCode)
        {
            string gitExe = FindGitExe();
            if (string.IsNullOrEmpty(gitExe))
            {
                exitCode = -1;
                return "git.exe not found";
            }

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = gitExe,
                    Arguments = args,
                    WorkingDirectory = Directory.Exists(workingDir) ? workingDir : Environment.CurrentDirectory,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using (var proc = Process.Start(psi))
                {
                    string output = proc.StandardOutput.ReadToEnd();
                    string error = proc.StandardError.ReadToEnd();
                    if (!proc.WaitForExit(30000))
                    {
                        proc.Kill();
                        exitCode = -2;
                        return "Git command timed out: " + args;
                    }

                    exitCode = proc.ExitCode;
                    if (exitCode != 0 && !string.IsNullOrEmpty(error))
                        return error.Trim();
                    return output.Trim();
                }
            }
            catch (Exception ex)
            {
                exitCode = -3;
                return ex.Message;
            }
        }

        public static string DeriveOrgCode(string schoolName, string repoName)
        {
            if (!string.IsNullOrEmpty(schoolName))
            {
                string[] words = schoolName.Split(new[] { ' ', '-', '_' }, StringSplitOptions.RemoveEmptyEntries);
                var sb = new StringBuilder();
                for (int i = 0; i < words.Length; i++)
                {
                    string w = words[i].Trim();
                    if (w.Length > 0 && char.IsLetterOrDigit(w[0]))
                    {
                        sb.Append(char.ToUpperInvariant(w[0]));
                    }
                }
                if (sb.Length >= 2 && sb.Length <= 10) return sb.ToString();
            }
            if (!string.IsNullOrEmpty(repoName))
            {
                string clean = Regex.Replace(repoName, "[^a-zA-Z0-9]", "").ToUpperInvariant();
                if (clean.Length > 10) clean = clean.Substring(0, 10);
                if (clean.Length >= 2) return clean;
            }
            return "SKTR";
        }

        public static string GenerateCrockfordId(string seed)
        {
            const string alphabet = "0123456789abcdefghjkmnpqrstvwxyz";
            using (var sha = System.Security.Cryptography.SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(seed ?? ""));
                var sb = new StringBuilder(10);
                for (int i = 0; i < 10; i++)
                {
                    sb.Append(alphabet[hash[i] % 32]);
                }
                return sb.ToString();
            }
        }

        public static string NormalizeFallbackString(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            s = s.Trim().ToLowerInvariant();
            s = Regex.Replace(s, @"\s+", " ");
            s = Regex.Replace(s, @"[\/;:,\.=]+\s*$", "").Trim();
            return s;
        }

        public static string NormalizeFallbackTitle(string title)
        {
            string s = NormalizeFallbackString(title);
            string[] articles = new[] { "the", "a", "an", "sang", "si", "para" };
            int sp = s.IndexOf(' ');
            if (sp > 0)
            {
                string firstWord = s.Substring(0, sp);
                for (int a = 0; a < articles.Length; a++)
                {
                    if (firstWord == articles[a])
                    {
                        s = s.Substring(sp + 1).Trim();
                        break;
                    }
                }
            }
            return s;
        }

        public static string GenerateFallbackKey(Item item)
        {
            string title = NormalizeFallbackTitle(item.title + (string.IsNullOrEmpty(item.sub) ? "" : " " + item.sub));
            string author = NormalizeFallbackString(item.author);
            string publisher = NormalizeFallbackString(item.publisher);
            string year = "";
            if (!string.IsNullOrEmpty(item.year))
            {
                var ym = Regex.Match(item.year, @"\d{4}");
                if (ym.Success) year = ym.Value;
            }
            return string.Format("{0}|{1}|{2}|{3}", title, author, publisher, year);
        }

        public static string XmlEscape(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;").Replace("'", "&apos;");
        }

        public static string ComputeSha256(string content)
        {
            using (var sha = System.Security.Cryptography.SHA256.Create())
            {
                byte[] b = sha.ComputeHash(Encoding.UTF8.GetBytes(content ?? ""));
                var sb = new StringBuilder(b.Length * 2);
                for (int i = 0; i < b.Length; i++) sb.Append(b[i].ToString("x2"));
                return sb.ToString();
            }
        }

        public static string BuildSingleRecordXml(Item i, string orgCode, string sourceCode, string timestampStr)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
            sb.AppendLine("<marc:collection xmlns:marc=\"http://www.loc.gov/MARC21/slim\">");
            sb.Append(BuildRecordXmlNode(i, orgCode, sourceCode, timestampStr));
            sb.AppendLine("</marc:collection>");
            return sb.ToString();
        }

        public static string BuildRecordXmlNode(Item i, string orgCode, string sourceCode, string timestampStr)
        {
            var sb = new StringBuilder();
            sb.AppendLine("  <marc:record>");
            sb.AppendLine("    <marc:leader>00000nam a2200000 a 4500</marc:leader>");
            
            string barcode = !string.IsNullOrEmpty(i.barcode) ? i.barcode : (!string.IsNullOrEmpty(i.ctl) ? i.ctl : (i.id ?? "1"));
            sb.Append("    <marc:controlfield tag=\"001\">").Append(XmlEscape(barcode)).AppendLine("</marc:controlfield>");
            sb.Append("    <marc:controlfield tag=\"003\">").Append(XmlEscape(orgCode)).AppendLine("</marc:controlfield>");
            sb.Append("    <marc:controlfield tag=\"005\">").Append(timestampStr).AppendLine("</marc:controlfield>");

            string yr = "    ";
            if (!string.IsNullOrEmpty(i.year))
            {
                var m = Regex.Match(i.year, @"\d{4}");
                if (m.Success) yr = m.Value;
            }
            string f008 = DateTime.UtcNow.ToString("yyMMdd") + "s" + yr.PadRight(4) + "    my            000 0 may d";
            sb.Append("    <marc:controlfield tag=\"008\">").Append(f008).AppendLine("</marc:controlfield>");

            // 020 ISBN
            string cleanIsbn = Lookup.NormalizeIsbn(i.isbn);
            if (!string.IsNullOrEmpty(cleanIsbn))
            {
                sb.AppendLine("    <marc:datafield tag=\"020\" ind1=\" \" ind2=\" \">");
                sb.Append("      <marc:subfield code=\"a\">").Append(XmlEscape(cleanIsbn)).AppendLine("</marc:subfield>");
                sb.AppendLine("    </marc:datafield>");
            }

            // 035 Identifiers
            sb.AppendLine("    <marc:datafield tag=\"035\" ind1=\" \" ind2=\" \">");
            sb.Append("      <marc:subfield code=\"a\">(").Append(XmlEscape(orgCode)).Append(")").Append(XmlEscape(barcode)).AppendLine("</marc:subfield>");
            sb.AppendLine("    </marc:datafield>");
            sb.AppendLine("    <marc:datafield tag=\"035\" ind1=\" \" ind2=\" \">");
            sb.Append("      <marc:subfield code=\"a\">").Append(XmlEscape(orgCode)).Append("-").Append(XmlEscape(barcode)).AppendLine("</marc:subfield>");
            sb.AppendLine("    </marc:datafield>");

            // 040 Cataloging agency
            sb.AppendLine("    <marc:datafield tag=\"040\" ind1=\" \" ind2=\" \">");
            sb.Append("      <marc:subfield code=\"c\">").Append(XmlEscape(orgCode)).AppendLine("</marc:subfield>");
            sb.AppendLine("    </marc:datafield>");

            // 082 Dewey Call Number
            if (!string.IsNullOrEmpty(i.call))
            {
                sb.AppendLine("    <marc:datafield tag=\"082\" ind1=\"0\" ind2=\"4\">");
                sb.Append("      <marc:subfield code=\"a\">").Append(XmlEscape(i.call)).AppendLine("</marc:subfield>");
                sb.AppendLine("    </marc:datafield>");
            }

            // 100 Author
            if (!string.IsNullOrEmpty(i.author))
            {
                sb.AppendLine("    <marc:datafield tag=\"100\" ind1=\"1\" ind2=\" \">");
                sb.Append("      <marc:subfield code=\"a\">").Append(XmlEscape(i.author)).AppendLine("</marc:subfield>");
                sb.AppendLine("    </marc:datafield>");
            }

            // 245 Title (Mandatory)
            string ind1 = !string.IsNullOrEmpty(i.author) ? "1" : "0";
            sb.Append("    <marc:datafield tag=\"245\" ind1=\"").Append(ind1).AppendLine("\" ind2=\"0\">");
            sb.Append("      <marc:subfield code=\"a\">").Append(XmlEscape(!string.IsNullOrEmpty(i.title) ? i.title : "Untitled")).AppendLine("</marc:subfield>");
            if (!string.IsNullOrEmpty(i.sub))
            {
                sb.Append("      <marc:subfield code=\"b\">").Append(XmlEscape(i.sub)).AppendLine("</marc:subfield>");
            }
            if (!string.IsNullOrEmpty(i.author))
            {
                sb.Append("      <marc:subfield code=\"c\">").Append(XmlEscape(i.author)).AppendLine("</marc:subfield>");
            }
            sb.AppendLine("    </marc:datafield>");

            // 260 Imprint
            if (!string.IsNullOrEmpty(i.publisher) || !string.IsNullOrEmpty(i.year) || !string.IsNullOrEmpty(i.place))
            {
                sb.AppendLine("    <marc:datafield tag=\"260\" ind1=\" \" ind2=\" \">");
                if (!string.IsNullOrEmpty(i.place))
                    sb.Append("      <marc:subfield code=\"a\">").Append(XmlEscape(i.place)).AppendLine("</marc:subfield>");
                if (!string.IsNullOrEmpty(i.publisher))
                    sb.Append("      <marc:subfield code=\"b\">").Append(XmlEscape(i.publisher)).AppendLine("</marc:subfield>");
                if (!string.IsNullOrEmpty(i.year))
                    sb.Append("      <marc:subfield code=\"c\">").Append(XmlEscape(i.year)).AppendLine("</marc:subfield>");
                sb.AppendLine("    </marc:datafield>");
            }

            // 300 Physical description
            if (!string.IsNullOrEmpty(i.pages))
            {
                sb.AppendLine("    <marc:datafield tag=\"300\" ind1=\" \" ind2=\" \">");
                sb.Append("      <marc:subfield code=\"a\">").Append(XmlEscape(i.pages)).Append(" p.").AppendLine("</marc:subfield>");
                sb.AppendLine("    </marc:datafield>");
            }

            // 650 Subject
            if (!string.IsNullOrEmpty(i.subject))
            {
                sb.AppendLine("    <marc:datafield tag=\"650\" ind1=\" \" ind2=\"0\">");
                sb.Append("      <marc:subfield code=\"a\">").Append(XmlEscape(i.subject)).AppendLine("</marc:subfield>");
                sb.AppendLine("    </marc:datafield>");
            }

            // 852 Holdings
            sb.AppendLine("    <marc:datafield tag=\"852\" ind1=\" \" ind2=\" \">");
            sb.Append("      <marc:subfield code=\"b\">").Append(XmlEscape(orgCode)).AppendLine("</marc:subfield>");
            if (!string.IsNullOrEmpty(i.call))
                sb.Append("      <marc:subfield code=\"h\">").Append(XmlEscape(i.call)).AppendLine("</marc:subfield>");
            sb.Append("      <marc:subfield code=\"p\">").Append(XmlEscape(barcode)).AppendLine("</marc:subfield>");
            sb.AppendLine("    </marc:datafield>");

            // 942 Koha DDC & Type
            sb.AppendLine("    <marc:datafield tag=\"942\" ind1=\" \" ind2=\" \">");
            sb.AppendLine("      <marc:subfield code=\"2\">ddc</marc:subfield>");
            sb.AppendLine("      <marc:subfield code=\"c\">BK</marc:subfield>");
            sb.AppendLine("    </marc:datafield>");

            // 952 Koha item details
            sb.AppendLine("    <marc:datafield tag=\"952\" ind1=\" \" ind2=\" \">");
            sb.Append("      <marc:subfield code=\"a\">").Append(XmlEscape(orgCode)).AppendLine("</marc:subfield>");
            sb.Append("      <marc:subfield code=\"b\">").Append(XmlEscape(orgCode)).AppendLine("</marc:subfield>");
            if (!string.IsNullOrEmpty(i.call))
                sb.Append("      <marc:subfield code=\"o\">").Append(XmlEscape(i.call)).AppendLine("</marc:subfield>");
            sb.Append("      <marc:subfield code=\"p\">").Append(XmlEscape(barcode)).AppendLine("</marc:subfield>");
            sb.AppendLine("      <marc:subfield code=\"y\">BK</marc:subfield>");
            sb.AppendLine("    </marc:datafield>");

            // 996 Source tracking
            sb.AppendLine("    <marc:datafield tag=\"996\" ind1=\" \" ind2=\" \">");
            sb.Append("      <marc:subfield code=\"a\">").Append(XmlEscape(sourceCode)).AppendLine("</marc:subfield>");
            sb.AppendLine("    </marc:datafield>");

            sb.AppendLine("  </marc:record>");
            return sb.ToString();
        }

        public static void EnsurePustakaTools(string targetDir, string sourceCode, string schoolName, string orgCode)
        {
            try
            {
                string toolsDir = Path.Combine(targetDir, "tools");
                string testsDir = Path.Combine(targetDir, "tests");
                Directory.CreateDirectory(toolsDir);
                Directory.CreateDirectory(testsDir);

                // Copy from temp ppapp clone if present
                string tempClone = Path.Combine(Path.GetTempPath(), "ppapp-clone");
                if (Directory.Exists(tempClone))
                {
                    string srcTools = Path.Combine(tempClone, "tools");
                    if (Directory.Exists(srcTools))
                    {
                        CopyDirectoryRecursive(srcTools, toolsDir);
                    }
                }

                // Write harvester stub for this school
                string harvDir = Path.Combine(toolsDir, "harvesters\\" + sourceCode);
                Directory.CreateDirectory(harvDir);
                string harvJs = string.Format(@"#!/usr/bin/env node
import fs from 'fs';
import path from 'path';
import {{ fileURLToPath }} from 'url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

export const SOURCE_CODE = '{0}';
export const SOURCE_NAME = '{1}';
export const ORG_CODE = '{2}';
export const SOURCE_BASE_URL = 'https://pustaka-terbuka.github.io/{0}/';
export const HARVESTER_NAME = 'errin-ils-harvester';
export const HARVESTER_VERSION = '1.0.0';

export function parseArgs(args = process.argv.slice(2)) {{
  const opts = {{ maxRecords: null, runId: null }};
  for (let i = 0; i < args.length; i++) {{
    if (args[i] === '--max-records') opts.maxRecords = parseInt(args[++i], 10);
    else if (args[i] === '--run-id') opts.runId = args[++i];
  }}
  return opts;
}}
", sourceCode, schoolName, orgCode);
                File.WriteAllText(Path.Combine(harvDir, "harvester.js"), harvJs, new UTF8Encoding(false));

                // Write test file
                string testJs = string.Format(@"import {{ test }} from 'node:test';
import assert from 'node:assert';
import path from 'path';
import fs from 'fs';
import {{ SOURCE_CODE, ORG_CODE, parseArgs }} from '../tools/harvesters/{0}/harvester.js';
import {{ validateInbox }} from '../tools/validate-inbox.js';

test('{1} Harvester constants and argument parsing', () => {{
  assert.strictEqual(SOURCE_CODE, '{0}');
  assert.strictEqual(ORG_CODE, '{2}');
  const opts = parseArgs(['--max-records', '10', '--run-id', 'test-01']);
  assert.strictEqual(opts.maxRecords, 10);
  assert.strictEqual(opts.runId, 'test-01');
}});

test('{1} inbox passes validation', async () => {{
  const inboxBase = path.resolve('harvest/inbox/{0}');
  if (fs.existsSync(inboxBase)) {{
    const runs = fs.readdirSync(inboxBase);
    if (runs.length > 0) {{
      const latestRun = path.join(inboxBase, runs[runs.length - 1]);
      const res = await validateInbox(latestRun, {{ skipDropGate: true }});
      assert.strictEqual(res.valid, true, `Validation failed: ${{res.errors ? res.errors.join(', ') : ''}}`);
    }}
  }}
}});
", sourceCode, schoolName, orgCode);
                File.WriteAllText(Path.Combine(testsDir, "harvester-" + sourceCode + ".test.js"), testJs, new UTF8Encoding(false));
            }
            catch { }
        }

        static void CopyDirectoryRecursive(string source, string target)
        {
            if (!Directory.Exists(source)) return;
            Directory.CreateDirectory(target);
            foreach (string file in Directory.GetFiles(source))
            {
                string destFile = Path.Combine(target, Path.GetFileName(file));
                if (!File.Exists(destFile))
                {
                    try { File.Copy(file, destFile, true); } catch { }
                }
            }
            foreach (string dir in Directory.GetDirectories(source))
            {
                CopyDirectoryRecursive(dir, Path.Combine(target, Path.GetFileName(dir)));
            }
        }

        public static void GenerateCatalogFiles(IEnumerable<Item> items, string targetDir, string schoolName)
        {
            Directory.CreateDirectory(targetDir);
            var itemList = items.OrderBy(i => i.barcode).ThenBy(i => i.ctl).ToList();
            var u8 = new UTF8Encoding(false);
            var u8Bom = new UTF8Encoding(true);

            // 1. Root convenience files
            // 1a. catalog.json
            var jsonItems = itemList.Select(i => (object)i.ToJ()).ToList();
            string jsonText = Json.WritePretty(jsonItems);
            File.WriteAllText(Path.Combine(targetDir, "catalog.json"), jsonText, u8);

            // 1b. holdings.csv
            byte[] csvBytes = u8Bom.GetPreamble().Concat(u8.GetBytes(Lib.ToCsv(itemList))).ToArray();
            File.WriteAllBytes(Path.Combine(targetDir, "holdings.csv"), csvBytes);

            // 1c. catalog.mrc
            byte[] mrcBytes = Marc.ToMrcBytes(itemList);
            File.WriteAllBytes(Path.Combine(targetDir, "catalog.mrc"), mrcBytes);

            // 1d. catalog.mrk
            string mrkText = Marc.ToMrk(itemList);
            File.WriteAllText(Path.Combine(targetDir, "catalog.mrk"), mrkText, u8);

            // 1e. catalog.xml
            string xmlText = Marc.ToXml(itemList);
            File.WriteAllText(Path.Combine(targetDir, "catalog.xml"), xmlText, u8);

            // 2. Derive school metadata
            string sName = string.IsNullOrEmpty(schoolName) ? "SK Taman Ria" : schoolName;
            string repoName = "sk-taman-ria";
            if (CloudSyncManager.Instance != null && CloudSyncManager.Instance.Config != null && !string.IsNullOrEmpty(CloudSyncManager.Instance.Config.githubRepo))
            {
                repoName = CloudSyncManager.Instance.Config.githubRepo;
                int sl = repoName.LastIndexOf('/');
                if (sl >= 0) repoName = repoName.Substring(sl + 1);
            }
            else if (!string.IsNullOrEmpty(targetDir))
            {
                string fn = Path.GetFileName(targetDir.TrimEnd('\\', '/'));
                if (!string.IsNullOrEmpty(fn) && fn.IndexOf("repo", StringComparison.OrdinalIgnoreCase) < 0 && fn.IndexOf("temp", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    repoName = fn;
                }
            }
            if (string.IsNullOrEmpty(repoName)) repoName = "sk-taman-ria";

            string sourceCode = repoName.ToLowerInvariant();
            string orgCode = DeriveOrgCode(sName, repoName);
            string contactEmail = "tamanria.sk@gmail.com";
            if (CloudSyncManager.Instance != null && CloudSyncManager.Instance.Config != null && !string.IsNullOrEmpty(CloudSyncManager.Instance.Config.googleAccount))
            {
                contactEmail = CloudSyncManager.Instance.Config.googleAccount;
            }

            // 3. package.json, .gitignore, .gitattributes, README.md, docs/HARVESTER.md
            string pkgJson = string.Format("{{\n  \"name\": \"pustaka-terbuka-{0}\",\n  \"version\": \"0.1.0\",\n  \"description\": \"{1} ({2}) Bibliographic Dataset and Harvester for Pustaka Terbuka.\",\n  \"type\": \"module\",\n  \"scripts\": {{\n    \"test\": \"node --test tests/*.test.js\",\n    \"validate-inbox\": \"node tools/validate-inbox.js\",\n    \"merge\": \"node tools/merge.js\",\n    \"harvest\": \"node tools/harvesters/{0}/harvester.js\",\n    \"harvest:sample\": \"node tools/harvesters/{0}/harvester.js --max-records 10\"\n  }},\n  \"engines\": {{\n    \"node\": \">=20.0.0\"\n  }},\n  \"private\": true,\n  \"license\": \"MIT\",\n  \"dependencies\": {{\n    \"ajv\": \"^8.20.0\",\n    \"ajv-formats\": \"^3.0.1\",\n    \"fast-xml-parser\": \"^5.11.2\"\n  }}\n}}\n",
                sourceCode, sName, orgCode);
            File.WriteAllText(Path.Combine(targetDir, "package.json"), pkgJson, u8);
            File.WriteAllText(Path.Combine(targetDir, ".gitignore"), "node_modules/\n.DS_Store\nThumbs.db\n*.tmp\n*.bak\n", u8);
            File.WriteAllText(Path.Combine(targetDir, ".gitattributes"), "* text=auto eol=lf\n", u8);

            // README.md
            var sbReadme = new StringBuilder();
            sbReadme.Append("# Pustaka Terbuka — ").Append(sName).Append(" Data Provider\n\n");
            sbReadme.Append("> **PustakaTerbuka: an open, shared MARC catalog for Malaysian schools and libraries.**  \n");
            sbReadme.Append("> *Official ").Append(sName).Append(" (").Append(orgCode).Append(") Bibliographic Dataset and Harvester.*\n\n");
            sbReadme.Append("This repository contains the MARCXML bibliographic catalog synchronized from **").Append(sName).Append("** via **ErrinILS** for consumption by the Pustaka Terbuka Union Catalog ([`pustaka-terbuka/opac`](https://github.com/pustaka-terbuka/opac)).\n\n");
            sbReadme.Append("---\n\n## Catalog Overview\n\n");
            sbReadme.Append("- **Source Code:** `").Append(sourceCode).Append("`\n");
            sbReadme.Append("- **Organization Code:** `").Append(orgCode).Append("`\n");
            sbReadme.Append("- **Institution:** ").Append(sName).Append("\n");
            sbReadme.Append("- **Source Priority:** 10\n");
            sbReadme.Append("- **ILS Platform:** ErrinILS (School Integrated Library System)\n");
            sbReadme.Append("- **Classification:** Dewey Decimal Classification (DDC)\n");
            sbReadme.Append("- **Catalog Size:** ").Append(itemList.Count).Append(" records\n");
            sbReadme.Append("- **Repository:** `pustaka-terbuka/").Append(repoName).Append("`\n");
            sbReadme.Append("- **Reference Sibling:** `sk-taman-ria`\n");
            sbReadme.Append("- **Last Synced:** ").Append(DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")).Append(" UTC\n\n");
            sbReadme.Append("---\n\n## Repository Structure & Integration\n\n");
            sbReadme.Append("This repository follows the official Pustaka Terbuka Data Provider standard (as modeled by PPAPP and sibling providers):\n\n");
            sbReadme.Append("- `data/sources.json` — Source definition and authority priority.\n");
            sbReadme.Append("- `data/orgs.json` — Organization metadata, state, classification scheme, and contact.\n");
            sbReadme.Append("- `data/aliases.jsonl` — Canonical alias mapping table (ISBN, 035, and fallback keys to permanent Crockford Base32 IDs).\n");
            sbReadme.Append("- `data/bib/<shard>/<id>.xml` — Partitioned single-record MARCXML files for downstream OPAC indexing.\n");
            sbReadme.Append("- `data/holdings/").Append(orgCode).Append(".csv` — Normalized institutional holdings table.\n");
            sbReadme.Append("- `harvest/inbox/").Append(sourceCode).Append("/<run-id>/` — Intake batches with `manifest.json`, `partition_state.json`, `provenance.jsonl`, and `records/batch_0.xml`.\n");
            sbReadme.Append("- `tools/` — Self-contained validation, merging, and alias resolution scripts.\n");
            sbReadme.Append("- `package.json` — NPM toolchain scripts (`test`, `validate-inbox`, `merge`).\n\n");
            sbReadme.Append("### Convenience Exports\n");
            sbReadme.Append("For school librarians and external spreadsheets, the root directory also contains:\n");
            sbReadme.Append("- [`catalog.json`](catalog.json) — Full JSON export\n");
            sbReadme.Append("- [`holdings.csv`](holdings.csv) — CSV accession and inventory list\n");
            sbReadme.Append("- [`catalog.mrc`](catalog.mrc) — Binary MARC 21 (ISO 2709) export\n");
            sbReadme.Append("- [`catalog.mrk`](catalog.mrk) — MarcEdit text mnemonic format\n");
            sbReadme.Append("- [`catalog.xml`](catalog.xml) — Multi-record MARCXML collection\n");
            File.WriteAllText(Path.Combine(targetDir, "README.md"), sbReadme.ToString(), u8);

            // docs/HARVESTER.md
            string docsDir = Path.Combine(targetDir, "docs");
            Directory.CreateDirectory(docsDir);
            var sbHarv = new StringBuilder();
            sbHarv.Append("# ").Append(sName).Append(" (").Append(orgCode).Append(") Harvester Runbook\n\n");
            sbHarv.Append("> **Operational guide for harvesting and synchronizing bibliographic records from ").Append(sName).Append(".**\n\n");
            sbHarv.Append("## Target System Profile\n");
            sbHarv.Append("* **Institution:** ").Append(sName).Append("\n");
            sbHarv.Append("* **Organization Code:** `").Append(orgCode).Append("` (Priority: 10)\n");
            sbHarv.Append("* **Source Code:** `").Append(sourceCode).Append("`\n");
            sbHarv.Append("* **Catalog Software:** ErrinILS\n");
            sbHarv.Append("* **OPAC URL:** `https://pustaka-terbuka.github.io/").Append(repoName).Append("/`\n\n");
            sbHarv.Append("## Ingestion & Conversion Rules\n");
            sbHarv.Append("* **Output Inbox:** `harvest/inbox/").Append(sourceCode).Append("/<run-id>/`\n");
            sbHarv.Append("* **MARC Format:** Native MARC 21 Slim XML wrapped in `<collection xmlns=\"http://www.loc.gov/MARC21/slim\">`\n");
            sbHarv.Append("* **Tag Preservation:**\n");
            sbHarv.Append("  * Control number `001`: Barcode / Accession number\n");
            sbHarv.Append("  * Organization identifier `003`: `").Append(orgCode).Append("`\n");
            sbHarv.Append("  * Classification `082`: Dewey Decimal (DDC)\n");
            sbHarv.Append("  * Local holdings `852` / `952`: Branch codes, barcodes, and call numbers\n");
            File.WriteAllText(Path.Combine(docsDir, "HARVESTER.md"), sbHarv.ToString(), u8);

            // 4. Create standard data/ layout
            string dataDir = Path.Combine(targetDir, "data");
            string bibDir = Path.Combine(dataDir, "bib");
            string holdingsDir = Path.Combine(dataDir, "holdings");
            Directory.CreateDirectory(dataDir);
            Directory.CreateDirectory(bibDir);
            Directory.CreateDirectory(holdingsDir);

            // data/sources.json
            string sourcesJson = string.Format("{{\n  \"test-source\": {{\n    \"name\": \"Test Source\",\n    \"prefix\": \"TEST\",\n    \"priority\": 1\n  }},\n  \"{0}\": {{\n    \"name\": \"{1}\",\n    \"prefix\": \"{2}\",\n    \"priority\": 10\n  }}\n}}\n",
                sourceCode, sName, orgCode);
            File.WriteAllText(Path.Combine(dataDir, "sources.json"), sourcesJson, u8);

            // data/orgs.json
            string orgsJson = string.Format("{{\n  \"{0}\": {{\n    \"name\": \"{1}\",\n    \"state\": \"Kedah\",\n    \"type\": \"school\",\n    \"default_scheme\": \"DDC\",\n    \"contact\": \"{2}\",\n    \"opac_url\": \"https://pustaka-terbuka.github.io/{3}/\"\n  }}\n}}\n",
                orgCode, sName, contactEmail, repoName);
            File.WriteAllText(Path.Combine(dataDir, "orgs.json"), orgsJson, u8);

            // Read existing aliases.jsonl if present to preserve IDs and timestamps
            string aliasesPath = Path.Combine(dataDir, "aliases.jsonl");
            var aliasMap = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
            if (File.Exists(aliasesPath))
            {
                try
                {
                    string[] lines = File.ReadAllLines(aliasesPath, Encoding.UTF8);
                    for (int l = 0; l < lines.Length; l++)
                    {
                        string line = lines[l].Trim();
                        if (string.IsNullOrEmpty(line)) continue;
                        var d = Json.Parse(line) as Dictionary<string, object>;
                        if (d != null)
                        {
                            string key = Json.Str(d, "key");
                            string id = Json.Str(d, "id");
                            string type = Json.Str(d, "type");
                            string ts = Json.Str(d, "ts");
                            if (!string.IsNullOrEmpty(key) && !string.IsNullOrEmpty(id))
                            {
                                var entry = new Dictionary<string, string>();
                                entry["id"] = id; entry["type"] = type; entry["ts"] = ts;
                                aliasMap[key] = entry;
                            }
                        }
                    }
                }
                catch { }
            }

            // Read existing manifest.json in data/bib if present
            string bibManifestPath = Path.Combine(bibDir, "manifest.json");
            var bibManifest = new Dictionary<string, object>();
            if (File.Exists(bibManifestPath))
            {
                try
                {
                    var bm = Json.Parse(File.ReadAllText(bibManifestPath, Encoding.UTF8)) as Dictionary<string, object>;
                    if (bm != null) bibManifest = bm;
                }
                catch { }
            }

            string nowIso = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
            string ts005 = DateTime.UtcNow.ToString("yyyyMMddHHmmss.0");
            var holdingsRows = new List<string>();
            holdingsRows.Add("record,org,scheme,class_number,item_number,full_call_number,location");

            // Setup harvest/inbox batch
            string runId = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
            string inboxRunDir = Path.Combine(targetDir, "harvest\\inbox\\" + sourceCode + "\\" + runId);
            string inboxRecordsDir = Path.Combine(inboxRunDir, "records");
            Directory.CreateDirectory(inboxRecordsDir);

            var batchXmlSb = new StringBuilder();
            batchXmlSb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
            batchXmlSb.AppendLine("<marc:collection xmlns:marc=\"http://www.loc.gov/MARC21/slim\">");

            var provLines = new List<string>();

            for (int idx = 0; idx < itemList.Count; idx++)
            {
                var it = itemList[idx];
                string barcode = !string.IsNullOrEmpty(it.barcode) ? it.barcode : (!string.IsNullOrEmpty(it.ctl) ? it.ctl : (it.id ?? (idx + 1).ToString()));
                string k035Dash = "035:" + orgCode + "-" + barcode;
                string k035Paren = "035:(" + orgCode + ")" + barcode;
                string cleanIsbn = Lookup.NormalizeIsbn(it.isbn);
                string kIsbn = !string.IsNullOrEmpty(cleanIsbn) ? "isbn:" + cleanIsbn : null;
                string fbKey = GenerateFallbackKey(it);
                string kFb = "fb:" + fbKey;

                // Resolve ID
                string recordId = null;
                if (aliasMap.ContainsKey(k035Dash)) recordId = aliasMap[k035Dash]["id"];
                else if (aliasMap.ContainsKey(k035Paren)) recordId = aliasMap[k035Paren]["id"];
                else if (kIsbn != null && aliasMap.ContainsKey(kIsbn)) recordId = aliasMap[kIsbn]["id"];
                else if (aliasMap.ContainsKey(kFb)) recordId = aliasMap[kFb]["id"];

                if (string.IsNullOrEmpty(recordId))
                {
                    recordId = GenerateCrockfordId(orgCode + ":" + barcode);
                }

                // Register aliases
                Action<string, string> registerAlias = (k, t) =>
                {
                    if (string.IsNullOrEmpty(k)) return;
                    if (!aliasMap.ContainsKey(k))
                    {
                        var entry = new Dictionary<string, string>();
                        entry["id"] = recordId; entry["type"] = t; entry["ts"] = nowIso;
                        aliasMap[k] = entry;
                    }
                    else
                    {
                        aliasMap[k]["id"] = recordId;
                    }
                };
                registerAlias(k035Dash, "035");
                registerAlias(k035Paren, "035");
                if (kIsbn != null) registerAlias(kIsbn, "isbn");
                registerAlias(kFb, "fallback");

                // Write single-record XML in data/bib/<shard>/<id>.xml
                string shard = recordId.Substring(0, 2).ToLowerInvariant();
                string shardDir = Path.Combine(bibDir, shard);
                Directory.CreateDirectory(shardDir);

                string singleXml = BuildSingleRecordXml(it, orgCode, sourceCode, ts005);
                File.WriteAllText(Path.Combine(shardDir, recordId + ".xml"), singleXml, u8);
                bibManifest[recordId] = ComputeSha256(singleXml);

                // Holdings CSV row
                string classNum = "";
                string itemNum = "";
                if (!string.IsNullOrEmpty(it.call))
                {
                    int sp = it.call.IndexOf(' ');
                    if (sp > 0)
                    {
                        classNum = it.call.Substring(0, sp).Trim();
                        itemNum = it.call.Substring(sp + 1).Trim();
                    }
                    else
                    {
                        classNum = it.call.Trim();
                    }
                }
                holdingsRows.Add(string.Format("{0},{1},{2},{3},{4},{5},{6}",
                    recordId, orgCode, "DDC", classNum, itemNum, it.call ?? "", "Perpustakaan " + orgCode));

                // Append to inbox batch_0.xml
                batchXmlSb.Append(BuildRecordXmlNode(it, orgCode, sourceCode, ts005));

                // Provenance line
                provLines.Add(string.Format("{{\"source_001\":\"{0}\",\"source_url\":\"https://pustaka-terbuka.github.io/{1}/records/{0}\",\"harvested_at\":\"{2}\",\"file\":\"batch_0.xml\",\"index\":{3}}}",
                    barcode, repoName, nowIso, idx));
            }

            batchXmlSb.AppendLine("</marc:collection>");
            File.WriteAllText(Path.Combine(inboxRecordsDir, "batch_0.xml"), batchXmlSb.ToString(), u8);
            File.WriteAllText(Path.Combine(inboxRunDir, "provenance.jsonl"), string.Join("\n", provLines.ToArray()) + "\n", u8);

            // inbox partition_state.json
            string partJson = string.Format("{{\n  \"lastBibNumber\": {0},\n  \"nextBatchNumber\": 1,\n  \"totalRecordsSaved\": {0}\n}}\n", itemList.Count);
            File.WriteAllText(Path.Combine(inboxRunDir, "partition_state.json"), partJson, u8);

            // inbox manifest.json
            string manJson = string.Format("{{\n  \"schema_version\": \"1.0\",\n  \"source_code\": \"{0}\",\n  \"source_name\": \"{1}\",\n  \"source_base_url\": \"https://pustaka-terbuka.github.io/{2}/\",\n  \"terms_url\": \"https://pustaka-terbuka.github.io/{2}/\",\n  \"terms_verified_on\": \"{3}\",\n  \"harvester_name\": \"errin-ils-harvester\",\n  \"harvester_version\": \"1.0.0\",\n  \"run_id\": \"{4}\",\n  \"mode\": \"full\",\n  \"started_at\": \"{5}\",\n  \"finished_at\": \"{5}\",\n  \"record_count\": {6},\n  \"error_count\": 0,\n  \"status\": \"complete\",\n  \"user_agent\": \"ErrinILS-PustakaTerbuka/1.0 (+https://github.com/pustaka-terbuka/{2})\",\n  \"notes\": \"{1} catalog export from ErrinILS\"\n}}\n",
                sourceCode, sName, repoName, DateTime.UtcNow.ToString("yyyy-MM-dd"), runId, nowIso, itemList.Count);
            File.WriteAllText(Path.Combine(inboxRunDir, "manifest.json"), manJson, u8);

            // Write sorted data/aliases.jsonl
            var aliasEntries = new List<string>();
            foreach (var kvp in aliasMap.OrderBy(k => k.Key, StringComparer.Ordinal))
            {
                aliasEntries.Add(string.Format("{{\"key\":\"{0}\",\"id\":\"{1}\",\"type\":\"{2}\",\"ts\":\"{3}\"}}",
                    kvp.Key, kvp.Value["id"], kvp.Value["type"], kvp.Value["ts"]));
            }
            File.WriteAllText(aliasesPath, string.Join("\n", aliasEntries.ToArray()) + (aliasEntries.Count > 0 ? "\n" : ""), u8);

            // Write data/bib/manifest.json
            File.WriteAllText(bibManifestPath, Json.WritePretty(bibManifest), u8);

            // Write data/holdings/<orgCode>.csv
            File.WriteAllText(Path.Combine(holdingsDir, orgCode + ".csv"), string.Join("\n", holdingsRows.ToArray()) + "\n", u8);

            // Ensure tools and tests are created/copied
            EnsurePustakaTools(targetDir, sourceCode, sName, orgCode);
        }

        public static SyncResult SyncViaGit(Db db, string repoUrl, string localDir, string schoolName)
        {
            var res = new SyncResult();
            if (string.IsNullOrEmpty(localDir)) localDir = GetDefaultRepoDir();
            if (string.IsNullOrEmpty(repoUrl)) repoUrl = DefaultRemoteUrl;

            int ec;
            string gitDir = Path.Combine(localDir, ".git");
            if (!Directory.Exists(gitDir))
            {
                string parentDir = Path.GetDirectoryName(localDir);
                if (!Directory.Exists(parentDir)) Directory.CreateDirectory(parentDir);

                string cloneOut = RunGit(string.Format("clone \"{0}\" \"{1}\"", repoUrl, localDir), parentDir, out ec);
                if (ec != 0)
                {
                    res.Success = false;
                    res.Message = "Git clone failed: " + cloneOut;
                    return res;
                }
            }
            else
            {
                if (!string.IsNullOrEmpty(repoUrl)) RunGit(string.Format("remote set-url origin \"{0}\"", repoUrl), localDir, out ec);
                RunGit("pull --rebase origin main", localDir, out ec);
            }

            string catJsonFile = Path.Combine(localDir, "catalog.json");
            if (File.Exists(catJsonFile))
            {
                try
                {
                    string json = File.ReadAllText(catJsonFile, Encoding.UTF8);
                    var list = Json.Parse(json) as List<object>;
                    if (list != null)
                    {
                        var itemMap = new Dictionary<string, Item>(StringComparer.OrdinalIgnoreCase);
                        for (int i = 0; i < db.Items.Count; i++)
                        {
                            var it = db.Items[i];
                            if (!string.IsNullOrEmpty(it.barcode)) itemMap[it.barcode] = it;
                            if (!string.IsNullOrEmpty(it.id)) itemMap[it.id] = it;
                            if (!string.IsNullOrEmpty(it.ctl)) itemMap[it.ctl] = it;
                        }

                        for (int i = 0; i < list.Count; i++)
                        {
                            var d = list[i] as Dictionary<string, object>;
                            if (d == null) continue;
                            var remoteItem = Item.FromJ(d);
                            if (string.IsNullOrEmpty(remoteItem.barcode) && string.IsNullOrEmpty(remoteItem.title)) continue;

                            Item local;
                            if ((!string.IsNullOrEmpty(remoteItem.barcode) && itemMap.TryGetValue(remoteItem.barcode, out local)) ||
                                (!string.IsNullOrEmpty(remoteItem.id) && itemMap.TryGetValue(remoteItem.id, out local)) ||
                                (!string.IsNullOrEmpty(remoteItem.ctl) && itemMap.TryGetValue(remoteItem.ctl, out local)))
                            {
                                bool changed = false;
                                foreach (var k in Item.Keys)
                                {
                                    string rv = remoteItem.Get(k);
                                    string lv = local.Get(k);
                                    if (!string.IsNullOrEmpty(rv) && rv != lv)
                                    {
                                        local.Set(k, rv);
                                        changed = true;
                                    }
                                }
                                if (changed) res.ItemsUpdated++;
                            }
                            else
                            {
                                db.Items.Add(remoteItem);
                                if (!string.IsNullOrEmpty(remoteItem.barcode)) itemMap[remoteItem.barcode] = remoteItem;
                                res.ItemsAdded++;
                            }
                        }
                    }
                }
                catch { }
            }

            GenerateCatalogFiles(db.Items, localDir, schoolName);

            RunGit("add .", localDir, out ec);
            string status = RunGit("status --porcelain", localDir, out ec);
            if (!string.IsNullOrEmpty(status))
            {
                string commitMsg = string.Format("Update catalog ({0} items) from ErrinILS", db.Items.Count);
                RunGit(string.Format("commit -m \"{0}\"", commitMsg), localDir, out ec);
                string pushOut = RunGit("push origin main", localDir, out ec);
                if (ec != 0)
                {
                    pushOut = RunGit("push -u origin main", localDir, out ec);
                    if (ec != 0)
                    {
                        res.Success = false;
                        res.Message = "Git push failed: " + pushOut;
                        return res;
                    }
                }
            }

            res.Success = true;
            res.Message = string.Format("GitHub catalog in sync: {0} books published to pustaka-terbuka/sk-taman-ria.", db.Items.Count);
            return res;
        }

        public static SyncResult PushViaApi(string repo, string token, string localDir, string commitMsg)
        {
            var res = new SyncResult();
            if (string.IsNullOrEmpty(token))
            {
                res.Success = false;
                res.Message = "GitHub Personal Access Token is required to push without Git installed.";
                return res;
            }

            try
            {
                string baseUrl = "https://api.github.com/repos/" + repo;

                Func<string, string, string, string> request = (method, path, body) =>
                {
                    var req = (HttpWebRequest)WebRequest.Create(baseUrl + path);
                    req.Method = method;
                    req.UserAgent = "ErrinILS/1.3";
                    req.Headers["Authorization"] = "Bearer " + token;
                    req.Accept = "application/vnd.github.v3+json";
                    if (!string.IsNullOrEmpty(body))
                    {
                        req.ContentType = "application/json; charset=utf-8";
                        byte[] bytes = Encoding.UTF8.GetBytes(body);
                        req.ContentLength = bytes.Length;
                        using (var s = req.GetRequestStream()) s.Write(bytes, 0, bytes.Length);
                    }
                    using (var resp = (HttpWebResponse)req.GetResponse())
                    using (var reader = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                    {
                        return reader.ReadToEnd();
                    }
                };

                // 1. Get latest commit SHA on main branch
                string refJson = request("GET", "/git/ref/heads/main", null);
                var refObj = Json.Parse(refJson) as Dictionary<string, object>;
                var commitObj = refObj != null && refObj.ContainsKey("object") ? refObj["object"] as Dictionary<string, object> : null;
                string latestCommitSha = commitObj != null && commitObj.ContainsKey("sha") ? commitObj["sha"] as string : null;
                if (string.IsNullOrEmpty(latestCommitSha))
                {
                    res.Success = false;
                    res.Message = "Could not resolve latest commit on GitHub.";
                    return res;
                }

                // 2. Get base tree SHA from latest commit
                string commitJson = request("GET", "/git/commits/" + latestCommitSha, null);
                var commitData = Json.Parse(commitJson) as Dictionary<string, object>;
                var treeObj = commitData != null && commitData.ContainsKey("tree") ? commitData["tree"] as Dictionary<string, object> : null;
                string baseTreeSha = treeObj != null && treeObj.ContainsKey("sha") ? treeObj["sha"] as string : null;
                if (string.IsNullOrEmpty(baseTreeSha))
                {
                    res.Success = false;
                    res.Message = "Could not resolve base tree on GitHub.";
                    return res;
                }

                // 3. Scan files in localDir to upload
                var allFiles = Directory.GetFiles(localDir, "*.*", SearchOption.AllDirectories);
                var treeItems = new List<object>();

                for (int i = 0; i < allFiles.Length; i++)
                {
                    string f = allFiles[i];
                    string rel = f.Substring(localDir.Length).TrimStart('\\', '/').Replace('\\', '/');
                    if (rel.StartsWith(".git/") || rel.StartsWith("node_modules/")) continue;

                    byte[] fileBytes = File.ReadAllBytes(f);
                    string ext = Path.GetExtension(f).ToLowerInvariant();
                    bool isBinary = ext == ".mrc" || ext == ".ico" || ext == ".png" || ext == ".jpg";

                    var item = new Dictionary<string, object>();
                    item["path"] = rel;
                    item["mode"] = "100644";
                    item["type"] = "blob";

                    if (isBinary)
                    {
                        string base64 = Convert.ToBase64String(fileBytes);
                        var blobPayload = new Dictionary<string, object>();
                        blobPayload["content"] = base64;
                        blobPayload["encoding"] = "base64";
                        string blobResp = request("POST", "/git/blobs", Json.Write(blobPayload));
                        var blobObj = Json.Parse(blobResp) as Dictionary<string, object>;
                        string blobSha = blobObj != null && blobObj.ContainsKey("sha") ? blobObj["sha"] as string : null;
                        if (!string.IsNullOrEmpty(blobSha)) item["sha"] = blobSha;
                        else continue;
                    }
                    else
                    {
                        string textContent = Encoding.UTF8.GetString(fileBytes);
                        item["content"] = textContent;
                    }

                    treeItems.Add(item);
                }

                // 4. Create new tree
                var treePayload = new Dictionary<string, object>();
                treePayload["base_tree"] = baseTreeSha;
                treePayload["tree"] = treeItems;
                string treeResp = request("POST", "/git/trees", Json.Write(treePayload));
                var newTreeObj = Json.Parse(treeResp) as Dictionary<string, object>;
                string newTreeSha = newTreeObj != null && newTreeObj.ContainsKey("sha") ? newTreeObj["sha"] as string : null;
                if (string.IsNullOrEmpty(newTreeSha))
                {
                    res.Success = false;
                    res.Message = "Could not create new tree on GitHub.";
                    return res;
                }

                // 5. Create new commit
                var commitPayload = new Dictionary<string, object>();
                commitPayload["message"] = !string.IsNullOrEmpty(commitMsg) ? commitMsg : "Update catalog from ErrinILS";
                commitPayload["tree"] = newTreeSha;
                commitPayload["parents"] = new List<object> { latestCommitSha };
                string newCommitResp = request("POST", "/git/commits", Json.Write(commitPayload));
                var newCommitObj = Json.Parse(newCommitResp) as Dictionary<string, object>;
                string newCommitSha = newCommitObj != null && newCommitObj.ContainsKey("sha") ? newCommitObj["sha"] as string : null;
                if (string.IsNullOrEmpty(newCommitSha))
                {
                    res.Success = false;
                    res.Message = "Could not create commit on GitHub.";
                    return res;
                }

                // 6. Update main branch reference (PATCH /git/refs/heads/main)
                var patchPayload = new Dictionary<string, object>();
                patchPayload["sha"] = newCommitSha;
                patchPayload["force"] = false;
                request("PATCH", "/git/refs/heads/main", Json.Write(patchPayload));

                res.Success = true;
                res.Message = string.Format("Direct GitHub sync successful (commit {0} published via HTTPS API).", newCommitSha.Substring(0, Math.Min(7, newCommitSha.Length)));
                return res;
            }
            catch (WebException wex)
            {
                string detail = wex.Message;
                try
                {
                    if (wex.Response != null)
                    {
                        using (var s = wex.Response.GetResponseStream())
                        using (var r = new StreamReader(s, Encoding.UTF8))
                            detail = r.ReadToEnd();
                    }
                }
                catch { }
                res.Success = false;
                res.Message = "GitHub API error: " + detail;
                return res;
            }
            catch (Exception ex)
            {
                res.Success = false;
                res.Message = "GitHub API sync failed: " + ex.Message;
                return res;
            }
        }

        public static SyncResult SyncViaHttp(Db db, string repo, string token, string schoolName = null)
        {
            var res = new SyncResult();
            if (string.IsNullOrEmpty(repo)) repo = "pustaka-terbuka/sk-taman-ria";

            string rawUrl = string.Format("https://raw.githubusercontent.com/{0}/main/catalog.json", repo);
            try
            {
                using (var wc = new WebClient())
                {
                    wc.Encoding = Encoding.UTF8;
                    wc.Headers[HttpRequestHeader.UserAgent] = "ErrinILS/1.3";
                    if (!string.IsNullOrEmpty(token))
                        wc.Headers[HttpRequestHeader.Authorization] = "Bearer " + token;

                    string json = null;
                    try { json = wc.DownloadString(rawUrl); }
                    catch (WebException wex)
                    {
                        var resp = wex.Response as HttpWebResponse;
                        if (resp != null && resp.StatusCode == HttpStatusCode.NotFound) json = null;
                        else throw;
                    }

                    if (!string.IsNullOrEmpty(json))
                    {
                        var list = Json.Parse(json) as List<object>;
                        if (list != null)
                        {
                            var itemMap = new Dictionary<string, Item>(StringComparer.OrdinalIgnoreCase);
                            for (int i = 0; i < db.Items.Count; i++)
                            {
                                var it = db.Items[i];
                                if (!string.IsNullOrEmpty(it.barcode)) itemMap[it.barcode] = it;
                                if (!string.IsNullOrEmpty(it.id)) itemMap[it.id] = it;
                            }

                            for (int i = 0; i < list.Count; i++)
                            {
                                var d = list[i] as Dictionary<string, object>;
                                if (d == null) continue;
                                var remoteItem = Item.FromJ(d);
                                if (string.IsNullOrEmpty(remoteItem.barcode)) continue;

                                Item local;
                                if (itemMap.TryGetValue(remoteItem.barcode, out local))
                                {
                                    bool changed = false;
                                    foreach (var k in Item.Keys)
                                    {
                                        string rv = remoteItem.Get(k);
                                        string lv = local.Get(k);
                                        if (!string.IsNullOrEmpty(rv) && rv != lv)
                                        {
                                            local.Set(k, rv);
                                            changed = true;
                                        }
                                    }
                                    if (changed) res.ItemsUpdated++;
                                }
                                else
                                {
                                    db.Items.Add(remoteItem);
                                    itemMap[remoteItem.barcode] = remoteItem;
                                    res.ItemsAdded++;
                                }
                            }
                        }
                    }
                }

                // If token is provided, push local catalog via GitHub REST API without requiring Git!
                if (!string.IsNullOrEmpty(token))
                {
                    string localDir = GetRepoDir(repo);
                    GenerateCatalogFiles(db.Items, localDir, schoolName);
                    string commitMsg = string.Format("Update catalog ({0} items) from ErrinILS (HTTP API)", db.Items.Count);
                    var pushRes = PushViaApi(repo, token, localDir, commitMsg);
                    if (pushRes.Success)
                    {
                        res.Success = true;
                        res.Message = string.Format("Catalog synchronized with GitHub via Direct HTTP API ({0} items).", db.Items.Count);
                        return res;
                    }
                    else
                    {
                        res.Success = false;
                        res.Message = "Catalog downloaded, but HTTP API push failed: " + pushRes.Message;
                        return res;
                    }
                }

                res.Success = true;
                res.Message = string.Format("Retrieved catalog via HTTP ({0} items total). To push without Git installed, enter a GitHub Token in Settings.", db.Items.Count);
                return res;
            }
            catch (Exception ex)
            {
                res.Success = false;
                res.Message = "HTTP catalog fetch failed: " + ex.Message;
                return res;
            }
        }

        public static SyncResult Sync(Db db, string repo = null, string token = null, string schoolName = null)
        {
            if (string.IsNullOrEmpty(repo)) repo = "pustaka-terbuka/sk-taman-ria";

            if (HasGit())
            {
                string repoUrl = !string.IsNullOrEmpty(token)
                    ? string.Format("https://x-access-token:{0}@github.com/{1}.git", token, repo)
                    : "https://github.com/" + repo + ".git";
                string localDir = GetRepoDir(repo);
                return SyncViaGit(db, repoUrl, localDir, schoolName);
            }
            else
            {
                return SyncViaHttp(db, repo, token, schoolName);
            }
        }
    }

    /// <summary>
    /// Central manager orchestrating Direct Google Drive cloud user records sync and GitHub catalog sync.
    /// Provides unified sync methods, multi-PC consistency checks, and background sync hooks.
    /// </summary>
    public class CloudSyncManager
    {
        static CloudSyncManager instance;
        public static CloudSyncManager Instance
        {
            get
            {
                if (instance == null) instance = new CloudSyncManager();
                return instance;
            }
        }

        public SyncConfig Config = new SyncConfig();
        public string LastError = "";
        public string StatusSummary = "";
        readonly object syncLock = new object();
        bool isSyncing = false;

        public bool IsSyncing { get { return isSyncing; } }

        public void LoadConfig(string configFile)
        {
            try
            {
                if (File.Exists(configFile))
                {
                    var root = Json.Parse(File.ReadAllText(configFile, Encoding.UTF8)) as Dictionary<string, object>;
                    if (root != null && root.ContainsKey("sync"))
                    {
                        var sc = root["sync"] as Dictionary<string, object>;
                        if (sc != null) Config = SyncConfig.FromJ(sc);
                    }
                }
            }
            catch { }
        }

        public void SaveConfig(string configFile)
        {
            try
            {
                var root = new Dictionary<string, object>();
                if (File.Exists(configFile))
                {
                    try { root = Json.Parse(File.ReadAllText(configFile, Encoding.UTF8)) as Dictionary<string, object> ?? new Dictionary<string, object>(); } catch { }
                }
                root["sync"] = Config.ToJ();
                File.WriteAllText(configFile, Json.Write(root), new UTF8Encoding(false));
            }
            catch { }
        }

        /// <summary>
        /// Performs complete multi-PC synchronization:
        /// 1. Google Drive Cloud: user records for tamanria.sk@gmail.com (No desktop app needed).
        /// 2. GitHub: books and catalog data (pustaka-terbuka/sk-taman-ria).
        /// 3. Saves combined state into local SQLite database.
        /// </summary>
        public SyncResult SyncAll(Db db, Store store)
        {
            lock (syncLock)
            {
                isSyncing = true;
                var res = new SyncResult();
                try
                {
                    // 1. Google Drive Cloud Sync (Users for tamanria.sk@gmail.com)
                    var userRes = GoogleDriveCloudSync.Sync(db, Config, false);

                    // 2. GitHub Sync (Catalog for pustaka-terbuka/sk-taman-ria)
                    var catRes = GitHubCatalogSync.Sync(db, Config.githubRepo, Config.githubToken, db.Set.school);

                    // 3. Save combined result to local SQLite database
                    if (store != null && db != null)
                    {
                        store.Save(db);
                    }

                    res.Success = (userRes.Success || catRes.Success);
                    res.PatronsAdded = userRes.PatronsAdded;
                    res.PatronsUpdated = userRes.PatronsUpdated;
                    res.LoansUpdated = userRes.LoansUpdated;
                    res.FinesUpdated = userRes.FinesUpdated;
                    res.ItemsAdded = catRes.ItemsAdded;
                    res.ItemsUpdated = catRes.ItemsUpdated;

                    Config.lastSyncTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                    Config.lastSyncStatus = string.Format("Google Drive: {0} | GitHub: {1}",
                        userRes.Success ? "OK" : "Setup needed",
                        catRes.Success ? "OK" : "Error");
                    StatusSummary = Config.lastSyncStatus;

                    if (!userRes.Success && !catRes.Success)
                    {
                        res.Success = false;
                        res.Message = "Sync status: " + userRes.Message + " | " + catRes.Message;
                    }
                    else if (!userRes.Success)
                    {
                        res.Message = "Catalog in sync on GitHub. Google Drive: " + userRes.Message;
                    }
                    else if (!catRes.Success)
                    {
                        res.Message = "Users in sync on Google Drive. GitHub warning: " + catRes.Message;
                    }
                    else
                    {
                        res.Message = string.Format("All records in sync! ({0} books on GitHub, {1} students & loans on Google Drive)",
                            db.Items.Count, db.Patrons.Count);
                    }
                    return res;
                }
                catch (Exception ex)
                {
                    res.Success = false;
                    res.Message = "Unexpected sync error: " + ex.Message;
                    LastError = ex.Message;
                    return res;
                }
                finally
                {
                    isSyncing = false;
                }
            }
        }

        /// <summary>
        /// Background queue for non-blocking auto-sync after user edits (Instant On-Save).
        /// </summary>
        public void QueueBackgroundUserSync(Db db)
        {
            if (!Config.syncOnSave || db == null) return;
            ThreadPool.QueueUserWorkItem(s =>
            {
                try
                {
                    if (Config.IsGoogleCloudConfigured)
                    {
                        GoogleDriveCloudSync.Sync(db, Config);
                    }
                    else
                    {
                        string folder = GoogleDriveSync.GetSyncFolder(Config.gdriveFolder);
                        if (!string.IsNullOrEmpty(folder))
                        {
                            string err;
                            GoogleDriveSync.ExportUsers(db, folder, out err);
                        }
                    }
                }
                catch { }
            });
        }
    }
}
