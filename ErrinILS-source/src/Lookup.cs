// Copy cataloguing by ISBN from five free sources: Library of Congress (Z39.50), Open Library, Google Books, HathiTrust and the K10plus union catalogue.
// Needs internet only while looking up. A source that is down or has no record never stops the others.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace Errin
{
    public class LookupResult { public bool Ok; public string Err; public Item Rec; public List<string> Src = new List<string>(); }

    public static class Lookup
    {
        static Lookup()
        {
            try { ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072 | (SecurityProtocolType)768 | (SecurityProtocolType)192; } catch { }  // TLS 1.2 where available
        }
        /// <summary>Display names, in priority order (earlier sources win when two have the same field).</summary>
        public static readonly string[] SourceNames = { "Library of Congress", "Open Library", "Google Books", "HathiTrust", "K10plus (German union catalogue)" };

        public static string NormalizeIsbn(string raw)
        {
            string s = System.Text.RegularExpressions.Regex.Replace(raw ?? "", "[^0-9Xx]", "").ToUpperInvariant();
            if (s.Length == 10)
            {
                int sum = 0; for (int k = 0; k < 10; k++) { int v = s[k] == 'X' ? 10 : (char.IsDigit(s[k]) ? s[k] - '0' : -1); if (v < 0 || (s[k] == 'X' && k != 9)) return null; sum += v * (10 - k); }
                return sum % 11 == 0 ? s : null;
            }
            if (s.Length == 13 && (s.StartsWith("978") || s.StartsWith("979")))
            {
                int sum = 0; for (int k = 0; k < 13; k++) { if (!char.IsDigit(s[k])) return null; sum += (s[k] - '0') * (k % 2 == 1 ? 3 : 1); }
                return sum % 10 == 0 ? s : null;
            }
            return null;
        }
        static string Get(string url)
        {
            var rq = (HttpWebRequest)WebRequest.Create(url); rq.Timeout = 9000; rq.ReadWriteTimeout = 9000; rq.UserAgent = "ErrinILS/1.1 (school library)";
            using (var rs = (HttpWebResponse)rq.GetResponse()) using (var sr = new StreamReader(rs.GetResponseStream(), Encoding.UTF8)) return sr.ReadToEnd();
        }
        static string S(object o) { return o as string ?? ""; }
        static string FirstName(object list) { var l = list as List<object>; if (l == null || l.Count == 0) return ""; var d = l[0] as Dictionary<string, object>; return d != null ? Json.Str(d, "name") : S(l[0]); }

        static Item FromLoc(string isbn)
        {
            var r = Z.Search(new Z.SearchOpts { Host = "lx2.loc.gov", Port = 210, Db = "LCDB", Field = "isbn", Term = isbn, Max = 1, Timeout = 9000 });
            return r.Items.Count > 0 && r.Items[0].title != "" ? r.Items[0] : null;
        }
        static Item FromOpenLibrary(string isbn)
        {
            var root = Json.Parse(Get("https://openlibrary.org/api/books?bibkeys=ISBN:" + isbn + "&format=json&jscmd=data")) as Dictionary<string, object>;
            var b = root != null ? Json.Obj(root, "ISBN:" + isbn) : null; if (b == null || b.Count == 0) return null;
            var it = new Item { title = Json.Str(b, "title"), sub = Json.Str(b, "subtitle"), isbn = isbn, pages = Json.Num(b, "number_of_pages") > 0 ? ((int)Json.Num(b, "number_of_pages")).ToString() : "" };
            it.author = FirstName(b.ContainsKey("authors") ? b["authors"] : null); it.publisher = FirstName(b.ContainsKey("publishers") ? b["publishers"] : null);
            it.place = FirstName(b.ContainsKey("publish_places") ? b["publish_places"] : null);
            var ym = System.Text.RegularExpressions.Regex.Match(Json.Str(b, "publish_date"), @"\d{4}"); it.year = ym.Success ? ym.Value : "";
            var subs = Json.Arr(b, "subjects").Take(3).Select(x => FirstName(new List<object> { x })).Where(x => x != "").ToArray(); it.subject = string.Join("; ", subs);
            return it.title != "" ? it : null;
        }
        static Item FromGoogle(string isbn)
        {
            var root = Json.Parse(Get("https://www.googleapis.com/books/v1/volumes?q=isbn:" + isbn)) as Dictionary<string, object>;
            var items = Json.Arr(root, "items"); if (items.Count == 0) return null;
            var vi = Json.Obj(items[0] as Dictionary<string, object>, "volumeInfo");
            var it = new Item { title = Json.Str(vi, "title"), sub = Json.Str(vi, "subtitle"), isbn = isbn, publisher = Json.Str(vi, "publisher"),
                pages = Json.Num(vi, "pageCount") > 0 ? ((int)Json.Num(vi, "pageCount")).ToString() : "" };
            var au = Json.Arr(vi, "authors"); it.author = au.Count > 0 ? S(au[0]) : "";
            var ym = System.Text.RegularExpressions.Regex.Match(Json.Str(vi, "publishedDate"), @"\d{4}"); it.year = ym.Success ? ym.Value : "";
            it.subject = string.Join("; ", Json.Arr(vi, "categories").Take(3).Select(S).ToArray());
            return it.title != "" ? it : null;
        }

        /// <summary>HathiTrust Bibliographic API ("full" gives MARC-XML for each matching record).</summary>
        public static Item ParseHathi(string json, string isbn)
        {
            var root = Json.Parse(json) as Dictionary<string, object>; if (root == null) return null;
            foreach (var kv in Json.Obj(root, "records"))
            {
                var r = kv.Value as Dictionary<string, object>; string xml = Json.Str(r, "marc-xml"); if (xml == "") continue;
                var p = Marc.ParseXml(xml); if (p.Records.Count == 0) continue;
                var it = Marc.ToItem(p.Records[0]); it.isbn = isbn; if (it.title != "") return it;
            }
            return null;
        }
        /// <summary>An SRU searchRetrieve response whose records are MARCXML (K10plus, DNB, Library of Congress SRU...).</summary>
        public static Item ParseSru(string xml, string isbn)
        {
            var recs = new List<Rec>();
            foreach (Match m in Regex.Matches(xml, @"<(?:\w+:)?recordData[^>]*>([\s\S]*?)</(?:\w+:)?recordData>")) recs.AddRange(Marc.ParseXml(m.Groups[1].Value).Records);
            if (recs.Count == 0) return null;
            var it = Marc.ToItem(recs[0]); it.isbn = isbn; return it.title != "" ? it : null;
        }
        static Item FromHathi(string isbn) { return ParseHathi(Get("https://catalog.hathitrust.org/api/volumes/full/isbn/" + isbn + ".json"), isbn); }
        static Item FromK10plus(string isbn)
        {
            return ParseSru(Get("https://sru.k10plus.de/opac-de-627?version=1.1&operation=searchRetrieve&query=pica.isb%3D" + isbn + "&maximumRecords=1&recordSchema=marcxml"), isbn);
        }

        /// <summary>Blocking; call from a background thread. only = -1 for all sources, or the index of one entry in SourceNames.</summary>
        public static LookupResult Find(string raw, int only = -1)
        {
            var res = new LookupResult(); string isbn = NormalizeIsbn(raw);
            if (isbn == null) { res.Err = "invalid"; return res; }
            Func<string, Item>[] fn = { FromLoc, FromOpenLibrary, FromGoogle, FromHathi, FromK10plus };
            var use = Enumerable.Range(0, fn.Length).Where(k => only < 0 || k == only).ToList();
            var found = new Item[fn.Length]; var failed = new bool[fn.Length]; var th = new List<Thread>();
            foreach (int k in use)
            {
                int j = k; var t = new Thread(() => { try { found[j] = fn[j](isbn); } catch { failed[j] = true; } }) { IsBackground = true }; t.Start(); th.Add(t);
            }
            foreach (var t in th) t.Join(14000);
            var rec = new Item { isbn = isbn };
            foreach (int k in use)
            {
                if (found[k] == null) continue;
                res.Src.Add(SourceNames[k]);
                foreach (var key in new[] { "title", "sub", "author", "publisher", "place", "year", "pages", "subject", "call" })
                    if (rec.Get(key) == "" && found[k].Get(key) != "") rec.Set(key, found[k].Get(key));
            }
            if (res.Src.Count > 0) { res.Ok = true; res.Rec = rec; return res; }
            res.Err = use.All(k => failed[k]) ? "offline" : "notfound"; return res;
        }
    }
}
