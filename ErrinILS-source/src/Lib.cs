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
        public const int LOAN = 7, RET_YEARS = 6;
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
            if (string.IsNullOrEmpty(id)) return null;
            var p = D.Patrons.FirstOrDefault(x => string.Equals(x.id, id, StringComparison.OrdinalIgnoreCase) ||
                                                  string.Equals(x.no_pengenalan, id, StringComparison.OrdinalIgnoreCase) ||
                                                  string.Equals(x.id_murid, id, StringComparison.OrdinalIgnoreCase));
            if (p != null) return p;
            string n = NormIc(id);                                   // "120315100001" finds "120315-10-0001"
            return n == null ? null : D.Patrons.FirstOrDefault(x => x.id == n || NormIc(x.id) == n || NormIc(x.no_pengenalan) == n);
        }
        public bool IsActive(Patron p)
        {
            if (p == null) return false;
            string st = (p.status_pengajian ?? "").Trim();
            if (string.Equals(st, "Deactivated", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(st, "Tidak Aktif", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(st, "Inactive", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(st, "Tamat", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(st, "Berhenti", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(st, "Pindah Sekolah", StringComparison.OrdinalIgnoreCase))
                return false;
            return true;
        }
        public bool IsActive(string pid)
        {
            return IsActive(PatronById(pid));
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
                case "Item has been weeded and withdrawn from circulation": return "This book has been retired from the library. Please choose another.";
                case "Blocked: student has overdue items": return "You have a late book. Please bring it back first.";
                case "Loan limit reached": return "You have the most books you can borrow. Bring one back first.";
                case "Reserved for another student": return "This book is saved for a friend.";
                case "Item is not on loan": return "This book is already back. Thank you!";
            }
            return "Oops! Please ask the librarian to help.";
        }
        public Loan OpenLoan(string iid) { return D.Loans.FirstOrDefault(l => l.iid == iid && l.ret == null); }
        public List<Loan> ItemLoans(string iid) { return D.Loans.Where(l => l.iid == iid).OrderByDescending(l => l.@out).ToList(); }
        public string Title(string iid) { var i = D.Items.FirstOrDefault(x => x.id == iid); return i != null ? i.title : "(deleted)"; }
        public string PName(string pid) { var p = D.Patrons.FirstOrDefault(x => x.id == pid); return p != null ? p.name : "(removed)"; }
        public string PClass(string pid) { var p = D.Patrons.FirstOrDefault(x => x.id == pid); return p != null ? p.cls : ""; }
        public bool Overdue(Loan l) { return l.ret == null && string.CompareOrdinal(l.due, Today()) < 0; }
        public double Unpaid(string pid) { return D.Fines.Where(f => f.pid == pid && f.paid == "").Sum(f => f.amt); }
        public double Owed(string pid)
        {
            return D.Loans.Where(l => l.pid == pid && Overdue(l)).Sum(l => Diff(Today(), l.due) * D.Set.rate) + Unpaid(pid);
        }

        /* ---- weeding helpers ---- */
        public bool IsWeeded(Item i) { return i != null && !string.IsNullOrEmpty(i.weeded); }
        public void Weed(Item i, string reason)
        {
            if (i == null) return;
            i.weeded = Today();
            i.weed_reason = (reason ?? "Weeded").Trim();
        }
        public void Unweed(Item i)
        {
            if (i == null) return;
            i.weeded = "";
            i.weed_reason = "";
        }

        /* ---- circulation: each returns null on success or an error message; msg carries the success text ---- */
        public string Checkout(string pid, string bc, out string msg)
        {
            msg = null; var p = PatronById(pid); var i = ItemByBarcode(bc);
            if (p == null) return "Student not found"; if (i == null) return "Item not found";
            if (!IsActive(p)) return "Blocked: student account is deactivated or inactive";
            if (IsWeeded(i)) return "Item has been weeded and withdrawn from circulation";
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
            if (!IsActive(p)) return "Blocked: student account is deactivated or inactive";
            if (IsWeeded(i)) return "Cannot place hold on a weeded item";
            if (OpenLoan(i.id) == null) return "Item is available. Check it out instead";
            double un = Unpaid(p.id);
            if (D.Set != null && un >= D.Set.block) return "Blocked: unpaid fines " + Money(un);
            if (D.Loans.Any(l => l.pid == p.id && Overdue(l))) return "Blocked: student has overdue items";
            int activeHolds = D.Holds.Count(h => h.pid == p.id && h.st != "done");
            int maxHolds = (D.Set != null && D.Set.maxLoans > 0) ? D.Set.maxLoans : 3;
            if (activeHolds >= maxHolds) return "Hold limit reached (maximum " + maxHolds + " reservations)";
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

        /* ---- data retention: archive inactive students after RET_YEARS to password-protected Archive ---- */
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
                if (D.ArchivedPatrons == null) D.ArchivedPatrons = new List<ArchivedPatron>();
                foreach (string pid in gone)
                {
                    var p = D.Patrons.FirstOrDefault(x => x.id == pid);
                    if (p == null) continue;
                    var ap = new ArchivedPatron
                    {
                        id = p.id,
                        name = p.name,
                        cls = p.cls,
                        contact = p.contact,
                        reg = p.reg,
                        last = p.last,
                        archivedDate = Today(),
                        loans = D.Loans.Where(l => l.pid == pid).ToList(),
                        fines = D.Fines.Where(f => f.pid == pid).ToList()
                    };
                    foreach (var k in Patron.Keys) ap.Set(k, p.Get(k));
                    ap.status_pengajian = "Deactivated";
                    D.ArchivedPatrons.RemoveAll(x => x.id == pid);
                    D.ArchivedPatrons.Add(ap);
                }

                D.Patrons.RemoveAll(p => gone.Contains(p.id));
                D.Loans.RemoveAll(l => gone.Contains(l.pid));
                D.Fines.RemoveAll(f => gone.Contains(f.pid));
                D.Holds.RemoveAll(h => gone.Contains(h.pid));
            }
            return gone.Count;
        }

        public bool RestoreArchivedPatron(string id)
        {
            if (D.ArchivedPatrons == null) return false;
            var ap = D.ArchivedPatrons.FirstOrDefault(x => string.Equals(x.id, id, StringComparison.OrdinalIgnoreCase) || NormIc(x.id) == NormIc(id));
            if (ap == null) return false;
            if (PatronById(ap.id) != null) return false; // already active
            var p = new Patron { id = ap.id, name = ap.name, cls = ap.cls, contact = ap.contact, reg = ap.reg, last = Today() };
            foreach (var k in Patron.Keys) p.Set(k, ap.Get(k));
            p.status_pengajian = "Aktif";
            p.last = Today();
            D.Patrons.Add(p);
            if (ap.loans != null) { foreach (var l in ap.loans) D.Loans.Add(l); }
            if (ap.fines != null) { foreach (var f in ap.fines) D.Fines.Add(f); }
            D.ArchivedPatrons.Remove(ap);
            return true;
        }

        /* ---- class year archiving (31st of December or manual snapshot) ---- */
        public int ArchiveCurrentYearClasses(int year)
        {
            if (D.ClassHistory == null) D.ClassHistory = new List<ClassYearRecord>();
            D.ClassHistory.RemoveAll(ch => ch.year == year);

            var classes = D.Patrons.GroupBy(p => string.IsNullOrEmpty(p.cls) ? "(No class)" : p.cls.Trim()).OrderBy(g => g.Key).ToList();
            int count = 0;
            string yPrefix = year.ToString();

            foreach (var grp in classes)
            {
                var rec = new ClassYearRecord
                {
                    year = year,
                    cls = grp.Key,
                    studentCount = grp.Count()
                };

                int classLoans = 0;
                foreach (var p in grp)
                {
                    int pLoans = D.Loans.Count(l => l.pid == p.id && l.@out != null && l.@out.StartsWith(yPrefix));
                    classLoans += pLoans;
                    rec.students.Add(new ClassYearStudent { id = p.id, name = p.name, loans = pLoans });
                }
                rec.totalLoans = classLoans;
                D.ClassHistory.Add(rec);
                count++;
            }
            return count;
        }

        public bool CheckDec31ClassArchive()
        {
            DateTime now = DateTime.Now;
            int curYear = now.Year;

            if (now.Month == 12 && now.Day == 31)
            {
                if (D.ClassHistory == null || !D.ClassHistory.Any(ch => ch.year == curYear))
                {
                    if (D.Patrons.Count > 0)
                    {
                        ArchiveCurrentYearClasses(curYear);
                        return true;
                    }
                }
            }
            // If year recently rolled over (early January) and previous year is not yet archived:
            int prevYear = curYear - 1;
            if (now.Month == 1 && now.Day <= 7 && (D.ClassHistory == null || !D.ClassHistory.Any(ch => ch.year == prevYear)))
            {
                if (D.Patrons.Count > 0)
                {
                    ArchiveCurrentYearClasses(prevYear);
                    return true;
                }
            }
            return false;
        }

        /* ---- batch patron registration with duplicate IC update ---- */
        public string BatchUpsertPatron(Patron pIn, out bool updated)
        {
            updated = false;
            if (pIn == null) return "No patron data";
            string rawIc = !string.IsNullOrEmpty(pIn.no_pengenalan) ? pIn.no_pengenalan : pIn.id;
            if (string.IsNullOrEmpty(rawIc)) rawIc = pIn.id_murid;
            string cleanName = (pIn.name ?? "").Trim();
            if (string.IsNullOrEmpty(rawIc)) return "Missing IC or student ID";
            if (string.IsNullOrEmpty(cleanName)) return "Missing student name";

            string norm = NormIc(rawIc) ?? rawIc.Trim();

            var existing = PatronById(norm);
            if (existing == null && norm != rawIc) existing = PatronById(rawIc);

            if (existing != null)
            {
                foreach (var k in Patron.Keys)
                {
                    string v = pIn.Get(k);
                    if (!string.IsNullOrEmpty(v)) existing.Set(k, v);
                }
                existing.name = cleanName;
                if (!string.IsNullOrEmpty(pIn.cls)) existing.cls = pIn.cls.Trim();
                if (!string.IsNullOrEmpty(pIn.contact)) existing.contact = pIn.contact.Trim();
                existing.last = Today();
                updated = true;
                return null;
            }

            var p = pIn.Clone();
            p.id = norm;
            p.name = cleanName;
            if (string.IsNullOrEmpty(p.no_pengenalan)) p.no_pengenalan = norm;
            if (string.IsNullOrEmpty(p.id_murid)) p.id_murid = norm;
            if (string.IsNullOrEmpty(p.cls) && !string.IsNullOrEmpty(p.nama_kelas)) p.cls = p.nama_kelas;
            if (string.IsNullOrEmpty(p.contact) && !string.IsNullOrEmpty(p.tel_penjaga1)) p.contact = p.tel_penjaga1;
            p.reg = Today();
            p.last = Today();
            D.Patrons.Add(p);
            return null;
        }

        public string BatchUpsertPatron(string rawIc, string name, string cls, string contact, out bool updated)
        {
            var p = new Patron
            {
                id = rawIc,
                no_pengenalan = rawIc,
                name = name,
                cls = cls,
                nama_kelas = cls,
                contact = contact,
                tel_penjaga1 = contact
            };
            return BatchUpsertPatron(p, out updated);
        }

        public static string FormatSchoolClass(string tahun, string namaKelas)
        {
            tahun = (tahun ?? "").Trim();
            namaKelas = (namaKelas ?? "").Trim();

            if (string.IsNullOrEmpty(tahun)) return namaKelas;
            if (string.IsNullOrEmpty(namaKelas)) return tahun;

            // If namaKelas already starts with a digit, e.g. "1 BIJAK", return it directly
            if (namaKelas.Length > 0 && char.IsDigit(namaKelas[0]))
                return namaKelas;

            string tUp = tahun.ToUpperInvariant();
            string prefix = "";

            if (tUp.Contains("PRASEKOLAH") || tUp.Contains("PRA-SEKOLAH"))
                prefix = "PRASEKOLAH";
            else if (tUp.Contains("PERALIHAN"))
                prefix = "PERALIHAN";
            else if (tUp.Contains("SATU") || tUp.Contains("1"))
                prefix = "1";
            else if (tUp.Contains("DUA") || tUp.Contains("2"))
                prefix = "2";
            else if (tUp.Contains("TIGA") || tUp.Contains("3"))
                prefix = "3";
            else if (tUp.Contains("EMPAT") || tUp.Contains("4"))
                prefix = "4";
            else if (tUp.Contains("LIMA") || tUp.Contains("5"))
                prefix = "5";
            else if (tUp.Contains("ENAM") || tUp.Contains("6"))
                prefix = "6";
            else if (tUp.Contains("TINGKATAN"))
            {
                var m = Regex.Match(tUp, @"\d+");
                if (m.Success) prefix = m.Value;
                else prefix = "T";
            }

            if (!string.IsNullOrEmpty(prefix))
            {
                if (namaKelas.ToUpperInvariant().StartsWith(prefix))
                    return namaKelas;
                return prefix + " " + namaKelas;
            }

            return tahun + " " + namaKelas;
        }

        public List<PatronImportRow> ParsePatronSpreadsheet(List<List<string>> rawRows)
        {
            var list = new List<PatronImportRow>();
            if (rawRows == null || rawRows.Count == 0) return list;

            int headerRow = -1;
            int icCol = -1, nameCol = -1, tahunCol = -1, namaKelasCol = -1, singleClassCol = -1;
            int tel1Col = -1, tel2Col = -1, telRumahCol = -1, penjaga1Col = -1, genericContactCol = -1;
            int idMuridCol = -1, dobCol = -1, statusPengajianCol = -1;
            int tarikhMasukSekolahCol = -1, tarikhMasukKelasCol = -1, guruKelasCol = -1;
            int jantinaCol = -1, kaumCol = -1, agamaCol = -1, warganegaraCol = -1;
            int statusOkuCol = -1, statusYatimCol = -1, penjaga2Col = -1;
            int alamat1Col = -1, alamat2Col = -1, alamat3Col = -1;
            int poskodCol = -1, bandarCol = -1, daerahCol = -1, negeriCol = -1;

            // 1. Scan the first rows to locate the header row (supports APDM rows 1-25)
            int maxScan = Math.Min(rawRows.Count, 25);
            for (int r = 0; r < maxScan; r++)
            {
                var row = rawRows[r];
                int foundIc = -1, foundName = -1;

                for (int c = 0; c < row.Count; c++)
                {
                    string h = (row[c] ?? "").Trim().ToLowerInvariant();
                    if (string.IsNullOrEmpty(h)) continue;

                    bool isGuardian = h.Contains("penjaga") || h.Contains("bapa") || h.Contains("ibu") || h.Contains("waris") || h.Contains("wali");
                    if (!isGuardian && !h.Contains("status") && !h.Contains("tarikh"))
                    {
                        if (h == "no. pengenalan" || h == "no pengenalan" || h == "mykid" || h == "no. kp" || h == "no kp" ||
                            h == "ic" || h == "no. ic" || h == "no ic" || h == "nric" || h == "nokp" || h == "kad pengenalan" ||
                            h.Contains("mykid") || h.Contains("no. pengenalan") || h.Contains("no pengenalan"))
                        {
                            foundIc = c;
                        }
                        else if (h == "id murid" || h == "id pelajar" || h == "student id" || h == "id")
                        {
                            if (foundIc < 0) foundIc = c;
                        }
                    }

                    bool isOtherName = isGuardian || h.Contains("guru") || h.Contains("sekolah") || h.Contains("kelas") || h.Contains("asrama") || h.Contains("bank") || h.Contains("majikan");
                    if (!isOtherName)
                    {
                        if (h == "nama" || h == "name" || h == "nama murid" || h == "nama pelajar" || h == "student name" || h == "full name")
                        {
                            foundName = c;
                        }
                        else if (h.Contains("nama") || h.Contains("name") || h.Contains("murid") || h.Contains("pelajar"))
                        {
                            if (foundName < 0) foundName = c;
                        }
                    }
                }

                if (foundIc >= 0 && foundName >= 0)
                {
                    headerRow = r;
                    break;
                }
            }

            // 2. Identify exact column mappings from the detected header row
            int startRow = 0;
            if (headerRow >= 0)
            {
                startRow = headerRow + 1;
                var hRow = rawRows[headerRow];
                for (int c = 0; c < hRow.Count; c++)
                {
                    string h = (hRow[c] ?? "").Trim().ToLowerInvariant();
                    if (string.IsNullOrEmpty(h)) continue;

                    bool isGuardian = h.Contains("penjaga") || h.Contains("bapa") || h.Contains("ibu") || h.Contains("waris") || h.Contains("wali");

                    // Student IC
                    if (!isGuardian && !h.Contains("status") && !h.Contains("tarikh"))
                    {
                        if (h == "no. pengenalan" || h == "no pengenalan" || h == "mykid" || h == "no. kp" || h == "no kp" ||
                            h == "ic" || h == "no. ic" || h == "no ic" || h == "nric" || h == "nokp" || h == "kad pengenalan")
                        {
                            icCol = c;
                        }
                        else if (h.Contains("mykid") || h.Contains("no. pengenalan") || h.Contains("no pengenalan"))
                        {
                            if (icCol < 0) icCol = c;
                        }
                        else if (h == "id murid" || h == "id pelajar" || h == "student id")
                        {
                            idMuridCol = c;
                        }
                    }

                    // Student Name
                    bool isOtherName = isGuardian || h.Contains("guru") || h.Contains("sekolah") || h.Contains("kelas") || h.Contains("asrama") || h.Contains("bank") || h.Contains("majikan");
                    if (!isOtherName)
                    {
                        if (h == "nama" || h == "name" || h == "nama murid" || h == "nama pelajar" || h == "student name" || h == "full name")
                        {
                            nameCol = c;
                        }
                        else if (h.Contains("nama") || h.Contains("name"))
                        {
                            if (nameCol < 0) nameCol = c;
                        }
                    }

                    // Class columns
                    if (!h.Contains("guru") && !h.Contains("tarikh") && !h.Contains("masuk") && !h.Contains("keluar"))
                    {
                        if (h == "tahun / tingkatan" || h == "tahun/tingkatan" || h == "tahun" || h == "tingkatan" || h == "darjah" || h == "standard" || h == "grade" || h == "form")
                        {
                            tahunCol = c;
                        }
                        else if (h == "nama kelas" || h == "nama_kelas" || h == "class name")
                        {
                            namaKelasCol = c;
                        }
                        else if (h == "kelas" || h == "class" || h == "tingkatan" || h == "darjah")
                        {
                            if (singleClassCol < 0) singleClassCol = c;
                        }
                    }

                    // Contact / Guardian / Phone columns
                    if (h.Contains("tel") || h.Contains("phone") || h.Contains("telefon") || h.Contains("bimbit") || h.Contains("hp") || h.Contains("mobile") || h.Contains("contact"))
                    {
                        if (h.Contains("1") && h.Contains("bimbit")) tel1Col = c;
                        else if (h.Contains("1") && (h.Contains("rumah") || h.Contains("home"))) telRumahCol = c;
                        else if (h.Contains("2")) tel2Col = c;
                        else if (h.Contains("1")) tel1Col = c;
                        else if (tel1Col < 0) tel1Col = c;
                        else if (genericContactCol < 0) genericContactCol = c;
                    }
                    else if (h == "penjaga 1" || h == "nama penjaga 1" || h == "penjaga" || h == "nama penjaga" || h == "guardian" || h == "guardian name")
                    {
                        penjaga1Col = c;
                    }
                    else if (h == "penjaga 2" || h == "nama penjaga 2" || h == "guardian 2")
                    {
                        penjaga2Col = c;
                    }

                    // APDM demographic & school columns
                    if (h == "tarikh lahir" || h == "t.lahir" || h == "tarikh_lahir" || h == "dob" || h == "birth date") dobCol = c;
                    else if (h == "status pengajian" || h == "status murid" || h == "status persekolahan" || h == "status") statusPengajianCol = c;
                    else if (h == "tarikh masuk sekolah" || h == "tarikh_masuk_sekolah" || h == "t.masuk sekolah") tarikhMasukSekolahCol = c;
                    else if (h == "tarikh masuk kelas" || h == "tarikh_masuk_kelas" || h == "t.masuk kelas") tarikhMasukKelasCol = c;
                    else if (h == "nama guru kelas" || h == "guru kelas" || h == "class teacher") guruKelasCol = c;
                    else if (h == "jantina" || h == "gender" || h == "sex") jantinaCol = c;
                    else if (h == "kaum" || h == "bangsa" || h == "race" || h == "ethnicity") kaumCol = c;
                    else if (h == "agama" || h == "religion") agamaCol = c;
                    else if (h == "warganegara" || h == "kewarganegaraan" || h == "citizenship") warganegaraCol = c;
                    else if (h == "status oku" || h == "oku") statusOkuCol = c;
                    else if (h == "status yatim" || h == "yatim") statusYatimCol = c;
                    else if (h == "alamat 1" || h == "alamat baris 1" || h == "alamat") alamat1Col = c;
                    else if (h == "alamat 2" || h == "alamat baris 2") alamat2Col = c;
                    else if (h == "alamat 3" || h == "alamat baris 3") alamat3Col = c;
                    else if (h == "poskod" || h == "postcode" || h == "zip") poskodCol = c;
                    else if (h == "bandar" || h == "city") bandarCol = c;
                    else if (h == "daerah" || h == "district") daerahCol = c;
                    else if (h == "negeri" || h == "state") negeriCol = c;
                }

                if (icCol < 0 && idMuridCol >= 0) icCol = idMuridCol;
            }
            else
            {
                // Fallback default columns
                icCol = 0;
                nameCol = 1;
                singleClassCol = 2;
                genericContactCol = 3;
                if (rawRows.Count > 0)
                {
                    string first = (rawRows[0].Count > 0 ? rawRows[0][0] : "").Trim();
                    if (Regex.IsMatch(first, @"[A-Za-z]") && !Regex.IsMatch(first, @"^\d"))
                        startRow = 1;
                    else
                        startRow = 0;
                }
            }

            // 3. Extract and process rows
            var seenInBatch = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int r = startRow; r < rawRows.Count; r++)
            {
                var row = rawRows[r];
                Func<int, string> getVal = col => (col >= 0 && col < row.Count) ? (row[col] ?? "").Trim() : "";

                string rawIc = getVal(icCol);
                if (string.IsNullOrEmpty(rawIc) && idMuridCol >= 0) rawIc = getVal(idMuridCol);

                string name = getVal(nameCol);

                // Class resolution
                string cls = "";
                if (tahunCol >= 0 && namaKelasCol >= 0)
                {
                    cls = FormatSchoolClass(getVal(tahunCol), getVal(namaKelasCol));
                }
                else if (namaKelasCol >= 0)
                {
                    cls = getVal(namaKelasCol);
                }
                else if (singleClassCol >= 0)
                {
                    cls = getVal(singleClassCol);
                }
                else if (tahunCol >= 0)
                {
                    cls = getVal(tahunCol);
                }

                // Contact resolution
                string phone = getVal(tel1Col);
                if (string.IsNullOrEmpty(phone)) phone = getVal(telRumahCol);
                if (string.IsNullOrEmpty(phone)) phone = getVal(tel2Col);
                if (string.IsNullOrEmpty(phone)) phone = getVal(genericContactCol);

                if (phone.StartsWith("'")) phone = phone.Substring(1).Trim();

                string phone2 = getVal(tel2Col);
                if (phone2.StartsWith("'")) phone2 = phone2.Substring(1).Trim();

                string guardian = getVal(penjaga1Col);
                string contact = phone;
                if (!string.IsNullOrEmpty(phone) && !string.IsNullOrEmpty(guardian))
                    contact = phone + " (" + guardian + ")";
                else if (string.IsNullOrEmpty(phone) && !string.IsNullOrEmpty(guardian))
                    contact = guardian;

                // Skip blank or footer rows
                if (string.IsNullOrEmpty(rawIc) && string.IsNullOrEmpty(name)) continue;
                string upIc = rawIc.ToUpperInvariant();
                string upName = name.ToUpperInvariant();
                if (upIc.StartsWith("JUMLAH") || upName.StartsWith("JUMLAH") || upIc.StartsWith("TOTAL") || upName.StartsWith("TOTAL"))
                    continue;

                string norm = NormIc(rawIc);
                if (string.IsNullOrEmpty(norm)) norm = rawIc;

                var item = new PatronImportRow
                {
                    rawIc = rawIc,
                    normIc = norm,
                    name = name,
                    cls = cls,
                    contact = contact
                };

                item.patron = new Patron
                {
                    id = norm,
                    id_murid = getVal(idMuridCol),
                    no_pengenalan = getVal(icCol),
                    name = name,
                    dob = getVal(dobCol),
                    status_pengajian = getVal(statusPengajianCol),
                    tarikh_masuk_sekolah = getVal(tarikhMasukSekolahCol),
                    tarikh_masuk_kelas = getVal(tarikhMasukKelasCol),
                    tahun_tingkatan = getVal(tahunCol),
                    nama_kelas = !string.IsNullOrEmpty(getVal(namaKelasCol)) ? getVal(namaKelasCol) : cls,
                    nama_guru_kelas = getVal(guruKelasCol),
                    jantina = getVal(jantinaCol),
                    kaum = getVal(kaumCol),
                    agama = getVal(agamaCol),
                    warganegara = getVal(warganegaraCol),
                    status_oku = getVal(statusOkuCol),
                    status_yatim = getVal(statusYatimCol),
                    nama_penjaga1 = guardian,
                    tel_penjaga1 = phone,
                    nama_penjaga2 = getVal(penjaga2Col),
                    tel_penjaga2 = phone2,
                    alamat1 = getVal(alamat1Col),
                    alamat2 = getVal(alamat2Col),
                    alamat3 = getVal(alamat3Col),
                    poskod = getVal(poskodCol),
                    bandar = getVal(bandarCol),
                    daerah = getVal(daerahCol),
                    negeri = getVal(negeriCol),
                    cls = cls,
                    contact = contact
                };
                if (string.IsNullOrEmpty(item.patron.id_murid)) item.patron.id_murid = norm;
                if (string.IsNullOrEmpty(item.patron.no_pengenalan)) item.patron.no_pengenalan = norm;

                if (string.IsNullOrEmpty(rawIc))
                {
                    item.action = "Invalid: Missing IC / ID";
                    item.isValid = false;
                }
                else if (string.IsNullOrEmpty(name))
                {
                    item.action = "Invalid: Missing Name";
                    item.isValid = false;
                }
                else
                {
                    var existing = PatronById(norm);
                    if (existing == null && norm != rawIc) existing = PatronById(rawIc);

                    if (existing != null)
                    {
                        item.isUpdate = true;
                        item.isValid = true;
                        item.action = "Update Existing: " + existing.name + (existing.cls != "" ? " (" + existing.cls + ")" : "");
                    }
                    else if (seenInBatch.Contains(norm))
                    {
                        item.isUpdate = true;
                        item.isValid = true;
                        item.action = "Duplicate in File (will update earlier row)";
                    }
                    else
                    {
                        item.isUpdate = false;
                        item.isValid = true;
                        item.action = "New Student";
                    }
                    seenInBatch.Add(norm);
                }

                list.Add(item);
            }

            return list;
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
                new[]{"isbn","ISBN"}, new[]{"publisher","Publisher"}, new[]{"place","Place"}, new[]{"year","Year"}, new[]{"pages","Pages"}, new[]{"subject","Subject"}, new[]{"call","Call number"},
                new[]{"weeded","Weeded date"}, new[]{"weed_reason","Weeding reason"} };
            Func<string, string> q = x => "\"" + (x ?? "").Replace("\"", "\"\"") + "\"";
            var sb = new System.Text.StringBuilder();
            sb.Append(string.Join(",", H.Select(h => q(h[1])).ToArray())).Append("\r\n");
            foreach (var i in items) sb.Append(string.Join(",", H.Select(h => q(i.Get(h[0]))).ToArray())).Append("\r\n");
            return sb.ToString();
        }
    }
}
