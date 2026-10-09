// Library rules: circulation, fines, numbering, retention. Same behaviour as the earlier version.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace Errin
{
    public class Lib
    {
        public const int LOAN = 7, RET_YEARS = 5;
        public Db D;
        public Lib(Db d) { D = d; }

        public static string Today() { return DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture); }
        public static DateTime Dt(string s) { return DateTime.ParseExact(s, "yyyy-MM-dd", CultureInfo.InvariantCulture); }
        public static string AddD(string d, int n) { return Dt(d).AddDays(n).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture); }
        public static int Diff(string a, string b) { return (int)Math.Round((Dt(a) - Dt(b)).TotalDays); }
        public static string Money(double n) { return "RM " + n.ToString("0.00", CultureInfo.InvariantCulture); }
        public static string Uid() { return Guid.NewGuid().ToString("N").Substring(0, 7); }
        public static string Isb(string s) { return Regex.Replace(s ?? "", @"[\s-]", ""); }

        public Item ItemByBarcode(string bc) { return D.Items.FirstOrDefault(i => string.Equals(i.barcode, bc, StringComparison.OrdinalIgnoreCase)); }
        public Patron PatronById(string id)
        {
            var p = D.Patrons.FirstOrDefault(x => string.Equals(x.id, id, StringComparison.OrdinalIgnoreCase));
            if (p != null) return p;
            string n = NormIc(id);                                   // "060306100261" finds "060306-10-0261"
            return n == null ? null : D.Patrons.FirstOrDefault(x => x.id == n || NormIc(x.id) == n);
        }
        /// <summary>Malaysian IC / MyKid number: 12 digits shown as YYMMDD-PB-###G. Returns null if it is not 12 digits.</summary>
        public static string NormIc(string s)
        {
            string d = Regex.Replace(s ?? "", @"[\s-]", "");
            return Regex.IsMatch(d, @"^\d{12}$") ? d.Substring(0, 6) + "-" + d.Substring(6, 2) + "-" + d.Substring(8, 4) : null;
        }
        /// <summary>The first six digits must be a possible birth date (month 01-12, day valid for that month).</summary>
        public static bool IcOk(string ic)
        {
            string n = NormIc(ic); if (n == null) return false;
            int m = int.Parse(n.Substring(2, 2)), d = int.Parse(n.Substring(4, 2));
            return m >= 1 && m <= 12 && d >= 1 && d <= DateTime.DaysInMonth(2000, m);
        }
        /// <summary>Simple wording for young students (used by the Simplified View).</summary>
        public static string Friendly(string err)
        {
            if (err == null) return "";
            if (err.StartsWith("Blocked: unpaid")) return "Please see the librarian before you borrow.";
            switch (err)
            {
                case "Student not found": return "I can't find that card. Please ask the librarian to help.";
                case "Item not found": return "I don't know this book. Please ask the librarian.";
                case "Item is already checked out": return "Someone else has this book right now.";
                case "Blocked: student has overdue items": return "You have a late book. Please bring it back first.";
                case "Loan limit reached": return "You have the most books you can borrow. Bring one back first.";
                case "Reserved for another student": return "This book is saved for a friend.";
                case "Item is not on loan": return "This book is already back. Thank you!";
            }
            return "Oops! Please ask the librarian to help.";
        }
        public Loan OpenLoan(string iid) { return D.Loans.FirstOrDefault(l => l.iid == iid && l.ret == null); }
        public string Title(string iid) { var i = D.Items.FirstOrDefault(x => x.id == iid); return i != null ? i.title : "(deleted)"; }
        public string PName(string pid) { var p = D.Patrons.FirstOrDefault(x => x.id == pid); return p != null ? p.name : "(removed)"; }
        public string PClass(string pid) { var p = D.Patrons.FirstOrDefault(x => x.id == pid); return p != null ? p.cls : ""; }
        public bool Overdue(Loan l) { return l.ret == null && string.CompareOrdinal(l.due, Today()) < 0; }
        public double Unpaid(string pid) { return D.Fines.Where(f => f.pid == pid && f.paid == "").Sum(f => f.amt); }
        public double Owed(string pid)
        {
            return D.Loans.Where(l => l.pid == pid && Overdue(l)).Sum(l => Diff(Today(), l.due) * D.Set.rate) + Unpaid(pid);
        }

        /* ---- circulation: each returns null on success or an error message; msg carries the success text ---- */
        public string Checkout(string pid, string bc, out string msg)
        {
            msg = null; var p = PatronById(pid); var i = ItemByBarcode(bc);
            if (p == null) return "Student not found"; if (i == null) return "Item not found";
            if (OpenLoan(i.id) != null) return "Item is already checked out";
            double un = Unpaid(p.id); if (un >= D.Set.block) return "Blocked: unpaid fines " + Money(un);
            if (D.Loans.Any(l => l.pid == p.id && Overdue(l))) return "Blocked: student has overdue items";
            if (D.Loans.Count(l => l.pid == p.id && l.ret == null) >= D.Set.maxLoans) return "Loan limit reached";
            var q = D.Holds.Where(h => h.iid == i.id && h.st != "done").ToList();
            if (q.Count > 0 && q[0].pid != p.id) return "Reserved for another student";
            if (q.Count > 0) q[0].st = "done";
            D.Loans.Add(new Loan { id = Uid(), iid = i.id, pid = p.id, @out = Today(), due = AddD(Today(), LOAN), ret = null, ren = 0 });
            p.last = Today(); msg = "Checked out. Due " + AddD(Today(), LOAN); return null;
        }
        public string Return(string bc, out string msg)
        {
            msg = null; var i = ItemByBarcode(bc ?? ""); if (i == null) return "Item not found";
            var l = OpenLoan(i.id); if (l == null) return "Item is not on loan";
            l.ret = Today(); string m = "Returned"; int d = Diff(Today(), l.due);
            if (d > 0)
            {
                double amt = Math.Round(d * D.Set.rate, 2);
                D.Fines.Add(new Fine { id = Uid(), pid = l.pid, iid = i.id, loan = l.id, days = d, amt = amt, date = Today(), paid = "" });
                m += ". Late " + d + " day(s): fine " + Money(amt);
            }
            var h = D.Holds.FirstOrDefault(x => x.iid == i.id && x.st == "waiting");
            if (h != null) { h.st = "ready"; m += ". Hold ready for " + PName(h.pid); }
            msg = m; return null;
        }
        public string Renew(string bc, out string msg)
        {
            msg = null; var i = ItemByBarcode(bc ?? ""); if (i == null) return "Item not found";
            var l = OpenLoan(i.id); if (l == null) return "Item is not on loan";
            if (Overdue(l)) return "Overdue items cannot be renewed";
            if (l.ren >= 2) return "Renewal limit (2) reached";
            if (D.Holds.Any(h => h.iid == i.id && h.st != "done")) return "Cannot renew: item has a hold";
            l.ren++; l.due = AddD(Today(), LOAN); msg = "Renewed. New due date " + l.due; return null;
        }
        public string PlaceHold(string pid, string bc, out string msg)
        {
            msg = null; var p = PatronById(pid); var i = ItemByBarcode(bc);
            if (p == null || i == null) return "Student or item not found";
            if (OpenLoan(i.id) == null) return "Item is available. Check it out instead";
            if (D.Holds.Any(h => h.iid == i.id && h.pid == p.id && h.st != "done")) return "Hold already exists";
            D.Holds.Add(new Hold { id = Uid(), iid = i.id, pid = p.id, date = Today(), st = "waiting" });
            msg = "Hold placed"; return null;
        }

        /* ---- numbering ---- */
        public static string FCtl(int n) { return "ER" + n.ToString().PadLeft(8, '0'); }
        public static string FAcq(int n) { return "ACQ-" + n.ToString().PadLeft(6, '0'); }
        public static readonly Regex RCtl = new Regex(@"^ER(\d+)$"), RAcq = new Regex(@"^ACQ-(\d+)$"), RBc = new Regex(@"^B(\d+)$");
        public Func<string> Gen(string field, Regex re, Func<int, string> fmt, HashSet<string> used, int bas)
        {
            int m = bas;
            foreach (var i in D.Items) { var k = re.Match(i.Get(field)); if (k.Success) { int v; if (int.TryParse(k.Groups[1].Value, out v) && v > m) m = v; } }
            return () => { string c; do { c = fmt(++m); } while (used != null && used.Contains(c.ToLowerInvariant())); return c; };
        }
        public string NextCtl() { return Gen("ctl", RCtl, FCtl, null, 0)(); }
        public string NextAcq() { return Gen("acq", RAcq, FAcq, null, 0)(); }
        public string NextBarcode() { return Gen("barcode", RBc, n => "B" + n, null, 1000)(); }
        public int FixItems()
        {
            var used = new HashSet<string>();
            foreach (var i in D.Items) if (i.ctl != "") { var k = i.ctl.ToLowerInvariant(); if (used.Contains(k)) i.ctl = ""; else used.Add(k); }
            var g = Gen("ctl", RCtl, FCtl, used, 0); int n = 0;
            foreach (var i in D.Items) if (i.ctl == "") { i.ctl = g(); used.Add(i.ctl.ToLowerInvariant()); n++; }
            return n;
        }
        public int AddItems(List<Item> list, string mode, bool assignAcq, HashSet<Item> dups, out int skipped)
        {
            var uc = new HashSet<string>(D.Items.Select(i => i.ctl.ToLowerInvariant())); var ub = new HashSet<string>(D.Items.Select(i => i.barcode.ToLowerInvariant()));
            var gc = Gen("ctl", RCtl, FCtl, uc, 0); var gb = Gen("barcode", RBc, n => "B" + n, ub, 1000); var ga = Gen("acq", RAcq, FAcq, null, 0);
            int added = 0; skipped = 0;
            foreach (var it in list)
            {
                if (dups != null && dups.Contains(it) && mode == "skip") { skipped++; continue; }
                var o = it.Clone(); o.id = Uid();
                o.barcode = it.barcode != "" && !ub.Contains(it.barcode.ToLowerInvariant()) ? it.barcode : gb(); ub.Add(o.barcode.ToLowerInvariant());
                o.ctl = it.ctl != "" && !uc.Contains(it.ctl.ToLowerInvariant()) ? it.ctl : gc(); uc.Add(o.ctl.ToLowerInvariant());
                if (o.acq == "" && assignAcq) o.acq = ga();
                D.Items.Add(o); added++;
            }
            return added;
        }
        public HashSet<Item> DupSet(List<Item> list)
        {
            var bc = new HashSet<string>(D.Items.Select(i => i.barcode.ToLowerInvariant())); var isb = new HashSet<string>(D.Items.Select(i => Isb(i.isbn)).Where(x => x != ""));
            return new HashSet<Item>(list.Where(i => (i.barcode != "" && bc.Contains(i.barcode.ToLowerInvariant())) || (i.barcode == "" && Isb(i.isbn) != "" && isb.Contains(Isb(i.isbn)))));
        }

        /* ---- data retention: delete inactive students after RET_YEARS ---- */
        public int Purge()
        {
            string lim = AddD(Today(), -365 * RET_YEARS - 1); var gone = new List<string>();
            foreach (var p in D.Patrons.ToList())
            {
                var dates = D.Loans.Where(l => l.pid == p.id).Select(l => l.@out).Concat(new[] { p.reg, p.last }).OrderBy(x => x, StringComparer.Ordinal).ToList();
                string act = dates.Last();
                bool busy = D.Loans.Any(l => l.pid == p.id && l.ret == null) || D.Fines.Any(f => f.pid == p.id && f.paid == "");
                if (string.CompareOrdinal(act, lim) < 0 && !busy) gone.Add(p.id);
            }
            if (gone.Count > 0)
            {
                D.Patrons.RemoveAll(p => gone.Contains(p.id)); D.Loans.RemoveAll(l => gone.Contains(l.pid));
                D.Fines.RemoveAll(f => gone.Contains(f.pid)); D.Holds.RemoveAll(h => gone.Contains(h.pid));
            }
            return gone.Count;
        }

        public static Db Seed()
        {
            string[][] b = {
                new[]{"Charlotte's Web","E. B. White","9780064400558","Harper","2001","F WHI","Animals--Fiction","184"},
                new[]{"A Brief History of Time","Stephen Hawking","9780553380163","Bantam","1998","523.1 HAW","Cosmology","212"},
                new[]{"Hatchet","Gary Paulsen","9781416936473","Simon & Schuster","2006","F PAU","Wilderness survival--Fiction","195"},
                new[]{"The Hobbit","J. R. R. Tolkien","9780547928227","Houghton Mifflin","2012","F TOL","Fantasy","300"},
                new[]{"Atlas of the World","National Geographic","9781426220654","National Geographic","2015","912 ATL","Atlases","304"} };
            var db = new Db();
            for (int k = 0; k < b.Length; k++)
                db.Items.Add(new Item { id = Uid(), ctl = FCtl(k + 1), barcode = "B" + (1001 + k), title = b[k][0], author = b[k][1], isbn = b[k][2], publisher = b[k][3], year = b[k][4], call = b[k][5], subject = b[k][6], pages = b[k][7] });
            db.Patrons.Add(new Patron { id = "120315-10-0001", name = "Aisha Rahman", cls = "Grade 7A", contact = "guardian@example.com", reg = Today(), last = Today() });
            return db;
        }

        /* ---- exports ---- */
        public static string ToCsv(IEnumerable<Item> items)
        {
            string[][] H = { new[]{"ctl","Control number"}, new[]{"acq","Acquisition number"}, new[]{"barcode","Barcode"}, new[]{"title","Title"}, new[]{"sub","Subtitle"}, new[]{"author","Author"},
                new[]{"isbn","ISBN"}, new[]{"publisher","Publisher"}, new[]{"place","Place"}, new[]{"year","Year"}, new[]{"pages","Pages"}, new[]{"subject","Subject"}, new[]{"call","Call number"} };
            Func<string, string> q = x => "\"" + (x ?? "").Replace("\"", "\"\"") + "\"";
            var sb = new System.Text.StringBuilder();
            sb.Append(string.Join(",", H.Select(h => q(h[1])).ToArray())).Append("\r\n");
            foreach (var i in items) sb.Append(string.Join(",", H.Select(h => q(i.Get(h[0]))).ToArray())).Append("\r\n");
            return sb.ToString();
        }
    }
}
