// ErrinILS Remote Access: zero-dependency HTTP server, device pairing, and web application.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace Errin
{
    public static class Web
    {
        public class Device
        {
            public string token = "";
            public string name = "";
            public string paired = "";
            public string seen = "";

            public Dictionary<string, object> ToJ()
            {
                var d = new Dictionary<string, object>();
                d["token"] = token ?? "";
                d["name"] = name ?? "";
                d["paired"] = paired ?? "";
                d["seen"] = seen ?? "";
                return d;
            }

            public static Device FromJ(Dictionary<string, object> d)
            {
                return new Device
                {
                    token = Json.Str(d, "token"),
                    name = Json.Str(d, "name"),
                    paired = Json.Str(d, "paired"),
                    seen = Json.Str(d, "seen")
                };
            }
        }

        public class PairSession
        {
            public string code = "";
            public DateTime expiresAt;
            public int failedAttempts;
        }

        public class Server
        {
            TcpListener lis;
            volatile bool running;
            readonly object syncLock = new object();

            public int Port { get; private set; }
            public bool LocalOnly { get; private set; }
            public List<Device> Devices { get; set; }
            public PairSession ActivePair { get; private set; }
            public Device LastPairedDevice { get; private set; }

            public Func<Db> GetDb;
            public Func<Lib> GetLib;
            public Action SaveStore;
            public Action<Action> RunOnUi;
            public Action<string> OnRemoteWrite;
            public Action<Device> OnDevicePaired;
            public Action<string> Log;

            public Server()
            {
                Devices = new List<Device>();
            }

            public void Start(int port, bool localOnly)
            {
                Port = port;
                LocalOnly = localOnly;
                lis = new TcpListener(localOnly ? IPAddress.Loopback : IPAddress.Any, port);
                lis.Start();
                running = true;

                var acceptThread = new Thread(AcceptLoop) { IsBackground = true, Name = "ErrinWebAccept" };
                acceptThread.Start();
            }

            public void Stop()
            {
                running = false;
                try { if (lis != null) lis.Stop(); } catch { }
            }

            public void StartPairing(string code, int seconds)
            {
                lock (syncLock)
                {
                    ActivePair = new PairSession
                    {
                        code = Regex.Replace(code ?? "", @"[\s-]", ""),
                        expiresAt = DateTime.UtcNow.AddSeconds(seconds),
                        failedAttempts = 0
                    };
                    LastPairedDevice = null;
                }
            }

            public void CancelPairing()
            {
                lock (syncLock)
                {
                    ActivePair = null;
                }
            }

            void AcceptLoop()
            {
                while (running)
                {
                    try
                    {
                        var client = lis.AcceptTcpClient();
                        ThreadPool.QueueUserWorkItem(c => HandleClient((TcpClient)c), client);
                    }
                    catch
                    {
                        if (!running) break;
                    }
                }
            }

            void HandleClient(TcpClient client)
            {
                using (client)
                {
                    try
                    {
                        client.ReceiveTimeout = 15000;
                        client.SendTimeout = 15000;
                        var stream = client.GetStream();

                        // Read request headers
                        var ms = new MemoryStream();
                        byte[] buf = new byte[4096];
                        int headerEnd = -1;
                        while (ms.Length < 65536)
                        {
                            int read = stream.Read(buf, 0, buf.Length);
                            if (read <= 0) break;
                            ms.Write(buf, 0, read);
                            byte[] cur = ms.ToArray();
                            headerEnd = FindPattern(cur, new byte[] { 13, 10, 13, 10 });
                            if (headerEnd >= 0) break;
                        }

                        if (headerEnd < 0) return;

                        byte[] allBytes = ms.ToArray();
                        string headerText = Encoding.UTF8.GetString(allBytes, 0, headerEnd);
                        string[] lines = headerText.Split(new[] { "\r\n" }, StringSplitOptions.None);
                        if (lines.Length == 0) return;

                        string[] reqTokens = lines[0].Split(' ');
                        if (reqTokens.Length < 2) return;
                        string method = reqTokens[0].ToUpperInvariant();
                        string rawUrl = reqTokens[1];

                        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                        for (int k = 1; k < lines.Length; k++)
                        {
                            int colon = lines[k].IndexOf(':');
                            if (colon > 0)
                            {
                                string hk = lines[k].Substring(0, colon).Trim();
                                string hv = lines[k].Substring(colon + 1).Trim();
                                headers[hk] = hv;
                            }
                        }

                        // Read request body if Content-Length specified
                        string body = "";
                        int contentLength = 0;
                        if (headers.ContainsKey("Content-Length"))
                            int.TryParse(headers["Content-Length"], out contentLength);

                        int bodyStart = headerEnd + 4;
                        int bodyBytesAvailable = allBytes.Length - bodyStart;
                        var bodyMs = new MemoryStream();
                        if (bodyBytesAvailable > 0)
                            bodyMs.Write(allBytes, bodyStart, Math.Min(bodyBytesAvailable, contentLength));

                        while (bodyMs.Length < contentLength)
                        {
                            int toRead = Math.Min(buf.Length, (int)(contentLength - bodyMs.Length));
                            int read = stream.Read(buf, 0, toRead);
                            if (read <= 0) break;
                            bodyMs.Write(buf, 0, read);
                        }
                        body = Encoding.UTF8.GetString(bodyMs.ToArray());

                        // Handle CORS preflight
                        if (method == "OPTIONS")
                        {
                            SendCorsHeaders(stream);
                            return;
                        }

                        // Parse path and query
                        string path = rawUrl;
                        string query = "";
                        int qIdx = rawUrl.IndexOf('?');
                        if (qIdx >= 0)
                        {
                            path = rawUrl.Substring(0, qIdx);
                            query = rawUrl.Substring(qIdx + 1);
                        }
                        var qParams = ParseQuery(query);

                        // Route static files / SPA
                        if (path == "/" || path == "/index.html")
                        {
                            SendHtml(stream, HtmlPage);
                            return;
                        }
                        if (path == "/favicon.ico")
                        {
                            SendResponse(stream, "204 No Content", "text/plain", new byte[0]);
                            return;
                        }

                        // API Routes
                        if (path.StartsWith("/api/"))
                        {
                            HandleApi(stream, method, path, qParams, headers, body);
                            return;
                        }

                        SendResponse(stream, "404 Not Found", "text/plain", Encoding.UTF8.GetBytes("Not Found"));
                    }
                    catch { }
                }
            }

            void HandleApi(NetworkStream stream, string method, string path, Dictionary<string, string> query, Dictionary<string, string> headers, string body)
            {
                // Pairing endpoint (no token required)
                if (path == "/api/pair" && method == "POST")
                {
                    HandlePair(stream, body);
                    return;
                }

                // Check authorization token for all other API endpoints
                string token = "";
                if (headers.ContainsKey("X-Errin-Token")) token = headers["X-Errin-Token"];
                else if (query.ContainsKey("token")) token = query["token"];

                Device dev = null;
                lock (syncLock)
                {
                    if (!string.IsNullOrEmpty(token))
                        dev = Devices.FirstOrDefault(d => string.Equals(d.token, token, StringComparison.OrdinalIgnoreCase));
                }

                if (dev == null)
                {
                    SendJson(stream, 401, new Dictionary<string, object> { { "ok", false }, { "error", "Unauthorized. Device not paired." } });
                    return;
                }

                // Update device seen timestamp
                dev.seen = DateTime.Now.ToString("yyyy-MM-dd HH:mm");

                // Authenticated routes
                try
                {
                    switch (path)
                    {
                        case "/api/me":
                            SendJson(stream, 200, new Dictionary<string, object> { { "ok", true }, { "name", dev.name }, { "paired", dev.paired } });
                            break;

                        case "/api/dashboard":
                            HandleDashboard(stream);
                            break;

                        case "/api/items":
                            HandleGetItems(stream, query);
                            break;

                        case "/api/item":
                            if (method == "POST") HandleSaveItem(stream, body);
                            else HandleGetItem(stream, query);
                            break;

                        case "/api/delete-item":
                            HandleDeleteItem(stream, body);
                            break;

                        case "/api/weed":
                            HandleWeedItem(stream, body);
                            break;

                        case "/api/unweed":
                            HandleUnweedItem(stream, body);
                            break;

                        case "/api/patrons":
                            HandleGetPatrons(stream, query);
                            break;

                        case "/api/patron":
                            if (method == "POST") HandleSavePatron(stream, body);
                            else HandleGetPatron(stream, query);
                            break;

                        case "/api/delete-patron":
                            HandleDeletePatron(stream, body);
                            break;

                        case "/api/checkout":
                            HandleCheckout(stream, body);
                            break;

                        case "/api/return":
                            HandleReturn(stream, body);
                            break;

                        case "/api/renew":
                            HandleRenew(stream, body);
                            break;

                        case "/api/hold":
                            HandleHold(stream, body);
                            break;

                        case "/api/cancel-hold":
                            HandleCancelHold(stream, body);
                            break;

                        case "/api/loans":
                            HandleGetLoans(stream, query);
                            break;

                        case "/api/fines":
                            HandleGetFines(stream, query);
                            break;

                        case "/api/pay-fine":
                            HandlePayFine(stream, body);
                            break;

                        case "/api/waive-fine":
                            HandleWaiveFine(stream, body);
                            break;

                        case "/api/reports":
                            HandleReports(stream);
                            break;

                        default:
                            SendJson(stream, 404, new Dictionary<string, object> { { "ok", false }, { "error", "Endpoint not found" } });
                            break;
                    }
                }
                catch (Exception ex)
                {
                    SendJson(stream, 500, new Dictionary<string, object> { { "ok", false }, { "error", ex.Message } });
                }
            }

            void HandlePair(NetworkStream stream, string body)
            {
                Dictionary<string, object> json = null;
                try { json = Json.Parse(body) as Dictionary<string, object>; } catch { }
                if (json == null)
                {
                    SendJson(stream, 400, new Dictionary<string, object> { { "ok", false }, { "error", "Invalid JSON payload" } });
                    return;
                }

                string code = Regex.Replace(Json.Str(json, "code"), @"[\s-]", "");
                string name = Json.Str(json, "name").Trim();
                if (string.IsNullOrEmpty(name)) name = "Remote Device";

                lock (syncLock)
                {
                    if (ActivePair == null)
                    {
                        SendJson(stream, 400, new Dictionary<string, object> {
                            { "ok", false },
                            { "error", "No active pairing session. Please ask the librarian to click 'Pair new device' in Settings." }
                        });
                        return;
                    }

                    if (DateTime.UtcNow > ActivePair.expiresAt)
                    {
                        ActivePair = null;
                        SendJson(stream, 400, new Dictionary<string, object> {
                            { "ok", false },
                            { "error", "Pairing code has expired. Please ask for a new code." }
                        });
                        return;
                    }

                    if (!string.Equals(code, ActivePair.code, StringComparison.OrdinalIgnoreCase))
                    {
                        ActivePair.failedAttempts++;
                        if (ActivePair.failedAttempts >= 3)
                        {
                            ActivePair = null;
                            SendJson(stream, 400, new Dictionary<string, object> {
                                { "ok", false },
                                { "error", "Too many incorrect attempts. Pairing code cancelled for security." }
                            });
                            return;
                        }
                        SendJson(stream, 400, new Dictionary<string, object> {
                            { "ok", false },
                            { "error", "Invalid pairing code. Please check and try again." }
                        });
                        return;
                    }

                    // Code matched! Generate 32-byte cryptographically secure random token (64 hex digits)
                    byte[] b = new byte[32];
                    using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(b);
                    string token = BitConverter.ToString(b).Replace("-", "").ToLowerInvariant();

                    var dev = new Device
                    {
                        token = token,
                        name = name,
                        paired = Lib.Today(),
                        seen = DateTime.Now.ToString("yyyy-MM-dd HH:mm")
                    };
                    Devices.Add(dev);
                    LastPairedDevice = dev;
                    ActivePair = null;

                    if (OnDevicePaired != null)
                    {
                        try { OnDevicePaired(dev); } catch { }
                    }

                    SendJson(stream, 200, new Dictionary<string, object> {
                        { "ok", true },
                        { "token", token },
                        { "name", dev.name }
                    });
                }
            }

            void HandleDashboard(NetworkStream stream)
            {
                Dictionary<string, object> res = null;
                ExecuteOnUi(() =>
                {
                    var db = GetDb();
                    var lib = GetLib();
                    var today = Lib.Today();

                    int activeTitles = db.Items.Count(i => !lib.IsWeeded(i));
                    int totalStudents = db.Patrons.Count;
                    int onLoan = db.Loans.Count(l => l.ret == null);
                    int allTimeLoans = db.Loans.Count;
                    int overdueCount = db.Loans.Count(l => lib.Overdue(l));
                    double unpaidFines = db.Fines.Where(f => f.paid == "").Sum(f => f.amt);

                    var overdueList = new List<object>();
                    foreach (var l in db.Loans.Where(l => lib.Overdue(l)).OrderBy(l => l.due).Take(30))
                    {
                        var item = db.Items.FirstOrDefault(i => i.id == l.iid);
                        var patron = db.Patrons.FirstOrDefault(p => p.id == l.pid);
                        int days = Lib.Diff(today, l.due);
                        double fine = Math.Round(days * db.Set.rate, 2);
                        overdueList.Add(new Dictionary<string, object> {
                            { "loanId", l.id },
                            { "patronId", l.pid },
                            { "patronName", patron != null ? patron.name : "(unknown)" },
                            { "patronClass", patron != null ? patron.cls : "" },
                            { "title", item != null ? item.title : "(deleted)" },
                            { "barcode", item != null ? item.barcode : "" },
                            { "due", l.due },
                            { "daysLate", (double)days },
                            { "fine", fine }
                        });
                    }

                    res = new Dictionary<string, object> {
                        { "ok", true },
                        { "activeTitles", (double)activeTitles },
                        { "totalStudents", (double)totalStudents },
                        { "onLoan", (double)onLoan },
                        { "allTimeLoans", (double)allTimeLoans },
                        { "overdueCount", (double)overdueCount },
                        { "unpaidFines", unpaidFines },
                        { "overdue", overdueList },
                        { "school", db.Set.school },
                        { "today", today }
                    };
                });
                SendJson(stream, 200, res);
            }

            void HandleGetItems(NetworkStream stream, Dictionary<string, string> query)
            {
                string q = (query.ContainsKey("q") ? query["q"] : "").Trim().ToLowerInvariant();
                string status = (query.ContainsKey("status") ? query["status"] : "active").ToLowerInvariant();
                int limit = 150;
                if (query.ContainsKey("limit")) int.TryParse(query["limit"], out limit);
                if (limit <= 0) limit = 150;

                var list = new List<object>();
                ExecuteOnUi(() =>
                {
                    var db = GetDb();
                    var lib = GetLib();
                    var openLoans = db.Loans.Where(l => l.ret == null).ToDictionary(l => l.iid, l => l);

                    IEnumerable<Item> items = db.Items;
                    if (status == "active") items = items.Where(i => !lib.IsWeeded(i));
                    else if (status == "weeded") items = items.Where(i => lib.IsWeeded(i));
                    else if (status == "onloan") items = items.Where(i => openLoans.ContainsKey(i.id));

                    if (!string.IsNullOrEmpty(q))
                    {
                        items = items.Where(i =>
                            (i.title != null && i.title.ToLowerInvariant().Contains(q)) ||
                            (i.author != null && i.author.ToLowerInvariant().Contains(q)) ||
                            (i.barcode != null && i.barcode.ToLowerInvariant().Contains(q)) ||
                            (i.isbn != null && i.isbn.ToLowerInvariant().Contains(q)) ||
                            (i.call != null && i.call.ToLowerInvariant().Contains(q)) ||
                            (i.subject != null && i.subject.ToLowerInvariant().Contains(q)));
                    }

                    foreach (var i in items.Take(limit))
                    {
                        var d = i.ToJ();
                        Loan l;
                        bool onLoan = openLoans.TryGetValue(i.id, out l);
                        d["onLoan"] = onLoan;
                        d["loanDue"] = onLoan ? l.due : "";
                        d["loanPatron"] = onLoan ? lib.PName(l.pid) : "";
                        d["loanPatronId"] = onLoan ? l.pid : "";
                        list.Add(d);
                    }
                });

                SendJson(stream, 200, new Dictionary<string, object> { { "ok", true }, { "items", list } });
            }

            void HandleGetItem(NetworkStream stream, Dictionary<string, string> query)
            {
                string id = query.ContainsKey("id") ? query["id"] : "";
                string bc = query.ContainsKey("barcode") ? query["barcode"] : "";
                Dictionary<string, object> d = null;

                ExecuteOnUi(() =>
                {
                    var db = GetDb();
                    var lib = GetLib();
                    Item i = null;
                    if (!string.IsNullOrEmpty(id)) i = db.Items.FirstOrDefault(x => x.id == id);
                    if (i == null && !string.IsNullOrEmpty(bc)) i = lib.ItemByBarcode(bc);
                    if (i != null)
                    {
                        d = i.ToJ();
                        var l = lib.OpenLoan(i.id);
                        d["onLoan"] = l != null;
                        if (l != null)
                        {
                            d["loanId"] = l.id;
                            d["loanPatronId"] = l.pid;
                            d["loanPatronName"] = lib.PName(l.pid);
                            d["loanDue"] = l.due;
                        }
                    }
                });

                if (d == null) SendJson(stream, 404, new Dictionary<string, object> { { "ok", false }, { "error", "Item not found" } });
                else SendJson(stream, 200, new Dictionary<string, object> { { "ok", true }, { "item", d } });
            }

            void HandleSaveItem(NetworkStream stream, string body)
            {
                var json = Json.Parse(body) as Dictionary<string, object>;
                if (json == null) { SendJson(stream, 400, new Dictionary<string, object> { { "ok", false }, { "error", "Bad JSON" } }); return; }

                Dictionary<string, object> result = null;
                ExecuteOnUi(() =>
                {
                    var db = GetDb();
                    var lib = GetLib();
                    string id = Json.Str(json, "id");
                    Item item = null;
                    bool isNew = false;
                    if (!string.IsNullOrEmpty(id)) item = db.Items.FirstOrDefault(x => x.id == id);

                    if (item == null)
                    {
                        item = new Item { id = Lib.Uid() };
                        isNew = true;
                    }

                    foreach (var k in Item.Keys)
                    {
                        if (json.ContainsKey(k)) item.Set(k, Json.Str(json, k).Trim());
                    }

                    if (string.IsNullOrEmpty(item.barcode)) item.barcode = lib.NextBarcode();
                    if (string.IsNullOrEmpty(item.ctl)) item.ctl = lib.NextCtl();

                    if (isNew) db.Items.Add(item);
                    if (OnRemoteWrite != null) OnRemoteWrite(isNew ? "Book added: " + item.title : "Book updated: " + item.title);

                    result = item.ToJ();
                });

                SendJson(stream, 200, new Dictionary<string, object> { { "ok", true }, { "item", result } });
            }

            void HandleDeleteItem(NetworkStream stream, string body)
            {
                var json = Json.Parse(body) as Dictionary<string, object>;
                string id = Json.Str(json, "id");
                string error = null;

                ExecuteOnUi(() =>
                {
                    var db = GetDb();
                    var lib = GetLib();
                    var item = db.Items.FirstOrDefault(i => i.id == id);
                    if (item == null) { error = "Book not found"; return; }
                    if (lib.OpenLoan(item.id) != null) { error = "Book is currently checked out. Return it first."; return; }

                    db.Items.Remove(item);
                    db.Holds.RemoveAll(h => h.iid == item.id);
                    if (OnRemoteWrite != null) OnRemoteWrite("Book deleted: " + item.title);
                });

                if (error != null) SendJson(stream, 400, new Dictionary<string, object> { { "ok", false }, { "error", error } });
                else SendJson(stream, 200, new Dictionary<string, object> { { "ok", true } });
            }

            void HandleWeedItem(NetworkStream stream, string body)
            {
                var json = Json.Parse(body) as Dictionary<string, object>;
                string id = Json.Str(json, "id");
                string reason = Json.Str(json, "reason");
                string error = null;

                ExecuteOnUi(() =>
                {
                    var db = GetDb();
                    var lib = GetLib();
                    var item = db.Items.FirstOrDefault(i => i.id == id);
                    if (item == null) { error = "Book not found"; return; }
                    if (lib.OpenLoan(item.id) != null) { error = "Book is currently checked out. Return it before weeding."; return; }

                    lib.Weed(item, reason);
                    if (OnRemoteWrite != null) OnRemoteWrite("Book weeded: " + item.title);
                });

                if (error != null) SendJson(stream, 400, new Dictionary<string, object> { { "ok", false }, { "error", error } });
                else SendJson(stream, 200, new Dictionary<string, object> { { "ok", true } });
            }

            void HandleUnweedItem(NetworkStream stream, string body)
            {
                var json = Json.Parse(body) as Dictionary<string, object>;
                string id = Json.Str(json, "id");
                string error = null;

                ExecuteOnUi(() =>
                {
                    var db = GetDb();
                    var lib = GetLib();
                    var item = db.Items.FirstOrDefault(i => i.id == id);
                    if (item == null) { error = "Book not found"; return; }
                    lib.Unweed(item);
                    if (OnRemoteWrite != null) OnRemoteWrite("Book restored: " + item.title);
                });

                if (error != null) SendJson(stream, 400, new Dictionary<string, object> { { "ok", false }, { "error", error } });
                else SendJson(stream, 200, new Dictionary<string, object> { { "ok", true } });
            }

            void HandleGetPatrons(NetworkStream stream, Dictionary<string, string> query)
            {
                string q = (query.ContainsKey("q") ? query["q"] : "").Trim().ToLowerInvariant();
                var list = new List<object>();

                ExecuteOnUi(() =>
                {
                    var db = GetDb();
                    var lib = GetLib();
                    var today = Lib.Today();

                    IEnumerable<Patron> patrons = db.Patrons;
                    if (!string.IsNullOrEmpty(q))
                    {
                        patrons = patrons.Where(p =>
                            (p.name != null && p.name.ToLowerInvariant().Contains(q)) ||
                            (p.id != null && p.id.ToLowerInvariant().Contains(q)) ||
                            (p.cls != null && p.cls.ToLowerInvariant().Contains(q)) ||
                            (p.contact != null && p.contact.ToLowerInvariant().Contains(q)));
                    }

                    foreach (var p in patrons.OrderBy(p => p.name))
                    {
                        int loansCount = db.Loans.Count(l => l.pid == p.id && l.ret == null);
                        double unpaid = lib.Unpaid(p.id);
                        bool isBlocked = unpaid >= db.Set.block || db.Loans.Any(l => l.pid == p.id && lib.Overdue(l));

                        list.Add(new Dictionary<string, object> {
                            { "id", p.id },
                            { "name", p.name },
                            { "cls", p.cls },
                            { "contact", p.contact },
                            { "reg", p.reg },
                            { "last", p.last },
                            { "activeLoans", (double)loansCount },
                            { "unpaidFines", unpaid },
                            { "blocked", isBlocked }
                        });
                    }
                });

                SendJson(stream, 200, new Dictionary<string, object> { { "ok", true }, { "patrons", list } });
            }

            void HandleGetPatron(NetworkStream stream, Dictionary<string, string> query)
            {
                string id = query.ContainsKey("id") ? query["id"] : "";
                Dictionary<string, object> res = null;

                ExecuteOnUi(() =>
                {
                    var db = GetDb();
                    var lib = GetLib();
                    var p = lib.PatronById(id);
                    if (p == null) return;

                    var activeLoans = new List<object>();
                    var pastLoans = new List<object>();
                    foreach (var l in db.Loans.Where(x => x.pid == p.id).OrderByDescending(x => x.@out))
                    {
                        var it = db.Items.FirstOrDefault(x => x.id == l.iid);
                        var ld = new Dictionary<string, object> {
                            { "id", l.id },
                            { "title", it != null ? it.title : "(deleted)" },
                            { "barcode", it != null ? it.barcode : "" },
                            { "out", l.@out },
                            { "due", l.due },
                            { "ret", l.ret },
                            { "overdue", lib.Overdue(l) },
                            { "ren", (double)l.ren }
                        };
                        if (l.ret == null) activeLoans.Add(ld);
                        else pastLoans.Add(ld);
                    }

                    var fines = new List<object>();
                    foreach (var f in db.Fines.Where(x => x.pid == p.id).OrderByDescending(x => x.date))
                    {
                        var it = db.Items.FirstOrDefault(x => x.id == f.iid);
                        fines.Add(new Dictionary<string, object> {
                            { "id", f.id },
                            { "title", it != null ? it.title : "" },
                            { "date", f.date },
                            { "days", (double)f.days },
                            { "amt", f.amt },
                            { "paid", f.paid }
                        });
                    }

                    res = new Dictionary<string, object> {
                        { "id", p.id },
                        { "name", p.name },
                        { "cls", p.cls },
                        { "contact", p.contact },
                        { "reg", p.reg },
                        { "last", p.last },
                        { "unpaidFines", lib.Unpaid(p.id) },
                        { "activeLoans", activeLoans },
                        { "pastLoans", pastLoans },
                        { "fines", fines }
                    };
                });

                if (res == null) SendJson(stream, 404, new Dictionary<string, object> { { "ok", false }, { "error", "Student not found" } });
                else SendJson(stream, 200, new Dictionary<string, object> { { "ok", true }, { "patron", res } });
            }

            void HandleSavePatron(NetworkStream stream, string body)
            {
                var json = Json.Parse(body) as Dictionary<string, object>;
                if (json == null) { SendJson(stream, 400, new Dictionary<string, object> { { "ok", false }, { "error", "Bad JSON" } }); return; }

                string id = Json.Str(json, "id").Trim();
                string name = Json.Str(json, "name").Trim();
                string cls = Json.Str(json, "cls").Trim();
                string contact = Json.Str(json, "contact").Trim();

                if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(name))
                {
                    SendJson(stream, 400, new Dictionary<string, object> { { "ok", false }, { "error", "Student ID and Name are required" } });
                    return;
                }

                // If IC is 12 digits, normalize
                string norm = Lib.NormIc(id);
                if (norm != null) id = norm;

                Dictionary<string, object> result = null;
                ExecuteOnUi(() =>
                {
                    var db = GetDb();
                    var lib = GetLib();
                    var p = lib.PatronById(id);
                    bool isNew = false;
                    if (p == null)
                    {
                        p = new Patron { id = id, reg = Lib.Today() };
                        db.Patrons.Add(p);
                        isNew = true;
                    }
                    p.name = name;
                    p.cls = cls;
                    p.contact = contact;
                    p.last = Lib.Today();

                    if (OnRemoteWrite != null) OnRemoteWrite(isNew ? "Student added: " + p.name : "Student updated: " + p.name);
                    result = new Dictionary<string, object> {
                        { "id", p.id },
                        { "name", p.name },
                        { "cls", p.cls },
                        { "contact", p.contact }
                    };
                });

                SendJson(stream, 200, new Dictionary<string, object> { { "ok", true }, { "patron", result } });
            }

            void HandleDeletePatron(NetworkStream stream, string body)
            {
                var json = Json.Parse(body) as Dictionary<string, object>;
                string id = Json.Str(json, "id");
                string error = null;

                ExecuteOnUi(() =>
                {
                    var db = GetDb();
                    var lib = GetLib();
                    var p = lib.PatronById(id);
                    if (p == null) { error = "Student not found"; return; }
                    if (db.Loans.Any(l => l.pid == p.id && l.ret == null)) { error = "Student has books on loan. Return them first."; return; }
                    if (lib.Unpaid(p.id) > 0) { error = "Student has unpaid fines."; return; }

                    db.Patrons.Remove(p);
                    db.Loans.RemoveAll(l => l.pid == p.id);
                    db.Fines.RemoveAll(f => f.pid == p.id);
                    db.Holds.RemoveAll(h => h.pid == p.id);
                    if (OnRemoteWrite != null) OnRemoteWrite("Student deleted: " + p.name);
                });

                if (error != null) SendJson(stream, 400, new Dictionary<string, object> { { "ok", false }, { "error", error } });
                else SendJson(stream, 200, new Dictionary<string, object> { { "ok", true } });
            }

            void HandleCheckout(NetworkStream stream, string body)
            {
                var json = Json.Parse(body) as Dictionary<string, object>;
                string patron = Json.Str(json, "patron").Trim();
                string barcode = Json.Str(json, "barcode").Trim();

                string msg = null;
                string err = null;
                ExecuteOnUi(() =>
                {
                    var lib = GetLib();
                    err = lib.Checkout(patron, barcode, out msg);
                    if (err == null && OnRemoteWrite != null) OnRemoteWrite(msg);
                });

                if (err != null)
                {
                    SendJson(stream, 400, new Dictionary<string, object> {
                        { "ok", false },
                        { "error", err },
                        { "friendly", Lib.Friendly(err) }
                    });
                }
                else
                {
                    SendJson(stream, 200, new Dictionary<string, object> { { "ok", true }, { "msg", msg } });
                }
            }

            void HandleReturn(NetworkStream stream, string body)
            {
                var json = Json.Parse(body) as Dictionary<string, object>;
                string barcode = Json.Str(json, "barcode").Trim();

                string msg = null;
                string err = null;
                ExecuteOnUi(() =>
                {
                    var lib = GetLib();
                    err = lib.Return(barcode, out msg);
                    if (err == null && OnRemoteWrite != null) OnRemoteWrite(msg);
                });

                if (err != null)
                {
                    SendJson(stream, 400, new Dictionary<string, object> {
                        { "ok", false },
                        { "error", err },
                        { "friendly", Lib.Friendly(err) }
                    });
                }
                else
                {
                    SendJson(stream, 200, new Dictionary<string, object> { { "ok", true }, { "msg", msg } });
                }
            }

            void HandleRenew(NetworkStream stream, string body)
            {
                var json = Json.Parse(body) as Dictionary<string, object>;
                string barcode = Json.Str(json, "barcode").Trim();

                string msg = null;
                string err = null;
                ExecuteOnUi(() =>
                {
                    var lib = GetLib();
                    err = lib.Renew(barcode, out msg);
                    if (err == null && OnRemoteWrite != null) OnRemoteWrite(msg);
                });

                if (err != null)
                {
                    SendJson(stream, 400, new Dictionary<string, object> {
                        { "ok", false },
                        { "error", err },
                        { "friendly", Lib.Friendly(err) }
                    });
                }
                else
                {
                    SendJson(stream, 200, new Dictionary<string, object> { { "ok", true }, { "msg", msg } });
                }
            }

            void HandleHold(NetworkStream stream, string body)
            {
                var json = Json.Parse(body) as Dictionary<string, object>;
                string patron = Json.Str(json, "patron").Trim();
                string barcode = Json.Str(json, "barcode").Trim();

                string msg = null;
                string err = null;
                ExecuteOnUi(() =>
                {
                    var lib = GetLib();
                    err = lib.PlaceHold(patron, barcode, out msg);
                    if (err == null && OnRemoteWrite != null) OnRemoteWrite(msg);
                });

                if (err != null) SendJson(stream, 400, new Dictionary<string, object> { { "ok", false }, { "error", err } });
                else SendJson(stream, 200, new Dictionary<string, object> { { "ok", true }, { "msg", msg } });
            }

            void HandleCancelHold(NetworkStream stream, string body)
            {
                var json = Json.Parse(body) as Dictionary<string, object>;
                string holdId = Json.Str(json, "holdId").Trim();
                bool found = false;

                ExecuteOnUi(() =>
                {
                    var db = GetDb();
                    int rem = db.Holds.RemoveAll(h => h.id == holdId);
                    found = rem > 0;
                    if (found && OnRemoteWrite != null) OnRemoteWrite("Hold cancelled");
                });

                if (!found) SendJson(stream, 404, new Dictionary<string, object> { { "ok", false }, { "error", "Hold not found" } });
                else SendJson(stream, 200, new Dictionary<string, object> { { "ok", true } });
            }

            void HandleGetLoans(NetworkStream stream, Dictionary<string, string> query)
            {
                string q = (query.ContainsKey("q") ? query["q"] : "").Trim().ToLowerInvariant();
                var list = new List<object>();

                ExecuteOnUi(() =>
                {
                    var db = GetDb();
                    var lib = GetLib();
                    var today = Lib.Today();

                    foreach (var l in db.Loans.Where(l => l.ret == null).OrderBy(l => l.due))
                    {
                        var it = db.Items.FirstOrDefault(i => i.id == l.iid);
                        var p = db.Patrons.FirstOrDefault(x => x.id == l.pid);
                        string t = it != null ? it.title : "(deleted)";
                        string bc = it != null ? it.barcode : "";
                        string pn = p != null ? p.name : "(unknown)";
                        string pc = p != null ? p.cls : "";

                        if (!string.IsNullOrEmpty(q))
                        {
                            if (!t.ToLowerInvariant().Contains(q) &&
                                !bc.ToLowerInvariant().Contains(q) &&
                                !pn.ToLowerInvariant().Contains(q) &&
                                !pc.ToLowerInvariant().Contains(q)) continue;
                        }

                        bool overdue = lib.Overdue(l);
                        int daysLate = overdue ? Lib.Diff(today, l.due) : 0;

                        list.Add(new Dictionary<string, object> {
                            { "id", l.id },
                            { "iid", l.iid },
                            { "pid", l.pid },
                            { "title", t },
                            { "barcode", bc },
                            { "patronName", pn },
                            { "patronClass", pc },
                            { "out", l.@out },
                            { "due", l.due },
                            { "ren", (double)l.ren },
                            { "overdue", overdue },
                            { "daysLate", (double)daysLate }
                        });
                    }
                });

                SendJson(stream, 200, new Dictionary<string, object> { { "ok", true }, { "loans", list } });
            }

            void HandleGetFines(NetworkStream stream, Dictionary<string, string> query)
            {
                string status = (query.ContainsKey("status") ? query["status"] : "all").ToLowerInvariant();
                var list = new List<object>();

                ExecuteOnUi(() =>
                {
                    var db = GetDb();
                    var lib = GetLib();

                    IEnumerable<Fine> fines = db.Fines;
                    if (status == "unpaid") fines = fines.Where(f => f.paid == "");
                    else if (status == "paid") fines = fines.Where(f => f.paid != "");

                    foreach (var f in fines.OrderByDescending(f => f.date))
                    {
                        var it = db.Items.FirstOrDefault(i => i.id == f.iid);
                        var p = db.Patrons.FirstOrDefault(x => x.id == f.pid);
                        list.Add(new Dictionary<string, object> {
                            { "id", f.id },
                            { "pid", f.pid },
                            { "patronName", p != null ? p.name : "(unknown)" },
                            { "patronClass", p != null ? p.cls : "" },
                            { "iid", f.iid },
                            { "title", it != null ? it.title : "(deleted)" },
                            { "barcode", it != null ? it.barcode : "" },
                            { "date", f.date },
                            { "days", (double)f.days },
                            { "amt", f.amt },
                            { "paid", f.paid }
                        });
                    }
                });

                SendJson(stream, 200, new Dictionary<string, object> { { "ok", true }, { "fines", list } });
            }

            void HandlePayFine(NetworkStream stream, string body)
            {
                var json = Json.Parse(body) as Dictionary<string, object>;
                string id = Json.Str(json, "id");
                string error = null;

                ExecuteOnUi(() =>
                {
                    var db = GetDb();
                    var f = db.Fines.FirstOrDefault(x => x.id == id);
                    if (f == null) { error = "Fine record not found"; return; }
                    f.paid = Lib.Today();
                    if (OnRemoteWrite != null) OnRemoteWrite("Fine paid: " + Lib.Money(f.amt));
                });

                if (error != null) SendJson(stream, 400, new Dictionary<string, object> { { "ok", false }, { "error", error } });
                else SendJson(stream, 200, new Dictionary<string, object> { { "ok", true } });
            }

            void HandleWaiveFine(NetworkStream stream, string body)
            {
                var json = Json.Parse(body) as Dictionary<string, object>;
                string id = Json.Str(json, "id");
                string error = null;

                ExecuteOnUi(() =>
                {
                    var db = GetDb();
                    var f = db.Fines.FirstOrDefault(x => x.id == id);
                    if (f == null) { error = "Fine record not found"; return; }
                    f.paid = "waived";
                    if (OnRemoteWrite != null) OnRemoteWrite("Fine waived: " + Lib.Money(f.amt));
                });

                if (error != null) SendJson(stream, 400, new Dictionary<string, object> { { "ok", false }, { "error", error } });
                else SendJson(stream, 200, new Dictionary<string, object> { { "ok", true } });
            }

            void HandleReports(NetworkStream stream)
            {
                Dictionary<string, object> res = null;
                ExecuteOnUi(() =>
                {
                    var db = GetDb();
                    var lib = GetLib();

                    int totalItems = db.Items.Count;
                    int activeItems = db.Items.Count(i => !lib.IsWeeded(i));
                    int weededItems = totalItems - activeItems;
                    int totalStudents = db.Patrons.Count;
                    int totalLoans = db.Loans.Count;

                    double finesAssessed = db.Fines.Sum(f => f.amt);
                    double finesCollected = db.Fines.Where(f => f.paid != "" && f.paid != "waived").Sum(f => f.amt);
                    double finesWaived = db.Fines.Where(f => f.paid == "waived").Sum(f => f.amt);
                    double finesUnpaid = db.Fines.Where(f => f.paid == "").Sum(f => f.amt);

                    // Top borrowed books
                    var topBooks = db.Loans.GroupBy(l => l.iid)
                        .OrderByDescending(g => g.Count())
                        .Take(10)
                        .Select(g =>
                        {
                            var it = db.Items.FirstOrDefault(i => i.id == g.Key);
                            return (object)new Dictionary<string, object> {
                                { "title", it != null ? it.title : "(deleted)" },
                                { "author", it != null ? it.author : "" },
                                { "count", (double)g.Count() }
                            };
                        }).ToList();

                    // Top student borrowers
                    var topBorrowers = db.Loans.GroupBy(l => l.pid)
                        .OrderByDescending(g => g.Count())
                        .Take(10)
                        .Select(g =>
                        {
                            var p = db.Patrons.FirstOrDefault(x => x.id == g.Key);
                            return (object)new Dictionary<string, object> {
                                { "name", p != null ? p.name : "(removed)" },
                                { "cls", p != null ? p.cls : "" },
                                { "count", (double)g.Count() }
                            };
                        }).ToList();

                    // Class breakdown
                    var classBreakdown = db.Patrons.GroupBy(p => string.IsNullOrEmpty(p.cls) ? "(No class)" : p.cls)
                        .OrderBy(g => g.Key)
                        .Select(g =>
                        {
                            var pids = new HashSet<string>(g.Select(p => p.id));
                            int activeLoans = db.Loans.Count(l => l.ret == null && pids.Contains(l.pid));
                            return (object)new Dictionary<string, object> {
                                { "cls", g.Key },
                                { "students", (double)g.Count() },
                                { "activeLoans", (double)activeLoans }
                            };
                        }).ToList();

                    res = new Dictionary<string, object> {
                        { "ok", true },
                        { "totalItems", (double)totalItems },
                        { "activeItems", (double)activeItems },
                        { "weededItems", (double)weededItems },
                        { "totalStudents", (double)totalStudents },
                        { "totalLoans", (double)totalLoans },
                        { "finesAssessed", finesAssessed },
                        { "finesCollected", finesCollected },
                        { "finesWaived", finesWaived },
                        { "finesUnpaid", finesUnpaid },
                        { "topBooks", topBooks },
                        { "topBorrowers", topBorrowers },
                        { "classBreakdown", classBreakdown }
                    };
                });

                SendJson(stream, 200, res);
            }

            void ExecuteOnUi(Action action)
            {
                if (RunOnUi != null) RunOnUi(action);
                else action();
            }

            // HTTP helpers
            void SendCorsHeaders(NetworkStream stream)
            {
                string header = "HTTP/1.1 204 No Content\r\n" +
                    "Connection: close\r\n" +
                    "Access-Control-Allow-Origin: *\r\n" +
                    "Access-Control-Allow-Methods: GET, POST, OPTIONS\r\n" +
                    "Access-Control-Allow-Headers: Content-Type, X-Errin-Token\r\n" +
                    "Content-Length: 0\r\n\r\n";
                byte[] b = Encoding.ASCII.GetBytes(header);
                stream.Write(b, 0, b.Length);
            }

            void SendJson(NetworkStream stream, int statusCode, object data)
            {
                string json = Json.Write(data);
                byte[] body = Encoding.UTF8.GetBytes(json);
                string statusText = statusCode == 200 ? "200 OK" :
                                    statusCode == 400 ? "400 Bad Request" :
                                    statusCode == 401 ? "401 Unauthorized" :
                                    statusCode == 404 ? "404 Not Found" : "500 Internal Server Error";
                SendResponse(stream, statusText, "application/json; charset=utf-8", body);
            }

            void SendHtml(NetworkStream stream, string html)
            {
                byte[] body = Encoding.UTF8.GetBytes(html);
                SendResponse(stream, "200 OK", "text/html; charset=utf-8", body);
            }

            void SendResponse(NetworkStream stream, string statusText, string contentType, byte[] body)
            {
                string header = "HTTP/1.1 " + statusText + "\r\n" +
                    "Content-Type: " + contentType + "\r\n" +
                    "Content-Length: " + body.Length + "\r\n" +
                    "Connection: close\r\n" +
                    "Access-Control-Allow-Origin: *\r\n" +
                    "Access-Control-Allow-Methods: GET, POST, OPTIONS\r\n" +
                    "Access-Control-Allow-Headers: Content-Type, X-Errin-Token\r\n\r\n";
                byte[] hBytes = Encoding.ASCII.GetBytes(header);
                stream.Write(hBytes, 0, hBytes.Length);
                if (body.Length > 0) stream.Write(body, 0, body.Length);
            }

            static Dictionary<string, string> ParseQuery(string q)
            {
                var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                if (string.IsNullOrEmpty(q)) return d;
                string[] parts = q.Split('&');
                foreach (var part in parts)
                {
                    int eq = part.IndexOf('=');
                    if (eq >= 0)
                    {
                        string k = Uri.UnescapeDataString(part.Substring(0, eq));
                        string v = Uri.UnescapeDataString(part.Substring(eq + 1));
                        d[k] = v;
                    }
                    else
                    {
                        d[Uri.UnescapeDataString(part)] = "";
                    }
                }
                return d;
            }

            static int FindPattern(byte[] src, byte[] pattern)
            {
                int max = src.Length - pattern.Length;
                for (int i = 0; i <= max; i++)
                {
                    bool match = true;
                    for (int j = 0; j < pattern.Length; j++)
                    {
                        if (src[i + j] != pattern[j]) { match = false; break; }
                    }
                    if (match) return i;
                }
                return -1;
            }
        }

        public static Server StartServer(
            int port,
            bool localOnly,
            List<Device> devices,
            Func<Db> getDb,
            Func<Lib> getLib,
            Action saveDb,
            Action<Action> runOnUi,
            Action<string> onRemoteWrite,
            Action<Device> onDevicePaired,
            Action<string> log)
        {
            var s = new Server
            {
                Devices = devices ?? new List<Device>(),
                GetDb = getDb,
                GetLib = getLib,
                SaveStore = saveDb,
                RunOnUi = runOnUi,
                OnRemoteWrite = onRemoteWrite,
                OnDevicePaired = onDevicePaired,
                Log = log ?? (m => { })
            };
            s.Start(port, localOnly);
            return s;
        }

        // Single-Page Web Application: HTML, CSS, JavaScript in a single self-contained string
        public const string HtmlPage = @"<!DOCTYPE html>
<html lang=""en"">
<head>
<meta charset=""utf-8"">
<meta name=""viewport"" content=""width=device-width, initial-scale=1.0, maximum-scale=1.0, user-scalable=no"">
<title>ErrinILS Remote Access</title>
<style>
* { box-sizing: border-box; margin: 0; padding: 0; }
body { font-family: -apple-system, BlinkMacSystemFont, ""Segoe UI"", Roboto, Helvetica, Arial, sans-serif; background: #232427; color: #f1efe8; line-height: 1.4; -webkit-font-smoothing: antialiased; }
button, input, select, textarea { font-family: inherit; font-size: 15px; }
a { color: #f6c61b; text-decoration: none; }

/* Top Header */
.top-header { background: #232427; border-bottom: 2px solid #33343a; display: flex; align-items: center; justify-content: space-between; padding: 0 16px; height: 56px; position: sticky; top: 0; z-index: 100; }
.logo-area { display: flex; align-items: baseline; gap: 8px; }
.logo-title { font-size: 24px; font-weight: 800; color: #2f6bff; letter-spacing: -0.5px; }
.logo-view { font-size: 16px; font-weight: 600; color: #f1efe8; }
.device-badge { display: flex; align-items: center; gap: 8px; font-size: 12px; color: #9a9a94; }
.dot-green { width: 8px; height: 8px; border-radius: 50%; background: #2fb36b; display: inline-block; }
.btn-unpair { background: transparent; border: 1px solid #52545c; color: #9a9a94; border-radius: 4px; padding: 4px 8px; font-size: 11px; cursor: pointer; }
.btn-unpair:hover { background: #3d1b1b; color: #e8392f; border-color: #e8392f; }

/* Navigation Tab Bar */
.tab-bar { display: flex; overflow-x: auto; background: #1c1d20; border-bottom: 1px solid #33343a; padding: 0 8px; scrollbar-width: none; }
.tab-bar::-webkit-scrollbar { display: none; }
.tab-btn { background: transparent; border: none; color: #9a9a94; padding: 12px 16px; font-weight: 600; font-size: 14px; cursor: pointer; white-space: nowrap; border-bottom: 3px solid transparent; transition: all 0.15s; }
.tab-btn:hover { color: #f1efe8; }
.tab-btn.active { color: #f6c61b; border-bottom-color: #f6c61b; }

/* Content Layout */
.container { max-width: 1000px; margin: 0 auto; padding: 16px; padding-bottom: 60px; }
.card { background: #2c2d31; border: 1px solid #44454a; border-radius: 8px; padding: 16px; margin-bottom: 16px; box-shadow: 0 2px 6px rgba(0,0,0,0.25); }
.card-title { font-size: 17px; font-weight: 700; color: #f1efe8; margin-bottom: 12px; display: flex; justify-content: space-between; align-items: center; }
.card-desc { font-size: 13px; color: #9a9a94; margin-bottom: 14px; }

/* Metrics Grid */
.metrics-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(130px, 1fr)); gap: 12px; margin-bottom: 16px; }
.metric-box { background: #2c2d31; border: 1px solid #44454a; border-radius: 8px; padding: 14px 12px; text-align: center; }
.metric-val { font-size: 26px; font-weight: 800; color: #f6c61b; line-height: 1.1; margin-bottom: 4px; }
.metric-lbl { font-size: 12px; font-weight: 600; color: #9a9a94; text-transform: uppercase; letter-spacing: 0.5px; }

/* Buttons & Inputs */
.btn { display: inline-flex; align-items: center; justify-content: center; padding: 10px 16px; border-radius: 6px; font-weight: 600; cursor: pointer; border: none; transition: background 0.15s; }
.btn-primary { background: #f6c61b; color: #1b1b1b; }
.btn-primary:hover { background: #e5b610; }
.btn-sec { background: #44454a; color: #f1efe8; }
.btn-sec:hover { background: #52545c; }
.btn-danger { background: #e8392f; color: #ffffff; }
.btn-danger:hover { background: #cc2f26; }
.btn-sm { padding: 6px 10px; font-size: 12px; border-radius: 4px; }

.input-text, .select-box { width: 100%; background: #1b1b1b; border: 1px solid #52545c; color: #f1efe8; padding: 10px 12px; border-radius: 6px; outline: none; }
.input-text:focus, .select-box:focus { border-color: #f6c61b; }
.field-group { margin-bottom: 12px; }
.field-label { display: block; font-size: 12px; font-weight: 600; color: #9a9a94; margin-bottom: 4px; text-transform: uppercase; }

/* Tables */
.table-wrap { overflow-x: auto; -webkit-overflow-scrolling: touch; }
table { width: 100%; border-collapse: collapse; text-align: left; font-size: 14px; }
th { background: #232427; color: #9a9a94; font-size: 12px; font-weight: 600; text-transform: uppercase; padding: 10px 12px; border-bottom: 1px solid #44454a; }
td { padding: 10px 12px; border-bottom: 1px solid #38393e; color: #f1efe8; vertical-align: middle; }
tr:hover td { background: rgba(255,255,255,0.02); }

/* Badges */
.badge { display: inline-block; padding: 3px 8px; border-radius: 4px; font-size: 11px; font-weight: 700; }
.badge-ok { background: #1b3d29; color: #2fb36b; }
.badge-err { background: #3d1b1b; color: #e8392f; }
.badge-warn { background: #3d341b; color: #f6c61b; }
.badge-dim { background: #33343a; color: #9a9a94; }

/* Banners / Notifications */
.notice { padding: 12px 16px; border-radius: 6px; font-size: 14px; font-weight: 600; margin-bottom: 14px; }
.notice-ok { background: #1b3d29; border: 1px solid #2fb36b; color: #2fb36b; }
.notice-err { background: #3d1b1b; border: 1px solid #e8392f; color: #e8392f; }

/* Pairing Card View */
.pairing-screen { min-height: 100vh; display: flex; align-items: center; justify-content: center; padding: 20px; }
.pair-box { background: #2c2d31; border: 1px solid #44454a; border-radius: 12px; padding: 32px 24px; max-width: 440px; width: 100%; text-align: center; box-shadow: 0 8px 24px rgba(0,0,0,0.4); }
.pair-logo { font-size: 44px; font-weight: 900; color: #2f6bff; margin-bottom: 12px; }
.pair-heading { font-size: 18px; font-weight: 700; margin-bottom: 8px; }
.pair-sub { font-size: 14px; color: #9a9a94; margin-bottom: 24px; }
.code-box { font-size: 32px; font-weight: 800; letter-spacing: 6px; text-align: center; padding: 12px; color: #f6c61b; background: #1b1b1b; border: 2px solid #52545c; border-radius: 8px; width: 100%; margin-bottom: 16px; text-transform: uppercase; }
.code-box:focus { border-color: #f6c61b; }

/* Sub-nav buttons for Circulation */
.mode-bar { display: flex; gap: 8px; margin-bottom: 16px; overflow-x: auto; }
.mode-btn { flex: 1; padding: 10px; border-radius: 6px; border: 1px solid #44454a; background: #232427; color: #f1efe8; font-weight: 600; font-size: 13px; cursor: pointer; text-align: center; white-space: nowrap; }
.mode-btn.active { background: #f6c61b; color: #1b1b1b; border-color: #f6c61b; }

/* Modal overlay */
.modal-overlay { position: fixed; top: 0; left: 0; right: 0; bottom: 0; background: rgba(0,0,0,0.7); display: flex; align-items: center; justify-content: center; z-index: 200; padding: 16px; }
.modal-content { background: #2c2d31; border: 1px solid #52545c; border-radius: 8px; padding: 20px; width: 100%; max-width: 500px; max-height: 90vh; overflow-y: auto; }
.modal-title { font-size: 18px; font-weight: 700; margin-bottom: 14px; }
.modal-actions { display: flex; justify-content: flex-end; gap: 8px; margin-top: 18px; }
</style>
</head>
<body>

<div id=""pairView"" class=""pairing-screen"" style=""display: none;"">
  <div class=""pair-box"">
    <div class=""pair-logo"">errin.</div>
    <div class=""pair-heading"">Connect this device</div>
    <div class=""pair-sub"">This device is not yet connected to the library system. Ask the librarian for a pairing code, then type it below:</div>
    <input type=""text"" id=""pairCode"" class=""code-box"" placeholder=""000 000"" maxlength=""7"" autocomplete=""off"" />
    <div class=""field-group"" style=""text-align: left;"">
      <label class=""field-label"">Device name</label>
      <input type=""text"" id=""pairName"" class=""input-text"" placeholder=""e.g. Teacher Room iPad, Desk PC"" />
    </div>
    <button id=""pairBtn"" class=""btn btn-primary"" style=""width: 100%; padding: 14px; font-size: 16px; margin-top: 8px;"" onclick=""doPair()"">Connect Device</button>
    <div id=""pairErr"" style=""color: #e8392f; font-weight: 600; font-size: 13px; margin-top: 12px; display: none;""></div>
  </div>
</div>

<div id=""appView"" style=""display: none;"">
  <header class=""top-header"">
    <div class=""logo-area"">
      <span class=""logo-title"">errin.</span>
      <span id=""viewTitle"" class=""logo-view"">Dashboard</span>
    </div>
    <div class=""device-badge"">
      <span class=""dot-green""></span>
      <span id=""devName"">Paired</span>
      <button class=""btn-unpair"" onclick=""doUnpair()"">Disconnect</button>
    </div>
  </header>

  <nav class=""tab-bar"">
    <button class=""tab-btn active"" onclick=""switchTab('dash')"">Dashboard</button>
    <button class=""tab-btn"" onclick=""switchTab('circ')"">Circulation</button>
    <button class=""tab-btn"" onclick=""switchTab('cat')"">Catalog</button>
    <button class=""tab-btn"" onclick=""switchTab('pat')"">Students</button>
    <button class=""tab-btn"" onclick=""switchTab('fine')"">Fines</button>
    <button class=""tab-btn"" onclick=""switchTab('rep')"">Reports</button>
  </nav>

  <main class=""container"">
    <div id=""toastArea""></div>

    <!-- DASHBOARD VIEW -->
    <div id=""tab-dash"" class=""tab-pane"">
      <div class=""metrics-grid"">
        <div class=""metric-box""><div id=""mTitles"" class=""metric-val"">0</div><div class=""metric-lbl"">Active Titles</div></div>
        <div class=""metric-box""><div id=""mStudents"" class=""metric-val"">0</div><div class=""metric-lbl"">Students</div></div>
        <div class=""metric-box""><div id=""mOnLoan"" class=""metric-val"">0</div><div class=""metric-lbl"">On Loan</div></div>
        <div class=""metric-box""><div id=""mAllLoans"" class=""metric-val"">0</div><div class=""metric-lbl"">All-time Loans</div></div>
        <div class=""metric-box""><div id=""mOverdue"" class=""metric-val"" style=""color:#e8392f;"">0</div><div class=""metric-lbl"">Overdue</div></div>
        <div class=""metric-box""><div id=""mFines"" class=""metric-val"" style=""font-size: 20px;"">RM 0.00</div><div class=""metric-lbl"">Unpaid Fines</div></div>
      </div>

      <div class=""card"">
        <div class=""card-title"">Overdue Items (<span id=""odCount"">0</span>)</div>
        <div class=""table-wrap"">
          <table id=""odTable"">
            <thead>
              <tr><th>Student</th><th>Class</th><th>Book Title</th><th>Due Date</th><th>Late</th><th>Fine</th></tr>
            </thead>
            <tbody></tbody>
          </table>
        </div>
      </div>
    </div>

    <!-- CIRCULATION VIEW -->
    <div id=""tab-circ"" class=""tab-pane"" style=""display: none;"">
      <div class=""mode-bar"">
        <button class=""mode-btn active"" onclick=""setCircMode('out')"">Check Out</button>
        <button class=""mode-btn"" onclick=""setCircMode('in')"">Check In / Return</button>
        <button class=""mode-btn"" onclick=""setCircMode('ren')"">Renew</button>
        <button class=""mode-btn"" onclick=""setCircMode('hold')"">Place Hold</button>
      </div>

      <div class=""card"">
        <div id=""circTitle"" class=""card-title"">Check Out Book</div>
        <form onsubmit=""doCircSubmit(event)"">
          <div id=""circPatronGroup"" class=""field-group"">
            <label class=""field-label"">Student ID or IC Number</label>
            <input type=""text"" id=""circPatron"" class=""input-text"" placeholder=""Scan card or enter IC (e.g. 120315-10-0001)"" autocomplete=""off"" onchange=""lookupPatronPreview()"" />
            <div id=""patronPreview"" style=""margin-top: 6px; font-size: 13px; color: #f6c61b; font-weight: 600; display: none;""></div>
          </div>
          <div class=""field-group"">
            <label class=""field-label"">Book Barcode</label>
            <input type=""text"" id=""circBarcode"" class=""input-text"" placeholder=""Scan or enter barcode (e.g. B1001)"" autocomplete=""off"" />
          </div>
          <button type=""submit"" id=""circBtn"" class=""btn btn-primary"" style=""width: 100%; padding: 12px; margin-top: 4px;"">Complete Checkout</button>
        </form>
      </div>

      <div class=""card"">
        <div class=""card-title"">
          <span>Active Loans</span>
          <input type=""text"" id=""loanFilter"" class=""input-text"" style=""width: 200px; padding: 6px 10px; font-size: 13px;"" placeholder=""Filter loans..."" oninput=""loadLoans()"" />
        </div>
        <div class=""table-wrap"">
          <table id=""loansTable"">
            <thead>
              <tr><th>Title</th><th>Barcode</th><th>Student</th><th>Class</th><th>Due</th><th>Status</th><th>Action</th></tr>
            </thead>
            <tbody></tbody>
          </table>
        </div>
      </div>
    </div>

    <!-- CATALOG VIEW -->
    <div id=""tab-cat"" class=""tab-pane"" style=""display: none;"">
      <div class=""card"">
        <div style=""display: flex; gap: 8px; flex-wrap: wrap; align-items: center; justify-content: space-between;"">
          <div style=""display: flex; gap: 8px; flex: 1; min-width: 260px;"">
            <input type=""text"" id=""catQ"" class=""input-text"" placeholder=""Search titles, authors, barcode, ISBN..."" oninput=""debouncedLoadCat()"" />
            <select id=""catStatus"" class=""select-box"" style=""width: 130px;"" onchange=""loadCatalog()"">
              <option value=""active"">Active</option>
              <option value=""weeded"">Weeded</option>
              <option value=""onloan"">On Loan</option>
              <option value=""all"">All</option>
            </select>
          </div>
          <button class=""btn btn-primary"" onclick=""openItemModal()"">+ Add Book</button>
        </div>
      </div>

      <div class=""card"">
        <div class=""table-wrap"">
          <table id=""catTable"">
            <thead>
              <tr><th>Title</th><th>Author</th><th>Barcode</th><th>Call No</th><th>Status</th><th style=""text-align: right;"">Actions</th></tr>
            </thead>
            <tbody></tbody>
          </table>
        </div>
      </div>
    </div>

    <!-- STUDENTS VIEW -->
    <div id=""tab-pat"" class=""tab-pane"" style=""display: none;"">
      <div class=""card"">
        <div style=""display: flex; gap: 8px; flex-wrap: wrap; align-items: center; justify-content: space-between;"">
          <input type=""text"" id=""patQ"" class=""input-text"" style=""flex: 1; min-width: 240px;"" placeholder=""Search students by name, IC/ID, class..."" oninput=""debouncedLoadPatrons()"" />
          <button class=""btn btn-primary"" onclick=""openPatronModal()"">+ Add Student</button>
        </div>
      </div>

      <div class=""card"">
        <div class=""table-wrap"">
          <table id=""patTable"">
            <thead>
              <tr><th>IC / Student ID</th><th>Name</th><th>Class</th><th>Loans</th><th>Unpaid Fines</th><th style=""text-align: right;"">Actions</th></tr>
            </thead>
            <tbody></tbody>
          </table>
        </div>
      </div>
    </div>

    <!-- FINES VIEW -->
    <div id=""tab-fine"" class=""tab-pane"" style=""display: none;"">
      <div class=""card"">
        <div style=""display: flex; justify-content: space-between; align-items: center; flex-wrap: wrap; gap: 8px;"">
          <div class=""card-title"" style=""margin: 0;"">Fines & Penalties</div>
          <select id=""fineStatus"" class=""select-box"" style=""width: 150px;"" onchange=""loadFines()"">
            <option value=""all"">All Fines</option>
            <option value=""unpaid"" selected>Unpaid Only</option>
            <option value=""paid"">Paid / Waived</option>
          </select>
        </div>
      </div>

      <div class=""card"">
        <div class=""table-wrap"">
          <table id=""finesTable"">
            <thead>
              <tr><th>Student</th><th>Class</th><th>Book Title</th><th>Days Late</th><th>Amount</th><th>Status</th><th style=""text-align: right;"">Action</th></tr>
            </thead>
            <tbody></tbody>
          </table>
        </div>
      </div>
    </div>

    <!-- REPORTS VIEW -->
    <div id=""tab-rep"" class=""tab-pane"" style=""display: none;"">
      <div class=""card"">
        <div class=""card-title"">Collection & Circulation Overview</div>
        <div class=""metrics-grid"">
          <div class=""metric-box""><div id=""rTotalItems"" class=""metric-val"">0</div><div class=""metric-lbl"">Total Catalogued</div></div>
          <div class=""metric-box""><div id=""rWeeded"" class=""metric-val"" style=""color:#9a9a94;"">0</div><div class=""metric-lbl"">Weeded / Retired</div></div>
          <div class=""metric-box""><div id=""rStudents"" class=""metric-val"">0</div><div class=""metric-lbl"">Registered Students</div></div>
          <div class=""metric-box""><div id=""rFinesColl"" class=""metric-val"" style=""font-size:20px; color:#2fb36b;"">RM 0.00</div><div class=""metric-lbl"">Fines Collected</div></div>
        </div>
      </div>

      <div style=""display: grid; grid-template-columns: repeat(auto-fit, minmax(300px, 1fr)); gap: 16px;"">
        <div class=""card"">
          <div class=""card-title"">Top 10 Borrowed Titles</div>
          <table id=""topBooksTable""><thead><tr><th>Title</th><th style=""text-align: right;"">Loans</th></tr></thead><tbody></tbody></table>
        </div>
        <div class=""card"">
          <div class=""card-title"">Top 10 Student Borrowers</div>
          <table id=""topBorrowersTable""><thead><tr><th>Student</th><th>Class</th><th style=""text-align: right;"">Loans</th></tr></thead><tbody></tbody></table>
        </div>
      </div>

      <div class=""card"" style=""margin-top: 16px;"">
        <div class=""card-title"">Circulation by Class</div>
        <table id=""classTable""><thead><tr><th>Class</th><th>Students</th><th style=""text-align: right;"">Active Loans</th></tr></thead><tbody></tbody></table>
      </div>
    </div>
  </main>
</div>

<!-- ITEM EDIT MODAL -->
<div id=""itemModal"" class=""modal-overlay"" style=""display: none;"">
  <div class=""modal-content"">
    <div id=""itemModalTitle"" class=""modal-title"">Add Book</div>
    <form onsubmit=""saveItem(event)"">
      <input type=""hidden"" id=""itemId"" />
      <div class=""field-group""><label class=""field-label"">Title *</label><input type=""text"" id=""itTitle"" class=""input-text"" required /></div>
      <div class=""field-group""><label class=""field-label"">Subtitle</label><input type=""text"" id=""itSub"" class=""input-text"" /></div>
      <div class=""field-group""><label class=""field-label"">Author</label><input type=""text"" id=""itAuthor"" class=""input-text"" /></div>
      <div style=""display: grid; grid-template-columns: 1fr 1fr; gap: 8px;"">
        <div class=""field-group""><label class=""field-label"">Barcode</label><input type=""text"" id=""itBarcode"" class=""input-text"" placeholder=""Auto-generated"" /></div>
        <div class=""field-group""><label class=""field-label"">ISBN</label><input type=""text"" id=""itIsbn"" class=""input-text"" /></div>
      </div>
      <div style=""display: grid; grid-template-columns: 1fr 1fr; gap: 8px;"">
        <div class=""field-group""><label class=""field-label"">Call Number</label><input type=""text"" id=""itCall"" class=""input-text"" /></div>
        <div class=""field-group""><label class=""field-label"">Year</label><input type=""text"" id=""itYear"" class=""input-text"" /></div>
      </div>
      <div class=""field-group""><label class=""field-label"">Publisher</label><input type=""text"" id=""itPub"" class=""input-text"" /></div>
      <div class=""field-group""><label class=""field-label"">Subject</label><input type=""text"" id=""itSubj"" class=""input-text"" /></div>
      <div class=""modal-actions"">
        <button type=""button"" class=""btn btn-sec"" onclick=""closeItemModal()"">Cancel</button>
        <button type=""submit"" class=""btn btn-primary"">Save Book</button>
      </div>
    </form>
  </div>
</div>

<!-- PATRON EDIT MODAL -->
<div id=""patronModal"" class=""modal-overlay"" style=""display: none;"">
  <div class=""modal-content"">
    <div id=""patronModalTitle"" class=""modal-title"">Add Student</div>
    <form onsubmit=""savePatron(event)"">
      <div class=""field-group""><label class=""field-label"">Student ID or IC Number *</label><input type=""text"" id=""ptId"" class=""input-text"" required /></div>
      <div class=""field-group""><label class=""field-label"">Full Name *</label><input type=""text"" id=""ptName"" class=""input-text"" required /></div>
      <div class=""field-group""><label class=""field-label"">Class / Grade</label><input type=""text"" id=""ptClass"" class=""input-text"" placeholder=""e.g. 3 Suri, Grade 7A"" /></div>
      <div class=""field-group""><label class=""field-label"">Contact / Parent Email</label><input type=""text"" id=""ptContact"" class=""input-text"" /></div>
      <div class=""modal-actions"">
        <button type=""button"" class=""btn btn-sec"" onclick=""closePatronModal()"">Cancel</button>
        <button type=""submit"" class=""btn btn-primary"">Save Student</button>
      </div>
    </form>
  </div>
</div>

<script>
let token = localStorage.getItem('errin_token');
let deviceName = localStorage.getItem('errin_name') || 'Remote Device';
let currentTab = 'dash';
let circMode = 'out';
let catDebounce = null;
let patDebounce = null;

async function api(path, opts = {}) {
  opts.headers = opts.headers || {};
  if (token) opts.headers['X-Errin-Token'] = token;
  if (opts.body && typeof opts.body === 'object') {
    opts.headers['Content-Type'] = 'application/json';
    opts.body = JSON.stringify(opts.body);
  }
  try {
    const res = await fetch(path, opts);
    if (res.status === 401) {
      localStorage.removeItem('errin_token');
      token = null;
      renderAuth();
      throw new Error('Unauthorized');
    }
    return await res.json();
  } catch (err) {
    if (err.message !== 'Unauthorized') showToast(err.message, true);
    throw err;
  }
}

function showToast(msg, isErr = false) {
  const area = document.getElementById('toastArea');
  if (!area) return;
  const n = document.createElement('div');
  n.className = 'notice ' + (isErr ? 'notice-err' : 'notice-ok');
  n.textContent = msg;
  area.prepend(n);
  setTimeout(() => n.remove(), 4000);
}

// Pairing logic
async function doPair() {
  const code = document.getElementById('pairCode').value.trim();
  const name = document.getElementById('pairName').value.trim() || 'Remote Device';
  const errBox = document.getElementById('pairErr');
  errBox.style.display = 'none';

  if (!code) {
    errBox.textContent = 'Please enter the 6-digit code shown on the host computer.';
    errBox.style.display = 'block';
    return;
  }

  const btn = document.getElementById('pairBtn');
  btn.disabled = true;
  btn.textContent = 'Connecting...';

  try {
    const res = await fetch('/api/pair', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ code: code, name: name })
    });
    const data = await res.json();
    if (data.ok && data.token) {
      token = data.token;
      deviceName = data.name || name;
      localStorage.setItem('errin_token', token);
      localStorage.setItem('errin_name', deviceName);
      renderAuth();
    } else {
      errBox.textContent = data.error || 'Connection failed.';
      errBox.style.display = 'block';
    }
  } catch (e) {
    errBox.textContent = 'Failed to connect: ' + e.message;
    errBox.style.display = 'block';
  } finally {
    btn.disabled = false;
    btn.textContent = 'Connect Device';
  }
}

function doUnpair() {
  if (confirm('Disconnect this device from ErrinILS?')) {
    localStorage.removeItem('errin_token');
    token = null;
    renderAuth();
  }
}

function renderAuth() {
  if (!token) {
    document.getElementById('appView').style.display = 'none';
    document.getElementById('pairView').style.display = 'flex';
    document.getElementById('pairCode').value = '';
    document.getElementById('pairCode').focus();
  } else {
    document.getElementById('pairView').style.display = 'none';
    document.getElementById('appView').style.display = 'block';
    document.getElementById('devName').textContent = deviceName;
    switchTab(currentTab);
  }
}

// Tab navigation
function switchTab(tab) {
  currentTab = tab;
  document.querySelectorAll('.tab-btn').forEach(b => b.classList.remove('active'));
  document.querySelectorAll('.tab-pane').forEach(p => p.style.display = 'none');

  const names = { dash: 'Dashboard', circ: 'Circulation', cat: 'Catalog', pat: 'Students', fine: 'Fines', rep: 'Reports' };
  document.getElementById('viewTitle').textContent = names[tab] || 'ErrinILS';

  const btn = Array.from(document.querySelectorAll('.tab-btn')).find(b => b.textContent.toLowerCase().startsWith(tab));
  if (btn) btn.classList.add('active');

  const pane = document.getElementById('tab-' + tab);
  if (pane) pane.style.display = 'block';

  if (tab === 'dash') loadDashboard();
  else if (tab === 'circ') { loadLoans(); document.getElementById('circBarcode').focus(); }
  else if (tab === 'cat') loadCatalog();
  else if (tab === 'pat') loadPatrons();
  else if (tab === 'fine') loadFines();
  else if (tab === 'rep') loadReports();
}

// Dashboard
async function loadDashboard() {
  const d = await api('/api/dashboard');
  if (!d.ok) return;

  document.getElementById('mTitles').textContent = d.activeTitles;
  document.getElementById('mStudents').textContent = d.totalStudents;
  document.getElementById('mOnLoan').textContent = d.onLoan;
  document.getElementById('mAllLoans').textContent = d.allTimeLoans;
  document.getElementById('mOverdue').textContent = d.overdueCount;
  document.getElementById('mFines').textContent = 'RM ' + Number(d.unpaidFines).toFixed(2);
  document.getElementById('odCount').textContent = d.overdue.length;

  const tbody = document.querySelector('#odTable tbody');
  tbody.innerHTML = '';
  if (d.overdue.length === 0) {
    tbody.innerHTML = '<tr><td colspan=""6"" style=""text-align:center; color:#9a9a94;"">No overdue items right now. Excellent!</td></tr>';
    return;
  }
  d.overdue.forEach(o => {
    const tr = document.createElement('tr');
    tr.innerHTML = `<td><strong>${escape(o.patronName)}</strong></td><td>${escape(o.patronClass)}</td><td>${escape(o.title)}</td><td>${o.due}</td><td><span class=""badge badge-err"">${o.daysLate}d late</span></td><td>RM ${o.fine.toFixed(2)}</td>`;
    tbody.appendChild(tr);
  });
}

// Circulation
function setCircMode(m) {
  circMode = m;
  document.querySelectorAll('.mode-btn').forEach(b => b.classList.remove('active'));
  const btn = Array.from(document.querySelectorAll('.mode-btn')).find(b => b.getAttribute('onclick').includes(m));
  if (btn) btn.classList.add('active');

  const pGroup = document.getElementById('circPatronGroup');
  const title = document.getElementById('circTitle');
  const btnSubmit = document.getElementById('circBtn');

  if (m === 'out') {
    title.textContent = 'Check Out Book';
    pGroup.style.display = 'block';
    btnSubmit.textContent = 'Complete Checkout';
  } else if (m === 'in') {
    title.textContent = 'Return Book';
    pGroup.style.display = 'none';
    btnSubmit.textContent = 'Return Book';
  } else if (m === 'ren') {
    title.textContent = 'Renew Loan';
    pGroup.style.display = 'none';
    btnSubmit.textContent = 'Renew Loan';
  } else if (m === 'hold') {
    title.textContent = 'Place Hold';
    pGroup.style.display = 'block';
    btnSubmit.textContent = 'Place Hold';
  }
  document.getElementById('circBarcode').value = '';
  document.getElementById('circBarcode').focus();
}

async function lookupPatronPreview() {
  const pid = document.getElementById('circPatron').value.trim();
  const prev = document.getElementById('patronPreview');
  if (!pid) { prev.style.display = 'none'; return; }
  try {
    const res = await api('/api/patron?id=' + encodeURIComponent(pid));
    if (res.ok && res.patron) {
      const p = res.patron;
      prev.textContent = `✓ ${p.name} (${p.cls || 'No class'}) \u00B7 ${p.activeLoans.length} active loans \u00B7 Unpaid fines: RM ${Number(p.unpaidFines).toFixed(2)}`;
      prev.style.display = 'block';
    } else {
      prev.style.display = 'none';
    }
  } catch (e) { prev.style.display = 'none'; }
}

async function doCircSubmit(e) {
  e.preventDefault();
  const patron = document.getElementById('circPatron').value.trim();
  const barcode = document.getElementById('circBarcode').value.trim();

  if (!barcode) { showToast('Please scan or enter a book barcode', true); return; }
  if ((circMode === 'out' || circMode === 'hold') && !patron) {
    showToast('Please scan or enter student ID / IC', true); return;
  }

  let endpoint = '/api/checkout';
  let payload = { patron: patron, barcode: barcode };
  if (circMode === 'in') { endpoint = '/api/return'; payload = { barcode: barcode }; }
  else if (circMode === 'ren') { endpoint = '/api/renew'; payload = { barcode: barcode }; }
  else if (circMode === 'hold') { endpoint = '/api/hold'; payload = { patron: patron, barcode: barcode }; }

  try {
    const res = await api(endpoint, { method: 'POST', body: payload });
    if (res.ok) {
      showToast(res.msg || 'Success!');
      document.getElementById('circBarcode').value = '';
      if (circMode === 'out' || circMode === 'hold') {
        // Keep patron for multiple items, focus barcode
        lookupPatronPreview();
      }
      document.getElementById('circBarcode').focus();
      loadLoans();
    } else {
      showToast(res.friendly || res.error || 'Operation failed', true);
    }
  } catch (err) {}
}

async function loadLoans() {
  const q = document.getElementById('loanFilter').value.trim();
  const res = await api('/api/loans?q=' + encodeURIComponent(q));
  if (!res.ok) return;

  const tbody = document.querySelector('#loansTable tbody');
  tbody.innerHTML = '';
  if (res.loans.length === 0) {
    tbody.innerHTML = '<tr><td colspan=""7"" style=""text-align:center; color:#9a9a94;"">No active loans found.</td></tr>';
    return;
  }
  res.loans.forEach(l => {
    const tr = document.createElement('tr');
    const badge = l.overdue ? `<span class=""badge badge-err"">${l.daysLate}d late</span>` : `<span class=""badge badge-ok"">On time</span>`;
    tr.innerHTML = `
      <td><strong>${escape(l.title)}</strong></td>
      <td><code>${escape(l.barcode)}</code></td>
      <td>${escape(l.patronName)}</td>
      <td>${escape(l.patronClass)}</td>
      <td>${l.due}</td>
      <td>${badge}</td>
      <td style=""white-space:nowrap;"">
        <button class=""btn btn-sec btn-sm"" onclick=""quickReturn('${l.barcode}')"">Return</button>
        <button class=""btn btn-sec btn-sm"" onclick=""quickRenew('${l.barcode}')"">Renew</button>
      </td>`;
    tbody.appendChild(tr);
  });
}

async function quickReturn(bc) {
  const res = await api('/api/return', { method: 'POST', body: { barcode: bc } });
  if (res.ok) { showToast(res.msg || 'Returned'); loadLoans(); }
  else showToast(res.friendly || res.error, true);
}

async function quickRenew(bc) {
  const res = await api('/api/renew', { method: 'POST', body: { barcode: bc } });
  if (res.ok) { showToast(res.msg || 'Renewed'); loadLoans(); }
  else showToast(res.friendly || res.error, true);
}

// Catalog
function debouncedLoadCat() {
  clearTimeout(catDebounce);
  catDebounce = setTimeout(loadCatalog, 250);
}

async function loadCatalog() {
  const q = document.getElementById('catQ').value.trim();
  const st = document.getElementById('catStatus').value;
  const res = await api(`/api/items?q=${encodeURIComponent(q)}&status=${st}`);
  if (!res.ok) return;

  const tbody = document.querySelector('#catTable tbody');
  tbody.innerHTML = '';
  if (res.items.length === 0) {
    tbody.innerHTML = '<tr><td colspan=""6"" style=""text-align:center; color:#9a9a94;"">No items found.</td></tr>';
    return;
  }
  res.items.forEach(i => {
    const tr = document.createElement('tr');
    let stBadge = '<span class=""badge badge-ok"">Available</span>';
    if (i.weeded) stBadge = `<span class=""badge badge-dim"">Weeded (${escape(i.weed_reason || '')})</span>`;
    else if (i.onLoan) stBadge = `<span class=""badge badge-warn"">Loaned: ${escape(i.loanPatron)}</span>`;

    tr.innerHTML = `
      <td><strong>${escape(i.title)}</strong><div style=""font-size:12px; color:#9a9a94;"">${escape(i.sub || '')}</div></td>
      <td>${escape(i.author)}</td>
      <td><code>${escape(i.barcode)}</code></td>
      <td>${escape(i.call)}</td>
      <td>${stBadge}</td>
      <td style=""text-align:right; white-space:nowrap;"">
        <button class=""btn btn-sec btn-sm"" onclick='editItem(${JSON.stringify(i)})'>Edit</button>
        ${i.weeded ? `<button class=""btn btn-sec btn-sm"" onclick=""unweedItem('${i.id}')"">Restore</button>` : `<button class=""btn btn-sec btn-sm"" onclick=""weedItemPrompt('${i.id}')"">Weed</button>`}
        <button class=""btn btn-danger btn-sm"" onclick=""deleteItem('${i.id}', '${escape(i.title)}')"" style=""margin-left:4px;"">\u00D7</button>
      </td>`;
    tbody.appendChild(tr);
  });
}

function openItemModal(item = null) {
  document.getElementById('itemModalTitle').textContent = item ? 'Edit Book' : 'Add Book';
  document.getElementById('itemId').value = item ? item.id : '';
  document.getElementById('itTitle').value = item ? item.title : '';
  document.getElementById('itSub').value = item ? (item.sub || '') : '';
  document.getElementById('itAuthor').value = item ? (item.author || '') : '';
  document.getElementById('itBarcode').value = item ? item.barcode : '';
  document.getElementById('itIsbn').value = item ? (item.isbn || '') : '';
  document.getElementById('itCall').value = item ? (item.call || '') : '';
  document.getElementById('itYear').value = item ? (item.year || '') : '';
  document.getElementById('itPub').value = item ? (item.publisher || '') : '';
  document.getElementById('itSubj').value = item ? (item.subject || '') : '';
  document.getElementById('itemModal').style.display = 'flex';
}

function closeItemModal() { document.getElementById('itemModal').style.display = 'none'; }
function editItem(item) { openItemModal(item); }

async function saveItem(e) {
  e.preventDefault();
  const payload = {
    id: document.getElementById('itemId').value,
    title: document.getElementById('itTitle').value.trim(),
    sub: document.getElementById('itSub').value.trim(),
    author: document.getElementById('itAuthor').value.trim(),
    barcode: document.getElementById('itBarcode').value.trim(),
    isbn: document.getElementById('itIsbn').value.trim(),
    call: document.getElementById('itCall').value.trim(),
    year: document.getElementById('itYear').value.trim(),
    publisher: document.getElementById('itPub').value.trim(),
    subject: document.getElementById('itSubj').value.trim()
  };
  const res = await api('/api/item', { method: 'POST', body: payload });
  if (res.ok) {
    showToast('Book saved successfully');
    closeItemModal();
    loadCatalog();
  } else {
    showToast(res.error || 'Failed to save', true);
  }
}

async function deleteItem(id, title) {
  if (confirm(`Permanently delete '${title}'?`)) {
    const res = await api('/api/delete-item', { method: 'POST', body: { id: id } });
    if (res.ok) { showToast('Book deleted'); loadCatalog(); }
    else showToast(res.error, true);
  }
}

async function weedItemPrompt(id) {
  const reason = prompt('Reason for weeding (e.g. damaged, obsolete, lost):', 'Condition/Damaged');
  if (reason !== null) {
    const res = await api('/api/weed', { method: 'POST', body: { id: id, reason: reason } });
    if (res.ok) { showToast('Book weeded'); loadCatalog(); }
    else showToast(res.error, true);
  }
}

async function unweedItem(id) {
  const res = await api('/api/unweed', { method: 'POST', body: { id: id } });
  if (res.ok) { showToast('Book restored to circulation'); loadCatalog(); }
  else showToast(res.error, true);
}

// Students (Patrons)
function debouncedLoadPatrons() {
  clearTimeout(patDebounce);
  patDebounce = setTimeout(loadPatrons, 250);
}

async function loadPatrons() {
  const q = document.getElementById('patQ').value.trim();
  const res = await api('/api/patrons?q=' + encodeURIComponent(q));
  if (!res.ok) return;

  const tbody = document.querySelector('#patTable tbody');
  tbody.innerHTML = '';
  if (res.patrons.length === 0) {
    tbody.innerHTML = '<tr><td colspan=""6"" style=""text-align:center; color:#9a9a94;"">No students found.</td></tr>';
    return;
  }
  res.patrons.forEach(p => {
    const tr = document.createElement('tr');
    tr.innerHTML = `
      <td><code>${escape(p.id)}</code></td>
      <td><strong>${escape(p.name)}</strong></td>
      <td>${escape(p.cls)}</td>
      <td>${p.activeLoans > 0 ? `<span class=""badge badge-warn"">${p.activeLoans} books</span>` : '0'}</td>
      <td>${p.unpaidFines > 0 ? `<span class=""badge badge-err"">RM ${p.unpaidFines.toFixed(2)}</span>` : 'RM 0.00'}</td>
      <td style=""text-align:right; white-space:nowrap;"">
        <button class=""btn btn-sec btn-sm"" onclick='editPatron(${JSON.stringify(p)})'>Edit</button>
        <button class=""btn btn-danger btn-sm"" onclick=""deletePatron('${p.id}', '${escape(p.name)}')"" style=""margin-left:4px;"">\u00D7</button>
      </td>`;
    tbody.appendChild(tr);
  });
}

function openPatronModal(p = null) {
  document.getElementById('patronModalTitle').textContent = p ? 'Edit Student' : 'Add Student';
  const idIn = document.getElementById('ptId');
  idIn.value = p ? p.id : '';
  idIn.disabled = !!p;
  document.getElementById('ptName').value = p ? p.name : '';
  document.getElementById('ptClass').value = p ? (p.cls || '') : '';
  document.getElementById('ptContact').value = p ? (p.contact || '') : '';
  document.getElementById('patronModal').style.display = 'flex';
}

function closePatronModal() { document.getElementById('patronModal').style.display = 'none'; }
function editPatron(p) { openPatronModal(p); }

async function savePatron(e) {
  e.preventDefault();
  const payload = {
    id: document.getElementById('ptId').value.trim(),
    name: document.getElementById('ptName').value.trim(),
    cls: document.getElementById('ptClass').value.trim(),
    contact: document.getElementById('ptContact').value.trim()
  };
  const res = await api('/api/patron', { method: 'POST', body: payload });
  if (res.ok) {
    showToast('Student saved');
    closePatronModal();
    loadPatrons();
  } else {
    showToast(res.error || 'Failed to save', true);
  }
}

async function deletePatron(id, name) {
  if (confirm(`Delete student record for '${name}'?`)) {
    const res = await api('/api/delete-patron', { method: 'POST', body: { id: id } });
    if (res.ok) { showToast('Student deleted'); loadPatrons(); }
    else showToast(res.error, true);
  }
}

// Fines
async function loadFines() {
  const st = document.getElementById('fineStatus').value;
  const res = await api('/api/fines?status=' + st);
  if (!res.ok) return;

  const tbody = document.querySelector('#finesTable tbody');
  tbody.innerHTML = '';
  if (res.fines.length === 0) {
    tbody.innerHTML = '<tr><td colspan=""7"" style=""text-align:center; color:#9a9a94;"">No fines recorded.</td></tr>';
    return;
  }
  res.fines.forEach(f => {
    const tr = document.createElement('tr');
    let stBadge = '<span class=""badge badge-err"">Unpaid</span>';
    let action = `
      <button class=""btn btn-primary btn-sm"" onclick=""payFine('${f.id}')"">Pay</button>
      <button class=""btn btn-sec btn-sm"" onclick=""waiveFine('${f.id}')"">Waive</button>`;
    if (f.paid === 'waived') {
      stBadge = '<span class=""badge badge-dim"">Waived</span>';
      action = '';
    } else if (f.paid) {
      stBadge = `<span class=""badge badge-ok"">Paid (${f.paid})</span>`;
      action = '';
    }

    tr.innerHTML = `
      <td><strong>${escape(f.patronName)}</strong></td>
      <td>${escape(f.patronClass)}</td>
      <td>${escape(f.title)}</td>
      <td>${f.days} days</td>
      <td><strong>RM ${Number(f.amt).toFixed(2)}</strong></td>
      <td>${stBadge}</td>
      <td style=""text-align:right; white-space:nowrap;"">${action}</td>`;
    tbody.appendChild(tr);
  });
}

async function payFine(id) {
  const res = await api('/api/pay-fine', { method: 'POST', body: { id: id } });
  if (res.ok) { showToast('Fine marked as paid'); loadFines(); }
  else showToast(res.error, true);
}

async function waiveFine(id) {
  if (confirm('Waive this fine?')) {
    const res = await api('/api/waive-fine', { method: 'POST', body: { id: id } });
    if (res.ok) { showToast('Fine waived'); loadFines(); }
    else showToast(res.error, true);
  }
}

// Reports
async function loadReports() {
  const res = await api('/api/reports');
  if (!res.ok) return;

  document.getElementById('rTotalItems').textContent = res.totalItems;
  document.getElementById('rWeeded').textContent = res.weededItems;
  document.getElementById('rStudents').textContent = res.totalStudents;
  document.getElementById('rFinesColl').textContent = 'RM ' + Number(res.finesCollected).toFixed(2);

  const tb = document.querySelector('#topBooksTable tbody');
  tb.innerHTML = '';
  res.topBooks.forEach(b => {
    const tr = document.createElement('tr');
    tr.innerHTML = `<td><strong>${escape(b.title)}</strong><div style=""font-size:12px; color:#9a9a94;"">${escape(b.author)}</div></td><td style=""text-align:right;""><strong>${b.count}</strong></td>`;
    tb.appendChild(tr);
  });

  const tp = document.querySelector('#topBorrowersTable tbody');
  tp.innerHTML = '';
  res.topBorrowers.forEach(p => {
    const tr = document.createElement('tr');
    tr.innerHTML = `<td><strong>${escape(p.name)}</strong></td><td>${escape(p.cls)}</td><td style=""text-align:right;""><strong>${p.count}</strong></td>`;
    tp.appendChild(tr);
  });

  const tc = document.querySelector('#classTable tbody');
  tc.innerHTML = '';
  res.classBreakdown.forEach(c => {
    const tr = document.createElement('tr');
    tr.innerHTML = `<td><strong>${escape(c.cls)}</strong></td><td>${c.students}</td><td style=""text-align:right;"">${c.activeLoans}</td>`;
    tc.appendChild(tr);
  });
}

// Security: escape HTML entities
function escape(s) {
  if (!s) return '';
  return String(s).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/""/g, '&quot;').replace(/'/g, '&#39;');
}

// Format 6 digit pairing code with space
document.getElementById('pairCode').addEventListener('input', function(e) {
  let val = this.value.replace(/[^0-9A-Za-z]/g, '');
  if (val.length > 3) val = val.substring(0, 3) + ' ' + val.substring(3, 6);
  this.value = val;
});

// Auto-suggest device name based on user agent
(function() {
  const ua = navigator.userAgent;
  let def = 'Remote Device';
  if (/iPad/i.test(ua)) def = 'Library iPad';
  else if (/iPhone/i.test(ua)) def = 'iPhone';
  else if (/Android/i.test(ua)) def = 'Android Device';
  else if (/Macintosh/i.test(ua)) def = 'Mac Laptop';
  else if (/Windows/i.test(ua)) def = 'Staff PC';
  const nameIn = document.getElementById('pairName');
  if (nameIn && !nameIn.value) nameIn.value = def;
})();

// Start app
renderAuth();
</script>
</body>
</html>";
    }
}
