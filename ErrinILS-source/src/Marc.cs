// ErrinILS MARC 21 toolkit: build ISO 2709 (.mrc), MARCXML, MarcEdit text (.mrk), MARC-in-JSON; parse .mrc, MARCXML, .mrk.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Errin
{
    public class Field
    {
        public string Tag;
        public string Ind;                       // null for control fields (001-009)
        public string V;                         // control field value
        public List<string[]> Subs = new List<string[]>();   // {code, value}
    }
    public class Rec { public string Leader; public List<Field> Fields = new List<Field>(); }
    public class ParseResult { public List<Rec> Records = new List<Rec>(); public int Bad; public string Format = ""; }

    public static class Marc
    {
        public const string LDR = "00000nam a2200000 a 4500";   // position 9 = 'a' -> UTF-8
        static readonly Encoding U8 = new UTF8Encoding(false);
        static string Clean(string x) { return (x ?? "").Trim(); }

        /* ---------- item -> MARC fields ---------- */
        public static List<Field> RecFields(Item i)
        {
            var F = new List<Field>();
            F.Add(new Field { Tag = "001", V = !string.IsNullOrEmpty(i.ctl) ? i.ctl : (i.barcode ?? "") });
            Action<string, string, string[][]> add = (t, ind, sf) =>
            {
                var l = sf.Where(x => Clean(x[1]) != "").ToList();
                if (l.Count > 0) F.Add(new Field { Tag = t, Ind = ind, Subs = l });
            };
            add("020", "  ", new[] { new[] { "a", i.isbn } });
            add("082", "04", new[] { new[] { "a", i.call } });
            add("100", "1 ", new[] { new[] { "a", i.author } });
            add("245", Clean(i.author) != "" ? "10" : "00", new[] { new[] { "a", i.title }, new[] { "b", i.sub }, new[] { "c", i.author } });
            add("260", "  ", new[] { new[] { "a", i.place }, new[] { "b", i.publisher }, new[] { "c", i.year } });
            add("300", "  ", new[] { new[] { "a", Clean(i.pages) != "" ? i.pages + " p." : "" } });
            add("541", "  ", new[] { new[] { "e", i.acq } });          // 541 $e = accession / acquisition number
            add("650", " 0", new[] { new[] { "a", i.subject } });
            add("852", "  ", new[] { new[] { "p", i.barcode } });
            return F;
        }

        /* ---------- build ---------- */
        public static byte[] RecordBytes(Item i)
        {
            var dirs = new StringBuilder(); var data = new List<byte[]>(); int pos = 0;
            foreach (var f in RecFields(i))
            {
                byte[] b;
                if (f.Ind == null) b = U8.GetBytes(f.V + "\x1e");
                else b = U8.GetBytes(f.Ind + string.Concat(f.Subs.Select(x => "\x1f" + x[0] + Clean(x[1]))) + "\x1e");
                dirs.Append(f.Tag).Append(b.Length.ToString().PadLeft(4, '0')).Append(pos.ToString().PadLeft(5, '0'));
                data.Add(b); pos += b.Length;
            }
            byte[] dir = U8.GetBytes(dirs.ToString() + "\x1e");
            int bas = 24 + dir.Length, len = bas + pos + 1;
            byte[] lead = U8.GetBytes(len.ToString().PadLeft(5, '0') + "nam a22" + bas.ToString().PadLeft(5, '0') + " a 4500");
            var r = new byte[len]; int o = 0;
            Action<byte[]> put = b => { Buffer.BlockCopy(b, 0, r, o, b.Length); o += b.Length; };
            put(lead); put(dir); foreach (var d in data) put(d); put(new byte[] { 0x1D });
            return r;
        }
        public static byte[] ToMrcBytes(IEnumerable<Item> items)
        {
            var ms = new System.IO.MemoryStream();
            foreach (var i in items) { var b = RecordBytes(i); ms.Write(b, 0, b.Length); }
            return ms.ToArray();
        }
        static string X(string s) { return (s ?? "").Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;"); }
        public static string RecordXml(Item i)
        {
            var sb = new StringBuilder("<record>\n<leader>" + LDR + "</leader>\n");
            var lines = RecFields(i).Select(f => f.Ind == null
                ? "<controlfield tag=\"" + f.Tag + "\">" + X(f.V) + "</controlfield>"
                : "<datafield tag=\"" + f.Tag + "\" ind1=\"" + f.Ind[0] + "\" ind2=\"" + f.Ind[1] + "\">" +
                  string.Concat(f.Subs.Select(x => "<subfield code=\"" + x[0] + "\">" + X(Clean(x[1])) + "</subfield>")) + "</datafield>");
            sb.Append(string.Join("\n", lines.ToArray())).Append("\n</record>");
            return sb.ToString();
        }
        public static string ToXml(IEnumerable<Item> items)
        {
            return "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<collection xmlns=\"http://www.loc.gov/MARC21/slim\">\n" +
                string.Join("\n", items.Select(RecordXml).ToArray()) + "\n</collection>\n";
        }
        public static string ToMrk(IEnumerable<Item> items)
        {
            var recs = items.Select(i => "=LDR  " + LDR + "\n" + string.Join("\n", RecFields(i).Select(f => f.Ind == null
                ? "=" + f.Tag + "  " + f.V
                : "=" + f.Tag + "  " + f.Ind.Replace(' ', '\\') + string.Concat(f.Subs.Select(x => "$" + x[0] + Clean(x[1]).Replace("$", "{dollar}")))).ToArray()));
            return string.Join("\n\n", recs.ToArray()) + "\n";
        }
        public static string ToJson(IEnumerable<Item> items)
        {
            var sb = new StringBuilder("[\n"); bool firstRec = true;
            foreach (var i in items)
            {
                if (!firstRec) sb.Append(",\n"); firstRec = false;
                sb.Append(" {\"leader\":").Append(Json.Quote(LDR)).Append(",\"fields\":[");
                bool first = true;
                foreach (var f in RecFields(i))
                {
                    if (!first) sb.Append(","); first = false;
                    if (f.Ind == null) sb.Append("{" + Json.Quote(f.Tag) + ":" + Json.Quote(f.V) + "}");
                    else sb.Append("{" + Json.Quote(f.Tag) + ":{\"ind1\":" + Json.Quote(f.Ind.Substring(0, 1)) + ",\"ind2\":" + Json.Quote(f.Ind.Substring(1, 1)) + ",\"subfields\":[" +
                        string.Join(",", f.Subs.Select(x => "{" + Json.Quote(x[0]) + ":" + Json.Quote(Clean(x[1])) + "}").ToArray()) + "]}}");
                }
                sb.Append("]}");
            }
            return sb.Append("\n]").ToString();
        }
        // human-readable listing (catalogue "MARC" button)
        public static string ToText(Item i)
        {
            var lines = new List<string> { "LDR " + LDR };
            foreach (var f in RecFields(i))
                lines.Add(f.Tag + " " + (f.Ind == null ? f.V : f.Ind.Replace(' ', '#') + " " + string.Join(" ", f.Subs.Select(x => "$" + x[0] + " " + Clean(x[1])).ToArray())));
            return string.Join("\r\n", lines.ToArray());
        }

        /* ---------- character sets ---------- */
        static readonly Dictionary<int, string> M8C = new Dictionary<int, string> {
            {0xE1,"\u0300"},{0xE2,"\u0301"},{0xE3,"\u0302"},{0xE4,"\u0303"},{0xE5,"\u0304"},{0xE6,"\u0306"},{0xE7,"\u0307"},{0xE8,"\u0308"},{0xE9,"\u030C"},{0xEA,"\u030A"},
            {0xEE,"\u030B"},{0xF0,"\u0327"},{0xF1,"\u0328"},{0xF2,"\u0323"},{0xF3,"\u0324"},{0xF4,"\u030A"},{0xF6,"\u0332"} };
        static readonly Dictionary<int, string> M8S = new Dictionary<int, string> {
            {0xA1,"\u0141"},{0xA2,"\u00D8"},{0xA3,"\u0110"},{0xA4,"\u00DE"},{0xA5,"\u00C6"},{0xA6,"\u0152"},{0xB1,"\u0142"},{0xB2,"\u00F8"},{0xB3,"\u0111"},{0xB4,"\u00FE"},
            {0xB5,"\u00E6"},{0xB6,"\u0153"},{0xB8,"\u0131"},{0xB9,"\u00A3"},{0xBA,"\u00F0"},{0xC0,"\u00B0"},{0xC3,"\u00A9"},{0xC5,"\u00BF"},{0xC6,"\u00A1"},{0xC7,"\u00DF"} };
        // Basic-Latin MARC-8 (the common case for older English-language records). Other MARC-8 scripts are not converted.
        public static string Marc8(byte[] b)
        {
            var o = new StringBuilder(); var pend = new StringBuilder();
            for (int k = 0; k < b.Length; k++)
            {
                int c = b[k];
                if (c == 0x1B) { k++; while (k < b.Length && b[k] >= 0x20 && b[k] <= 0x2F) k++; continue; }
                if (M8C.ContainsKey(c)) { pend.Append(M8C[c]); continue; }
                string ch = c < 0x80 ? ((char)c).ToString() : (M8S.ContainsKey(c) ? M8S[c] : "");
                if (ch != "") { o.Append(ch).Append(pend.ToString()); pend.Length = 0; }
            }
            return o.ToString().Normalize(NormalizationForm.FormC);
        }
        static string Decode(byte[] b, int off, int len, bool utf8)
        {
            if (utf8) return Encoding.UTF8.GetString(b, off, len);
            try { return new UTF8Encoding(false, true).GetString(b, off, len); }
            catch { var c = new byte[len]; Buffer.BlockCopy(b, off, c, 0, len); return Marc8(c); }
        }
        static string Ascii(byte[] u, int s, int n)
        {
            var sb = new StringBuilder();
            for (int k = s; k < s + n && k < u.Length; k++) sb.Append((char)u[k]);
            return sb.ToString();
        }

        /* ---------- parse ---------- */
        static Rec ParseOne(byte[] u, int p, int len)
        {
            string leader = Ascii(u, p, 24);
            int bas; if (!int.TryParse(leader.Substring(12, 5), out bas) || !(bas >= 25 && bas <= len)) throw new Exception("bad base address");
            bool utf8 = leader[9] == 'a';
            var rec = new Rec { Leader = leader };
            for (int d = 24; d + 12 <= bas - 1; d += 12)
            {
                string tag = Ascii(u, p + d, 3); int l, s;
                if (!Regex.IsMatch(tag, @"^\d{3}$") || !int.TryParse(Ascii(u, p + d + 3, 4), out l) || !int.TryParse(Ascii(u, p + d + 7, 5), out s)) throw new Exception("bad directory");
                int st = p + bas + s, n = l;
                if (st + n > p + len || n < 0) throw new Exception("field out of range");
                if (n > 0 && u[st + n - 1] == 0x1E) n--;
                if (string.CompareOrdinal(tag, "010") < 0) { rec.Fields.Add(new Field { Tag = tag, V = Decode(u, st, n, utf8) }); continue; }
                var f = new Field { Tag = tag, Ind = n >= 2 ? Ascii(u, st, 2) : "  " };
                int k = 2;
                while (k < n)
                {
                    if (u[st + k] == 0x1F && k + 1 < n)
                    {
                        string code = ((char)u[st + k + 1]).ToString(); int e = k + 2;
                        while (e < n && u[st + e] != 0x1F) e++;
                        f.Subs.Add(new[] { code, Decode(u, st + k + 2, e - (k + 2), utf8) }); k = e;
                    }
                    else k++;
                }
                rec.Fields.Add(f);
            }
            return rec;
        }
        public static ParseResult ParseMrc(byte[] u)
        {
            var res = new ParseResult(); int p = 0, N = u.Length;
            while (p < N)
            {
                while (p < N && (u[p] == 10 || u[p] == 13 || u[p] == 32 || u[p] == 0 || u[p] == 0x1D)) p++;
                if (p >= N) break;
                if (p == 0 && N > 2 && u[0] == 0xEF && u[1] == 0xBB && u[2] == 0xBF) { p = 3; continue; }
                string ls = Ascii(u, p, 5); int len;
                if (!Regex.IsMatch(ls, @"^\d{5}$") || (len = int.Parse(ls)) < 25 || p + len > N || u[p + len - 1] != 0x1D)
                {   // damaged: skip to next record terminator
                    int q = p; while (q < N && u[q] != 0x1D) q++; p = q + 1; res.Bad++; continue;
                }
                try { res.Records.Add(ParseOne(u, p, len)); } catch { res.Bad++; }
                p += len;
            }
            res.Format = "MARC 21 (ISO 2709)";
            return res;
        }
        static string Unent(string s)
        {
            s = Regex.Replace(s, "&#x([0-9a-fA-F]+);", m => char.ConvertFromUtf32(Convert.ToInt32(m.Groups[1].Value, 16)));
            s = Regex.Replace(s, "&#(\\d+);", m => char.ConvertFromUtf32(int.Parse(m.Groups[1].Value)));
            return s.Replace("&lt;", "<").Replace("&gt;", ">").Replace("&quot;", "\"").Replace("&apos;", "'").Replace("&amp;", "&");
        }
        public static ParseResult ParseXml(string text)
        {
            var res = new ParseResult();
            foreach (Match m in Regex.Matches(text, @"<(?:\w+:)?record\b[^>]*>([\s\S]*?)</(?:\w+:)?record>"))
            {
                try
                {
                    string body = m.Groups[1].Value; var fields = new List<Field>();
                    var ld = Regex.Match(body, @"<(?:\w+:)?leader[^>]*>([\s\S]*?)</(?:\w+:)?leader>");
                    foreach (Match c in Regex.Matches(body, @"<(?:\w+:)?controlfield\b([^>]*)>([\s\S]*?)</(?:\w+:)?controlfield>"))
                    {
                        var t = Regex.Match(c.Groups[1].Value, "tag=\"(\\d{3})\"");
                        if (t.Success) fields.Add(new Field { Tag = t.Groups[1].Value, V = Unent(c.Groups[2].Value) });
                    }
                    foreach (Match d in Regex.Matches(body, @"<(?:\w+:)?datafield\b([^>]*)>([\s\S]*?)</(?:\w+:)?datafield>"))
                    {
                        var t = Regex.Match(d.Groups[1].Value, "tag=\"(\\d{3})\""); if (!t.Success) continue;
                        var m1 = Regex.Match(d.Groups[1].Value, "ind1=\"(.?)\""); var m2 = Regex.Match(d.Groups[1].Value, "ind2=\"(.?)\"");
                        string i1 = m1.Success && m1.Groups[1].Value != "" ? m1.Groups[1].Value : " ", i2 = m2.Success && m2.Groups[1].Value != "" ? m2.Groups[1].Value : " ";
                        var f = new Field { Tag = t.Groups[1].Value, Ind = i1 + i2 };
                        foreach (Match s in Regex.Matches(d.Groups[2].Value, @"<(?:\w+:)?subfield\b[^>]*\bcode=""(.)""[^>]*>([\s\S]*?)</(?:\w+:)?subfield>"))
                            f.Subs.Add(new[] { s.Groups[1].Value, Unent(s.Groups[2].Value) });
                        fields.Add(f);
                    }
                    res.Records.Add(new Rec { Leader = ld.Success ? ld.Groups[1].Value : LDR, Fields = fields.OrderBy(f => f.Tag, StringComparer.Ordinal).ToList() });
                }
                catch { res.Bad++; }
            }
            res.Format = "MARCXML";
            return res;
        }
        public static ParseResult ParseMrk(string text)
        {
            var res = new ParseResult();
            foreach (var blk in Regex.Split(text.TrimStart('\ufeff'), @"\r?\n\s*\r?\n"))
            {
                var fields = new List<Field>(); string leader = LDR;
                foreach (var ln in Regex.Split(blk, @"\r?\n"))
                {
                    var m = Regex.Match(ln, @"^=(\w{3})  ?(.*)$"); if (!m.Success) continue;
                    string tag = m.Groups[1].Value, rest = m.Groups[2].Value;
                    if (tag == "LDR") { leader = rest; continue; }
                    if (string.CompareOrdinal(tag, "010") < 0) { fields.Add(new Field { Tag = tag, V = rest }); continue; }
                    var f = new Field { Tag = tag, Ind = (rest.Length >= 2 ? rest.Substring(0, 2) : rest.PadRight(2)).Replace('\\', ' ') };
                    var parts = (rest.Length >= 2 ? rest.Substring(2) : "").Split('$');
                    for (int k = 1; k < parts.Length; k++) { var p = parts[k]; if (p.Length > 0) f.Subs.Add(new[] { p.Substring(0, 1), p.Substring(1).Replace("{dollar}", "$") }); }
                    fields.Add(f);
                }
                if (fields.Count > 0) res.Records.Add(new Rec { Leader = leader, Fields = fields }); else if (blk.Trim() != "") res.Bad++;
            }
            res.Format = "MarcEdit text";
            return res;
        }
        // auto-detect by content
        public static ParseResult ParseAny(byte[] u)
        {
            int k = 0; if (u.Length > 2 && u[0] == 0xEF && u[1] == 0xBB && u[2] == 0xBF) k = 3;
            while (k < u.Length && u[k] <= 32) k++;
            int c = k < u.Length ? u[k] : 0;
            if (c == 0x3C) return ParseXml(Encoding.UTF8.GetString(u));
            if (c == 0x3D) return ParseMrk(Encoding.UTF8.GetString(u));
            return ParseMrc(u);
        }

        /* ---------- MARC record -> catalogue item ---------- */
        static string Tidy(string s)
        {
            s = Regex.Replace(s ?? "", "[\u0080-\u009f]", "");          // non-sorting-character marks used by some catalogues
            s = Regex.Replace(s, @"\s+", " ");
            s = Regex.Replace(s, @"[\s/:;,=]+$", "");
            s = Regex.Replace(s, @"([A-Za-z]{3})\.$", "$1");
            return s.Trim();
        }
        static string Nat(string s)
        {
            s = Regex.Replace(s ?? "", @"\(.*?\)", "");
            s = Regex.Replace(s, @",?\s*\d{4}\??\s*-\s*(\d{4})?\.?\s*$", "");
            s = Regex.Replace(s, @"[\s,]+$", "").Trim();
            if (s.Contains(","))
            {
                var p = s.Split(',').Select(x => x.Trim()).ToArray();
                s = (string.Join(" ", p.Skip(1).ToArray()) + " " + p[0]).Trim();
            }
            return s;
        }
        static string SubOf(Field f, string codes)
        {
            if (f == null || f.Ind == null) return "";
            var s = f.Subs.FirstOrDefault(x => codes.Contains(x[0])); return s != null ? s[1] : "";
        }
        public static Item ToItem(Rec rec)
        {
            var F = rec.Fields;
            Func<string, List<Field>> all = t => F.Where(f => f.Tag == t).ToList();
            Func<string, string, string> one = (t, c) => SubOf(all(t).FirstOrDefault(), c);
            var c1 = all("001").FirstOrDefault(); string ctl = c1 != null ? c1.V : "";
            string author = Nat(new[] { one("100", "a"), one("110", "a"), one("111", "a"), one("700", "a") }.FirstOrDefault(x => x != "") ?? "");
            Field pub = all("264").FirstOrDefault(f => f.Ind != null && f.Ind.Length > 1 && f.Ind[1] == '1') ?? all("260").FirstOrDefault() ?? all("264").FirstOrDefault();
            var subj = all("650").Concat(all("651")).Take(3)
                .Select(f => string.Join("--", f.Subs.Where(x => "avxyz".Contains(x[0])).Select(x => Regex.Replace(x[1], @"[\s.]+$", "")).ToArray()))
                .Where(x => x != "").ToList();
            string isbnRaw = all("020").Select(f => SubOf(f, "a")).FirstOrDefault(x => Regex.IsMatch(x.Replace("-", ""), @"\d{9}[\dXx]")) ?? "";
            var im = Regex.Match(isbnRaw, @"\d[\d-]{8,15}[\dXx]");
            var ym = Regex.Match(SubOf(pub, "c"), @"\d{4}"); var pm = Regex.Match(one("300", "a"), @"\d+");
            bool fic = all("655").Any(f => Regex.IsMatch(string.Join(" ", f.Subs.Select(x => x[1]).ToArray()), "fiction", RegexOptions.IgnoreCase));
            string w;
            if (author != "") w = author.Split(' ').Last();
            else w = Regex.Replace(Tidy(one("245", "a")), @"^(the|a|an)\s+", "", RegexOptions.IgnoreCase).Split(' ')[0];
            w = Regex.Replace(w, "[^A-Za-z]", ""); if (w.Length > 3) w = w.Substring(0, 3); w = w.ToUpperInvariant();
            string d = one("082", "a").Replace("/", "").Trim(), call = "";
            if (Regex.IsMatch(d, @"^\[?fic\]?$", RegexOptions.IgnoreCase)) call = ("F " + w).Trim();
            else if (Regex.IsMatch(d, @"^\[?e\]?$", RegexOptions.IgnoreCase)) call = ("E " + w).Trim();
            else if (Regex.IsMatch(d, @"^[\d.]+$")) call = (Regex.Replace(d, @"\.$", "") + " " + w).Trim();
            else if (d != "") call = d;                                   // already a local call number such as "F WHI" or "523.1 HAW"
            else if (one("852", "h") != "") call = (one("852", "h") + " " + one("852", "i")).Trim();
            else if (one("050", "a") != "") call = (one("050", "a") + " " + one("050", "b")).Trim();
            else if (fic) call = ("F " + w).Trim();
            var it = new Item();
            it.ctl = Clean(ctl); it.acq = Clean(one("541", "e")); it.barcode = Clean(one("852", "p"));
            it.title = Tidy(one("245", "a")); it.sub = Tidy(one("245", "b")); it.author = author;
            it.isbn = im.Success ? im.Value.Replace("-", "").ToUpperInvariant() : "";
            it.publisher = Tidy(SubOf(pub, "b")); it.place = Tidy(SubOf(pub, "a")); it.year = ym.Success ? ym.Value : ""; it.pages = pm.Success ? pm.Value : "";
            it.subject = string.Join("; ", subj.Distinct().ToArray()); it.call = call;
            return it;
        }
    }
}
