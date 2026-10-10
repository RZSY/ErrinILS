// ErrinILS main window: header tabs, status bar and the eight pages.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace Errin
{
    public partial class MainForm : Form
    {
        const int LIM = 200;
        readonly Store store; Db db; Lib lib; string view = "dash"; bool unlocked;
        Panel host; Label sbar, toast; Timer toastT, debT; FlowLayoutPanel tabBar;
        string pwSalt = "", pwHash = ""; int pwIter = Auth.Iterations;
        readonly Dictionary<string, Button> tabs = new Dictionary<string, Button>();
        string catQ = "", patQ = "";
        DataGridView catGrid, patGrid; Label catNote, patNote;
        static readonly string[][] NAV = { new[] { "dash", "Dashboard" }, new[] { "circ", "Circulation" }, new[] { "cat", "Catalog" }, new[] { "pat", "Students" }, new[] { "fine", "Fines" }, new[] { "rep", "Reports" }, new[] { "bak", "Backup" }, new[] { "set", "Settings" } };

        public MainForm()
        {
            store = new Store(null);
            Text = "ErrinILS"; Font = Th.F; BackColor = Th.Bg; ForeColor = Th.Ink; ClientSize = new Size(1240, 800); MinimumSize = new Size(900, 600); StartPosition = FormStartPosition.CenterScreen; AllowDrop = true;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
            db = store.Load(); bool fresh = db == null; if (fresh) db = Lib.Seed();
            lib = new Lib(db); lib.FixItems(); if (db.Set == null) db.Set = new Settings(); lib.Purge();
            BuildFrame(); Save(); LoadCfg(); ApplyZ(); Render();
            DragEnter += (s, e) => { if (e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Copy; };
            DragDrop += (s, e) =>
            {
                var f = e.Data.GetData(DataFormats.FileDrop) as string[];
                if (f != null && f.Length > 0 && Regex.IsMatch(f[0], @"\.(mrc|marc|dat|xml|mrk|txt)$", RegexOptions.IgnoreCase)) { Go("cat"); ImportFile(f[0]); }
            };
            FormClosing += (s, e) => { if (zsrv != null) zsrv.Stop(); };
        }

        void BuildFrame()
        {
            var main = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, BackColor = Th.Bg, Margin = new Padding(0) };
            main.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            main.RowStyles.Add(new RowStyle(SizeType.Absolute, 74)); main.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); main.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
            var head = new Panel { Dock = DockStyle.Fill, BackColor = Th.Bg, Margin = new Padding(0) };
            var logo = new Panel { Dock = DockStyle.Left, Width = 250, BackColor = Th.Blue };
            logo.Controls.Add(new Label { UseMnemonic = false, Text = "errin.", Font = new Font("Segoe UI", 24f, FontStyle.Bold), ForeColor = Color.White, AutoSize = true, Location = new Point(20, 2), BackColor = Color.Transparent });
            logo.Controls.Add(new Label { UseMnemonic = false, Text = "A school library system.", Font = Th.FS, ForeColor = Color.White, AutoSize = true, Location = new Point(24, 50), BackColor = Color.Transparent });
            var tb = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, BackColor = Th.Bg, Padding = new Padding(0), AutoScroll = false };
            foreach (var n in NAV)
            {
                string key = n[0];
                var b = new Button { Text = n[1], FlatStyle = FlatStyle.Flat, Font = Th.FB, Height = 74, Width = 108, Margin = new Padding(0), Cursor = Cursors.Hand, BackColor = Th.Bg, ForeColor = Th.Ink, UseVisualStyleBackColor = false };
                b.FlatAppearance.BorderSize = 0; b.FlatAppearance.MouseOverBackColor = Th.Dim; b.Click += (s, e) => Go(key); tabs[key] = b;
                tb.Controls.Add(new Panel { Width = 2, Height = 74, BackColor = Th.Ink, Margin = new Padding(0) }); tb.Controls.Add(b);
            }
            tabBar = tb; head.Controls.Add(tb); head.Controls.Add(logo);
            host = new Panel { Dock = DockStyle.Fill, Padding = new Padding(28, 22, 28, 6), BackColor = Th.Bg, Margin = new Padding(0) };
            sbar = new Label { UseMnemonic = false, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold), Padding = new Padding(10, 0, 0, 0), Margin = new Padding(0) };
            main.Controls.Add(head, 0, 0); main.Controls.Add(host, 0, 1); main.Controls.Add(sbar, 0, 2);
            Controls.Add(main);
            toast = new Label { UseMnemonic = false, AutoSize = true, BackColor = Th.Yel, ForeColor = Th.Dark, Font = new Font("Segoe UI", 10.5f, FontStyle.Bold), Padding = new Padding(16, 10, 16, 10), Visible = false };
            Controls.Add(toast); toast.BringToFront();
            toastT = new Timer { Interval = 3300 }; toastT.Tick += (s, e) => { toastT.Stop(); toast.Visible = false; };
            debT = new Timer { Interval = 150 }; debT.Tick += (s, e) => { debT.Stop(); if (view == "cat") FilterCat(); else if (view == "pat") FilterPat(); };
        }

        /* ---------- state helpers ---------- */
        void Save() { SetSaved(store.Save(db)); }
        void SetSaved(bool ok)
        {
            sbar.BackColor = ok ? Th.Card : Th.Red; sbar.ForeColor = ok ? Th.Mut : Color.White;
            sbar.Text = ok ? "\u25CF Saved to this computer " + DateTime.Now.ToString("HH:mm") : "\u26A0 NOT saving: ErrinILS could not write to the disk. Check that the drive has free space and is not read-only.";
        }
        void Toast(string m)
        {
            toast.Text = m; toast.Visible = true; toast.PerformLayout();
            toast.Location = new Point(Math.Max(10, (ClientSize.Width - toast.Width) / 2), ClientSize.Height - 90); toast.BringToFront(); toastT.Stop(); toastT.Start();
        }
        void Commit(string msg) { Save(); Render(); if (msg != null) Toast(msg); }
        void Go(string v)
        {
            if (v == "set" && !unlocked) { if (!AskPw()) return; unlocked = true; }
            if (v != "set") unlocked = false;
            view = v; Render();
        }
        void Render()
        {
            foreach (var kv in tabs) { bool on = kv.Key == view; kv.Value.BackColor = on ? Th.Yel : Th.Bg; kv.Value.ForeColor = on ? Th.Dark : Th.Ink; }
            tabBar.Visible = view != "simple";
            host.SuspendLayout();
            var old = host.Controls.Cast<Control>().ToList(); host.Controls.Clear(); foreach (var c in old) c.Dispose();
            Control page;
            switch (view)
            {
                case "simple": page = SimplePage(); break; case "circ": page = CircPage(); break; case "cat": page = CatPage(); break; case "pat": page = PatPage(); break; case "fine": page = FinePage(); break;
                case "rep": page = RepPage(); break; case "bak": page = BakPage(); break; case "set": page = SetPage(); break; default: page = DashPage(); break;
            }
            page.Dock = DockStyle.Fill; host.Controls.Add(page); host.ResumeLayout(true);
            if (view == "cat") FilterCat(); if (view == "pat") FilterPat();
        }
        static Control ScrollPage(params Control[] rows)
        {
            var p = new Panel { AutoScroll = true, BackColor = Th.Bg };
            var t = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 1, BackColor = Th.Bg, Margin = new Padding(0), Padding = new Padding(0, 0, 18, 0) };
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            for (int k = 0; k < rows.Length; k++) { t.RowStyles.Add(new RowStyle(SizeType.AutoSize)); rows[k].Dock = DockStyle.Fill; t.Controls.Add(rows[k], 0, k); }
            p.Controls.Add(t); return p;
        }
        static Control Cols(int h, params Control[] cells)
        {
            var t = new TableLayoutPanel { Height = h, ColumnCount = cells.Length, RowCount = 1, BackColor = Color.Transparent, Margin = new Padding(0) };
            for (int k = 0; k < cells.Length; k++) { t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / cells.Length)); cells[k].Margin = new Padding(0, 0, k < cells.Length - 1 ? 14 : 0, 14); cells[k].Dock = DockStyle.Fill; t.Controls.Add(cells[k], k, 0); }
            t.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); return t;
        }
        static Control Spacer(int h) { return new Panel { Height = h, BackColor = Color.Transparent, Margin = new Padding(0) }; }
        static DataGridView MakeGrid(string[] h, List<string[]> rows, List<object> tags, string empty) { var g = Ui.Grid(); Ui.Fill(g, h, rows, tags, empty); return g; }
        static Control WithButtons(Control grid, params Button[] btns)
        {
            var t = new TableLayoutPanel { ColumnCount = 1, RowCount = 2, BackColor = Color.Transparent, Margin = new Padding(0) };
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); t.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); t.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            grid.Dock = DockStyle.Fill; t.Controls.Add(grid, 0, 0);
            var f = Ui.Flow(false); f.Padding = new Padding(0, 8, 0, 0); f.Dock = DockStyle.Fill; foreach (var b in btns) f.Controls.Add(b); t.Controls.Add(f, 0, 1); return t;
        }
        static Control Vbox(params Control[] c) { var f = Ui.Flow(false, FlowDirection.TopDown); foreach (var x in c) f.Controls.Add(x); return f; }
        string Mn(double v) { return Lib.Money(v); }

        /* ---------- Dashboard ---------- */
        Control DashPage()
        {
            var o = db.Loans.Where(l => l.ret == null).ToList(); var od = o.Where(lib.Overdue).ToList(); double un = db.Fines.Where(f => f.paid == "").Sum(f => f.amt);
            var stats = Ui.Flow(); var cl = new[] { Th.Blue, Th.Yel, Th.Red };
            var vals = new[] { new[] { db.Items.Count.ToString(), "Titles in catalog" }, new[] { db.Patrons.Count.ToString(), "Registered students" }, new[] { o.Count.ToString(), "Items on loan now" }, new[] { db.Loans.Count.ToString(), "Checkouts, all time" },
                new[] { od.Count.ToString(), "Overdue items" }, new[] { Mn(un), "Unpaid fines" }, new[] { db.Holds.Count(h => h.st != "done").ToString(), "Active holds" } };
            for (int k = 0; k < vals.Length; k++) stats.Controls.Add(Ui.Stat(vals[k][0], vals[k][1], cl[k % 3]));
            var rows = od.Select(l => new[] { lib.PName(l.pid), lib.PClass(l.pid), lib.Title(l.iid), l.due, Lib.Diff(Lib.Today(), l.due).ToString(), Mn(Lib.Diff(Lib.Today(), l.due) * db.Set.rate), Mn(lib.Owed(l.pid)) }).ToList();
            var og = MakeGrid(new[] { "Student", "Class", "Title", "Due", "Days late", "Fine on item", "Total owed" }, rows, null, "Nothing overdue \U0001F389");
            Func<string, Control> top = pre =>
            {
                var c = new Dictionary<string, int>(); foreach (var l in db.Loans.Where(l => pre == "" || l.@out.StartsWith(pre))) c[l.pid] = (c.ContainsKey(l.pid) ? c[l.pid] : 0) + 1;
                var r = c.OrderByDescending(x => x.Value).Take(10).Select((x, k) => new[] { (k + 1).ToString(), lib.PName(x.Key), lib.PClass(x.Key), x.Value.ToString() }).ToList();
                var g = MakeGrid(new[] { "#", "Student", "Class", "Books" }, r, null, "No borrowing yet"); g.Columns[0].FillWeight = 20; return g;
            };
            string t = Lib.Today();
            var kt = new Label { UseMnemonic = false, Text = "A simpler circulation screen with just Check out and Return, for quick use at the library desk. The other tabs are hidden while it is open.", AutoSize = false, Size = new Size(760, 44), ForeColor = Th.Ink };
            var kb = Ui.Btn("Open Simplified View", 0, (s, e) => Go("simple")); kb.Margin = new Padding(0, 6, 0, 0);
            var kcard = Ui.Card("Simplified View", Vbox(kt, kb));
            return ScrollPage(Ui.Heading("Welcome to Errin", "Library overview for " + DateTime.Now.ToString("ddd MMM dd yyyy", CultureInfo.InvariantCulture)), kcard, stats,
                Ui.Card("Overdue items", og, 230), Spacer(0),
                Cols(330, Ui.Card("top 10 borrowers · this month", top(t.Substring(0, 7)), 1), Ui.Card("top 10 borrowers · this year", top(t.Substring(0, 4)), 1), Ui.Card("top 10 borrowers · all time", top(""), 1)));
        }

        /* ---------- Circulation ---------- */
        Control CircPage()
        {
            var coP = Ui.Txt(240); var coI = Ui.Txt(240); var rtI = Ui.Txt(240); var hP = Ui.Txt(240); var hI = Ui.Txt(240);
            Action doCo = () => { string m; var e = lib.Checkout(coP.Text.Trim(), coI.Text.Trim(), out m); if (e != null) { Toast(e); return; } Commit(m); };
            Action doRet = () => { string m; var e = lib.Return(rtI.Text.Trim(), out m); if (e != null) { Toast(e); return; } Commit(m); };
            Action doRen = () => { string m; var e = lib.Renew(rtI.Text.Trim(), out m); if (e != null) { Toast(e); return; } Commit(m); };
            Action doHold = () => { string m; var e = lib.PlaceHold(hP.Text.Trim(), hI.Text.Trim(), out m); if (e != null) { Toast(e); return; } Commit(m); };
            Func<Action, KeyEventHandler> enter = a => (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; a(); } };
            coI.KeyDown += enter(doCo); rtI.KeyDown += enter(doRet); hI.KeyDown += enter(doHold);
            var c1 = Ui.Card("Check out", Vbox(Ui.Caption("IC number (scan library card)"), coP, Ui.Caption("Item barcode"), coI, Ui.Btn("Check out", 0, (s, e) => doCo())));
            var rr = Ui.Flow(false); rr.Controls.Add(Ui.Btn("Return", 0, (s, e) => doRet())); rr.Controls.Add(Ui.Btn("Renew", 1, (s, e) => doRen()));
            var c2 = Ui.Card("Return / Renew", Vbox(Ui.Caption("Item barcode"), rtI, rr));
            var c3 = Ui.Card("Place hold", Vbox(Ui.Caption("IC number"), hP, Ui.Caption("Item barcode"), hI, Ui.Btn("Place hold", 1, (s, e) => doHold())));
            var loans = db.Loans.Where(l => l.ret == null).OrderBy(l => l.due, StringComparer.Ordinal).ToList();
            var lg = MakeGrid(new[] { "Student", "Title", "Barcode", "Due", "Renewals" }, loans.Select(l => { var it = db.Items.FirstOrDefault(x => x.id == l.iid); return new[] { lib.PName(l.pid), lib.Title(l.iid), it != null ? it.barcode : "", l.due + (lib.Overdue(l) ? "  (overdue)" : ""), l.ren.ToString() }; }).ToList(), loans.Cast<object>().ToList(), "No active loans");
            Func<string> selBc = () => { var l = Ui.Sel<Loan>(lg); if (l == null) return null; var it = db.Items.FirstOrDefault(x => x.id == l.iid); return it != null ? it.barcode : null; };
            var lcard = Ui.Card("Current loans", WithButtons(lg, Ui.Btn("Renew selected", 1, (s, e) => { var b = selBc(); if (b == null) { Toast("Select a loan first"); return; } string m; var er = lib.Renew(b, out m); if (er != null) Toast(er); else Commit(m); }),
                Ui.Btn("Return selected", 0, (s, e) => { var b = selBc(); if (b == null) { Toast("Select a loan first"); return; } string m; var er = lib.Return(b, out m); if (er != null) Toast(er); else Commit(m); })), 270);
            var holds = db.Holds.Where(h => h.st != "done").ToList();
            var hg = MakeGrid(new[] { "Title", "Student", "Placed", "Status" }, holds.Select(h => new[] { lib.Title(h.iid), lib.PName(h.pid), h.date, h.st == "ready" ? "Ready for pickup" : "Waiting" }).ToList(), holds.Cast<object>().ToList(), "No holds");
            var hcard = Ui.Card("Holds queue", WithButtons(hg, Ui.Btn("Cancel hold", 2, (s, e) => { var h = Ui.Sel<Hold>(hg); if (h == null) { Toast("Select a hold first"); return; } h.st = "done"; Commit(null); })), 220);
            BeginInvoke(new Action(() => { if (!coP.IsDisposed) coP.Focus(); }));
            return ScrollPage(Ui.Heading("Circulation", "Loan period is " + Lib.LOAN + " days. Scan or type IC numbers and barcodes."), Cols(200, c1, c2, c3), lcard, hcard);
        }

        /* ---------- Catalog ---------- */
        Control CatPage()
        {
            var q = Ui.Txt(300); q.Text = catQ; q.Margin = new Padding(0, 0, 14, 0); q.TextChanged += (s, e) => { catQ = q.Text; debT.Stop(); debT.Start(); };
            var bar = Ui.Flow(false); bar.Controls.Add(Ui.Field("Search", q, 330));
            var bf = Ui.Flow(false); bf.Padding = new Padding(0, 18, 0, 0);
            bf.Controls.Add(Ui.Btn("+ Add record", 0, (s, e) => ItemForm(null))); bf.Controls.Add(Ui.Btn("Import MARC", 1, (s, e) => PickImport()));
            bf.Controls.Add(Ui.Btn("Z39.50 search", 1, (s, e) => ZDialog())); bf.Controls.Add(Ui.Btn("Print labels", 1, (s, e) => PrintShown())); bf.Controls.Add(Ui.Btn("Print spine labels", 1, (s, e) => PrintSpines())); bar.Controls.Add(bf);
            var tool = Ui.Card(null, bar);
            catGrid = Ui.Grid(); catNote = Ui.Lbl("", true);
            catGrid.CellDoubleClick += (s, e) => { var i = Ui.Sel<Item>(catGrid); if (i != null) ItemForm(i); };
            var btns = new[] { Ui.Btn("Edit", 1, (s, e) => { var i = Ui.Sel<Item>(catGrid); if (i == null) Toast("Select a record first"); else ItemForm(i); }),
                Ui.Btn("MARC", 1, (s, e) => { var i = Ui.Sel<Item>(catGrid); if (i == null) Toast("Select a record first"); else MarcView(i); }),
                Ui.Btn("Label", 1, (s, e) => { var i = Ui.Sel<Item>(catGrid); if (i == null) Toast("Select a record first"); else LabelDialog(i); }),
                Ui.Btn("Spine label", 1, (s, e) => { var i = Ui.Sel<Item>(catGrid); if (i == null) Toast("Select a record first"); else SpineDialog(i); }),
                Ui.Btn("Delete", 2, (s, e) => { var i = Ui.Sel<Item>(catGrid); if (i == null) Toast("Select a record first"); else DelItem(i); }) };
            var body = WithButtons(catGrid, btns);
            var card = Ui.Card("Records", body, 1);
            return Ui.Stack(new Control[] { Ui.Heading("Catalog", "Bibliographic records with MARC 21 fields"), tool, catNote, card }, 3);
        }
        List<Item> shown = new List<Item>();
        void FilterCat()
        {
            if (catGrid == null || catGrid.IsDisposed) return; string q = (catQ ?? "").ToLowerInvariant();
            shown = db.Items.Where(i => i.All().ToLowerInvariant().Contains(q)).ToList();
            var rows = shown.Take(LIM).Select(i => { var l = lib.OpenLoan(i.id); return new[] { i.barcode, i.ctl, i.acq, i.title, i.author, i.call, l != null ? "Due " + l.due : "Available" }; }).ToList();
            Ui.Fill(catGrid, new[] { "Barcode", "Control no.", "Acq. no.", "Title", "Author", "Call no.", "Status" }, rows, shown.Take(LIM).Cast<object>().ToList(), "No records");
            catNote.Text = shown.Count > LIM ? "Showing the first " + LIM + " of " + shown.Count + ". Use the search box to narrow down." : shown.Count + " record(s)";
        }
        void DelItem(Item i)
        {
            if (lib.OpenLoan(i.id) != null) { Toast("Item is on loan"); return; }
            if (!AskPw()) return;
            if (Ui.Confirm(this, "Delete this record?")) { db.Items.Remove(i); Commit(null); }
        }
        void PrintSpines()
        {
            int n = shown.Count(i => SpineArt.Lines(i.call).Count > 0);
            if (n == 0) { Toast("No items with a call number to print"); return; }
            int skip = shown.Count - n;
            if (Ui.Confirm(this, "Print spine labels for " + n + " item(s)?" + (skip > 0 ? "\n" + skip + " item(s) without a call number will be skipped." : "") + "\nUse the search box first to choose which items.")) { if (SpineArt.Print(this, shown, 1)) Toast("Sent to printer"); }
        }
        void PrintShown()
        {
            if (shown.Count == 0) { Toast("No items to print"); return; }
            if (Ui.Confirm(this, "Print labels for " + shown.Count + " item(s)?\nUse the search box first to choose which items.")) { if (Labels.Print(this, shown, 1)) Toast("Sent to printer"); }
        }

        /* ---------- Students ---------- */
        Control PatPage()
        {
            var q = Ui.Txt(300); q.Text = patQ; q.Margin = new Padding(0, 0, 14, 0); q.TextChanged += (s, e) => { patQ = q.Text; debT.Stop(); debT.Start(); };
            var bar = Ui.Flow(false); bar.Controls.Add(Ui.Field("Search", q, 330));
            var bf = Ui.Flow(false); bf.Padding = new Padding(0, 18, 0, 0); bf.Controls.Add(Ui.Btn("+ Register student", 0, (s, e) => PatForm())); bar.Controls.Add(bf);
            patGrid = Ui.Grid(); patNote = Ui.Lbl("", true);
            patGrid.CellDoubleClick += (s, e) => { var p = Ui.Sel<Patron>(patGrid); if (p != null) PatView(p); };
            var body = WithButtons(patGrid, Ui.Btn("Account", 1, (s, e) => { var p = Ui.Sel<Patron>(patGrid); if (p == null) Toast("Select a student first"); else PatView(p); }),
                Ui.Btn("Print library card", 0, (s, e) => { var p = Ui.Sel<Patron>(patGrid); if (p == null) Toast("Select a student first"); else CardDialog(p); }),
                Ui.Btn("Print cards for list", 1, (s, e) => PrintCards()),
                Ui.Btn("Delete", 2, (s, e) => { var p = Ui.Sel<Patron>(patGrid); if (p == null) Toast("Select a student first"); else DelPat(p); }));
            return Ui.Stack(new Control[] { Ui.Heading("Students", "Register students with their IC number, print library cards and manage accounts. Records are deleted after " + Lib.RET_YEARS + " years of inactivity."), Ui.Card(null, bar), patNote, Ui.Card("Students", body, 1) }, 3);
        }
        void FilterPat()
        {
            if (patGrid == null || patGrid.IsDisposed) return; string q = (patQ ?? "").ToLowerInvariant();
            var r = db.Patrons.Where(p => (p.id + p.name + p.cls).ToLowerInvariant().Contains(q) || (Lib.NormIc(q) != null && Lib.NormIc(p.id) == Lib.NormIc(q))).ToList(); shownPat = r;
            Ui.Fill(patGrid, new[] { "IC number", "Name", "Class", "Registered", "On loan", "Fines" }, r.Take(LIM).Select(p => new[] { p.id, p.name, p.cls, p.reg, db.Loans.Count(l => l.pid == p.id && l.ret == null).ToString(), Mn(lib.Unpaid(p.id)) }).ToList(), r.Take(LIM).Cast<object>().ToList(), "No students");
            patNote.Text = r.Count > LIM ? "Showing the first " + LIM + " of " + r.Count + ". Use the search box to narrow down." : r.Count + " student(s)";
        }
        List<Patron> shownPat = new List<Patron>();
        void PrintCards()
        {
            if (shownPat.Count == 0) { Toast("No students to print"); return; }
            if (!Ui.Confirm(this, "Print library cards for " + shownPat.Count + " student(s) in the list?\nUse the search box first to choose a class or a single student.")) return;
            if (PrintCardList(shownPat)) Toast("Sent to printer");
        }
        bool PrintCardList(List<Patron> list) { string sch = db.Set.school; return Labels.PrintSheet(this, list, CardArt.W, CardArt.H, 40, (g, x, y, p) => CardArt.Draw(g, x, y, p, sch), "ErrinILS library cards"); }
        void DelPat(Patron p)
        {
            if (db.Loans.Any(l => l.pid == p.id && l.ret == null)) { Toast("Student has unreturned items"); return; }
            if (!AskPw()) return;
            if (Ui.Confirm(this, "Delete this student and their history?"))
            { db.Patrons.Remove(p); db.Loans.RemoveAll(l => l.pid == p.id); db.Fines.RemoveAll(f => f.pid == p.id); db.Holds.RemoveAll(h => h.pid == p.id); Commit(null); }
        }

        /* ---------- Fines ---------- */
        Control FinePage()
        {
            var f = db.Fines.AsEnumerable().Reverse().ToList(); double un = f.Where(x => x.paid == "").Sum(x => x.amt), pd = f.Where(x => x.paid == "paid").Sum(x => x.amt);
            var st = Ui.Flow(); st.Controls.Add(Ui.Stat(Mn(un), "Outstanding", Th.Blue)); st.Controls.Add(Ui.Stat(Mn(pd), "Collected", Th.Yel));
            var g = MakeGrid(new[] { "Date", "Student", "Title", "Days", "Amount", "Status" }, f.Take(LIM).Select(x => new[] { x.date, lib.PName(x.pid), lib.Title(x.iid), x.days.ToString(), Mn(x.amt), x.paid == "paid" ? "Paid" : x.paid == "waived" ? "Waived" : "Unpaid" }).ToList(), f.Take(LIM).Cast<object>().ToList(), "No fines recorded");
            Action<string> mark = s => { var x = Ui.Sel<Fine>(g); if (x == null) { Toast("Select a fine first"); return; } if (x.paid != "") { Toast("Already settled"); return; } x.paid = s; Commit(s == "paid" ? "Payment recorded" : "Fine waived"); };
            var body = WithButtons(g, Ui.Btn("Pay", 0, (s, e) => mark("paid")), Ui.Btn("Waive", 1, (s, e) => mark("waived")));
            return Ui.Stack(new Control[] { Ui.Heading("Fine tracker", "Fines accrue at " + Mn(db.Set.rate) + " per day late, charged on return."), st, Ui.Card("All fines", body, 1) }, 2);
        }

        /* ---------- Reports ---------- */
        Control RepPage()
        {
            var c = new Dictionary<string, int>(); foreach (var l in db.Loans) c[l.iid] = (c.ContainsKey(l.iid) ? c[l.iid] : 0) + 1;
            var top = c.OrderByDescending(x => x.Value).Take(8).Select(x => new KeyValuePair<string, double>(lib.Title(x.Key), x.Value)).ToList();
            var mo = new List<KeyValuePair<string, double>>();
            for (int i = 5; i >= 0; i--) { var d = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1).AddMonths(-i); string k = d.ToString("yyyy-MM", CultureInfo.InvariantCulture); mo.Add(new KeyValuePair<string, double>(d.ToString("MMM", CultureInfo.InvariantCulture), db.Loans.Count(l => l.@out.StartsWith(k)))); }
            var cl = new Dictionary<string, int>(); foreach (var l in db.Loans) { var k = lib.PClass(l.pid); if (k == "") k = "?"; cl[k] = (cl.ContainsKey(k) ? cl[k] : 0) + 1; }
            double tot = db.Fines.Sum(f => f.amt), paid = db.Fines.Where(f => f.paid == "paid").Sum(f => f.amt);
            int late = db.Loans.Count(l => l.ret != null && string.CompareOrdinal(l.ret, l.due) > 0), rr = db.Loans.Count(l => l.ret != null);
            var st = Ui.Flow(); st.Controls.Add(Ui.Stat(db.Loans.Count(l => l.ret == null).ToString(), "Books on loan now", Th.Blue)); st.Controls.Add(Ui.Stat(rr > 0 ? Math.Round(late * 100.0 / rr) + "%" : "0%", "Returned late", Th.Yel));
            st.Controls.Add(Ui.Stat(Mn(tot), "Fines assessed", Th.Red)); st.Controls.Add(Ui.Stat(Mn(paid), "Fines collected", Th.Blue));
            Func<List<KeyValuePair<string, double>>, bool, BarChart> chart = (d, v) => new BarChart { Data = d, Vertical = v };
            var unpaid = db.Patrons.Select(p => new { p.name, v = lib.Unpaid(p.id) }).Where(x => x.v > 0).Select(x => new[] { x.name, Mn(x.v) }).ToList();
            return ScrollPage(Ui.Heading("Reports", "Circulation and fine analytics"), st,
                Cols(230, Ui.Card("Checkouts, last 6 months", chart(mo, true), 1), Ui.Card("Most borrowed titles", chart(top, false), 1)),
                Cols(250, Ui.Card("Checkouts by class", chart(cl.OrderByDescending(x => x.Value).Take(8).Select(x => new KeyValuePair<string, double>(x.Key, x.Value)).ToList(), false), 1),
                    Ui.Card("Students with unpaid fines", MakeGrid(new[] { "Student", "Owed" }, unpaid, null, "None"), 1)));
        }

        /* ---------- Backup ---------- */
        Control BakPage()
        {
            var info = new Label { UseMnemonic = false, Text = "Download a backup of everything (students, loans, holds, fines, catalog), or restore from one. Keep a copy on a USB drive or cloud storage.", AutoSize = false, Size = new Size(760, 44), ForeColor = Th.Ink };
            var row = Ui.Flow(false); row.Controls.Add(Ui.Btn("Download backup", 0, (s, e) => Backup())); row.Controls.Add(Ui.Btn("Restore from backup (password)", 1, (s, e) => { if (AskPw()) Restore(); }));
            return ScrollPage(Ui.Heading("Backup", "Protect your data. Back up regularly and keep a copy on a USB drive or cloud storage."), Ui.Card("Backup & restore", Vbox(info, row)));
        }
        void Backup()
        {
            using (var d = new SaveFileDialog { FileName = "errin-backup-" + Lib.Today() + ".json", Filter = "Errin backup (*.json)|*.json" })
            {
                if (d.ShowDialog(this) != DialogResult.OK) { Toast("Cancelled"); return; }
                try { File.WriteAllText(d.FileName, db.ToJson(), new UTF8Encoding(false)); Toast("Backup ready"); } catch { Toast("Backup failed"); }
            }
        }
        void Restore()
        {
            using (var d = new OpenFileDialog { Filter = "Errin backup (*.json)|*.json|All files|*.*" })
            {
                if (d.ShowDialog(this) != DialogResult.OK) return;
                Db n; try { var root = Json.Parse(File.ReadAllText(d.FileName, Encoding.UTF8)) as Dictionary<string, object>; if (root == null || !root.ContainsKey("items") || !root.ContainsKey("patrons") || !root.ContainsKey("loans")) throw new Exception(); n = Db.FromJson(File.ReadAllText(d.FileName, Encoding.UTF8)); }
                catch { Toast("Not a valid Errin backup"); return; }
                if (!Ui.Confirm(this, "Replace ALL current data with this backup?")) return;
                db = n; lib = new Lib(db); lib.FixItems(); Commit("Backup restored");
            }
        }

        /* ---------- Settings ---------- */
        Control SetPage()
        {
            var s = db.Set;
            var rate = Ui.Num(110, 0, 1000, (decimal)s.rate, 2, 0.05m); var mx = Ui.Num(110, 1, 100, s.maxLoans); var blk = Ui.Num(110, 0, 100000, (decimal)s.block, 2, 1);
            var sch = Ui.Txt(300); sch.Text = s.school; var pol = Ui.Flow(); pol.Controls.Add(Ui.Field("School / library name (printed on library cards)", sch, 400)); pol.Controls.Add(Ui.Field("Fine per day late", rate, 190)); pol.Controls.Add(Ui.Field("Max items per student", mx, 210)); pol.Controls.Add(Ui.Field("Block checkout at unpaid fines of", blk, 290));
            var polBody = Vbox(pol, Ui.Lbl("Loan period fixed at " + Lib.LOAN + " days \u00B7 max 2 renewals.", true), Spacer(6), Ui.Btn("Save", 0, (a, b) => { db.Set.school = sch.Text.Trim(); db.Set.rate = (double)rate.Value; db.Set.maxLoans = (int)mx.Value; db.Set.block = (double)blk.Value; Save(); Toast("Settings saved"); }));
            Func<string, int, Control> para = (t, h) => new Label { UseMnemonic = false, Text = t, AutoSize = false, Size = new Size(820, h), ForeColor = Th.Ink };
            var zE = Ui.Combo(150, "Off", "On"); var zP = Ui.Num(110, 1, 65535, zc.port); var zD = Ui.Txt(150); zD.Text = zc.db; var zB = Ui.Combo(170, "Other computers", "This computer only");
            zE.SelectedIndex = zc.enabled ? 1 : 0; zB.SelectedIndex = zc.bind == "local" ? 1 : 0;
            var zs = new Label { UseMnemonic = false, AutoSize = false, Size = new Size(820, 70), ForeColor = Th.Mut, Margin = new Padding(0, 10, 0, 0) }; zStatusLabel = zs; ZShow();
            var zf = Ui.Flow(); zf.Controls.Add(Ui.Field("Server", zE, 160)); zf.Controls.Add(Ui.Field("Port", zP, 120)); zf.Controls.Add(Ui.Field("Database name", zD, 160)); zf.Controls.Add(Ui.Field("Reachable from", zB, 190));
            var zBody = Vbox(para("Lets other library systems and Z39.50 clients search this catalogue over the network. It is read-only and shares bibliographic records only: never students, loans or fines. Off by default.", 44), zf,
                Ui.Btn("Apply", 0, (a, b) => ZApply(zE.SelectedIndex == 1, (int)zP.Value, zD.Text.Trim(), zB.SelectedIndex == 1 ? "local" : "network")), zs);
            var pc = Ui.Txt(240, true); var pn = Ui.Txt(240, true); var pn2 = Ui.Txt(240, true);
            var pf = Ui.Flow(); pf.Controls.Add(Ui.Field("Current password", pc, 260)); pf.Controls.Add(Ui.Field("New password", pn, 260)); pf.Controls.Add(Ui.Field("Type the new password again", pn2, 260));
            var pwBody = Vbox(para("The password protects Settings, deleting records and restoring backups. Use at least 4 characters and keep it somewhere safe.", 44), pf,
                Ui.Btn("Change password", 0, (a, b) => { if (ChangePw(pc.Text, pn.Text, pn2.Text)) { pc.Text = ""; pn.Text = ""; pn2.Text = ""; } }));
            return ScrollPage(Ui.Heading("Settings", "Policies and data management"), Ui.Card("Policies", polBody), Ui.Card("Admin password", pwBody),
                Ui.Card("Data retention", para("Student records (with their loan and fine history) are deleted " + Lib.RET_YEARS + " years after their last activity, provided they have no unreturned items or unpaid fines. This happens automatically when ErrinILS starts.", 44)),
                Ui.Card("Switching systems?", Vbox(para("Export every catalogued item as MARC 21, MARCXML, MarcEdit text, JSON and CSV to move to another ILS. To bring records in, use Import MARC on the Catalog page.", 44), Ui.Btn("Export catalog", 0, (a, b) => ExportDialog()))),
                Ui.Card("z39.50 server", zBody),
                Ui.Card("Local database", Vbox(para("Every change is written to the database file on this computer the instant you make it. No save button needed.", 24), Ui.Lbl("Database file: " + store.DataFile, true))));
        }

        /* ---------- Z39.50 server (settings + lifecycle) ---------- */
        class ZCfg { public bool enabled; public int port = 210; public string db = "errin"; public string bind = "network"; }
        ZCfg zc = new ZCfg(); Z.Server zsrv; string zerr = ""; Label zStatusLabel; List<Item> zCache; int zCacheVer = -1; readonly object zLock = new object();
        void LoadCfg()
        {
            try
            {
                var root = Json.Parse(File.ReadAllText(store.ConfigFile, Encoding.UTF8)) as Dictionary<string, object>; var z = Json.Obj(root, "z");
                if (z.ContainsKey("enabled")) zc.enabled = z["enabled"] is bool && (bool)z["enabled"];
                if (z.ContainsKey("port")) zc.port = (int)Json.Num(z, "port"); if (z.ContainsKey("db")) zc.db = Json.Str(z, "db"); if (z.ContainsKey("bind")) zc.bind = Json.Str(z, "bind") == "local" ? "local" : "network";
                var pw = Json.Obj(root, "pw"); pwSalt = Json.Str(pw, "s"); pwHash = Json.Str(pw, "h"); pwIter = pw.ContainsKey("i") ? (int)Json.Num(pw, "i") : Auth.Iterations;
            }
            catch { }
        }
        void SaveCfg()
        {
            try
            {
                Directory.CreateDirectory(store.Dir);
                var cfg = new Dictionary<string, object> { { "z", new Dictionary<string, object> { { "enabled", zc.enabled }, { "port", (double)zc.port }, { "db", zc.db }, { "bind", zc.bind } } } };
                if (pwHash != "") cfg["pw"] = new Dictionary<string, object> { { "s", pwSalt }, { "h", pwHash }, { "i", (double)pwIter } };
                File.WriteAllText(store.ConfigFile, Json.Write(cfg));
            }
            catch { }
        }
        List<Item> ZItems() { lock (zLock) { if (zCacheVer != store.Version || zCache == null) { zCache = store.SnapshotItems(); zCacheVer = store.Version; } return zCache; } }
        void ApplyZ()
        {
            if (zsrv != null) { zsrv.Stop(); zsrv = null; }
            zerr = ""; if (!zc.enabled) return;
            try { zsrv = Z.StartServer(zc.port, zc.bind == "local", zc.db, ZItems, null); }
            catch (SocketException e)
            {
                zerr = e.SocketErrorCode == SocketError.AddressAlreadyInUse ? "Port " + zc.port + " is already in use by another program. Choose a different port."
                    : e.SocketErrorCode == SocketError.AccessDenied ? "Windows did not allow port " + zc.port + ". Choose a different port." : e.Message;
            }
            catch (Exception e) { zerr = e.Message; }
        }
        void ZApply(bool on, int port, string dbn, string bind)
        {
            if (!Regex.IsMatch(dbn, @"^[A-Za-z0-9_.-]{1,30}$")) { Toast("Database name: letters, digits, dot, dash or underscore only (max 30)"); return; }
            zc = new ZCfg { enabled = on, port = port, db = dbn, bind = bind }; SaveCfg(); ApplyZ(); ZShow();
            Toast(zerr != "" ? zerr : zsrv != null ? "Z39.50 server is running" : "Z39.50 server is off");
        }
        void ZShow()
        {
            if (zStatusLabel == null || zStatusLabel.IsDisposed) return;
            if (zerr != "") { zStatusLabel.ForeColor = Th.Red; zStatusLabel.Text = "Problem: " + zerr; return; }
            if (zsrv != null)
            {
                var addrs = zc.bind == "local" ? new List<string> { "127.0.0.1" } : Z.LanAddresses().Concat(new[] { "127.0.0.1" }).ToList();
                zStatusLabel.ForeColor = Th.Yel;
                zStatusLabel.Text = "Running. Other systems connect to " + string.Join(" or ", addrs.Select(a => a + ":" + zc.port).ToArray()) + ", database " + zc.db + ". " + zsrv.Connections + " connected now, " + zsrv.Served +
                    " since start.\nWindows may ask whether to allow ErrinILS through the firewall the first time. Choose Private networks.";
            }
            else { zStatusLabel.ForeColor = Th.Mut; zStatusLabel.Text = "Off. Not accepting connections."; }
        }

        /* ---------- password ---------- */
        bool PwOk(string pw) { return Auth.Verify(pw, pwSalt, pwHash, pwIter); }
        bool ChangePw(string cur, string n1, string n2)
        {
            if (!PwOk(cur)) { Toast("Current password is not correct"); return false; }
            if (n1.Length < 4) { Toast("The new password needs at least 4 characters"); return false; }
            if (n1 != n2) { Toast("The two new passwords do not match"); return false; }
            string salt = Auth.NewSalt(); pwSalt = salt; pwIter = Auth.Iterations; pwHash = Auth.Hash(n1, salt, pwIter); SaveCfg();
            Toast("Password changed. Use the new one from now on."); return true;
        }
        public bool AskPw()
        {
            using (var d = Ui.Dlg("Enter password", 380, 190))
            {
                var t = Ui.Txt(340, true); t.Location = new Point(20, 76);
                d.Controls.Add(new Label { UseMnemonic = false, Text = "enter password", Font = Th.FT, AutoSize = true, Location = new Point(20, 16), ForeColor = Th.Ink });
                d.Controls.Add(Ui.Lbl("This area is password protected.", true)); d.Controls[d.Controls.Count - 1].Location = new Point(20, 44);
                d.Controls.Add(t);
                var ok = Ui.Btn("Unlock", 0); ok.Location = new Point(20, 124); var cancel = Ui.Btn("Cancel", 1); cancel.Location = new Point(110, 124);
                bool good = false;
                Action check = () =>
                {
                    if (PwOk(t.Text)) { good = true; d.Close(); } else { Toast("Incorrect password"); t.Text = ""; t.Focus(); }
                };
                ok.Click += (s, e) => check(); cancel.Click += (s, e) => d.Close(); t.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; check(); } };
                d.Controls.Add(ok); d.Controls.Add(cancel); d.Shown += (s, e) => t.Focus();
                d.ShowDialog(this); return good;
            }
        }
    }
}
