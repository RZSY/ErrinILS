// Console self-test: MARC round trips and Z39.50 server <-> client over TCP. Run:  mono selftest.exe
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Errin;

static class SelfTest
{
    static int n;
    static void Ok(bool c, string m) { if (!c) { Console.WriteLine("FAILED: " + m); Environment.Exit(1); } n++; }
    static Item I(string id, string ctl, string acq, string bc, string t, string sub, string a, string isbn, string pub, string place, string yr, string call, string subj, string pages)
    { return new Item { id = id, ctl = ctl, acq = acq, barcode = bc, title = t, sub = sub, author = a, isbn = isbn, publisher = pub, place = place, year = yr, call = call, subject = subj, pages = pages }; }
    static void Same(Item a, Item b, string l) { foreach (var k in Item.Keys) Ok(a.Get(k) == b.Get(k), l + " " + k + " [" + a.Get(k) + "] vs [" + b.Get(k) + "]"); }
    static int Main()
    {
        var items = new List<Item> {
            I("1","ER00000001","ACQ-000001","B1001","Charlotte's Web","","E. B. White","9780064400558","Harper","New York","2001","F WHI","Animals--Fiction","184"),
            I("2","ER00000002","ACQ-000002","B1002","A Brief History of Time","from the Big Bang to black holes","Stephen Hawking","9780553380163","Bantam","","1998","523.1 HAW","Cosmology","212"),
            I("3","ER00000003","","B1003","Hatchet","","Gary Paulsen","9781416936473","Simon & Schuster","","2006","F PAU","Wilderness survival--Fiction","195"),
            I("4","ER00000004","ACQ-000004","B1004","Caf\u00e9 M\u00fcller & S\u00f8ren","","Zo\u00eb \u00c5ngstr\u00f6m","","","","2015","","","") };
        var enc = new UTF8Encoding(false);
        var fmts = new Dictionary<string, byte[]> { { "mrc", Marc.ToMrcBytes(items) }, { "xml", enc.GetBytes(Marc.ToXml(items)) }, { "mrk", enc.GetBytes(Marc.ToMrk(items)) } };
        foreach (var kv in fmts)
        {
            var r = Marc.ParseAny(kv.Value); Ok(r.Records.Count == 4 && r.Bad == 0, kv.Key + " count " + r.Records.Count + "/" + r.Bad);
            for (int k = 0; k < 4; k++) Same(Marc.ToItem(r.Records[k]), items[k], kv.Key + " #" + k);
        }
        Ok(Marc.ToText(items[0]).Contains("541 ## $e ACQ-000001"), "541 shown");
        Ok(Marc.ToXml(items).Contains("<controlfield tag=\"001\">ER00000001</controlfield>"), "001 is control number");
        var g = Marc.ToMrcBytes(items.Take(1)); var junk = enc.GetBytes("00099garbage\x1d");
        var all = g.Concat(junk).Concat(g).ToArray(); var dm = Marc.ParseMrc(all);
        Ok(dm.Records.Count == 2 && dm.Bad == 1, "resync after damaged record");
        Ok(Marc.Marc8(new byte[] { 0x43, 0x61, 0x66, 0xE2, 0x65 }) == "Caf\u00e9", "MARC-8 combining mark");
        var lc = Marc.ParseXml(@"<record><leader>00000cam a2200000 a 4500</leader><controlfield tag=""001"">12345</controlfield>
<datafield tag=""020"" ind1="" "" ind2="" ""><subfield code=""a"">0-547-92822-8 (pbk.)</subfield></datafield>
<datafield tag=""082"" ind1=""0"" ind2=""4""><subfield code=""a"">[Fic]</subfield></datafield>
<datafield tag=""100"" ind1=""1"" ind2="" ""><subfield code=""a"">Tolkien, J. R. R.,</subfield><subfield code=""d"">1892-1973.</subfield></datafield>
<datafield tag=""245"" ind1=""1"" ind2=""4""><subfield code=""a"">The hobbit /</subfield><subfield code=""c"">by J.R.R. Tolkien.</subfield></datafield>
<datafield tag=""264"" ind1="" "" ind2=""1""><subfield code=""a"">Boston :</subfield><subfield code=""b"">Houghton Mifflin,</subfield><subfield code=""c"">2012.</subfield></datafield></record>");
        var li = Marc.ToItem(lc.Records[0]);
        Ok(li.title == "The hobbit" && li.author == "J. R. R. Tolkien" && li.isbn == "0547928228" && li.call == "F TOL" && li.year == "2012" && li.publisher == "Houghton Mifflin",
           "LC record mapped: " + li.title + "|" + li.author + "|" + li.isbn + "|" + li.call + "|" + li.year + "|" + li.publisher);

        // JSON store round trip (compatibility with the earlier data file)
        var db = new Db(); db.Items.AddRange(items); db.Patrons.Add(new Patron { id = "S0001", name = "Aisha \"A\" Rahman", reg = "2026-01-01", last = "2026-01-01" });
        db.Loans.Add(new Loan { id = "l1", iid = "1", pid = "S0001", @out = "2026-01-01", due = "2026-01-08", ren = 1 }); db.Fines.Add(new Fine { id = "f1", pid = "S0001", iid = "1", amt = 0.7, days = 7, paid = "" });
        var db2 = Db.FromJson(db.ToJson());
        Ok(db2.Items.Count == 4 && db2.Patrons[0].name == "Aisha \"A\" Rahman" && db2.Loans[0].ret == null && db2.Loans[0].ren == 1 && Math.Abs(db2.Fines[0].amt - 0.7) < 1e-9, "JSON db round trip");
        var legacy = Db.FromJson("{\"items\":[{\"id\":\"x\",\"title\":\"T\",\"barcode\":\"B1\"}],\"patrons\":[],\"loans\":[{\"id\":\"l\",\"iid\":\"x\",\"pid\":\"p\",\"out\":\"2026-01-01\",\"due\":\"2026-01-08\",\"ret\":null,\"ren\":0}],\"holds\":[],\"fines\":[],\"set\":{\"rate\":0.1,\"maxLoans\":3,\"block\":5}}");
        Ok(legacy.Items[0].title == "T" && legacy.Loans[0].ret == null && legacy.Set.maxLoans == 3, "reads the old Electron data file");


        // Library rules
        var ldb = Lib.Seed(); var lib = new Lib(ldb); string msg; string S1 = ldb.Patrons[0].id;
        Ok(lib.Checkout(S1, "B1001", out msg) == null && msg.StartsWith("Checked out"), "checkout");
        Ok(lib.Checkout(S1, "B1001", out msg) == "Item is already checked out", "double checkout blocked");
        Ok(lib.PlaceHold(S1, "B1001", out msg) == null, "hold on loaned item");
        Ok(lib.Renew("B1001", out msg) == "Cannot renew: item has a hold", "renew blocked by hold");
        ldb.Loans[0].due = Lib.AddD(Lib.Today(), -3);                                // make it 3 days late
        Ok(lib.Renew("B1001", out msg) == "Overdue items cannot be renewed", "overdue cannot renew");
        Ok(lib.Checkout(S1, "B1002", out msg) == "Blocked: student has overdue items", "overdue blocks checkout");
        Ok(lib.Return("B1001", out msg) == null && msg.Contains("fine RM 0.30") && msg.Contains("Hold ready"), "return late: fine + hold ready (" + msg + ")");
        Ok(Math.Abs(lib.Unpaid(S1) - 0.30) < 1e-9, "fine recorded");
        ldb.Set.block = 0.25; Ok(lib.Checkout(S1, "B1002", out msg).StartsWith("Blocked: unpaid fines"), "fines block checkout");
        ldb.Fines[0].paid = "paid"; Ok(lib.Checkout(S1, "B1001", out msg) == null, "held item goes to the hold owner");
        var imp = new List<Item> { new Item { title = "New", isbn = "9780064400558", ctl = "ER00000001" }, new Item { title = "Dup", barcode = "B1002" }, new Item { title = "Fresh", barcode = "Z9" } };
        var dups = lib.DupSet(imp); int sk; int added = lib.AddItems(imp, "skip", true, dups, out sk);
        Ok(added == 1 && sk == 2, "import skips duplicates by barcode or ISBN: " + added + "/" + sk);
        Ok(ldb.Items.Select(i => i.ctl).Distinct().Count() == ldb.Items.Count && ldb.Items.Select(i => i.barcode).Distinct().Count() == ldb.Items.Count, "control numbers and barcodes stay unique");
        ldb.Patrons.Add(new Patron { id = "OLD", name = "Old", reg = "2010-01-01", last = "2010-01-01" }); Ok(lib.Purge() == 1 && lib.PatronById("OLD") == null, "5-year retention purge");
        Ok(Lookup.NormalizeIsbn("0-547-92822-x") == "054792822X" && Lookup.NormalizeIsbn("0547928228") == null && Lookup.NormalizeIsbn("9780064400558") != null && Lookup.NormalizeIsbn("9780064400559") == null, "ISBN validation");
        var zip = new ZipOut(); zip.Add("a.txt", Encoding.UTF8.GetBytes("hello hello hello hello")); zip.Add("b.mrc", Marc.ToMrcBytes(items)); System.IO.File.WriteAllBytes("/tmp/t.zip", zip.Finish());

        // IC numbers, Simplified View wording, admin password
        Ok(Lib.NormIc("060306100261") == "060306-10-0261" && Lib.NormIc("060306-10-0261") == "060306-10-0261" && Lib.NormIc(" 060306 10 0261 ") == "060306-10-0261", "IC normalised with dashes");
        Ok(Lib.NormIc("12345") == null && Lib.NormIc("S0001") == null, "non-IC input rejected");
        Ok(Lib.IcOk("060306-10-0261") && Lib.IcOk("000229-10-0001") && !Lib.IcOk("061306-10-0261") && !Lib.IcOk("060332-10-0261"), "IC birth-date digits checked");
        Ok(S1 == "120315-10-0001" && lib.PatronById("120315100001") == ldb.Patrons[0] && lib.PatronById("120315-10-0001") == ldb.Patrons[0], "scanner without dashes finds the student");
        ldb.Patrons.Add(new Patron { id = "S0042", name = "Legacy", reg = Lib.Today(), last = Lib.Today() }); Ok(lib.PatronById("s0042") != null, "older non-IC IDs still work");
        Ok(Lib.Friendly("Loan limit reached").Contains("most books") && Lib.Friendly("Blocked: unpaid fines RM 5.00").Contains("librarian") && Lib.Friendly("whatever").StartsWith("Oops"), "child-friendly messages");
        Ok(Auth.Verify("zzz", "", "", 0) == false, "default hash rejects a wrong password");
        string salt = Auth.NewSalt(), h1 = Auth.Hash("my new pass", salt, Auth.Iterations);
        Ok(Auth.Verify("my new pass", salt, h1, Auth.Iterations) && !Auth.Verify("my new pasS", salt, h1, Auth.Iterations) && !Auth.Verify("", salt, h1, Auth.Iterations), "changed password verifies, wrong ones fail");
        Ok(Auth.Hash("x", salt, 10) != Auth.Hash("x", Auth.NewSalt(), 10), "salted");
        var sdb = Lib.Seed(); sdb.Set.school = "SK Errin"; Ok(Db.FromJson(sdb.ToJson()).Set.school == "SK Errin", "school name persists");

        // Copy-cataloguing sources: response parsing (HathiTrust JSON with MARC-XML, SRU MARCXML)
        {
            string marc = "<collection xmlns=\"http://www.loc.gov/MARC21/slim\"><record><leader>01234cam a2200301 a 4500</leader><controlfield tag=\"001\">009876543</controlfield>" +
                "<datafield tag=\"020\" ind1=\" \" ind2=\" \"><subfield code=\"a\">9780064400558</subfield></datafield>" +
                "<datafield tag=\"050\" ind1=\" \" ind2=\"4\"><subfield code=\"a\">PZ7.W5815</subfield><subfield code=\"b\">Ch 2001</subfield></datafield>" +
                "<datafield tag=\"100\" ind1=\"1\" ind2=\" \"><subfield code=\"a\">White, E. B.</subfield><subfield code=\"q\">(Elwyn Brooks),</subfield><subfield code=\"d\">1899-1985.</subfield></datafield>" +
                "<datafield tag=\"245\" ind1=\"1\" ind2=\"4\"><subfield code=\"a\">Charlotte's web /</subfield><subfield code=\"c\">by E.B. White.</subfield></datafield>" +
                "<datafield tag=\"260\" ind1=\" \" ind2=\" \"><subfield code=\"a\">New York :</subfield><subfield code=\"b\">HarperCollins,</subfield><subfield code=\"c\">c2001.</subfield></datafield>" +
                "<datafield tag=\"300\" ind1=\" \" ind2=\" \"><subfield code=\"a\">184 p. :</subfield><subfield code=\"b\">ill. ;</subfield><subfield code=\"c\">20 cm.</subfield></datafield>" +
                "<datafield tag=\"650\" ind1=\" \" ind2=\"0\"><subfield code=\"a\">Spiders</subfield><subfield code=\"v\">Fiction.</subfield></datafield></record></collection>";
            string hathi = "{\"records\":{\"009876543\":{\"recordURL\":\"https://catalog.hathitrust.org/Record/009876543\",\"titles\":[\"Charlotte's web\"],\"isbns\":[\"9780064400558\"],\"marc-xml\":" + Json.Quote(marc) + "}},\"items\":[]}";
            var hi = Lookup.ParseHathi(hathi, "9780064400558");
            Ok(hi != null && hi.title == "Charlotte's web" && hi.author == "E. B. White" && hi.publisher == "HarperCollins" && hi.year == "2001" && hi.pages == "184" && hi.subject == "Spiders--Fiction" && hi.call.StartsWith("PZ7.W5815"), "HathiTrust record mapped: " + (hi == null ? "null" : hi.title + "|" + hi.author + "|" + hi.publisher + "|" + hi.year + "|" + hi.pages + "|" + hi.subject + "|" + hi.call));
            Ok(Lookup.ParseHathi("{\"records\":{},\"items\":[]}", "9780064400558") == null, "HathiTrust: no match gives null");
            string sru = "<?xml version=\"1.0\"?><zs:searchRetrieveResponse xmlns:zs=\"http://www.loc.gov/zing/srw/\"><zs:version>1.1</zs:version><zs:numberOfRecords>1</zs:numberOfRecords><zs:records><zs:record><zs:recordSchema>marcxml</zs:recordSchema><zs:recordPacking>xml</zs:recordPacking><zs:recordData>" +
                "<record xmlns=\"http://www.loc.gov/MARC21/slim\" type=\"Bibliographic\"><leader>00000nam a2200000 a 4500</leader><controlfield tag=\"001\">1234567890</controlfield>" +
                "<datafield tag=\"020\" ind1=\" \" ind2=\" \"><subfield code=\"a\">9780547928227</subfield></datafield>" +
                "<datafield tag=\"100\" ind1=\"1\" ind2=\" \"><subfield code=\"a\">Tolkien, J. R. R.</subfield></datafield>" +
                "<datafield tag=\"245\" ind1=\"1\" ind2=\"4\"><subfield code=\"a\">\u0098The\u009c hobbit</subfield><subfield code=\"b\">or there and back again</subfield></datafield>" +
                "<datafield tag=\"264\" ind1=\" \" ind2=\"1\"><subfield code=\"a\">Boston</subfield><subfield code=\"b\">Houghton Mifflin Harcourt</subfield><subfield code=\"c\">2012</subfield></datafield></record>" +
                "</zs:recordData><zs:recordPosition>1</zs:recordPosition></zs:record></zs:records></zs:searchRetrieveResponse>";
            var si = Lookup.ParseSru(sru, "9780547928227");
            Ok(si != null && si.title == "The hobbit" && si.sub == "or there and back again" && si.author == "J. R. R. Tolkien" && si.publisher == "Houghton Mifflin Harcourt" && si.year == "2012" && si.isbn == "9780547928227", "SRU record mapped, hidden sort marks removed: " + (si == null ? "null" : "[" + si.title + "]"));
            Ok(Lookup.ParseSru("<searchRetrieveResponse><numberOfRecords>0</numberOfRecords><records/></searchRetrieveResponse>", "1") == null, "SRU: zero records gives null");
            Ok(Lookup.SourceNames.Length == 5 && Lookup.SourceNames.Contains("HathiTrust") && Lookup.SourceNames.Any(x => x.StartsWith("K10plus")), "five lookup sources listed");
        }

        // Z39.50 wire format must match what real servers (YAZ etc.) accept
        {
            Node pdu; int used; var req = Z.BuildSearch(new Z.SearchOpts { Db = "LCDB", Field = "isbn", Term = "9780064400558" });
            Ok(Ber.TryDec(req, out pdu, out used) && used == req.Length && pdu.Tag == 22, "search request decodes");
            var dbs = Ber.Ctx(pdu, 18); Ok(dbs != null && dbs.Kids.Count == 1 && dbs.Kids[0].Cls == Ber.CTX && dbs.Kids[0].Tag == 105 && Ber.NStr(dbs.Kids[0]) == "LCDB", "database name is context tag [105]");
            var rpn = Ber.Ctx(pdu, 21).Kids[0].Kids[1].Kids[0];   // [21] > [1] type-1 > rpn [0] > AttributesPlusTerm [102]
            Ok(rpn.Tag == 102 && Ber.Ctx(rpn, 44) != null && Ber.Ctx(rpn, 45) != null && Ber.Kid(rpn, Ber.UNI, 16) == null, "attribute list is context tag [44]");
            var el = Ber.Ctx(rpn, 44).Kids[0]; Ok(Ber.NInt(Ber.Ctx(el, 120)) == 1 && Ber.NInt(Ber.Ctx(el, 121)) == 7, "ISBN uses attribute 1=7");
        }

        // Z39.50 server <-> client
        int port = 24210;
        var s = Z.StartServer(port, true, "errin", () => items, m => Console.WriteLine("  srv: " + m));
        Func<string, string, string, Z.SearchResult> q = (f, t, db_) => Z.Search(new Z.SearchOpts { Host = "127.0.0.1", Port = port, Db = db_ ?? "errin", Field = f, Term = t, Max = 10 });
        var r1 = q("isbn", "9780064400558", null); Ok(r1.Count == 1 && r1.Items.Count == 1, "isbn search"); Same(r1.Items[0], items[0], "z isbn");
        r1 = q("isbn", "0553380165", null); Ok(r1.Count == 1 && r1.Items[0].title == "A Brief History of Time", "ISBN-10 matches ISBN-13");
        r1 = q("title", "brief history", null); Ok(r1.Count == 1, "title words");
        r1 = q("author", "paulsen", null); Ok(r1.Count == 1 && r1.Items[0].title == "Hatchet", "author");
        r1 = q("subject", "fiction", null); Ok(r1.Count == 2 && r1.Items.Count == 2, "subject, 2 records via Present");
        r1 = q("any", "cafe muller", null); Ok(r1.Count == 1, "diacritics folded");
        r1 = q("any", "zzzz", null); Ok(r1.Count == 0 && r1.Items.Count == 0, "no hits");
        r1 = q("isbn", "9780064400558", "Default"); Ok(r1.Count == 1, "Default database name accepted");
        try { q("isbn", "9780064400558", "nope"); Ok(false, "should fail"); } catch (Exception e) { Ok(e.Message.Contains("235") || e.Message.Contains("does not exist"), "unknown db rejected: " + e.Message); }
        s.Stop();
        var many = Enumerable.Range(0, 60).Select(k => new Item { id = "m" + k, ctl = "M" + k, barcode = "X" + k, title = "Bulk record " + k, author = "A B", year = "2000" }).ToList();
        var s2 = Z.StartServer(port + 1, true, "errin", () => many, Console.WriteLine);
        var rb = Z.Search(new Z.SearchOpts { Host = "127.0.0.1", Port = port + 1, Db = "errin", Field = "title", Term = "bulk", Max = 50 });
        Ok(rb.Count == 60 && rb.Items.Count == 50, "fetch 50 of 60 across several Present calls (" + rb.Items.Count + ")");
        s2.Stop();
        Console.WriteLine("\nall " + n + " checks passed");
        return 0;
    }
}
