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
        public bool Matches(string q)
        {
            if (string.IsNullOrEmpty(q)) return true;
            if (title != null && title.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (author != null && author.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (barcode != null && barcode.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (ctl != null && ctl.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (acq != null && acq.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (call != null && call.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (isbn != null && isbn.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (publisher != null && publisher.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (subject != null && subject.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (sub != null && sub.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (weed_reason != null && weed_reason.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (year != null && year.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }
        public bool MatchesAll(string[] tokens)
        {
            if (tokens == null || tokens.Length == 0) return true;
            for (int i = 0; i < tokens.Length; i++)
            {
                if (!string.IsNullOrEmpty(tokens[i]) && !Matches(tokens[i])) return false;
            }
            return true;
        }
    }

    public class PatronFieldDef
    {
        public string Key;
        public string Label;
        public bool DefaultRequired;
        public string Section;
        public string[] Options;
        public PatronFieldDef(string key, string label, bool defReq, string section, string[] options = null)
        {
            Key = key; Label = label; DefaultRequired = defReq; Section = section; Options = options;
        }
    }

    public class Patron
    {
        public string id = "", name = "", cls = "", contact = "", reg = "", last = "";
        public string id_murid = "";
        public string no_pengenalan = "";
        public string dob = "";
        public string status_pengajian = "";
        public string tarikh_masuk_sekolah = "";
        public string tarikh_masuk_kelas = "";
        public string tahun_tingkatan = "";
        public string nama_kelas = "";
        public string nama_guru_kelas = "";
        public string jantina = "";
        public string kaum = "";
        public string agama = "";
        public string warganegara = "";
        public string status_oku = "";
        public string status_yatim = "";
        public string nama_penjaga1 = "";
        public string tel_penjaga1 = "";
        public string nama_penjaga2 = "";
        public string tel_penjaga2 = "";
        public string alamat1 = "";
        public string alamat2 = "";
        public string alamat3 = "";
        public string poskod = "";
        public string bandar = "";
        public string daerah = "";
        public string negeri = "";

        public static readonly PatronFieldDef[] PatronFields = new[]
        {
            // Maklumat Asas & Pengenalan
            new PatronFieldDef("id_murid", "ID Murid", true, "Maklumat Asas & Pengenalan"),
            new PatronFieldDef("name", "Nama", true, "Maklumat Asas & Pengenalan"),
            new PatronFieldDef("no_pengenalan", "No. Pengenalan", true, "Maklumat Asas & Pengenalan"),
            new PatronFieldDef("dob", "Tarikh Lahir", false, "Maklumat Asas & Pengenalan"),
            new PatronFieldDef("jantina", "Jantina", true, "Maklumat Asas & Pengenalan", new[] { "Lelaki", "Perempuan" }),
            new PatronFieldDef("status_pengajian", "Status Pengajian", false, "Maklumat Asas & Pengenalan", new[] { "Aktif", "Bersekolah", "Mengulang", "Pindah Sekolah", "Tamat", "Berhenti" }),

            // Maklumat Persekolahan & Kelas
            new PatronFieldDef("tarikh_masuk_sekolah", "Tarikh Masuk Sekolah", true, "Maklumat Persekolahan & Kelas"),
            new PatronFieldDef("tarikh_masuk_kelas", "Tarikh Masuk Kelas", false, "Maklumat Persekolahan & Kelas"),
            new PatronFieldDef("tahun_tingkatan", "Tahun / Tingkatan", true, "Maklumat Persekolahan & Kelas", new[] { "Prasekolah", "Tahun 1", "Tahun 2", "Tahun 3", "Tahun 4", "Tahun 5", "Tahun 6", "Peralihan", "Tingkatan 1", "Tingkatan 2", "Tingkatan 3", "Tingkatan 4", "Tingkatan 5", "Tingkatan 6", "Pendidikan Khas" }),
            new PatronFieldDef("nama_kelas", "Nama Kelas", true, "Maklumat Persekolahan & Kelas"),
            new PatronFieldDef("nama_guru_kelas", "Nama Guru Kelas", true, "Maklumat Persekolahan & Kelas"),

            // Demografi & Profil
            new PatronFieldDef("kaum", "Kaum", false, "Demografi & Profil", new[] { "Melayu", "Cina", "India", "Bumiputera Sabah", "Bumiputera Sarawak", "Orang Asli", "Lain-lain" }),
            new PatronFieldDef("agama", "Agama", false, "Demografi & Profil", new[] { "Islam", "Buddha", "Kristian", "Hindu", "Sikh", "Tao", "Lain-lain" }),
            new PatronFieldDef("warganegara", "Warganegara", false, "Demografi & Profil", new[] { "Warganegara", "Bukan Warganegara", "Penduduk Tetap" }),
            new PatronFieldDef("status_oku", "Status OKU", false, "Demografi & Profil", new[] { "Tidak", "Ya - Penglihatan", "Ya - Pendengaran", "Ya - Pertuturan", "Ya - Fizikal", "Ya - Pembelajaran", "Ya - Pelbagai", "Ya - Lain-lain" }),
            new PatronFieldDef("status_yatim", "Status Yatim", false, "Demografi & Profil", new[] { "Tidak", "Yatim (Kematian Bapa)", "Piatu (Kematian Ibu)", "Yatim Piatu (Kematian Ibu & Bapa)" }),

            // Maklumat Ibu Bapa / Penjaga
            new PatronFieldDef("nama_penjaga1", "Nama Penjaga 1", true, "Maklumat Ibu Bapa / Penjaga"),
            new PatronFieldDef("tel_penjaga1", "No. Tel. Bimbit Penjaga 1", true, "Maklumat Ibu Bapa / Penjaga"),
            new PatronFieldDef("nama_penjaga2", "Nama Penjaga 2", false, "Maklumat Ibu Bapa / Penjaga"),
            new PatronFieldDef("tel_penjaga2", "No. Telefon Penjaga 2", false, "Maklumat Ibu Bapa / Penjaga"),

            // Alamat Tempat Tinggal
            new PatronFieldDef("alamat1", "Alamat 1", true, "Alamat Tempat Tinggal"),
            new PatronFieldDef("alamat2", "Alamat 2", false, "Alamat Tempat Tinggal"),
            new PatronFieldDef("alamat3", "Alamat 3", false, "Alamat Tempat Tinggal"),
            new PatronFieldDef("poskod", "Poskod", false, "Alamat Tempat Tinggal"),
            new PatronFieldDef("bandar", "Bandar", false, "Alamat Tempat Tinggal"),
            new PatronFieldDef("daerah", "Daerah", false, "Alamat Tempat Tinggal"),
            new PatronFieldDef("negeri", "Negeri", false, "Alamat Tempat Tinggal", new[] { "Johor", "Kedah", "Kelantan", "Melaka", "Negeri Sembilan", "Pahang", "Perak", "Perlis", "Pulau Pinang", "Sabah", "Sarawak", "Selangor", "Terengganu", "Wilayah Persekutuan Kuala Lumpur", "Wilayah Persekutuan Labuan", "Wilayah Persekutuan Putrajaya" })
        };

        public static readonly string[] Keys = new[]
        {
            "id", "name", "cls", "contact", "reg", "last",
            "id_murid", "no_pengenalan", "dob", "status_pengajian", "tarikh_masuk_sekolah", "tarikh_masuk_kelas",
            "tahun_tingkatan", "nama_kelas", "nama_guru_kelas", "jantina", "kaum", "agama",
            "warganegara", "status_oku", "status_yatim", "nama_penjaga1", "tel_penjaga1", "nama_penjaga2", "tel_penjaga2",
            "alamat1", "alamat2", "alamat3", "poskod", "bandar", "daerah", "negeri"
        };

        public string Get(string k)
        {
            switch (k)
            {
                case "id": return id; case "name": return name; case "cls": return cls; case "contact": return contact; case "reg": return reg; case "last": return last;
                case "id_murid": return id_murid; case "no_pengenalan": return no_pengenalan; case "dob": return dob; case "status_pengajian": return status_pengajian;
                case "tarikh_masuk_sekolah": return tarikh_masuk_sekolah; case "tarikh_masuk_kelas": return tarikh_masuk_kelas;
                case "tahun_tingkatan": return tahun_tingkatan; case "nama_kelas": return nama_kelas; case "nama_guru_kelas": return nama_guru_kelas;
                case "jantina": return jantina; case "kaum": return kaum; case "agama": return agama;
                case "warganegara": return warganegara; case "status_oku": return status_oku; case "status_yatim": return status_yatim;
                case "nama_penjaga1": return nama_penjaga1; case "tel_penjaga1": return tel_penjaga1; case "nama_penjaga2": return nama_penjaga2; case "tel_penjaga2": return tel_penjaga2;
                case "alamat1": return alamat1; case "alamat2": return alamat2; case "alamat3": return alamat3;
                case "poskod": return poskod; case "bandar": return bandar; case "daerah": return daerah; case "negeri": return negeri;
            }
            return "";
        }

        public void Set(string k, string v)
        {
            v = v ?? "";
            switch (k)
            {
                case "id": id = v; break; case "name": name = v; break; case "cls": cls = v; break; case "contact": contact = v; break; case "reg": reg = v; break; case "last": last = v; break;
                case "id_murid": id_murid = v; break; case "no_pengenalan": no_pengenalan = v; break; case "dob": dob = v; break; case "status_pengajian": status_pengajian = v; break;
                case "tarikh_masuk_sekolah": tarikh_masuk_sekolah = v; break; case "tarikh_masuk_kelas": tarikh_masuk_kelas = v; break;
                case "tahun_tingkatan": tahun_tingkatan = v; break; case "nama_kelas": nama_kelas = v; break; case "nama_guru_kelas": nama_guru_kelas = v; break;
                case "jantina": jantina = v; break; case "kaum": kaum = v; break; case "agama": agama = v; break;
                case "warganegara": warganegara = v; break; case "status_oku": status_oku = v; break; case "status_yatim": status_yatim = v; break;
                case "nama_penjaga1": nama_penjaga1 = v; break; case "tel_penjaga1": tel_penjaga1 = v; break; case "nama_penjaga2": nama_penjaga2 = v; break; case "tel_penjaga2": tel_penjaga2 = v; break;
                case "alamat1": alamat1 = v; break; case "alamat2": alamat2 = v; break; case "alamat3": alamat3 = v; break;
                case "poskod": poskod = v; break; case "bandar": bandar = v; break; case "daerah": daerah = v; break; case "negeri": negeri = v; break;
            }
        }

        public Patron Clone()
        {
            var c = new Patron();
            foreach (var k in Keys) c.Set(k, Get(k));
            return c;
        }

        public void CopyFrom(Patron o)
        {
            foreach (var k in Keys) Set(k, o.Get(k));
        }

        public Dictionary<string, object> ToJ()
        {
            var d = new Dictionary<string, object>();
            foreach (var k in Keys) d[k] = Get(k);
            return d;
        }

        public static Patron FromJ(Dictionary<string, object> d)
        {
            var p = new Patron();
            foreach (var k in Keys)
            {
                if (d.ContainsKey(k)) p.Set(k, Json.Str(d, k));
            }
            if (string.IsNullOrEmpty(p.no_pengenalan)) p.no_pengenalan = p.id;
            if (string.IsNullOrEmpty(p.id_murid)) p.id_murid = p.id;
            if (string.IsNullOrEmpty(p.nama_kelas)) p.nama_kelas = p.cls;
            if (string.IsNullOrEmpty(p.tel_penjaga1)) p.tel_penjaga1 = p.contact;
            return p;
        }
    }
    public class Loan { public string id = "", iid = "", pid = "", @out = "", due = "", ret = null; public int ren; }
    public class Hold { public string id = "", iid = "", pid = "", date = "", st = "waiting"; }
    public class Fine { public string id = "", pid = "", iid = "", loan = "", date = "", paid = ""; public int days; public double amt; }
    public class ZTarget { public string n = "", host = "", db = ""; public int port = 210; }
    public class Settings
    {
        public string school = "";
        public double rate = 0.1;
        public int maxLoans = 3;
        public double block = 5;
        public bool opacLogin = false;
        public List<ZTarget> zt = new List<ZTarget>();
        public List<string> requiredPatronFields = null;

        public bool IsPatronFieldRequired(string key)
        {
            if (requiredPatronFields != null)
            {
                return requiredPatronFields.Contains(key);
            }
            for (int i = 0; i < Patron.PatronFields.Length; i++)
            {
                if (Patron.PatronFields[i].Key == key)
                    return Patron.PatronFields[i].DefaultRequired;
            }
            return false;
        }

        public List<string> GetRequiredPatronFields()
        {
            if (requiredPatronFields != null) return new List<string>(requiredPatronFields);
            var list = new List<string>();
            for (int i = 0; i < Patron.PatronFields.Length; i++)
            {
                if (Patron.PatronFields[i].DefaultRequired)
                    list.Add(Patron.PatronFields[i].Key);
            }
            return list;
        }
    }

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
        public string id_murid = "", no_pengenalan = "", dob = "", status_pengajian = "",
            tarikh_masuk_sekolah = "", tarikh_masuk_kelas = "", tahun_tingkatan = "",
            nama_kelas = "", nama_guru_kelas = "", jantina = "", kaum = "", agama = "",
            warganegara = "", status_oku = "", status_yatim = "", nama_penjaga1 = "",
            tel_penjaga1 = "", nama_penjaga2 = "", tel_penjaga2 = "", alamat1 = "", alamat2 = "", alamat3 = "",
            poskod = "", bandar = "", daerah = "", negeri = "";
        public List<Loan> loans = new List<Loan>();
        public List<Fine> fines = new List<Fine>();

        public string Get(string k)
        {
            switch (k)
            {
                case "id": return id; case "name": return name; case "cls": return cls; case "contact": return contact; case "reg": return reg; case "last": return last; case "archivedDate": return archivedDate;
                case "id_murid": return id_murid; case "no_pengenalan": return no_pengenalan; case "dob": return dob; case "status_pengajian": return status_pengajian;
                case "tarikh_masuk_sekolah": return tarikh_masuk_sekolah; case "tarikh_masuk_kelas": return tarikh_masuk_kelas;
                case "tahun_tingkatan": return tahun_tingkatan; case "nama_kelas": return nama_kelas; case "nama_guru_kelas": return nama_guru_kelas;
                case "jantina": return jantina; case "kaum": return kaum; case "agama": return agama;
                case "warganegara": return warganegara; case "status_oku": return status_oku; case "status_yatim": return status_yatim;
                case "nama_penjaga1": return nama_penjaga1; case "tel_penjaga1": return tel_penjaga1; case "nama_penjaga2": return nama_penjaga2; case "tel_penjaga2": return tel_penjaga2;
                case "alamat1": return alamat1; case "alamat2": return alamat2; case "alamat3": return alamat3;
                case "poskod": return poskod; case "bandar": return bandar; case "daerah": return daerah; case "negeri": return negeri;
            }
            return "";
        }

        public void Set(string k, string v)
        {
            v = v ?? "";
            switch (k)
            {
                case "id": id = v; break; case "name": name = v; break; case "cls": cls = v; break; case "contact": contact = v; break; case "reg": reg = v; break; case "last": last = v; break; case "archivedDate": archivedDate = v; break;
                case "id_murid": id_murid = v; break; case "no_pengenalan": no_pengenalan = v; break; case "dob": dob = v; break; case "status_pengajian": status_pengajian = v; break;
                case "tarikh_masuk_sekolah": tarikh_masuk_sekolah = v; break; case "tarikh_masuk_kelas": tarikh_masuk_kelas = v; break;
                case "tahun_tingkatan": tahun_tingkatan = v; break; case "nama_kelas": nama_kelas = v; break; case "nama_guru_kelas": nama_guru_kelas = v; break;
                case "jantina": jantina = v; break; case "kaum": kaum = v; break; case "agama": agama = v; break;
                case "warganegara": warganegara = v; break; case "status_oku": status_oku = v; break; case "status_yatim": status_yatim = v; break;
                case "nama_penjaga1": nama_penjaga1 = v; break; case "tel_penjaga1": tel_penjaga1 = v; break; case "nama_penjaga2": nama_penjaga2 = v; break; case "tel_penjaga2": tel_penjaga2 = v; break;
                case "alamat1": alamat1 = v; break; case "alamat2": alamat2 = v; break; case "alamat3": alamat3 = v; break;
                case "poskod": poskod = v; break; case "bandar": bandar = v; break; case "daerah": daerah = v; break; case "negeri": negeri = v; break;
            }
        }

        public Dictionary<string, object> ToJ()
        {
            var d = new Dictionary<string, object>();
            foreach (var k in Patron.Keys) d[k] = Get(k);
            d["archivedDate"] = archivedDate;
            d["loans"] = loans.Select(l => (object)new Dictionary<string, object> { { "id", l.id }, { "iid", l.iid }, { "pid", l.pid }, { "out", l.@out }, { "due", l.due }, { "ret", l.ret }, { "ren", (double)l.ren } }).ToList();
            d["fines"] = fines.Select(f => (object)new Dictionary<string, object> { { "id", f.id }, { "pid", f.pid }, { "iid", f.iid }, { "loan", f.loan }, { "days", (double)f.days }, { "amt", f.amt }, { "date", f.date }, { "paid", f.paid } }).ToList();
            return d;
        }

        public static ArchivedPatron FromJ(Dictionary<string, object> d)
        {
            var ap = new ArchivedPatron();
            foreach (var k in Patron.Keys)
            {
                if (d.ContainsKey(k)) ap.Set(k, Json.Str(d, k));
            }
            ap.archivedDate = Json.Str(d, "archivedDate");
            if (string.IsNullOrEmpty(ap.no_pengenalan)) ap.no_pengenalan = ap.id;
            if (string.IsNullOrEmpty(ap.id_murid)) ap.id_murid = ap.id;
            if (string.IsNullOrEmpty(ap.nama_kelas)) ap.nama_kelas = ap.cls;
            if (string.IsNullOrEmpty(ap.tel_penjaga1)) ap.tel_penjaga1 = ap.contact;
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
            return ap;
        }
    }

    public class PatronImportRow
    {
        public string rawIc = "", normIc = "", name = "", cls = "", contact = "", action = "";
        public bool isUpdate, isValid;
        public Patron patron = new Patron();
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
            d["patrons"] = Patrons.Select(p => (object)p.ToJ()).ToList();
            d["loans"] = Loans.Select(l => (object)new Dictionary<string, object> { { "id", l.id }, { "iid", l.iid }, { "pid", l.pid }, { "out", l.@out }, { "due", l.due }, { "ret", l.ret }, { "ren", (double)l.ren } }).ToList();
            d["holds"] = Holds.Select(h => (object)new Dictionary<string, object> { { "id", h.id }, { "iid", h.iid }, { "pid", h.pid }, { "date", h.date }, { "st", h.st } }).ToList();
            d["fines"] = Fines.Select(f => (object)new Dictionary<string, object> { { "id", f.id }, { "pid", f.pid }, { "iid", f.iid }, { "loan", f.loan }, { "days", (double)f.days }, { "amt", f.amt }, { "date", f.date }, { "paid", f.paid } }).ToList();
            var s = new Dictionary<string, object> { { "school", Set.school }, { "rate", Set.rate }, { "maxLoans", (double)Set.maxLoans }, { "block", Set.block }, { "opacLogin", Set.opacLogin } };
            if (Set.requiredPatronFields != null) s["requiredPatronFields"] = Set.requiredPatronFields.Select(x => (object)x).ToList();
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
                d["archivedPatrons"] = ArchivedPatrons.Select(ap => (object)ap.ToJ()).ToList();
            }

            return Json.Write(d);
        }

        public static Db FromJson(string json)
        {
            var root = Json.Parse(json) as Dictionary<string, object>;
            if (root == null) throw new FormatException("Not a catalogue file");
            var db = new Db();
            foreach (var o in Json.Arr(root, "items")) { var d = o as Dictionary<string, object>; if (d != null) db.Items.Add(Item.FromJ(d)); }
            foreach (var o in Json.Arr(root, "patrons")) { var d = o as Dictionary<string, object>; if (d != null) db.Patrons.Add(Patron.FromJ(d)); }
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
            if (s.ContainsKey("opacLogin")) db.Set.opacLogin = Json.Bool(s, "opacLogin");
            if (s.ContainsKey("requiredPatronFields")) { db.Set.requiredPatronFields = Json.Arr(s, "requiredPatronFields").Select(x => x != null ? x.ToString() : "").ToList(); }
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

            foreach (var o in Json.Arr(root, "archivedPatrons")) { var d = o as Dictionary<string, object>; if (d != null) db.ArchivedPatrons.Add(ArchivedPatron.FromJ(d)); }

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
