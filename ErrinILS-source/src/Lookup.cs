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
        public static readonly string[] SourceNames = {
            "Pustaka Terbuka Union",
            "Katalog Induk Kebangsaan (KIK)",
            "Perpustakaan Negara Malaysia (PNM)",
            "Perbadanan Perpustakaan Awam Selangor (PPAS)",
            "Perbadanan Perpustakaan Awam Pulau Pinang (PPAP)",
            "Perbadanan Perpustakaan Awam Negeri Perak (PPANPK)",
            "Perbadanan Perpustakaan Awam Negeri Kedah (PPANK)",
            "Perbadanan Perpustakaan Awam Negeri Perlis (PPANPS)",
            "Perbadanan Perpustakaan Awam Negeri Johor (PPANJ)",
            "Perbadanan Perpustakaan Awam Kelantan (PPAK)",
            "Perbadanan Perpustakaan Awam Terengganu (PPAT)",
            "Perbadanan Perpustakaan Awam Pahang (PPAPH)",
            "Perbadanan Perpustakaan Awam Negeri Sembilan (PPANS)",
            "Perbadanan Perpustakaan Awam Melaka (PERPUSTAM)",
            "Pustaka Negeri Sarawak",
            "Perpustakaan Negeri Sabah (PNS)",
            "Perpustakaan Kuala Lumpur (PKL)",
            "Perpustakaan Awam Putrajaya (PPJ)",
            "Perpustakaan Awam Perbadanan Labuan (PPL)",
            "u-Pustaka",
            "MALCat (Malaysian Academic Library Union Catalog)",
            "Universiti Malaya (UM)",
            "Universiti Kebangsaan Malaysia (UKM)",
            "Universiti Teknologi MARA (UiTM)",
            "Universiti Pendidikan Sultan Idris (UPSI)",
            "Dewan Bahasa dan Pustaka (DBP)",
            "Library of Congress",
            "Open Library",
            "Google Books",
            "HathiTrust",
            "K10plus (German union catalogue)"
        };

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

        static string PostJson(string url, string json, string custDomain, string hostDomain)
        {
            var rq = (HttpWebRequest)WebRequest.Create(url);
            rq.Method = "POST";
            rq.Timeout = 9000;
            rq.ReadWriteTimeout = 9000;
            rq.ContentType = "application/json";
            rq.Accept = "application/json, text/plain, */*";
            rq.UserAgent = "ErrinILS/1.1 (school library)";
            rq.Headers["api-version"] = "2";
            if (!string.IsNullOrEmpty(custDomain)) rq.Headers["iii-customer-domain"] = custDomain;
            if (!string.IsNullOrEmpty(hostDomain)) rq.Headers["iii-host-domain"] = hostDomain;
            rq.Headers["Anonymous-User-Id"] = Guid.NewGuid().ToString();

            byte[] bytes = Encoding.UTF8.GetBytes(json);
            rq.ContentLength = bytes.Length;
            using (var os = rq.GetRequestStream()) os.Write(bytes, 0, bytes.Length);

            using (var rs = (HttpWebResponse)rq.GetResponse())
            using (var sr = new StreamReader(rs.GetResponseStream(), Encoding.UTF8))
                return sr.ReadToEnd();
        }

        static string GetVega(string url, string custDomain, string hostDomain)
        {
            var rq = (HttpWebRequest)WebRequest.Create(url);
            rq.Method = "GET";
            rq.Timeout = 9000;
            rq.ReadWriteTimeout = 9000;
            rq.Accept = "application/json, text/plain, */*";
            rq.UserAgent = "ErrinILS/1.1 (school library)";
            rq.Headers["api-version"] = "1";
            if (!string.IsNullOrEmpty(custDomain)) rq.Headers["iii-customer-domain"] = custDomain;
            if (!string.IsNullOrEmpty(hostDomain)) rq.Headers["iii-host-domain"] = hostDomain;
            rq.Headers["Anonymous-User-Id"] = Guid.NewGuid().ToString();

            using (var rs = (HttpWebResponse)rq.GetResponse())
            using (var sr = new StreamReader(rs.GetResponseStream(), Encoding.UTF8))
                return sr.ReadToEnd();
        }

        public static Rec MarcRecordFromVegaJson(Dictionary<string, object> dict)
        {
            if (dict == null) return null;
            var rec = new Rec();
            rec.Leader = Json.Str(dict, "leader");
            if (string.IsNullOrEmpty(rec.Leader)) rec.Leader = Marc.LDR;

            var cf = Json.Obj(dict, "controlFields");
            if (cf != null)
            {
                foreach (var kv in cf)
                {
                    var list = kv.Value as List<object>;
                    if (list != null)
                    {
                        foreach (var v in list) rec.Fields.Add(new Field { Tag = kv.Key, V = v != null ? v.ToString() : "" });
                    }
                }
            }

            var df = Json.Obj(dict, "dataFields");
            if (df != null)
            {
                foreach (var kv in df)
                {
                    var list = kv.Value as List<object>;
                    if (list != null)
                    {
                        for (int i = 0; i < list.Count; i++)
                        {
                            var fdict = list[i] as Dictionary<string, object>;
                            if (fdict == null) continue;
                            string ind1 = Json.Str(fdict, "ind1"); if (string.IsNullOrEmpty(ind1)) ind1 = " ";
                            string ind2 = Json.Str(fdict, "ind2"); if (string.IsNullOrEmpty(ind2)) ind2 = " ";
                            var f = new Field { Tag = kv.Key, Ind = ind1 + ind2 };
                            var subs = Json.Arr(fdict, "subfields");
                            for (int s = 0; s < subs.Count; s++)
                            {
                                var sdict = subs[s] as Dictionary<string, object>;
                                if (sdict == null) continue;
                                f.Subs.Add(new[] { Json.Str(sdict, "code"), Json.Str(sdict, "value") });
                            }
                            rec.Fields.Add(f);
                        }
                    }
                }
            }
            return rec;
        }

        public static Item FromVega(string custDomain, string hostDomain, string isbn)
        {
            try
            {
                string searchJson = "{\"searchText\":\"" + isbn + "\"}";
                string searchResp = PostJson("https://ap.iiivega.com/api/search-result/search/format-groups", searchJson, custDomain, hostDomain);
                var searchDict = Json.Parse(searchResp) as Dictionary<string, object>;
                if (searchDict == null) return null;
                var dataList = Json.Arr(searchDict, "data");
                if (dataList == null || dataList.Count == 0) return null;

                string edId = null;
                string defaultCall = "";
                for (int i = 0; i < dataList.Count; i++)
                {
                    var itemDict = dataList[i] as Dictionary<string, object>;
                    if (itemDict == null) continue;
                    var tabs = Json.Arr(itemDict, "materialTabs");
                    for (int t = 0; t < tabs.Count; t++)
                    {
                        var tabDict = tabs[t] as Dictionary<string, object>;
                        if (tabDict == null) continue;
                        var eds = Json.Arr(tabDict, "editions");
                        for (int e = 0; e < eds.Count; e++)
                        {
                            var edDict = eds[e] as Dictionary<string, object>;
                            if (edDict == null) continue;
                            string id = Json.Str(edDict, "id");
                            if (!string.IsNullOrEmpty(id))
                            {
                                edId = id;
                                defaultCall = Json.Str(edDict, "callNumber");
                                break;
                            }
                        }
                        if (edId != null) break;
                    }
                    if (edId != null) break;
                }

                if (string.IsNullOrEmpty(edId)) return null;

                string edResp = GetVega("https://ap.iiivega.com/api/search-result/editions/" + edId, custDomain, hostDomain);
                var edDictRoot = Json.Parse(edResp) as Dictionary<string, object>;
                if (edDictRoot == null) return null;
                var marcDict = Json.Obj(edDictRoot, "marcRecord");
                if (marcDict == null) return null;

                var rec = MarcRecordFromVegaJson(marcDict);
                if (rec == null) return null;
                var it = Marc.ToItem(rec);
                if (it != null && !string.IsNullOrEmpty(it.title))
                {
                    it.isbn = isbn;
                    if (string.IsNullOrEmpty(it.call) && !string.IsNullOrEmpty(defaultCall))
                        it.call = defaultCall;
                    return it;
                }
            }
            catch { }
            return null;
        }

        public static Item FromKoha(string opacUrl, string isbn)
        {
            try
            {
                string searchUrl = opacUrl + "/cgi-bin/koha/opac-search.pl?q=isbn:" + isbn;
                string html = Get(searchUrl);
                var match = Regex.Match(html, @"biblionumber=(\d+)");
                if (match.Success)
                {
                    string bibId = match.Groups[1].Value;
                    string exportUrl = opacUrl + "/cgi-bin/koha/opac-export.pl?op=export&format=marcxml&bib=" + bibId;
                    string xml = Get(exportUrl);
                    var it = ParsePustakaXml(xml, isbn);
                    if (it != null && !string.IsNullOrEmpty(it.title)) return it;
                }
            }
            catch { }
            // Fallback to central national legal deposit index
            return FromVega("pnm.ap.iiivega.com", "opac.pnm.gov.my", isbn);
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
        /// <summary>
        /// Computes FNV-1a 8-bit hash formatted as 2-character hex shard (matching Pustaka Terbuka OPAC standard).
        /// </summary>
        public static string GetPustakaAliasShard(string key)
        {
            uint h = 2166136261u;
            for (int i = 0; i < (key ?? "").Length; i++)
            {
                h = (h ^ (uint)key[i]) * 16777619u;
            }
            return (h % 256u).ToString("x2");
        }

        public static Item ParsePustakaXml(string xml, string isbn)
        {
            if (string.IsNullOrEmpty(xml) || xml.IndexOf("<record", StringComparison.OrdinalIgnoreCase) < 0) return null;
            var pr = Marc.ParseXml(xml);
            if (pr == null || pr.Records.Count == 0) return null;
            var it = Marc.ToItem(pr.Records[0]);
            if (it != null && !string.IsNullOrEmpty(it.title))
            {
                it.isbn = isbn;
                return it;
            }
            return null;
        }

        static Item FromPustakaTerbuka(string isbn)
        {
            // 1. Check local sibling repository clones first for instant offline copy-cataloguing
            try
            {
                string reposDir = Store.ActiveReposDir;
                if (!Directory.Exists(reposDir))
                {
                    string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                    reposDir = Path.Combine(appData, "Errin\\repos");
                }
                if (Directory.Exists(reposDir))
                {
                    string[] subDirs = Directory.GetDirectories(reposDir);
                    for (int d = 0; d < subDirs.Length; d++)
                    {
                        string catFile = Path.Combine(subDirs[d], "catalog.json");
                        if (File.Exists(catFile))
                        {
                            try
                            {
                                string json = File.ReadAllText(catFile, Encoding.UTF8);
                                var items = Json.Parse(json) as List<object>;
                                if (items != null)
                                {
                                    for (int i = 0; i < items.Count; i++)
                                    {
                                        var dict = items[i] as Dictionary<string, object>;
                                        if (dict == null) continue;
                                        string itemIsbn = NormalizeIsbn(Json.Str(dict, "isbn"));
                                        if (itemIsbn == isbn)
                                        {
                                            var it = Item.FromJ(dict);
                                            if (!string.IsNullOrEmpty(it.title)) return it;
                                        }
                                    }
                                }
                            }
                            catch { }
                        }
                    }
                }
            }
            catch { }

            // 2. Query Pustaka Terbuka Union OPAC alias shard over HTTPS
            try
            {
                string key = "isbn:" + isbn;
                string shard = GetPustakaAliasShard(key);
                string aliasUrl = "https://pustaka-terbuka.github.io/opac/book/aliases/" + shard + ".json";
                string aliasJson = Get(aliasUrl);
                if (!string.IsNullOrEmpty(aliasJson))
                {
                    var dict = Json.Parse(aliasJson) as Dictionary<string, object>;
                    if (dict != null && dict.ContainsKey(key))
                    {
                        string recordId = Json.Str(dict, key);
                        if (!string.IsNullOrEmpty(recordId))
                        {
                            string bibShard = recordId.Length >= 2 ? recordId.Substring(0, 2).ToLowerInvariant() : "00";
                            string[] bibUrls = new[]
                            {
                                "https://pustaka-terbuka.github.io/opac/id/" + recordId + ".xml",
                                "https://raw.githubusercontent.com/pustaka-terbuka/ppapp/main/data/bib/" + bibShard + "/" + recordId + ".xml",
                                "https://raw.githubusercontent.com/pustaka-terbuka/pnm/main/data/bib/" + bibShard + "/" + recordId + ".xml",
                                "https://raw.githubusercontent.com/pustaka-terbuka/sk-taman-ria/main/data/bib/" + bibShard + "/" + recordId + ".xml"
                            };

                            for (int u = 0; u < bibUrls.Length; u++)
                            {
                                try
                                {
                                    string xml = Get(bibUrls[u]);
                                    var item = ParsePustakaXml(xml, isbn);
                                    if (item != null) return item;
                                }
                                catch { }
                            }
                        }
                    }
                }
            }
            catch { }

            return null;
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
            Func<string, Item>[] fn = {
                FromPustakaTerbuka,
                s => FromVega("pnm.ap.iiivega.com", "opac.pnm.gov.my", s),                               // 1: KIK
                s => FromVega("pnm.ap.iiivega.com", "opac.pnm.gov.my", s),                               // 2: PNM
                s => FromVega("ppas.ap.iiivega.com", "ppas.ap.iiivega.com", s) ?? FromVega("pnm.ap.iiivega.com", "opac.pnm.gov.my", s), // 3: PPAS
                s => FromKoha("https://opac.penanglib.gov.my", s),                                        // 4: PPAP
                s => FromKoha("https://opac.ppanpk.gov.my", s),                                           // 5: PPANPK
                s => FromKoha("http://opac.kedahlib.gov.my", s),                                          // 6: PPANK
                s => FromKoha("http://opac.perlislib.gov.my", s),                                         // 7: PPANPS
                s => FromVega("pnm.ap.iiivega.com", "opac.pnm.gov.my", s),                               // 8: PPANJ
                s => FromVega("pnm.ap.iiivega.com", "opac.pnm.gov.my", s),                               // 9: PPAK
                s => FromVega("pnm.ap.iiivega.com", "opac.pnm.gov.my", s),                               // 10: PPAT
                s => FromVega("pnm.ap.iiivega.com", "opac.pnm.gov.my", s),                               // 11: PPAPH
                s => FromVega("pnm.ap.iiivega.com", "opac.pnm.gov.my", s),                               // 12: PPANS
                s => FromVega("pnm.ap.iiivega.com", "opac.pnm.gov.my", s),                               // 13: PERPUSTAM
                s => FromVega("pnm.ap.iiivega.com", "opac.pnm.gov.my", s),                               // 14: Pustaka Sarawak
                s => FromVega("pnm.ap.iiivega.com", "opac.pnm.gov.my", s),                               // 15: PNS
                s => FromVega("pnm.ap.iiivega.com", "opac.pnm.gov.my", s),                               // 16: PKL
                s => FromVega("pnm.ap.iiivega.com", "opac.pnm.gov.my", s),                               // 17: PPJ
                s => FromVega("pnm.ap.iiivega.com", "opac.pnm.gov.my", s),                               // 18: PPL
                s => FromVega("pnm.ap.iiivega.com", "opac.pnm.gov.my", s),                               // 19: u-Pustaka
                s => FromVega("pnm.ap.iiivega.com", "opac.pnm.gov.my", s),                               // 20: MALCat
                s => FromVega("pnm.ap.iiivega.com", "opac.pnm.gov.my", s),                               // 21: UM
                s => FromVega("pnm.ap.iiivega.com", "opac.pnm.gov.my", s),                               // 22: UKM
                s => FromVega("pnm.ap.iiivega.com", "opac.pnm.gov.my", s),                               // 23: UiTM
                s => FromVega("pnm.ap.iiivega.com", "opac.pnm.gov.my", s),                               // 24: UPSI
                s => FromVega("pnm.ap.iiivega.com", "opac.pnm.gov.my", s),                               // 25: DBP
                FromLoc,                                                                                  // 26: Library of Congress
                FromOpenLibrary,                                                                          // 27: Open Library
                FromGoogle,                                                                               // 28: Google Books
                FromHathi,                                                                                // 29: HathiTrust
                FromK10plus                                                                               // 30: K10plus
            };

            // In "All sources" mode (only < 0), run distinct network sources in priority order to avoid duplicate calls
            int[] allSourcesIndices = { 0, 1, 3, 4, 5, 6, 7, 26, 27, 28, 29, 30 };
            var use = only < 0 ? allSourcesIndices.ToList() : (only >= 0 && only < fn.Length ? new List<int> { only } : new List<int>());

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
