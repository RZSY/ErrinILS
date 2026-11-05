// Data model. The JSON layout is identical to the earlier Electron version, so existing errin-data.json files open unchanged.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Errin
{
    public class Item
    {
        public string id = "", ctl = "", acq = "", barcode = "", title = "", sub = "", author = "", isbn = "",
            publisher = "", place = "", year = "", call = "", subject = "", pages = "",
            weeded = "", weed_reason = "";
        public static readonly string[] Keys = { "ctl", "acq", "barcode", "title", "sub", "author", "isbn", "publisher", "place", "year", "pages", "subject", "call", "weeded", "weed_reason" };
        public string Get(string k)
        {
            switch (k)
            {
                case "id": return id; case "ctl": return ctl; case "acq": return acq; case "barcode": return barcode; case "title": return title;
                case "sub": return sub; case "author": return author; case "isbn": return isbn; case "publisher": return publisher;
                case "place": return place; case "year": return year; case "call": return call; case "subject": return subject; case "pages": return pages;
                case "weeded": return weeded; case "weed_reason": return weed_reason;
            }
            return "";
        }
        public void Set(string k, string v)
        {
            v = v ?? "";
            switch (k)
            {
                case "id": id = v; break; case "ctl": ctl = v; break; case "acq": acq = v; break; case "barcode": barcode = v; break; case "title": title = v; break;
                case "sub": sub = v; break; case "author": author = v; break; case "isbn": isbn = v; break; case "publisher": publisher = v; break;
                case "place": place = v; break; case "year": year = v; break; case "call": call = v; break; case "subject": subject = v; break; case "pages": pages = v; break;
                case "weeded": weeded = v; break; case "weed_reason": weed_reason = v; break;
            }
        }
        public Item Clone() { var c = new Item(); c.id = id; foreach (var k in Keys) c.Set(k, Get(k)); return c; }
        public void CopyFrom(Item o) { foreach (var k in Keys) Set(k, o.Get(k)); }
        public Dictionary<string, object> ToJ()
        {
            var d = new Dictionary<string, object>(); d["id"] = id;
            foreach (var k in Keys) d[k] = Get(k);
            return d;
        }
        public static Item FromJ(Dictionary<string, object> d)
        {
            var i = new Item(); i.id = Json.Str(d, "id");
            foreach (var k in Keys) i.Set(k, Json.Str(d, k));
            return i;
        }
        public string All() { return string.Join(" ", new[] { id, ctl, acq, barcode, title, sub, author, isbn, publisher, place, year, call, subject, pages, weeded, weed_reason }); }
    }

    public class Patron { public string id = "", name = "", cls = "", contact = "", reg = "", last = ""; }
    public class Loan { public string id = "", iid = "", pid = "", @out = "", due = "", ret = null; public int ren; }
    public class Hold { public string id = "", iid = "", pid = "", date = "", st = "waiting"; }
    public class Fine { public string id = "", pid = "", iid = "", loan = "", date = "", paid = ""; public int days; public double amt; }
    public class ZTarget { public string n = "", host = "", db = ""; public int port = 210; }
    public class Settings { public string school = ""; public double rate = 0.1; public int maxLoans = 3; public double block = 5; public List<ZTarget> zt = new List<ZTarget>(); }

    public class ClassYearStudent
    {
        public string id = "", name = "";
        public int loans;
    }

    public class ClassYearRecord
    {
        public int year;
        public string cls = "";
        public int studentCount;
        public int totalLoans;
        public List<ClassYearStudent> students = new List<ClassYearStudent>();
    }

    public class ArchivedPatron
    {
        public string id = "", name = "", cls = "", contact = "", reg = "", last = "", archivedDate = "";
        public List<Loan> loans = new List<Loan>();
        public List<Fine> fines = new List<Fine>();
    }

    public class PatronImportRow
    {
        public string rawIc = "", normIc = "", name = "", cls = "", contact = "", action = "";
        public bool isUpdate, isValid;
    }

    public class Db
    {
        public List<Item> Items = new List<Item>();
        public List<Patron> Patrons = new List<Patron>();
        public List<Loan> Loans = new List<Loan>();
        public List<Hold> Holds = new List<Hold>();
        public List<Fine> Fines = new List<Fine>();
        public Settings Set = new Settings();
        public List<ClassYearRecord> ClassHistory = new List<ClassYearRecord>();
        public List<ArchivedPatron> ArchivedPatrons = new List<ArchivedPatron>();

        public string ToJson()
        {
            var d = new Dictionary<string, object>();
            d["items"] = Items.Select(i => (object)i.ToJ()).ToList();
            d["patrons"] = Patrons.Select(p => (object)new Dictionary<string, object> { { "id", p.id }, { "name", p.name }, { "cls", p.cls }, { "contact", p.contact }, { "reg", p.reg }, { "last", p.last } }).ToList();
            d["loans"] = Loans.Select(l => (object)new Dictionary<string, object> { { "id", l.id }, { "iid", l.iid }, { "pid", l.pid }, { "out", l.@out }, { "due", l.due }, { "ret", l.ret }, { "ren", (double)l.ren } }).ToList();
            d["holds"] = Holds.Select(h => (object)new Dictionary<string, object> { { "id", h.id }, { "iid", h.iid }, { "pid", h.pid }, { "date", h.date }, { "st", h.st } }).ToList();
            d["fines"] = Fines.Select(f => (object)new Dictionary<string, object> { { "id", f.id }, { "pid", f.pid }, { "iid", f.iid }, { "loan", f.loan }, { "days", (double)f.days }, { "amt", f.amt }, { "date", f.date }, { "paid", f.paid } }).ToList();
            var s = new Dictionary<string, object> { { "school", Set.school }, { "rate", Set.rate }, { "maxLoans", (double)Set.maxLoans }, { "block", Set.block } };
            s["zt"] = Set.zt.Select(z => (object)new Dictionary<string, object> { { "n", z.n }, { "host", z.host }, { "port", (double)z.port }, { "db", z.db } }).ToList();
            d["set"] = s;

            if (ClassHistory != null && ClassHistory.Count > 0)
            {
                d["classHistory"] = ClassHistory.Select(ch => (object)new Dictionary<string, object>
                {
                    { "year", (double)ch.year },
                    { "cls", ch.cls },
                    { "studentCount", (double)ch.studentCount },
                    { "totalLoans", (double)ch.totalLoans },
                    { "students", ch.students.Select(st => (object)new Dictionary<string, object> { { "id", st.id }, { "name", st.name }, { "loans", (double)st.loans } }).ToList() }
                }).ToList();
            }

            if (ArchivedPatrons != null && ArchivedPatrons.Count > 0)
            {
                d["archivedPatrons"] = ArchivedPatrons.Select(ap => (object)new Dictionary<string, object>
                {
                    { "id", ap.id }, { "name", ap.name }, { "cls", ap.cls }, { "contact", ap.contact }, { "reg", ap.reg }, { "last", ap.last }, { "archivedDate", ap.archivedDate },
                    { "loans", ap.loans.Select(l => (object)new Dictionary<string, object> { { "id", l.id }, { "iid", l.iid }, { "pid", l.pid }, { "out", l.@out }, { "due", l.due }, { "ret", l.ret }, { "ren", (double)l.ren } }).ToList() },
                    { "fines", ap.fines.Select(f => (object)new Dictionary<string, object> { { "id", f.id }, { "pid", f.pid }, { "iid", f.iid }, { "loan", f.loan }, { "days", (double)f.days }, { "amt", f.amt }, { "date", f.date }, { "paid", f.paid } }).ToList() }
                }).ToList();
            }

            return Json.Write(d);
        }

        public static Db FromJson(string json)
        {
            var root = Json.Parse(json) as Dictionary<string, object>;
            if (root == null) throw new FormatException("Not a catalogue file");
            var db = new Db();
            foreach (var o in Json.Arr(root, "items")) { var d = o as Dictionary<string, object>; if (d != null) db.Items.Add(Item.FromJ(d)); }
            foreach (var o in Json.Arr(root, "patrons")) { var d = o as Dictionary<string, object>; if (d == null) continue;
                db.Patrons.Add(new Patron { id = Json.Str(d, "id"), name = Json.Str(d, "name"), cls = Json.Str(d, "cls"), contact = Json.Str(d, "contact"), reg = Json.Str(d, "reg"), last = Json.Str(d, "last") }); }
            foreach (var o in Json.Arr(root, "loans")) { var d = o as Dictionary<string, object>; if (d == null) continue;
                var r = Json.Str(d, "ret");
                db.Loans.Add(new Loan { id = Json.Str(d, "id"), iid = Json.Str(d, "iid"), pid = Json.Str(d, "pid"), @out = Json.Str(d, "out"), due = Json.Str(d, "due"), ret = r == "" ? null : r, ren = (int)Json.Num(d, "ren") }); }
            foreach (var o in Json.Arr(root, "holds")) { var d = o as Dictionary<string, object>; if (d == null) continue;
                db.Holds.Add(new Hold { id = Json.Str(d, "id"), iid = Json.Str(d, "iid"), pid = Json.Str(d, "pid"), date = Json.Str(d, "date"), st = Json.Str(d, "st") }); }
            foreach (var o in Json.Arr(root, "fines")) { var d = o as Dictionary<string, object>; if (d == null) continue;
                db.Fines.Add(new Fine { id = Json.Str(d, "id"), pid = Json.Str(d, "pid"), iid = Json.Str(d, "iid"), loan = Json.Str(d, "loan"), date = Json.Str(d, "date"), days = (int)Json.Num(d, "days"), amt = Json.Num(d, "amt"), paid = Json.Str(d, "paid") }); }
            var s = Json.Obj(root, "set");
            if (s.ContainsKey("school")) db.Set.school = Json.Str(s, "school");
            if (s.ContainsKey("rate")) db.Set.rate = Json.Num(s, "rate");
            if (s.ContainsKey("maxLoans")) db.Set.maxLoans = (int)Json.Num(s, "maxLoans");
            if (s.ContainsKey("block")) db.Set.block = Json.Num(s, "block");
            foreach (var o in Json.Arr(s, "zt")) { var d = o as Dictionary<string, object>; if (d == null) continue;
                db.Set.zt.Add(new ZTarget { n = Json.Str(d, "n"), host = Json.Str(d, "host"), port = (int)Json.Num(d, "port"), db = Json.Str(d, "db") }); }

            foreach (var o in Json.Arr(root, "classHistory"))
            {
                var d = o as Dictionary<string, object>; if (d == null) continue;
                var rec = new ClassYearRecord { year = (int)Json.Num(d, "year"), cls = Json.Str(d, "cls"), studentCount = (int)Json.Num(d, "studentCount"), totalLoans = (int)Json.Num(d, "totalLoans") };
                foreach (var so in Json.Arr(d, "students"))
                {
                    var sd = so as Dictionary<string, object>; if (sd == null) continue;
                    rec.students.Add(new ClassYearStudent { id = Json.Str(sd, "id"), name = Json.Str(sd, "name"), loans = (int)Json.Num(sd, "loans") });
                }
                db.ClassHistory.Add(rec);
            }

            foreach (var o in Json.Arr(root, "archivedPatrons"))
            {
                var d = o as Dictionary<string, object>; if (d == null) continue;
                var ap = new ArchivedPatron { id = Json.Str(d, "id"), name = Json.Str(d, "name"), cls = Json.Str(d, "cls"), contact = Json.Str(d, "contact"), reg = Json.Str(d, "reg"), last = Json.Str(d, "last"), archivedDate = Json.Str(d, "archivedDate") };
                foreach (var lo in Json.Arr(d, "loans"))
                {
                    var ld = lo as Dictionary<string, object>; if (ld == null) continue;
                    var r = Json.Str(ld, "ret");
                    ap.loans.Add(new Loan { id = Json.Str(ld, "id"), iid = Json.Str(ld, "iid"), pid = Json.Str(ld, "pid"), @out = Json.Str(ld, "out"), due = Json.Str(ld, "due"), ret = r == "" ? null : r, ren = (int)Json.Num(ld, "ren") });
                }
                foreach (var fo in Json.Arr(d, "fines"))
                {
                    var fd = fo as Dictionary<string, object>; if (fd == null) continue;
                    ap.fines.Add(new Fine { id = Json.Str(fd, "id"), pid = Json.Str(fd, "pid"), iid = Json.Str(fd, "iid"), loan = Json.Str(fd, "loan"), date = Json.Str(fd, "date"), days = (int)Json.Num(fd, "days"), amt = Json.Num(fd, "amt"), paid = Json.Str(fd, "paid") });
                }
                db.ArchivedPatrons.Add(ap);
            }

            return db;
        }
    }

    /// <summary>Reads and writes errin-data.db SQLite database in %APPDATA%\Errin.</summary>
    public class Store
    {
        public readonly string Dir;
        public string DataFile { get { return Path.Combine(Dir, "errin-data.db"); } }
        public string LegacyJsonFile { get { return Path.Combine(Dir, "errin-data.json"); } }
        public string ConfigFile { get { return Path.Combine(Dir, "errin-config.json"); } }
        public volatile string LastJson;
        public int Version;
        public Store(string dir) { Dir = dir ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Errin"); }

        public Db Load()
        {
            try { Directory.CreateDirectory(Dir); } catch { }

            // 1. Load active SQLite database if it exists
            if (SqliteNative.IsAvailable && File.Exists(DataFile))
            {
                try
                {
                    var db = SqliteStore.Load(DataFile);
                    if (db != null) { LastJson = db.ToJson(); return db; }
                }
                catch { }
            }

            // 2. Try loading from SQLite rollback backup (.db.bak)
            var bakDb = DataFile + ".bak";
            if (SqliteNative.IsAvailable && File.Exists(bakDb))
            {
                try
                {
                    var db = SqliteStore.Load(bakDb);
                    if (db != null)
                    {
                        Save(db);
                        LastJson = db.ToJson();
                        return db;
                    }
                }
                catch { }
            }

            // 3. Automatic migration from existing JSON database if present
            Db migrated = null;
            try
            {
                if (File.Exists(LegacyJsonFile))
                    migrated = Db.FromJson(File.ReadAllText(LegacyJsonFile, Encoding.UTF8));
                else
                {
                    var legacyBak = LegacyJsonFile.Replace(".json", ".bak");
                    if (File.Exists(legacyBak))
                        migrated = Db.FromJson(File.ReadAllText(legacyBak, Encoding.UTF8));
                }
            }
            catch { }

            if (migrated != null)
            {
                try
                {
                    Save(migrated);
                    try
                    {
                        var migPath = LegacyJsonFile + ".migrated";
                        if (!File.Exists(migPath) && File.Exists(LegacyJsonFile))
                            File.Copy(LegacyJsonFile, migPath, true);
                    }
                    catch { }
                    LastJson = migrated.ToJson();
                    return migrated;
                }
                catch { }
            }

            return null;
        }

        public bool Save(Db db)
        {
            if (db == null) return false;
            try
            {
                Directory.CreateDirectory(Dir);
                if (SqliteNative.IsAvailable)
                {
                    if (File.Exists(DataFile))
                    {
                        try { File.Copy(DataFile, DataFile + ".bak", true); } catch { }
                    }
                    SqliteStore.Save(DataFile, db);
                    LastJson = db.ToJson();
                    Version++;
                    return true;
                }
                else
                {
                    // Fallback to JSON if SQLite native library is unavailable
                    string s = db.ToJson();
                    string f = LegacyJsonFile, tmp = f + ".tmp", bak = f.Replace(".json", ".bak");
                    File.WriteAllText(tmp, s, new UTF8Encoding(false));
                    if (File.Exists(f)) { File.Copy(f, bak, true); File.Delete(f); }
                    File.Move(tmp, f);
                    LastJson = s; Version++;
                    return true;
                }
            }
            catch { return false; }
        }

        public List<Item> SnapshotItems()
        {
            if (SqliteNative.IsAvailable && File.Exists(DataFile))
            {
                try
                {
                    var items = SqliteStore.SnapshotItems(DataFile);
                    if (items != null && items.Count > 0) return items;
                }
                catch { }
            }

            var res = new List<Item>();
            try
            {
                string s = LastJson ?? (File.Exists(LegacyJsonFile) ? File.ReadAllText(LegacyJsonFile, Encoding.UTF8) : null);
                if (s == null) return res;
                var root = Json.Parse(s) as Dictionary<string, object>;
                foreach (var o in Json.Arr(root, "items")) { var d = o as Dictionary<string, object>; if (d != null) res.Add(Item.FromJ(d)); }
            }
            catch { }
            return res;
        }
    }
}
