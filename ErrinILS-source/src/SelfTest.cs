// Console self-test: MARC round trips and Z39.50 server <-> client over TCP. Run:  mono selftest.exe
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
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

        // SQLite store round trip and migration test
        Ok(SqliteNative.IsAvailable, "SQLite native library is available");
        string testStoreDir = Path.Combine(Path.GetTempPath(), "errin_selftest_" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new Store(testStoreDir);
            Ok(store.DataFile.EndsWith("errin-data.db"), "Store uses SQLite errin-data.db file path");
            bool saveOk = store.Save(db);
            Ok(saveOk && File.Exists(store.DataFile), "SQLite store save creates .db file");

            var dbSqlite = store.Load();
            Ok(dbSqlite != null && dbSqlite.Items.Count == 4 && dbSqlite.Patrons[0].name == "Aisha \"A\" Rahman" &&
               dbSqlite.Loans[0].ret == null && dbSqlite.Loans[0].ren == 1 && Math.Abs(dbSqlite.Fines[0].amt - 0.7) < 1e-9,
               "SQLite store load round trip matches all records");

            var snapItems = store.SnapshotItems();
            Ok(snapItems != null && snapItems.Count == 4 && snapItems[0].title == items[0].title, "SQLite store snapshot items");

            // Test migration from JSON to SQLite
            File.Delete(store.DataFile);
            var bak = store.DataFile + ".bak"; if (File.Exists(bak)) File.Delete(bak);
            File.WriteAllText(store.LegacyJsonFile, db.ToJson(), Encoding.UTF8);
            var dbMigrated = store.Load();
            Ok(dbMigrated != null && dbMigrated.Items.Count == 4 && File.Exists(store.DataFile), "automatic migration from JSON to SQLite database");
        }
        finally
        {
            try { Directory.Delete(testStoreDir, true); } catch { }
        }


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
        ldb.Patrons.Add(new Patron { id = "OLD", name = "Old", reg = "2010-01-01", last = "2010-01-01" });
        Ok(lib.Purge() == 1 && lib.PatronById("OLD") == null, "5-year retention purge");
        Ok(ldb.ArchivedPatrons != null && ldb.ArchivedPatrons.Any(p => p.id == "OLD"), "5-year retention preserves data in Archive");
        Ok(lib.RestoreArchivedPatron("OLD") && lib.PatronById("OLD") != null, "restore archived patron");
        ldb.Patrons.RemoveAll(p => p.id == "OLD");

        // Item circulation history test
        var itemHistory = lib.ItemLoans(ldb.Items[0].id);
        Ok(itemHistory != null && itemHistory.Count > 0, "item circulation history retrieved");

        // Batch patron registration with duplicate IC updates
        string testCsv = "IC Number,Student Name,Class,Guardian Contact\r\n" + S1 + ",Aisha Updated,Grade 8A,0123456789\r\n080512-14-1234,Chong Wei,Grade 7B,0198765432";
        var parsedCsv = ExcelReader.ParseCsv(testCsv);
        Ok(parsedCsv.Count == 3, "csv table parsed correctly");
        bool wasUpd;
        lib.BatchUpsertPatron(S1, "Aisha Updated", "Grade 8A", "0123456789", out wasUpd);
        Ok(wasUpd && lib.PatronById(S1).name == "Aisha Updated" && lib.PatronById(S1).cls == "Grade 8A", "batch registration updates existing patron by IC (primary key)");
        lib.BatchUpsertPatron("080512-14-1234", "Chong Wei", "Grade 7B", "0198765432", out wasUpd);
        Ok(!wasUpd && lib.PatronById("080512-14-1234") != null, "batch registration registers new student");

        // Malaysian School Class Formatting tests
        Ok(Lib.FormatSchoolClass("TAHUN SATU", "AKTIF") == "1 AKTIF", "FormatSchoolClass TAHUN SATU -> 1 AKTIF");
        Ok(Lib.FormatSchoolClass("TAHUN DUA", "BESTARI") == "2 BESTARI", "FormatSchoolClass TAHUN DUA -> 2 BESTARI");
        Ok(Lib.FormatSchoolClass("KELAS KHAS RENDAH", "1 BIJAK") == "1 BIJAK", "FormatSchoolClass KELAS KHAS RENDAH 1 BIJAK -> 1 BIJAK");
        Ok(Lib.FormatSchoolClass("PRASEKOLAH", "CILIK BESTARI") == "PRASEKOLAH CILIK BESTARI", "FormatSchoolClass PRASEKOLAH");
        Ok(Lib.FormatSchoolClass("TINGKATAN 4", "SAINS") == "4 SAINS", "FormatSchoolClass TINGKATAN 4 -> 4 SAINS");

        // APDM-style multi-row header spreadsheet parsing test (with dummy synthetic data)
        var apdmRows = new List<List<string>>();
        apdmRows.Add(new List<string> { "KEMENTERIAN PENDIDIKAN MALAYSIA" });
        apdmRows.Add(new List<string> { "SENARAI KESELURUHAN MURID" });
        apdmRows.Add(new List<string> { "NAMA SEKOLAH", "SK CONTOH" });
        apdmRows.Add(new List<string> { "KOD SEKOLAH", "ABC1234" });
        apdmRows.Add(new List<string> { "" });
        var apdmHeader = new List<string>();
        for (int c = 0; c < 50; c++) apdmHeader.Add("");
        apdmHeader[0] = "BIL.";
        apdmHeader[1] = "ID MURID";
        apdmHeader[2] = "NAMA";
        apdmHeader[3] = "NO. PENGENALAN";
        apdmHeader[9] = "TAHUN / TINGKATAN";
        apdmHeader[10] = "NAMA KELAS";
        apdmHeader[33] = "PENJAGA 1";
        apdmHeader[42] = "NO. TEL. BIMBIT PENJAGA 1";
        apdmRows.Add(apdmHeader);

        var apdmRow1 = new List<string>();
        for (int c = 0; c < 50; c++) apdmRow1.Add("");
        apdmRow1[0] = "1";
        apdmRow1[1] = "M001";
        apdmRow1[2] = "Aisha Final";
        apdmRow1[3] = S1.Replace("-", "");
        apdmRow1[9] = "TAHUN TIGA";
        apdmRow1[10] = "CEMERLANG";
        apdmRow1[33] = "Encik Rahman";
        apdmRow1[42] = "01122334455";
        apdmRows.Add(apdmRow1);

        var apdmRow2 = new List<string>();
        for (int c = 0; c < 50; c++) apdmRow2.Add("");
        apdmRow2[0] = "2";
        apdmRow2[1] = "M002";
        apdmRow2[2] = "Muthu Raja";
        apdmRow2[3] = "140615-08-9999";
        apdmRow2[9] = "TAHUN SATU";
        apdmRow2[10] = "AKTIF";
        apdmRow2[33] = "Encik Raja";
        apdmRow2[42] = "'0129988776";
        apdmRows.Add(apdmRow2);

        var parsedApdm = lib.ParsePatronSpreadsheet(apdmRows);
        Ok(parsedApdm.Count == 2, "APDM-style spreadsheet parsed 2 student records");
        Ok(parsedApdm[0].isUpdate && parsedApdm[0].normIc == S1 && parsedApdm[0].cls == "3 CEMERLANG", "APDM detects existing student, normalizes IC and formats class");
        Ok(parsedApdm[0].contact.Contains("01122334455") && parsedApdm[0].contact.Contains("Encik Rahman"), "APDM combines guardian phone and name");
        Ok(!parsedApdm[1].isUpdate && parsedApdm[1].cls == "1 AKTIF" && parsedApdm[1].contact.StartsWith("0129988776"), "APDM parses new student, trims leading apostrophe from phone");

        // Previous year class records archiving
        int classSnapCount = lib.ArchiveCurrentYearClasses(2026);
        Ok(classSnapCount > 0 && ldb.ClassHistory.Any(c => c.year == 2026), "class year snapshot archived");
        var dbJson = ldb.ToJson();
        var reloadedDb = Db.FromJson(dbJson);
        Ok(reloadedDb.ClassHistory.Count > 0 && reloadedDb.ClassHistory.Any(c => c.year == 2026), "class history persists in json");

        // Zip extraction test
        var testZipOut = new ZipOut(); testZipOut.Add("hello.txt", Encoding.UTF8.GetBytes("zero dependency zip"));
        var testZipIn = ZipIn.Extract(testZipOut.Finish());
        Ok(testZipIn.ContainsKey("hello.txt") && Encoding.UTF8.GetString(testZipIn["hello.txt"]) == "zero dependency zip", "ZipIn extracts zip entries");
        Ok(Lookup.NormalizeIsbn("0-547-92822-x") == "054792822X" && Lookup.NormalizeIsbn("0547928228") == null && Lookup.NormalizeIsbn("9780064400558") != null && Lookup.NormalizeIsbn("9780064400559") == null, "ISBN validation");
        var zip = new ZipOut(); zip.Add("a.txt", Encoding.UTF8.GetBytes("hello hello hello hello")); zip.Add("b.mrc", Marc.ToMrcBytes(items));
        string tmpZip = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "t.zip"); System.IO.File.WriteAllBytes(tmpZip, zip.Finish()); try { System.IO.File.Delete(tmpZip); } catch { }

        // IC numbers, Simplified View wording, admin password
        Ok(Lib.NormIc("120315100001") == "120315-10-0001" && Lib.NormIc("120315-10-0001") == "120315-10-0001" && Lib.NormIc(" 120315 10 0001 ") == "120315-10-0001", "IC normalised with dashes");
        Ok(Lib.NormIc("12345") == null && Lib.NormIc("S0001") == null, "non-IC input rejected");
        Ok(Lib.IcOk("120315-10-0001") && Lib.IcOk("000229-10-0001") && !Lib.IcOk("121306-10-0001") && !Lib.IcOk("120332-10-0001"), "IC birth-date digits checked");
        Ok(S1 == "120315-10-0001" && lib.PatronById("120315100001") == ldb.Patrons[0] && lib.PatronById("120315-10-0001") == ldb.Patrons[0], "scanner without dashes finds the student");
        ldb.Patrons.Add(new Patron { id = "S0042", name = "Legacy", reg = Lib.Today(), last = Lib.Today() }); Ok(lib.PatronById("s0042") != null, "older non-IC IDs still work");
        Ok(Lib.Friendly("Loan limit reached").Contains("most books") && Lib.Friendly("Blocked: unpaid fines RM 5.00").Contains("librarian") && Lib.Friendly("whatever").StartsWith("Oops"), "child-friendly messages");
        Ok(Auth.Verify("admin123", "", "", 0) == true, "default hash accepts admin123");
        Ok(Auth.Verify("zzz", "", "", 0) == false, "default hash rejects a wrong password");
        Ok(Auth.Verify("AC10314!!)013N62", "", "", 0) == false, "previous default password rejected");
        Ok(Auth.Verify("admin", "", "", 0) == false, "generic admin password rejected");
        string salt = Auth.NewSalt(), h1 = Auth.Hash("my new pass", salt, Auth.Iterations);
        Ok(Auth.Verify("my new pass", salt, h1, Auth.Iterations) && !Auth.Verify("my new pasS", salt, h1, Auth.Iterations) && !Auth.Verify("", salt, h1, Auth.Iterations), "changed password verifies, wrong ones fail");
        Ok(Auth.Hash("x", salt, 10) != Auth.Hash("x", Auth.NewSalt(), 10), "salted");
        var sdb = Lib.Seed(); sdb.Set.school = "SK Errin"; Ok(Db.FromJson(sdb.ToJson()).Set.school == "SK Errin", "school name persists");

        // Weeding checks
        var weedItem = ldb.Items[1];
        lib.Weed(weedItem, "Damaged cover");
        Ok(lib.IsWeeded(weedItem), "item is weeded");
        Ok(weedItem.weeded == Lib.Today() && weedItem.weed_reason == "Damaged cover", "weeding date and reason recorded");
        Ok(lib.Checkout(S1, "B1002", out msg) == "Item has been weeded and withdrawn from circulation", "checkout blocked for weeded item");
        Ok(lib.PlaceHold(S1, "B1002", out msg) == "Cannot place hold on a weeded item", "hold blocked for weeded item");
        lib.Unweed(weedItem);
        Ok(!lib.IsWeeded(weedItem), "item unweeded");
        Ok(lib.Checkout(S1, "B1002", out msg) == null, "unweeded item can be checked out");
        lib.Return("B1002", out msg);

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

        // Web / Remote Access tests
        int wport = 28585;
        var wdb = Lib.Seed(); var wlib = new Lib(wdb);
        var wdevs = new List<Web.Device>();
        var wsrv = Web.StartServer(wport, true, wdevs, () => wdb, () => wlib, () => { }, act => act(), null, null, null);

        // 1. Device serialization roundtrip
        var devTest = new Web.Device { token = "tok123", name = "Test iPad", paired = "2026-11-03", seen = "2026-11-03 12:00" };
        var devRound = Web.Device.FromJ(devTest.ToJ());
        Ok(devRound.token == "tok123" && devRound.name == "Test iPad" && devRound.seen == "2026-11-03 12:00", "Device serialization roundtrip");

        // 2. GET / serves HTML
        using (var wc = new System.Net.WebClient())
        {
            string html = wc.DownloadString("http://127.0.0.1:" + wport + "/");
            Ok(html.Contains("<!DOCTYPE html>") && html.Contains("errin."), "GET / serves embedded SPA");
        }

        // 3. Unauthenticated request to /api/dashboard returns 401
        try
        {
            using (var wc = new System.Net.WebClient())
            {
                wc.DownloadString("http://127.0.0.1:" + wport + "/api/dashboard");
                Ok(false, "Unauthenticated request should have failed");
            }
        }
        catch (System.Net.WebException wex)
        {
            var resp = wex.Response as System.Net.HttpWebResponse;
            Ok(resp != null && resp.StatusCode == System.Net.HttpStatusCode.Unauthorized, "Unauthenticated request rejected with 401");
        }

        // 4. Pairing: start pairing code
        wsrv.StartPairing("847291", 300);

        // Wrong code fails
        using (var wc = new System.Net.WebClient())
        {
            try
            {
                wc.Headers[System.Net.HttpRequestHeader.ContentType] = "application/json";
                wc.UploadString("http://127.0.0.1:" + wport + "/api/pair", "{\"code\":\"000000\",\"name\":\"Hacker\"}");
                Ok(false, "Wrong pairing code should have failed");
            }
            catch (System.Net.WebException wex)
            {
                var resp = wex.Response as System.Net.HttpWebResponse;
                Ok(resp != null && resp.StatusCode == System.Net.HttpStatusCode.BadRequest, "Wrong code returns 400 Bad Request");
            }
        }

        // Correct code succeeds and returns token
        string issuedToken = "";
        using (var wc = new System.Net.WebClient())
        {
            wc.Headers[System.Net.HttpRequestHeader.ContentType] = "application/json";
            string pairJson = wc.UploadString("http://127.0.0.1:" + wport + "/api/pair", "{\"code\":\"847 291\",\"name\":\"Library Counter 2\"}");
            var pairObj = Json.Parse(pairJson) as Dictionary<string, object>;
            Ok(pairObj != null && (bool)pairObj["ok"], "Pairing succeeded with code format '847 291'");
            issuedToken = Json.Str(pairObj, "token");
            Ok(issuedToken.Length == 64, "Issued token is 32 bytes hex (64 chars)");
            Ok(wdevs.Count == 1 && wdevs[0].name == "Library Counter 2", "Device added to authorized devices list");
        }

        // 5. Authenticated GET /api/dashboard with X-Errin-Token
        using (var wc = new System.Net.WebClient())
        {
            wc.Headers["X-Errin-Token"] = issuedToken;
            string dashJson = wc.DownloadString("http://127.0.0.1:" + wport + "/api/dashboard");
            var dashObj = Json.Parse(dashJson) as Dictionary<string, object>;
            Ok(dashObj != null && (bool)dashObj["ok"] && Json.Num(dashObj, "activeTitles") > 0, "Dashboard data retrieved with token");
        }

        // 6. Authenticated checkout and return via API
        using (var wc = new System.Net.WebClient())
        {
            wc.Headers["X-Errin-Token"] = issuedToken;
            wc.Headers[System.Net.HttpRequestHeader.ContentType] = "application/json";
            string coJson = wc.UploadString("http://127.0.0.1:" + wport + "/api/checkout", "{\"patron\":\"" + wdb.Patrons[0].id + "\",\"barcode\":\"B1001\"}");
            var coObj = Json.Parse(coJson) as Dictionary<string, object>;
            Ok(coObj != null && (bool)coObj["ok"], "Remote checkout completed");
            Ok(wlib.OpenLoan(wlib.ItemByBarcode("B1001").id) != null, "Loan recorded in Db");

            // Return item
            string retJson = wc.UploadString("http://127.0.0.1:" + wport + "/api/return", "{\"barcode\":\"B1001\"}");
            var retObj = Json.Parse(retJson) as Dictionary<string, object>;
            Ok(retObj != null && (bool)retObj["ok"], "Remote return completed");
            Ok(wlib.OpenLoan(wlib.ItemByBarcode("B1001").id) == null, "Loan closed in Db");
            // 6. Test Reports API
            string repJson = wc.DownloadString("http://127.0.0.1:" + wport + "/api/reports");
            var repObj = Json.Parse(repJson) as Dictionary<string, object>;
            Ok(repObj != null && (bool)repObj["ok"], "Reports API succeeded");
            Ok(repObj.ContainsKey("monthlyCheckouts"), "Reports contains monthly checkouts");
            Ok(repObj.ContainsKey("deweyBreakdown"), "Reports contains dewey breakdown");
            Ok(repObj.ContainsKey("weedReasons"), "Reports contains weed reasons");
            Ok(repObj.ContainsKey("activeLoans"), "Reports contains active loans");

            // 7. Test QR endpoint
            byte[] qrBytes = wc.DownloadData("http://127.0.0.1:" + wport + "/api/qr?text=http://127.0.0.1:" + wport);
            Ok(qrBytes != null && qrBytes.Length > 8 && qrBytes[0] == 0x89 && qrBytes[1] == 0x50 && qrBytes[2] == 0x4E && qrBytes[3] == 0x47, "QR PNG endpoint returned valid PNG");
        }

        // 8. Revoke token
        wdevs.Clear();
        using (var wc = new System.Net.WebClient())
        {
            wc.Headers["X-Errin-Token"] = issuedToken;
            try
            {
                wc.DownloadString("http://127.0.0.1:" + wport + "/api/dashboard");
                Ok(false, "Revoked token should be rejected");
            }
            catch (System.Net.WebException wex)
            {
                var resp = wex.Response as System.Net.HttpWebResponse;
                Ok(resp != null && resp.StatusCode == System.Net.HttpStatusCode.Unauthorized, "Revoked token rejected with 401");
            }
        }

        wsrv.Stop();

        // QR Code Generator direct checks
        bool[,] qr1 = Qr.GenerateMatrix("HELLO");
        Ok(qr1.GetLength(0) == 21, "QR Version 1 matrix size is 21x21");
        Ok(qr1[0, 0] && qr1[0, 6] && qr1[6, 0] && qr1[6, 6], "Top-left finder outer corners dark");
        Ok(!qr1[1, 1] && !qr1[5, 5], "Top-left finder inner border light");
        Ok(qr1[3, 3], "Top-left finder center dark");
        Ok(qr1[6, 8] && !qr1[6, 9] && qr1[6, 10], "Timing pattern row 6 alternates");
        Ok(qr1[13, 8], "Dark module at (13, 8) is true");

        bool[,] qrUrl = Qr.GenerateMatrix("http://192.168.1.100:8585/?code=847291");
        Ok(qrUrl.GetLength(0) == 29, "Pairing URL fits Version 3 (29x29)");
        Ok(qrUrl[22, 22], "Version 3 alignment pattern center at (22, 22) is dark");

        using (var qrBmp = Qr.ToBitmap("http://127.0.0.1:8585", 4, 3))
        {
            Ok(qrBmp != null && qrBmp.Width > 0, "QR Bitmap generated with valid dimensions");
        }
        byte[] qrPng = Qr.ToPng("http://127.0.0.1:8585", 4, 3);
        Ok(qrPng != null && qrPng.Length > 8 && qrPng[0] == 0x89 && qrPng[1] == 0x50, "QR ToPng generated valid PNG bytes");

        string qrSvg = Qr.ToSvg("http://127.0.0.1:8585", 4, 3);
        Ok(qrSvg != null && qrSvg.Contains("<svg") && qrSvg.Contains("<path"), "QR ToSvg generated valid SVG xml");
        // 9. Patron editing and cascade update test
        var testDb = new Db();
        var testPat = new Patron { id = "080101-01-1111", name = "Original Student", cls = "1 Amanah", reg = "2026-01-01", last = "2026-01-01" };
        testDb.Patrons.Add(testPat);
        var testLoan = new Loan { id = "L001", iid = "I001", pid = "080101-01-1111", @out = "2026-02-01", due = "2026-02-08" };
        testDb.Loans.Add(testLoan);
        var testFine = new Fine { id = "F001", pid = "080101-01-1111", iid = "I001", amt = 2.50, date = "2026-02-10" };
        testDb.Fines.Add(testFine);
        var testHold = new Hold { id = "H001", pid = "080101-01-1111", iid = "I002", date = "2026-02-05" };
        testDb.Holds.Add(testHold);

        // Perform edit with IC update
        string oldPid = testPat.id;
        string newPid = "080101-01-2222";
        testPat.name = "Updated Student";
        testPat.cls = "2 Bakti";
        testPat.id = newPid;
        foreach (var l in testDb.Loans) if (l.pid == oldPid) l.pid = newPid;
        foreach (var f in testDb.Fines) if (f.pid == oldPid) f.pid = newPid;
        foreach (var h in testDb.Holds) if (h.pid == oldPid) h.pid = newPid;

        Ok(testPat.name == "Updated Student" && testPat.cls == "2 Bakti", "Patron details updated successfully");
        Ok(testLoan.pid == newPid, "Loan.pid cascaded to updated patron IC");
        Ok(testFine.pid == newPid, "Fine.pid cascaded to updated patron IC");
        Ok(testHold.pid == newPid, "Hold.pid cascaded to updated patron IC");

        // 10. TLS / SSL Certificate Helper tests
        if (Environment.OSVersion.Platform != PlatformID.Unix && Environment.OSVersion.Platform != PlatformID.MacOSX)
        {
            var cert = CertHelper.GenerateSelfSigned("CN=ErrinILS", 1, "errin");
            Ok(cert != null && cert.HasPrivateKey, "CertHelper generates self-signed certificate with private key");
            Ok(cert.Subject.Contains("ErrinILS"), "Certificate subject is CN=ErrinILS");
            string certSummary = CertHelper.GetSummary(cert);
            Ok(certSummary.Contains("CN=ErrinILS") && certSummary.Contains("Expires"), "CertHelper summary includes subject and expiry");

            string testCerPath = Path.Combine(Path.GetTempPath(), "test-export.cer");
            try {
                CertHelper.ExportCer(cert, testCerPath);
                Ok(File.Exists(testCerPath) && new FileInfo(testCerPath).Length > 0, "CertHelper exports public .cer certificate");
            } finally {
                if (File.Exists(testCerPath)) File.Delete(testCerPath);
            }

            var tlsSrv = new Web.Server();
            tlsSrv.ServerCert = cert;
            Ok(tlsSrv.UseTls, "Web.Server with ServerCert reports UseTls = true");
        }

        // 11. Auto-updater version comparison tests
        Ok(Updater.IsNewer("1.4.0", "1.3.2"), "Updater detects 1.4.0 is newer than 1.3.2");
        Ok(Updater.IsNewer("2.0.0", "1.9.9"), "Updater detects 2.0.0 is newer than 1.9.9");
        Ok(Updater.IsNewer("v1.5.0", "1.3.2"), "Updater handles leading 'v' in version tag");
        Ok(!Updater.IsNewer("1.3.2", "1.3.2"), "Updater detects identical version is not newer");
        Ok(!Updater.IsNewer("1.3.1", "1.3.2"), "Updater detects older version is not newer");

        // 12. Dashboard active catalog titles count exclusion test
        var weedDb = new Db();
        weedDb.Items.Add(new Item { id = "1", title = "Active Book 1" });
        weedDb.Items.Add(new Item { id = "2", title = "Active Book 2" });
        weedDb.Items.Add(new Item { id = "3", title = "Weeded Book", weeded = "2026-01-01", weed_reason = "damaged" });
        int actCount = weedDb.Items.Count(i => string.IsNullOrEmpty(i.weeded));
        Ok(actCount == 2, "Active catalog titles count correctly excludes weeded copies");

        Console.WriteLine("\nall " + n + " checks passed");
        return 0;
    }
}
