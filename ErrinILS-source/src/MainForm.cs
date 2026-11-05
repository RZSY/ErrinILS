// ErrinILS main window: header tabs, status bar and the eight pages.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using Timer = System.Windows.Forms.Timer;

namespace Errin
{
    public partial class MainForm : Form
    {
        const int LIM = 200;
        readonly Store store; Db db; Lib lib; string view = "dash"; bool unlocked;
        Panel host; Label sbar, toast; Timer toastT, debT; FlowLayoutPanel tabBar;
        string pwSalt = "", pwHash = ""; int pwIter = Auth.Iterations;
        readonly Dictionary<string, Button> tabs = new Dictionary<string, Button>();
        string catQ = "", patQ = "", catFilter = "active";
        DataGridView catGrid, patGrid; Label catNote, patNote;
        static readonly string[][] NAV = { new[] { "dash", "Dashboard" }, new[] { "circ", "Circulation" }, new[] { "cat", "Catalog" }, new[] { "pat", "Students" }, new[] { "fine", "Fines" }, new[] { "rep", "Reports" }, new[] { "bak", "Backup" }, new[] { "set", "Settings" } };

        public MainForm()
        {
            store = new Store(null);
            Text = "ErrinILS"; Font = Th.F; BackColor = Th.Bg; ForeColor = Th.Ink;
            var screen = Screen.FromControl(this).WorkingArea;
            int initW = Math.Min(Ui.S(1240), Math.Max(Ui.S(850), screen.Width - Ui.S(40)));
            int initH = Math.Min(Ui.S(800), Math.Max(Ui.S(580), screen.Height - Ui.S(40)));
            ClientSize = new Size(initW, initH);
            MinimumSize = new Size(Math.Min(initW, Ui.S(900)), Math.Min(initH, Ui.S(600)));
            StartPosition = FormStartPosition.CenterScreen; AllowDrop = true;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
            db = store.Load(); bool fresh = db == null; if (fresh) db = Lib.Seed();
            lib = new Lib(db); lib.FixItems(); if (db.Set == null) db.Set = new Settings(); lib.Purge();
            BuildFrame(); Save(); LoadCfg(); ApplyZ(); ApplyWeb(); Render();
            if (autoUpdate)
            {
                ThreadPool.QueueUserWorkItem(stObj =>
                {
                    try
                    {
                        var info = Updater.CheckForUpdate();
                        if (info != null && info.HasUpdate)
                        {
                            BeginInvoke(new Action(() => ShowUpdateBanner(info)));
                        }
                    }
                    catch { }
                });
            }
            DragEnter += (s, e) => { if (e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Copy; };
            DragDrop += (s, e) =>
            {
                var f = e.Data.GetData(DataFormats.FileDrop) as string[];
                if (f != null && f.Length > 0 && Regex.IsMatch(f[0], @"\.(mrc|marc|dat|xml|mrk|txt)$", RegexOptions.IgnoreCase)) { Go("cat"); ImportFile(f[0]); }
            };
            FormClosing += (s, e) => { if (zsrv != null) zsrv.Stop(); if (wsrv != null) wsrv.Stop(); };
        }

        void BuildFrame()
        {
            var main = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, BackColor = Th.Bg, Margin = new Padding(0) };
            main.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            main.RowStyles.Add(new RowStyle(SizeType.Absolute, Ui.S(74)));
            main.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            main.RowStyles.Add(new RowStyle(SizeType.Absolute, Ui.S(26)));
            var head = new Panel { Dock = DockStyle.Fill, BackColor = Th.Bg, Margin = new Padding(0) };
            var logo = new Panel { Dock = DockStyle.Left, Width = Ui.S(250), BackColor = Th.Blue };
            logo.Controls.Add(new Label { UseMnemonic = false, Text = "errin.", Font = Th.FLogo, ForeColor = Color.White, AutoSize = true, Location = new Point(Ui.S(20), Ui.S(2)), BackColor = Color.Transparent });
            logo.Controls.Add(new Label { UseMnemonic = false, Text = "A school library system.", Font = Th.FS, ForeColor = Color.White, AutoSize = true, Location = new Point(Ui.S(24), Ui.S(50)), BackColor = Color.Transparent });
            var tb = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, BackColor = Th.Bg, Padding = new Padding(0), AutoScroll = false };
            foreach (var n in NAV)
            {
                string key = n[0];
                var b = new Button { Text = n[1], FlatStyle = FlatStyle.Flat, Font = Th.FB, Height = Ui.S(74), Width = Ui.S(108), Margin = new Padding(0), Cursor = Cursors.Hand, BackColor = Th.Bg, ForeColor = Th.Ink, UseVisualStyleBackColor = false };
                b.FlatAppearance.BorderSize = 0; b.FlatAppearance.MouseOverBackColor = Th.Dim; b.Click += (s, e) => Go(key); tabs[key] = b;
                tb.Controls.Add(new Panel { Width = Math.Max(1, Ui.S(2)), Height = Ui.S(74), BackColor = Th.Ink, Margin = new Padding(0) }); tb.Controls.Add(b);
            }
            tabBar = tb; head.Controls.Add(tb); head.Controls.Add(logo);
            host = new Panel { Dock = DockStyle.Fill, Padding = Ui.S(new Padding(28, 22, 28, 6)), BackColor = Th.Bg, Margin = new Padding(0) };
            sbar = new Label { UseMnemonic = false, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold), Padding = Ui.S(new Padding(10, 0, 0, 0)), Margin = new Padding(0) };
            main.Controls.Add(head, 0, 0); main.Controls.Add(host, 0, 1); main.Controls.Add(sbar, 0, 2);
            Controls.Add(main);
            toast = new Label { UseMnemonic = false, AutoSize = true, BackColor = Th.Yel, ForeColor = Th.Dark, Font = new Font("Segoe UI", 10.5f, FontStyle.Bold), Padding = Ui.S(new Padding(16, 10, 16, 10)), Visible = false };
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
            toast.Location = new Point(Math.Max(10, (ClientSize.Width - toast.Width) / 2), ClientSize.Height - Ui.S(90)); toast.BringToFront(); toastT.Stop(); toastT.Start();
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
            var t = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 1, BackColor = Th.Bg, Margin = new Padding(0), Padding = Ui.S(new Padding(0, 0, 18, 0)) };
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            for (int k = 0; k < rows.Length; k++) { t.RowStyles.Add(new RowStyle(SizeType.AutoSize)); rows[k].Dock = DockStyle.Fill; t.Controls.Add(rows[k], 0, k); }
            p.Controls.Add(t); return p;
        }
        static Control Cols(int h, params Control[] cells)
        {
            var t = new TableLayoutPanel { Height = Ui.S(h), ColumnCount = cells.Length, RowCount = 1, BackColor = Color.Transparent, Margin = new Padding(0) };
            for (int k = 0; k < cells.Length; k++) { t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / cells.Length)); cells[k].Margin = Ui.S(new Padding(0, 0, k < cells.Length - 1 ? 14 : 0, 14)); cells[k].Dock = DockStyle.Fill; t.Controls.Add(cells[k], k, 0); }
            t.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); return t;
        }
        static Control Spacer(int h) { return new Panel { Height = Ui.S(h), BackColor = Color.Transparent, Margin = new Padding(0) }; }
        static DataGridView MakeGrid(string[] h, List<string[]> rows, List<object> tags, string empty) { var g = Ui.Grid(); Ui.Fill(g, h, rows, tags, empty); return g; }
        static Control WithButtons(Control grid, params Button[] btns)
        {
            var t = new TableLayoutPanel { ColumnCount = 1, RowCount = 2, BackColor = Color.Transparent, Margin = new Padding(0) };
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); t.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); t.RowStyles.Add(new RowStyle(SizeType.Absolute, Ui.S(44)));
            grid.Dock = DockStyle.Fill; t.Controls.Add(grid, 0, 0);
            var f = Ui.Flow(false); f.Padding = Ui.S(new Padding(0, 8, 0, 0)); f.Dock = DockStyle.Fill; foreach (var b in btns) f.Controls.Add(b); t.Controls.Add(f, 0, 1); return t;
        }
        static Control Vbox(params Control[] c) { var f = Ui.Flow(false, FlowDirection.TopDown); foreach (var x in c) f.Controls.Add(x); return f; }
        string Mn(double v) { return Lib.Money(v); }

        /* ---------- Dashboard ---------- */
        Control DashPage()
        {
            var o = db.Loans.Where(l => l.ret == null).ToList(); var od = o.Where(lib.Overdue).ToList(); double un = db.Fines.Where(f => f.paid == "").Sum(f => f.amt);
            var stats = Ui.Flow(); var cl = new[] { Th.Blue, Th.Yel, Th.Red };
            int act = db.Items.Count(i => string.IsNullOrEmpty(i.weeded));
            string titStat = act.ToString();
            var vals = new[] { new[] { titStat, "Active catalog titles" }, new[] { db.Patrons.Count.ToString(), "Registered students" }, new[] { o.Count.ToString(), "Items on loan now" }, new[] { db.Loans.Count.ToString(), "Checkouts, all time" },
                new[] { od.Count.ToString(), "Overdue items" }, new[] { Mn(un), "Unpaid fines" }, new[] { db.Holds.Count(h => h.st != "done").ToString(), "Active holds" } };
            for (int k = 0; k < vals.Length; k++) stats.Controls.Add(Ui.Stat(vals[k][0], vals[k][1], cl[k % 3]));
            var rows = od.Select(l => new[] { lib.PName(l.pid), lib.PClass(l.pid), lib.Title(l.iid), l.due, Lib.Diff(Lib.Today(), l.due).ToString(), Mn(Lib.Diff(Lib.Today(), l.due) * db.Set.rate), Mn(lib.Owed(l.pid)) }).ToList();
            var og = MakeGrid(new[] { "Student", "Class", "Title", "Due", "Days late", "Fine on item", "Total owed" }, rows, null, "Nothing overdue \U0001F389");
            og.CellFormatting += (s, e) => {
                if (e.RowIndex < 0) return;
                if (e.ColumnIndex == 4 || e.ColumnIndex == 5) { e.CellStyle.ForeColor = Th.Red; e.CellStyle.Font = Th.FB; }
            };
            Func<string, Control> top = pre =>
            {
                var c = new Dictionary<string, int>(); foreach (var l in db.Loans.Where(l => pre == "" || l.@out.StartsWith(pre))) c[l.pid] = (c.ContainsKey(l.pid) ? c[l.pid] : 0) + 1;
                var r = c.OrderByDescending(x => x.Value).Take(10).Select((x, k) => new[] { (k + 1).ToString(), lib.PName(x.Key), lib.PClass(x.Key), x.Value.ToString() }).ToList();
                var g = MakeGrid(new[] { "#", "Student", "Class", "Books" }, r, null, "No borrowing yet"); g.Columns[0].FillWeight = 20; return g;
            };
            string t = Lib.Today();
            var kt = new Label { UseMnemonic = false, Text = "A simpler circulation screen with just Check out and Return, for quick use at the library desk. The other tabs are hidden while it is open.", AutoSize = false, Size = new Size(Ui.S(760), Ui.S(44)), ForeColor = Th.Ink };
            var kb = Ui.Btn("Open Simplified View", 0, (s, e) => Go("simple")); kb.Margin = Ui.S(new Padding(0, 6, 0, 0));
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
            coP.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; var n = Lib.NormIc(coP.Text); if (n != null) coP.Text = n; coI.Focus(); } };
            coP.Leave += (s, e) => { var n = Lib.NormIc(coP.Text); if (n != null) coP.Text = n; };
            hP.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; var n = Lib.NormIc(hP.Text); if (n != null) hP.Text = n; hI.Focus(); } };
            hP.Leave += (s, e) => { var n = Lib.NormIc(hP.Text); if (n != null) hP.Text = n; };
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
        int catSort = 0;
        Control CatPage()
        {
            var q = Ui.Txt(240); q.Text = catQ; q.Margin = Ui.S(new Padding(0, 0, 14, 0)); q.TextChanged += (s, e) => { catQ = q.Text; debT.Stop(); debT.Start(); };
            var bar = Ui.Flow(false); bar.Controls.Add(Ui.Field("Search", q, 260));
            var cbFilt = Ui.Combo(150, "Active records", "Weeded records", "All records");
            if (catFilter == "weeded") cbFilt.SelectedIndex = 1;
            else if (catFilter == "all") cbFilt.SelectedIndex = 2;
            else cbFilt.SelectedIndex = 0;
            cbFilt.SelectedIndexChanged += (s, e) =>
            {
                catFilter = cbFilt.SelectedIndex == 1 ? "weeded" : cbFilt.SelectedIndex == 2 ? "all" : "active";
                FilterCat();
            };
            bar.Controls.Add(Ui.Field("Status", cbFilt, 165));
            var cbSort = Ui.Combo(175, "Title (A-Z)", "Title (Z-A)", "Author (A-Z)", "Barcode", "Call Number", "Acq. No.", "Newest Added");
            cbSort.SelectedIndex = catSort;
            cbSort.SelectedIndexChanged += (s, e) => { catSort = cbSort.SelectedIndex; FilterCat(); };
            bar.Controls.Add(Ui.Field("Sort by", cbSort, 190));
            var bf = Ui.Flow(false); bf.Padding = Ui.S(new Padding(0, 18, 0, 0));
            bf.Controls.Add(Ui.Btn("+ Add record", 0, (s, e) => ItemForm(null))); bf.Controls.Add(Ui.Btn("Import MARC", 1, (s, e) => PickImport()));
            bf.Controls.Add(Ui.Btn("Z39.50 search", 1, (s, e) => ZDialog())); bf.Controls.Add(Ui.Btn("Print labels", 1, (s, e) => PrintShown())); bf.Controls.Add(Ui.Btn("Print spine labels", 1, (s, e) => PrintSpines())); bar.Controls.Add(bf);
            var tool = Ui.Card(null, bar);
            catGrid = Ui.Grid(); catNote = Ui.Lbl("", true);
            catGrid.CellDoubleClick += (s, e) => { var i = Ui.Sel<Item>(catGrid); if (i != null) ItemForm(i); };
            catGrid.CellFormatting += (s, e) => {
                if (e.RowIndex < 0 || e.ColumnIndex != 6 || e.Value == null) return;
                string st = e.Value.ToString();
                if (st.StartsWith("Available")) { e.CellStyle.ForeColor = Th.Green; e.CellStyle.Font = Th.FB; }
                else if (st.StartsWith("Due")) { e.CellStyle.ForeColor = Th.Yel; e.CellStyle.Font = Th.FB; }
                else if (st.StartsWith("Overdue")) { e.CellStyle.ForeColor = Th.Red; e.CellStyle.Font = Th.FB; }
                else if (st.StartsWith("Weeded")) { e.CellStyle.ForeColor = Th.Mut; }
            };
            catGrid.ColumnHeaderMouseClick += (s, e) => {
                if (e.ColumnIndex == 0) catSort = 3;
                else if (e.ColumnIndex == 2) catSort = 5;
                else if (e.ColumnIndex == 3) catSort = catSort == 0 ? 1 : 0;
                else if (e.ColumnIndex == 4) catSort = 2;
                else if (e.ColumnIndex == 5) catSort = 4;
                if (cbSort.SelectedIndex != catSort && catSort < cbSort.Items.Count) cbSort.SelectedIndex = catSort;
                FilterCat();
            };
            var btns = new[] {
                Ui.Btn("Edit", 1, (s, e) => { var i = Ui.Sel<Item>(catGrid); if (i == null) Toast("Select a record first"); else ItemForm(i); }),
                Ui.Btn("Weed", 1, (s, e) => { var i = Ui.Sel<Item>(catGrid); if (i == null) Toast("Select a record first"); else WeedDialog(i); }),
                Ui.Btn("MARC", 1, (s, e) => { var i = Ui.Sel<Item>(catGrid); if (i == null) Toast("Select a record first"); else MarcView(i); }),
                Ui.Btn("Label", 1, (s, e) => { var i = Ui.Sel<Item>(catGrid); if (i == null) Toast("Select a record first"); else LabelDialog(i); }),
                Ui.Btn("Spine label", 1, (s, e) => { var i = Ui.Sel<Item>(catGrid); if (i == null) Toast("Select a record first"); else SpineDialog(i); }),
                Ui.Btn("Delete", 2, (s, e) => { var i = Ui.Sel<Item>(catGrid); if (i == null) Toast("Select a record first"); else DelItem(i); })
            };
            var body = WithButtons(catGrid, btns);
            var card = Ui.Card("Records", body, 1);
            return Ui.Stack(new Control[] { Ui.Heading("Catalog", "Bibliographic records with MARC 21 fields and weeding control"), tool, catNote, card }, 3);
        }
        List<Item> shown = new List<Item>();
        void FilterCat()
        {
            if (catGrid == null || catGrid.IsDisposed) return; string q = (catQ ?? "").ToLowerInvariant();
            var all = db.Items.Where(i => i.All().ToLowerInvariant().Contains(q));
            if (catFilter == "weeded") shown = all.Where(i => !string.IsNullOrEmpty(i.weeded)).ToList();
            else if (catFilter == "all") shown = all.ToList();
            else shown = all.Where(i => string.IsNullOrEmpty(i.weeded)).ToList();

            switch (catSort)
            {
                case 0: shown = shown.OrderBy(i => i.title ?? "", StringComparer.OrdinalIgnoreCase).ToList(); break;
                case 1: shown = shown.OrderByDescending(i => i.title ?? "", StringComparer.OrdinalIgnoreCase).ToList(); break;
                case 2: shown = shown.OrderBy(i => i.author ?? "", StringComparer.OrdinalIgnoreCase).ThenBy(i => i.title ?? "", StringComparer.OrdinalIgnoreCase).ToList(); break;
                case 3: shown = shown.OrderBy(i => i.barcode ?? "", StringComparer.OrdinalIgnoreCase).ToList(); break;
                case 4: shown = shown.OrderBy(i => i.call ?? "", StringComparer.OrdinalIgnoreCase).ToList(); break;
                case 5: shown = shown.OrderBy(i => i.acq ?? "", StringComparer.OrdinalIgnoreCase).ToList(); break;
                case 6: shown.Reverse(); break;
                default: break;
            }

            var rows = shown.Take(LIM).Select(i => {
                string status;
                if (!string.IsNullOrEmpty(i.weeded)) status = "Weeded (" + (string.IsNullOrEmpty(i.weed_reason) ? i.weeded : i.weed_reason) + ")";
                else { var l = lib.OpenLoan(i.id); status = l != null ? "Due " + l.due : "Available"; }
                return new[] { i.barcode, i.ctl, i.acq, i.title, i.author, i.call, status };
            }).ToList();
            Ui.Fill(catGrid, new[] { "Barcode", "Control no.", "Acq. no.", "Title", "Author", "Call no.", "Status" }, rows, shown.Take(LIM).Cast<object>().ToList(), "No records found");
            string countMsg = shown.Count > LIM ? "Showing the first " + LIM + " of " + shown.Count + " record(s)" : shown.Count + " record(s)";
            int onLoanCount = shown.Count(i => string.IsNullOrEmpty(i.weeded) && lib.OpenLoan(i.id) != null);
            int availCount = shown.Count(i => string.IsNullOrEmpty(i.weeded) && lib.OpenLoan(i.id) == null);
            countMsg += " \u00B7 " + availCount + " available \u00B7 " + onLoanCount + " on loan";
            int weededTotal = db.Items.Count(i => !string.IsNullOrEmpty(i.weeded));
            if (catFilter == "active" && weededTotal > 0) countMsg += " \u00B7 " + weededTotal + " weeded item(s) hidden";
            else if (catFilter == "weeded") countMsg += " (weeded / withdrawn from circulation)";
            catNote.Text = countMsg;
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
        int patSort = 0;
        Control PatPage()
        {
            var q = Ui.Txt(240); q.Text = patQ; q.Margin = Ui.S(new Padding(0, 0, 14, 0)); q.TextChanged += (s, e) => { patQ = q.Text; debT.Stop(); debT.Start(); };
            var bar = Ui.Flow(false); bar.Controls.Add(Ui.Field("Search", q, 260));
            var cbSort = Ui.Combo(180, "Name (A-Z)", "Name (Z-A)", "Class / Grade", "IC / ID Number", "Recently Registered", "Most Loans", "Highest Fines");
            cbSort.SelectedIndex = patSort;
            cbSort.SelectedIndexChanged += (s, e) => { patSort = cbSort.SelectedIndex; FilterPat(); };
            bar.Controls.Add(Ui.Field("Sort by", cbSort, 195));
            var bf = Ui.Flow(false); bf.Padding = Ui.S(new Padding(0, 18, 0, 0)); bf.Controls.Add(Ui.Btn("+ Register student", 0, (s, e) => PatForm())); bar.Controls.Add(bf);
            patGrid = Ui.Grid(); patNote = Ui.Lbl("", true);
            patGrid.CellDoubleClick += (s, e) => { var p = Ui.Sel<Patron>(patGrid); if (p != null) PatView(p); };
            patGrid.CellFormatting += (s, e) => {
                if (e.RowIndex < 0 || e.Value == null) return;
                if (e.ColumnIndex == 4 && e.Value.ToString() != "0") { e.CellStyle.ForeColor = Th.Yel; e.CellStyle.Font = Th.FB; }
                if (e.ColumnIndex == 5 && e.Value.ToString() != Mn(0)) { e.CellStyle.ForeColor = Th.Red; e.CellStyle.Font = Th.FB; }
            };
            patGrid.ColumnHeaderMouseClick += (s, e) => {
                if (e.ColumnIndex == 0) patSort = 3;
                else if (e.ColumnIndex == 1) patSort = patSort == 0 ? 1 : 0;
                else if (e.ColumnIndex == 2) patSort = 2;
                else if (e.ColumnIndex == 3) patSort = 4;
                else if (e.ColumnIndex == 4) patSort = 5;
                else if (e.ColumnIndex == 5) patSort = 6;
                if (cbSort.SelectedIndex != patSort && patSort < cbSort.Items.Count) cbSort.SelectedIndex = patSort;
                FilterPat();
            };
            var body = WithButtons(patGrid,
                Ui.Btn("Account", 1, (s, e) => { var p = Ui.Sel<Patron>(patGrid); if (p == null) Toast("Select a student first"); else PatView(p); }),
                Ui.Btn("Edit", 1, (s, e) => { var p = Ui.Sel<Patron>(patGrid); if (p == null) Toast("Select a student first"); else PatForm(p); }),
                Ui.Btn("Print library card", 0, (s, e) => { var p = Ui.Sel<Patron>(patGrid); if (p == null) Toast("Select a student first"); else CardDialog(p); }),
                Ui.Btn("Print cards for list", 1, (s, e) => PrintCards()),
                Ui.Btn("Delete", 2, (s, e) => { var p = Ui.Sel<Patron>(patGrid); if (p == null) Toast("Select a student first"); else DelPat(p); }));
            return Ui.Stack(new Control[] { Ui.Heading("Students", "Register students with their IC number, print library cards and manage accounts. Records are deleted after " + Lib.RET_YEARS + " years of inactivity."), Ui.Card(null, bar), patNote, Ui.Card("Students", body, 1) }, 3);
        }
        void FilterPat()
        {
            if (patGrid == null || patGrid.IsDisposed) return; string q = (patQ ?? "").ToLowerInvariant();
            var r = db.Patrons.Where(p => (p.id + p.name + p.cls).ToLowerInvariant().Contains(q) || (Lib.NormIc(q) != null && Lib.NormIc(p.id) == Lib.NormIc(q))).ToList();

            switch (patSort)
            {
                case 0: r = r.OrderBy(p => p.name ?? "", StringComparer.OrdinalIgnoreCase).ToList(); break;
                case 1: r = r.OrderByDescending(p => p.name ?? "", StringComparer.OrdinalIgnoreCase).ToList(); break;
                case 2: r = r.OrderBy(p => p.cls ?? "", StringComparer.OrdinalIgnoreCase).ThenBy(p => p.name ?? "", StringComparer.OrdinalIgnoreCase).ToList(); break;
                case 3: r = r.OrderBy(p => p.id ?? "", StringComparer.OrdinalIgnoreCase).ToList(); break;
                case 4: r = r.OrderByDescending(p => p.reg ?? "").ToList(); break;
                case 5: r = r.OrderByDescending(p => db.Loans.Count(l => l.pid == p.id && l.ret == null)).ThenBy(p => p.name ?? "").ToList(); break;
                case 6: r = r.OrderByDescending(p => lib.Unpaid(p.id)).ThenBy(p => p.name ?? "").ToList(); break;
                default: break;
            }

            shownPat = r;
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
            g.CellFormatting += (s, e) => {
                if (e.RowIndex < 0 || e.Value == null) return;
                if (e.ColumnIndex == 5) {
                    string fineStatus = e.Value.ToString();
                    if (fineStatus == "Unpaid") { e.CellStyle.ForeColor = Th.Red; e.CellStyle.Font = Th.FB; }
                    else if (fineStatus == "Paid") { e.CellStyle.ForeColor = Th.Green; e.CellStyle.Font = Th.FB; }
                    else if (fineStatus == "Waived") { e.CellStyle.ForeColor = Th.Mut; }
                }
            };
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
            for (int i = 5; i >= 0; i--) { var d = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1).AddMonths(-i); string k = d.ToString("yyyy-MM", CultureInfo.InvariantCulture); mo.Add(new KeyValuePair<string, double>(d.ToString("MMM", CultureInfo.InvariantCulture), db.Loans.Count(l => l.@out != null && l.@out.StartsWith(k)))); }
            var cl = new Dictionary<string, int>(); foreach (var l in db.Loans) { var k = lib.PClass(l.pid); if (k == "") k = "(No class)"; cl[k] = (cl.ContainsKey(k) ? cl[k] : 0) + 1; }

            int activeLoans = db.Loans.Count(l => l.ret == null);
            int overdueLoans = db.Loans.Count(lib.Overdue);
            int activeCopies = db.Items.Count(i => !lib.IsWeeded(i));
            int weedTot = db.Items.Count(i => !string.IsNullOrEmpty(i.weeded));
            var borrowedPids = new HashSet<string>(db.Loans.Select(l => l.pid));
            int activeBorrowers = borrowedPids.Count;
            int totStudents = db.Patrons.Count;
            double totFines = db.Fines.Sum(f => f.amt);
            double unpaidFines = db.Fines.Where(f => f.paid == "").Sum(f => f.amt);
            int late = db.Loans.Count(l => l.ret != null && string.CompareOrdinal(l.ret, l.due) > 0), rr = db.Loans.Count(l => l.ret != null);

            var st = Ui.Flow();
            st.Controls.Add(Ui.Stat(activeCopies.ToString(), "Active collection", Th.Green));
            st.Controls.Add(Ui.Stat(activeLoans.ToString(), "Books on loan", Th.Blue));
            st.Controls.Add(Ui.Stat(overdueLoans + (activeLoans > 0 ? " (" + (overdueLoans * 100 / activeLoans) + "%)" : ""), "Overdue items", overdueLoans > 0 ? Th.Red : Th.Green));
            st.Controls.Add(Ui.Stat(activeBorrowers + (totStudents > 0 ? " (" + (activeBorrowers * 100 / totStudents) + "%)" : ""), "Active readers", Th.Blue));
            st.Controls.Add(Ui.Stat(rr > 0 ? Math.Round(late * 100.0 / rr) + "%" : "0%", "Returned late", Th.Yel));
            st.Controls.Add(Ui.Stat(Mn(unpaidFines), "Unpaid fines", unpaidFines > 0 ? Th.Red : Th.Green));
            st.Controls.Add(Ui.Stat(weedTot.ToString(), "Weeded items", Th.Mut));

            Func<List<KeyValuePair<string, double>>, bool, BarChart> chart = (d, v) => new BarChart { Data = d, Vertical = v };

            // Reading champions (Top students)
            var pLoans = new Dictionary<string, int>();
            foreach (var l in db.Loans) pLoans[l.pid] = (pLoans.ContainsKey(l.pid) ? pLoans[l.pid] : 0) + 1;
            int rank = 1;
            var topReaders = pLoans.OrderByDescending(x => x.Value).Take(10).Select(x => {
                var p = lib.PatronById(x.Key);
                return new[] { (rank++).ToString(), p != null ? p.name : "(removed)", p != null ? p.cls : "", x.Value.ToString() };
            }).ToList();
            var readerGrid = MakeGrid(new[] { "Rank", "Student", "Class", "Books read" }, topReaders, null, "No borrowing records yet");
            if (readerGrid.Columns.Count > 0) readerGrid.Columns[0].FillWeight = 25;

            // Collection by Dewey Decimal classification
            var deweyMap = new Dictionary<string, int>();
            string[] deweyLabels = new[] { "000 Comp & Info", "100 Philosophy", "200 Religion", "300 Social Sci", "400 Languages", "500 Pure Sci", "600 Tech & Med", "700 Arts & Rec", "800 Literature", "900 History & Geo", "Fiction", "Other" };
            foreach (var dl in deweyLabels) deweyMap[dl] = 0;
            var loanedIids = new HashSet<string>(db.Loans.Select(l => l.iid));
            int dormantCount = 0;
            foreach (var it in db.Items)
            {
                if (!lib.IsWeeded(it))
                {
                    if (!loanedIids.Contains(it.id)) dormantCount++;
                    string cat = ClassifyDewey(it);
                    deweyMap[cat] = (deweyMap.ContainsKey(cat) ? deweyMap[cat] : 0) + 1;
                }
            }
            var deweyData = deweyMap.Where(x => x.Value > 0).Select(x => new KeyValuePair<string, double>(x.Key, x.Value)).ToList();

            // Students with unpaid fines
            var unpaid = db.Patrons.Select(p => new { p.name, p.cls, v = lib.Unpaid(p.id) }).Where(x => x.v > 0).OrderByDescending(x => x.v).Select(x => new[] { x.name, x.cls, Mn(x.v) }).ToList();
            var finesGrid = MakeGrid(new[] { "Student", "Class", "Owed" }, unpaid, null, "No unpaid fines \U0001F389");

            // Weeding breakdown
            int wDamaged = db.Items.Count(i => !string.IsNullOrEmpty(i.weeded) && (i.weed_reason ?? "").IndexOf("condition", StringComparison.OrdinalIgnoreCase) >= 0);
            int wObsolete = db.Items.Count(i => !string.IsNullOrEmpty(i.weeded) && (i.weed_reason ?? "").IndexOf("obsolete", StringComparison.OrdinalIgnoreCase) >= 0);
            int wLost = db.Items.Count(i => !string.IsNullOrEmpty(i.weeded) && (i.weed_reason ?? "").IndexOf("lost", StringComparison.OrdinalIgnoreCase) >= 0);
            int wDup = db.Items.Count(i => !string.IsNullOrEmpty(i.weeded) && (i.weed_reason ?? "").IndexOf("duplicate", StringComparison.OrdinalIgnoreCase) >= 0);
            int wOther = weedTot - (wDamaged + wObsolete + wLost + wDup);
            if (wOther < 0) wOther = 0;

            Func<string, int, Control> para = (t, h) => new Label { UseMnemonic = false, Text = t, AutoSize = false, Size = new Size(Ui.S(820), Ui.S(h)), ForeColor = Th.Ink };

            var healthBox = Vbox(
                para(string.Format("Dormant collection: {0} active items ({1}% of active catalog) have never been borrowed. Use this to identify candidates for reading displays or collection review.",
                    dormantCount, activeCopies > 0 ? (dormantCount * 100 / activeCopies) : 0), 40),
                para(string.Format("Weeded collection ({0} copies withdrawn): Condition / Damaged: {1} \u00B7 Obsolete / Outdated: {2} \u00B7 Lost / Missing: {3} \u00B7 Duplicate: {4}{5}",
                    weedTot, wDamaged, wObsolete, wLost, wDup, wOther > 0 ? " \u00B7 Other: " + wOther : ""), 38));

            var topRow = Ui.Flow(false);
            topRow.Controls.Add(Ui.Btn("Export executive report (HTML)", 0, (s, e) => ExportReportsHtml()));
            topRow.Controls.Add(Ui.Btn("Export spreadsheet (CSV)", 1, (s, e) => ExportReportsCsv()));

            return ScrollPage(
                Ui.Heading("Reports", "Circulation, collection and fine analytics"),
                topRow,
                st,
                Cols(230, Ui.Card("Checkouts, last 6 months", chart(mo, true), 1), Ui.Card("Most borrowed titles", chart(top, false), 1)),
                Cols(250, Ui.Card("Reading champions (top readers)", readerGrid, 1), Ui.Card("Checkouts by class", chart(cl.OrderByDescending(x => x.Value).Take(8).Select(x => new KeyValuePair<string, double>(x.Key, x.Value)).ToList(), false), 1)),
                Cols(250, Ui.Card("Collection by subject / Dewey range", chart(deweyData, false), 1), Ui.Card("Students with unpaid fines", finesGrid, 1)),
                Ui.Card("Collection health & weeding maintenance", healthBox));
        }

        static string ClassifyDewey(Item it)
        {
            string call = (it.call ?? "").Trim();
            if (call.Length > 0 && char.IsDigit(call[0]))
            {
                switch (call[0])
                {
                    case '0': return "000 Comp & Info";
                    case '1': return "100 Philosophy";
                    case '2': return "200 Religion";
                    case '3': return "300 Social Sci";
                    case '4': return "400 Languages";
                    case '5': return "500 Pure Sci";
                    case '6': return "600 Tech & Med";
                    case '7': return "700 Arts & Rec";
                    case '8': return "800 Literature";
                    case '9': return "900 History & Geo";
                }
            }
            if (call.IndexOf("fic", StringComparison.OrdinalIgnoreCase) >= 0 ||
                call.IndexOf("cerita", StringComparison.OrdinalIgnoreCase) >= 0 ||
                call.IndexOf("novel", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Fiction";
            return "Other";
        }

        static string HtmlEscape(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
        }

        void ExportReportsHtml()
        {
            using (var d = new SaveFileDialog { FileName = "errin-report-" + Lib.Today() + ".html", Filter = "HTML document (*.html)|*.html" })
            {
                if (d.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    int activeLoans = db.Loans.Count(l => l.ret == null);
                    int overdueLoans = db.Loans.Count(lib.Overdue);
                    int activeCopies = db.Items.Count(i => !lib.IsWeeded(i));
                    int weedTot = db.Items.Count(i => !string.IsNullOrEmpty(i.weeded));
                    var borrowedPids = new HashSet<string>(db.Loans.Select(l => l.pid));
                    int activeBorrowers = borrowedPids.Count;
                    int totStudents = db.Patrons.Count;
                    double totFines = db.Fines.Sum(f => f.amt);
                    double paidFines = db.Fines.Where(f => f.paid == "paid").Sum(f => f.amt);
                    double unpaidFines = db.Fines.Where(f => f.paid == "").Sum(f => f.amt);

                    var sb = new StringBuilder();
                    sb.AppendLine("<!DOCTYPE html>");
                    sb.AppendLine("<html lang=\"en\">");
                    sb.AppendLine("<head>");
                    sb.AppendLine("<meta charset=\"utf-8\">");
                    sb.AppendLine("<title>ErrinILS Library Executive Report - " + HtmlEscape(db.Set.school) + "</title>");
                    sb.AppendLine("<style>");
                    sb.AppendLine("body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; background: #f1f5f9; color: #1e293b; margin: 0; padding: 28px; line-height: 1.5; }");
                    sb.AppendLine(".wrap { max-width: 1060px; margin: 0 auto; background: #ffffff; padding: 36px 40px; border-radius: 12px; box-shadow: 0 4px 6px -1px rgba(0,0,0,0.07), 0 2px 4px -2px rgba(0,0,0,0.07); border: 1px solid #e2e8f0; }");
                    sb.AppendLine(".header { display: flex; justify-content: space-between; align-items: flex-start; border-bottom: 2px solid #e2e8f0; padding-bottom: 20px; margin-bottom: 24px; }");
                    sb.AppendLine(".title { font-size: 26px; font-weight: 800; color: #0f172a; margin: 0 0 6px 0; }");
                    sb.AppendLine(".sub { font-size: 13.5px; color: #64748b; margin: 0; }");
                    sb.AppendLine(".school-badge { background: #eff6ff; color: #1d4ed8; padding: 6px 14px; border-radius: 9999px; font-weight: 700; font-size: 13px; border: 1px solid #bfdbfe; }");
                    sb.AppendLine(".kpi-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(180px, 1fr)); gap: 14px; margin-bottom: 30px; }");
                    sb.AppendLine(".kpi-card { background: #f8fafc; border: 1px solid #e2e8f0; border-radius: 10px; padding: 18px 16px; text-align: center; position: relative; overflow: hidden; }");
                    sb.AppendLine(".kpi-card::before { content: ''; position: absolute; top: 0; left: 0; right: 0; height: 4px; background: #3b82f6; }");
                    sb.AppendLine(".kpi-card.warn::before { background: #f59e0b; }");
                    sb.AppendLine(".kpi-card.danger::before { background: #ef4444; }");
                    sb.AppendLine(".kpi-card.success::before { background: #10b981; }");
                    sb.AppendLine(".kpi-val { font-size: 26px; font-weight: 800; color: #0f172a; margin-bottom: 4px; }");
                    sb.AppendLine(".kpi-lbl { font-size: 11.5px; font-weight: 600; color: #64748b; text-transform: uppercase; letter-spacing: 0.5px; }");
                    sb.AppendLine("h2 { font-size: 17px; font-weight: 700; color: #0f172a; margin: 30px 0 12px 0; padding-bottom: 8px; border-bottom: 1px solid #e2e8f0; }");
                    sb.AppendLine("table { width: 100%; border-collapse: collapse; font-size: 13.5px; margin-bottom: 22px; }");
                    sb.AppendLine("th { background: #f1f5f9; color: #475569; font-weight: 700; text-align: left; padding: 10px 12px; border: 1px solid #e2e8f0; }");
                    sb.AppendLine("td { padding: 9px 12px; border: 1px solid #e2e8f0; vertical-align: middle; }");
                    sb.AppendLine("tr:nth-child(even) td { background: #f8fafc; }");
                    sb.AppendLine(".badge { display: inline-block; padding: 2px 8px; border-radius: 6px; font-size: 11px; font-weight: 700; }");
                    sb.AppendLine(".badge-rank1 { background: #fef3c7; color: #b45309; border: 1px solid #fde68a; }");
                    sb.AppendLine(".badge-rank2 { background: #f1f5f9; color: #475569; border: 1px solid #cbd5e1; }");
                    sb.AppendLine(".badge-rank3 { background: #ffedd5; color: #c2410c; border: 1px solid #fed7aa; }");
                    sb.AppendLine(".badge-danger { background: #fee2e2; color: #b91c1c; border: 1px solid #fecaca; }");
                    sb.AppendLine(".badge-ok { background: #d1fae5; color: #047857; border: 1px solid #a7f3d0; }");
                    sb.AppendLine(".bar-container { display: flex; align-items: center; gap: 8px; }");
                    sb.AppendLine(".bar-bg { flex: 1; height: 10px; background: #e2e8f0; border-radius: 5px; overflow: hidden; }");
                    sb.AppendLine(".bar-fill { height: 100%; background: #3b82f6; border-radius: 5px; }");
                    sb.AppendLine(".no-print { margin-bottom: 18px; }");
                    sb.AppendLine(".btn-print { background: #0f172a; color: #ffffff; border: none; padding: 10px 20px; border-radius: 6px; font-weight: 600; cursor: pointer; font-size: 14px; }");
                    sb.AppendLine("@media print { body { background: #ffffff; padding: 0; } .wrap { border: none; box-shadow: none; padding: 0; max-width: 100%; } .no-print { display: none !important; } }");
                    sb.AppendLine("</style>");
                    sb.AppendLine("</head>");
                    sb.AppendLine("<body>");
                    sb.AppendLine("<div class=\"wrap\">");
                    sb.AppendLine("<div class=\"no-print\"><button class=\"btn-print\" onclick=\"window.print()\">Print Report / Save as PDF</button></div>");

                    // Header
                    sb.AppendLine("<div class=\"header\">");
                    sb.AppendLine("<div><div class=\"title\">Library Circulation & Collection Report</div><div class=\"sub\">Generated on " + DateTime.Now.ToString("dddd, MMMM dd, yyyy \u00B7 HH:mm:ss") + "</div></div>");
                    sb.AppendLine("<div class=\"school-badge\">" + HtmlEscape(string.IsNullOrEmpty(db.Set.school) ? "ErrinILS School Library" : db.Set.school) + "</div>");
                    sb.AppendLine("</div>");

                    // KPIs
                    sb.AppendLine("<div class=\"kpi-grid\">");
                    sb.AppendLine("<div class=\"kpi-card\"><div class=\"kpi-val\">" + activeCopies + "</div><div class=\"kpi-lbl\">Active Copies (" + weedTot + " weeded)</div></div>");
                    sb.AppendLine("<div class=\"kpi-card\"><div class=\"kpi-val\">" + totStudents + "</div><div class=\"kpi-lbl\">Registered Students</div></div>");
                    sb.AppendLine("<div class=\"kpi-card success\"><div class=\"kpi-val\">" + activeBorrowers + (totStudents > 0 ? " (" + (activeBorrowers * 100 / totStudents) + "%)" : "") + "</div><div class=\"kpi-lbl\">Active Readers</div></div>");
                    sb.AppendLine("<div class=\"kpi-card warn\"><div class=\"kpi-val\">" + activeLoans + "</div><div class=\"kpi-lbl\">Books on Loan</div></div>");
                    sb.AppendLine("<div class=\"kpi-card danger\"><div class=\"kpi-val\">" + overdueLoans + (activeLoans > 0 ? " (" + (overdueLoans * 100 / activeLoans) + "%)" : "") + "</div><div class=\"kpi-lbl\">Overdue Items</div></div>");
                    sb.AppendLine("<div class=\"kpi-card danger\"><div class=\"kpi-val\">" + Lib.Money(unpaidFines) + "</div><div class=\"kpi-lbl\">Unpaid Fines</div></div>");
                    sb.AppendLine("</div>");

                    // Checkouts by month
                    sb.AppendLine("<h2>Checkouts by Month (Last 6 Months)</h2>");
                    sb.AppendLine("<table><thead><tr><th>Month</th><th style=\"width:60%;\">Checkouts Trend</th><th>Total</th></tr></thead><tbody>");
                    int maxMo = 1;
                    var moData = new List<KeyValuePair<string, int>>();
                    for (int i = 5; i >= 0; i--)
                    {
                        var dt = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1).AddMonths(-i);
                        string k = dt.ToString("yyyy-MM", CultureInfo.InvariantCulture);
                        int count = db.Loans.Count(l => l.@out != null && l.@out.StartsWith(k));
                        if (count > maxMo) maxMo = count;
                        moData.Add(new KeyValuePair<string, int>(dt.ToString("MMMM yyyy", CultureInfo.InvariantCulture), count));
                    }
                    foreach (var kv in moData)
                    {
                        int pct = maxMo > 0 ? (kv.Value * 100 / maxMo) : 0;
                        sb.AppendLine(string.Format("<tr><td><strong>{0}</strong></td><td><div class=\"bar-container\"><div class=\"bar-bg\"><div class=\"bar-fill\" style=\"width:{1}%;\"></div></div><span>{2}</span></div></td><td><strong>{2}</strong></td></tr>",
                            HtmlEscape(kv.Key), pct, kv.Value));
                    }
                    sb.AppendLine("</tbody></table>");

                    // Reading champions
                    sb.AppendLine("<h2>Reading Champions (Top Students)</h2>");
                    sb.AppendLine("<table><thead><tr><th>Rank</th><th>Student Name</th><th>Class</th><th>IC / Student ID</th><th>Books Borrowed</th></tr></thead><tbody>");
                    var pLoans = new Dictionary<string, int>();
                    foreach (var l in db.Loans) pLoans[l.pid] = (pLoans.ContainsKey(l.pid) ? pLoans[l.pid] : 0) + 1;
                    int rnk = 1;
                    foreach (var kv in pLoans.OrderByDescending(x => x.Value).Take(20))
                    {
                        var p = lib.PatronById(kv.Key);
                        string rankBadge = rnk == 1 ? "<span class=\"badge badge-rank1\">#1 Gold</span>" :
                                          rnk == 2 ? "<span class=\"badge badge-rank2\">#2 Silver</span>" :
                                          rnk == 3 ? "<span class=\"badge badge-rank3\">#3 Bronze</span>" : "#" + rnk;
                        sb.AppendLine(string.Format("<tr><td>{0}</td><td><strong>{1}</strong></td><td>{2}</td><td><code>{3}</code></td><td><strong>{4} books</strong></td></tr>",
                            rankBadge, HtmlEscape(p != null ? p.name : "(removed)"), HtmlEscape(p != null ? p.cls : ""), HtmlEscape(kv.Key), kv.Value));
                        rnk++;
                    }
                    sb.AppendLine("</tbody></table>");

                    // Top borrowed books
                    sb.AppendLine("<h2>Most Borrowed Books</h2>");
                    sb.AppendLine("<table><thead><tr><th>#</th><th>Title</th><th>Author</th><th>Call No.</th><th>Barcode</th><th>Checkouts</th></tr></thead><tbody>");
                    var c = new Dictionary<string, int>();
                    foreach (var l in db.Loans) c[l.iid] = (c.ContainsKey(l.iid) ? c[l.iid] : 0) + 1;
                    rnk = 1;
                    foreach (var kv in c.OrderByDescending(x => x.Value).Take(20))
                    {
                        var it = db.Items.FirstOrDefault(i => i.id == kv.Key);
                        sb.AppendLine(string.Format("<tr><td>{0}</td><td><strong>{1}</strong></td><td>{2}</td><td><code>{3}</code></td><td><code>{4}</code></td><td><strong>{5}</strong></td></tr>",
                            rnk++, HtmlEscape(it != null ? it.title : "(deleted)"), HtmlEscape(it != null ? it.author : ""), HtmlEscape(it != null ? it.call : ""), HtmlEscape(it != null ? it.barcode : ""), kv.Value));
                    }
                    sb.AppendLine("</tbody></table>");

                    // Circulation by class
                    sb.AppendLine("<h2>Circulation by Class</h2>");
                    sb.AppendLine("<table><thead><tr><th>Class / Grade</th><th>Registered Students</th><th>Total Checkouts</th><th>Active Loans</th></tr></thead><tbody>");
                    var classGroups = db.Patrons.GroupBy(p => string.IsNullOrEmpty(p.cls) ? "(No class)" : p.cls).OrderBy(g => g.Key);
                    foreach (var g in classGroups)
                    {
                        var pids = new HashSet<string>(g.Select(p => p.id));
                        int totL = db.Loans.Count(l => pids.Contains(l.pid));
                        int actL = db.Loans.Count(l => l.ret == null && pids.Contains(l.pid));
                        sb.AppendLine(string.Format("<tr><td><strong>{0}</strong></td><td>{1}</td><td>{2}</td><td>{3}</td></tr>",
                            HtmlEscape(g.Key), g.Count(), totL, actL));
                    }
                    sb.AppendLine("</tbody></table>");

                    // Current overdue items
                    sb.AppendLine("<h2>Current Overdue Items (" + overdueLoans + ")</h2>");
                    if (overdueLoans == 0)
                    {
                        sb.AppendLine("<p style=\"color: #047857; font-weight: 600;\">No overdue items currently! All borrowed books are on time.</p>");
                    }
                    else
                    {
                        sb.AppendLine("<table><thead><tr><th>Student</th><th>Class</th><th>Contact</th><th>Title</th><th>Barcode</th><th>Due Date</th><th>Days Late</th><th>Accrued Fine</th></tr></thead><tbody>");
                        foreach (var l in db.Loans.Where(lib.Overdue).OrderBy(l => l.due))
                        {
                            var it = db.Items.FirstOrDefault(x => x.id == l.iid);
                            var p = lib.PatronById(l.pid);
                            int lateDays = Lib.Diff(Lib.Today(), l.due);
                            double fineAmt = lateDays * db.Set.rate;
                            sb.AppendLine(string.Format("<tr><td><strong>{0}</strong></td><td>{1}</td><td>{2}</td><td>{3}</td><td><code>{4}</code></td><td>{5}</td><td><span class=\"badge badge-danger\">{6} days</span></td><td><strong>{7}</strong></td></tr>",
                                HtmlEscape(p != null ? p.name : "(removed)"), HtmlEscape(p != null ? p.cls : ""), HtmlEscape(p != null ? p.contact : ""), HtmlEscape(it != null ? it.title : ""), HtmlEscape(it != null ? it.barcode : ""), l.due, lateDays, Lib.Money(fineAmt)));
                        }
                        sb.AppendLine("</tbody></table>");
                    }

                    // Unpaid fines
                    var debtors = db.Patrons.Select(x => new { pat = x, owed = lib.Unpaid(x.id) }).Where(x => x.owed > 0).OrderByDescending(x => x.owed).ToList();
                    sb.AppendLine("<h2>Outstanding Unpaid Fines (" + Lib.Money(unpaidFines) + ")</h2>");
                    if (debtors.Count == 0)
                    {
                        sb.AppendLine("<p style=\"color: #047857; font-weight: 600;\">All library fines have been settled.</p>");
                    }
                    else
                    {
                        sb.AppendLine("<table><thead><tr><th>Student Name</th><th>Class</th><th>Contact</th><th>Outstanding Fine</th></tr></thead><tbody>");
                        foreach (var p in debtors)
                        {
                            sb.AppendLine(string.Format("<tr><td><strong>{0}</strong></td><td>{1}</td><td>{2}</td><td><strong style=\"color:#b91c1c;\">{3}</strong></td></tr>",
                                HtmlEscape(p.pat.name), HtmlEscape(p.pat.cls), HtmlEscape(p.pat.contact), Lib.Money(p.owed)));
                        }
                        sb.AppendLine("</tbody></table>");
                    }

                    sb.AppendLine("<div style=\"text-align: center; color: #94a3b8; font-size: 12px; margin-top: 36px; border-top: 1px solid #e2e8f0; padding-top: 16px;\">Generated by ErrinILS Library Management System \u00B7 Open Source School ILS</div>");
                    sb.AppendLine("</div>");
                    sb.AppendLine("</body>");
                    sb.AppendLine("</html>");

                    File.WriteAllText(d.FileName, sb.ToString(), Encoding.UTF8);
                    Toast("Report exported successfully");
                    try { Process.Start(d.FileName); } catch { }
                }
                catch (Exception ex)
                {
                    Toast("Export failed: " + ex.Message);
                }
            }
        }

        void ExportReportsCsv()
        {
            using (var d = new SaveFileDialog { FileName = "errin-report-" + Lib.Today() + ".csv", Filter = "CSV spreadsheet (*.csv)|*.csv" })
            {
                if (d.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    var sb = new StringBuilder();
                    sb.AppendLine("=== ERRINILS LIBRARY SYSTEM REPORT ===");
                    sb.AppendLine("Generated on," + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                    sb.AppendLine("School," + EscapeCsv(db.Set.school));
                    sb.AppendLine();

                    int activeLoans = db.Loans.Count(l => l.ret == null);
                    int overdueLoans = db.Loans.Count(lib.Overdue);
                    int activeCopies = db.Items.Count(i => !lib.IsWeeded(i));
                    int weedTot = db.Items.Count(i => !string.IsNullOrEmpty(i.weeded));
                    var borrowedPids = new HashSet<string>(db.Loans.Select(l => l.pid));
                    int activeBorrowers = borrowedPids.Count;
                    int totStudents = db.Patrons.Count;
                    double totFines = db.Fines.Sum(f => f.amt);
                    double paidFines = db.Fines.Where(f => f.paid == "paid").Sum(f => f.amt);
                    double unpaidFines = db.Fines.Where(f => f.paid == "").Sum(f => f.amt);

                    sb.AppendLine("--- EXECUTIVE SUMMARY ---");
                    sb.AppendLine("Active Collection Copies," + activeCopies);
                    sb.AppendLine("Weeded / Withdrawn Copies," + weedTot);
                    sb.AppendLine("Total Registered Students," + totStudents);
                    sb.AppendLine("Active Student Readers," + activeBorrowers + (totStudents > 0 ? " (" + (activeBorrowers * 100 / totStudents) + "%)" : ""));
                    sb.AppendLine("Books on Loan Now," + activeLoans);
                    sb.AppendLine("Currently Overdue Items," + overdueLoans + (activeLoans > 0 ? " (" + (overdueLoans * 100 / activeLoans) + "%)" : ""));
                    sb.AppendLine("Total Fines Assessed," + Lib.Money(totFines));
                    sb.AppendLine("Total Fines Collected," + Lib.Money(paidFines));
                    sb.AppendLine("Outstanding Unpaid Fines," + Lib.Money(unpaidFines));
                    sb.AppendLine();

                    // Checkouts by month
                    sb.AppendLine("--- CHECKOUTS BY MONTH (LAST 6 MONTHS) ---");
                    sb.AppendLine("Month,Checkouts");
                    for (int i = 5; i >= 0; i--)
                    {
                        var dt = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1).AddMonths(-i);
                        string k = dt.ToString("yyyy-MM", CultureInfo.InvariantCulture);
                        sb.AppendLine(dt.ToString("MMM yyyy", CultureInfo.InvariantCulture) + "," + db.Loans.Count(l => l.@out != null && l.@out.StartsWith(k)));
                    }
                    sb.AppendLine();

                    // Top borrowed books
                    sb.AppendLine("--- TOP BORROWED BOOKS ---");
                    sb.AppendLine("Rank,Title,Author,Call Number,Barcode,Total Checkouts");
                    var c = new Dictionary<string, int>();
                    foreach (var l in db.Loans) c[l.iid] = (c.ContainsKey(l.iid) ? c[l.iid] : 0) + 1;
                    int rnk = 1;
                    foreach (var kv in c.OrderByDescending(x => x.Value).Take(25))
                    {
                        var it = db.Items.FirstOrDefault(i => i.id == kv.Key);
                        sb.AppendLine(rnk++ + "," + EscapeCsv(it != null ? it.title : "(deleted)") + "," +
                            EscapeCsv(it != null ? it.author : "") + "," +
                            EscapeCsv(it != null ? it.call : "") + "," +
                            EscapeCsv(it != null ? it.barcode : "") + "," +
                            kv.Value);
                    }
                    sb.AppendLine();

                    // Top student readers
                    sb.AppendLine("--- READING CHAMPIONS (TOP STUDENTS) ---");
                    sb.AppendLine("Rank,Student Name,IC / ID,Class,Total Books Borrowed");
                    var pLoans = new Dictionary<string, int>();
                    foreach (var l in db.Loans) pLoans[l.pid] = (pLoans.ContainsKey(l.pid) ? pLoans[l.pid] : 0) + 1;
                    rnk = 1;
                    foreach (var kv in pLoans.OrderByDescending(x => x.Value).Take(25))
                    {
                        var p = lib.PatronById(kv.Key);
                        sb.AppendLine(rnk++ + "," + EscapeCsv(p != null ? p.name : "(removed)") + "," +
                            EscapeCsv(kv.Key) + "," +
                            EscapeCsv(p != null ? p.cls : "") + "," +
                            kv.Value);
                    }
                    sb.AppendLine();

                    // Circulation by class
                    sb.AppendLine("--- CIRCULATION BY CLASS ---");
                    sb.AppendLine("Class,Students,Total Loans,Active Loans");
                    var classGroups = db.Patrons.GroupBy(p => string.IsNullOrEmpty(p.cls) ? "(No class)" : p.cls).OrderBy(g => g.Key);
                    foreach (var g in classGroups)
                    {
                        var pids = new HashSet<string>(g.Select(p => p.id));
                        int totL = db.Loans.Count(l => pids.Contains(l.pid));
                        int actL = db.Loans.Count(l => l.ret == null && pids.Contains(l.pid));
                        sb.AppendLine(EscapeCsv(g.Key) + "," + g.Count() + "," + totL + "," + actL);
                    }
                    sb.AppendLine();

                    // Overdue loans
                    sb.AppendLine("--- CURRENT OVERDUE ITEMS ---");
                    sb.AppendLine("Student,Class,Contact,Title,Barcode,Due Date,Days Overdue,Fine Accrued");
                    foreach (var l in db.Loans.Where(lib.Overdue).OrderBy(l => l.due))
                    {
                        var it = db.Items.FirstOrDefault(x => x.id == l.iid);
                        var p = lib.PatronById(l.pid);
                        int lateDays = Lib.Diff(Lib.Today(), l.due);
                        double fineAmt = lateDays * db.Set.rate;
                        sb.AppendLine(EscapeCsv(p != null ? p.name : "(removed)") + "," +
                            EscapeCsv(p != null ? p.cls : "") + "," +
                            EscapeCsv(p != null ? p.contact : "") + "," +
                            EscapeCsv(it != null ? it.title : "") + "," +
                            EscapeCsv(it != null ? it.barcode : "") + "," +
                            l.due + "," + lateDays + "," + Lib.Money(fineAmt));
                    }
                    sb.AppendLine();

                    // Unpaid fines
                    sb.AppendLine("--- UNPAID FINES ---");
                    sb.AppendLine("Student,Class,Contact,Unpaid Amount");
                    foreach (var p in db.Patrons.Select(x => new { pat = x, owed = lib.Unpaid(x.id) }).Where(x => x.owed > 0).OrderByDescending(x => x.owed))
                    {
                        sb.AppendLine(EscapeCsv(p.pat.name) + "," + EscapeCsv(p.pat.cls) + "," + EscapeCsv(p.pat.contact) + "," + Lib.Money(p.owed));
                    }

                    byte[] preamble = new UTF8Encoding(true).GetPreamble();
                    byte[] bytes = Encoding.UTF8.GetBytes(sb.ToString());
                    byte[] full = preamble.Concat(bytes).ToArray();
                    File.WriteAllBytes(d.FileName, full);
                    Toast("Report exported successfully");
                }
                catch (Exception ex)
                {
                    Toast("Export failed: " + ex.Message);
                }
            }
        }

        static string EscapeCsv(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            if (s.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0) return "\"" + s.Replace("\"", "\"\"") + "\"";
            return s;
        }

        /* ---------- Backup ---------- */
        Control BakPage()
        {
            var info = new Label { UseMnemonic = false, Text = "Download a backup of everything (students, loans, holds, fines, catalog), or restore from one. Keep a copy on a USB drive or cloud storage.", AutoSize = false, Size = new Size(Ui.S(760), Ui.S(44)), ForeColor = Th.Ink };
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
            Func<string, int, Control> para = (t, h) => new Label { UseMnemonic = false, Text = t, AutoSize = false, Size = new Size(Ui.S(820), Ui.S(h)), ForeColor = Th.Ink };
            var zE = Ui.Combo(150, "Off", "On"); var zP = Ui.Num(110, 1, 65535, zc.port); var zD = Ui.Txt(150); zD.Text = zc.db; var zB = Ui.Combo(170, "Other computers", "This computer only");
            zE.SelectedIndex = zc.enabled ? 1 : 0; zB.SelectedIndex = zc.bind == "local" ? 1 : 0;
            var zs = new Label { UseMnemonic = false, AutoSize = false, Size = new Size(Ui.S(820), Ui.S(70)), ForeColor = Th.Mut, Margin = Ui.S(new Padding(0, 10, 0, 0)) }; zStatusLabel = zs; ZShow();
            var zf = Ui.Flow(); zf.Controls.Add(Ui.Field("Server", zE, 160)); zf.Controls.Add(Ui.Field("Port", zP, 120)); zf.Controls.Add(Ui.Field("Database name", zD, 160)); zf.Controls.Add(Ui.Field("Reachable from", zB, 190));
            var zBody = Vbox(para("Lets other library systems and Z39.50 clients search this catalogue over the network. It is read-only and shares bibliographic records only: never students, loans or fines. Off by default.", 44), zf,
                Ui.Btn("Apply", 0, (a, b) => ZApply(zE.SelectedIndex == 1, (int)zP.Value, zD.Text.Trim(), zB.SelectedIndex == 1 ? "local" : "network")), zs);
            var pc = Ui.Txt(240, true); var pn = Ui.Txt(240, true); var pn2 = Ui.Txt(240, true);
            var pf = Ui.Flow(); pf.Controls.Add(Ui.Field("Current password", pc, 260)); pf.Controls.Add(Ui.Field("New password", pn, 260)); pf.Controls.Add(Ui.Field("Type the new password again", pn2, 260));
            var pwBody = Vbox(para("The password protects Settings, deleting records and restoring backups. Use at least 4 characters and keep it somewhere safe.", 44), pf,
                Ui.Btn("Change password", 0, (a, b) => { if (ChangePw(pc.Text, pn.Text, pn2.Text)) { pc.Text = ""; pn.Text = ""; pn2.Text = ""; } }));
            var wE = Ui.Combo(140, "Off", "On"); var wP = Ui.Num(110, 1, 65535, wc.port); var wB = Ui.Combo(160, "Other computers", "This computer only");
            var wSsl = Ui.Combo(170, "HTTP (Standard)", "HTTPS (TLS/SSL)");
            wE.SelectedIndex = wc.enabled ? 1 : 0; wB.SelectedIndex = wc.bind == "local" ? 1 : 0; wSsl.SelectedIndex = wc.ssl ? 1 : 0;
            var ws = new Label { UseMnemonic = false, AutoSize = false, Size = new Size(Ui.S(820), Ui.S(54)), ForeColor = Th.Mut, Margin = Ui.S(new Padding(0, 10, 0, 0)) };
            wStatusLabel = ws; WebShow();
            var wf = Ui.Flow(); wf.Controls.Add(Ui.Field("Server", wE, 150)); wf.Controls.Add(Ui.Field("Port", wP, 120)); wf.Controls.Add(Ui.Field("Reachable from", wB, 170)); wf.Controls.Add(Ui.Field("Security", wSsl, 180));
            var certBtns = Ui.Flow(false); certBtns.Margin = Ui.S(new Padding(0, 4, 0, 0));
            certBtns.Controls.Add(Ui.Btn("Regenerate TLS Cert", 1, (a, b) => {
                try {
                    var c = CertHelper.GenerateSelfSigned("CN=ErrinILS", 10, "errin");
                    byte[] pfx = c.Export(X509ContentType.Pfx, "errin");
                    File.WriteAllBytes(Path.Combine(store.Dir, "errin-ssl.pfx"), pfx);
                    wc.certPath = ""; wc.certPass = ""; SaveCfg(); ApplyWeb(); WebShow();
                    Toast("New self-signed TLS certificate generated");
                } catch (Exception ex) { Toast("Cert generation failed: " + ex.Message); }
            }));
            certBtns.Controls.Add(Ui.Btn("Choose Custom PFX...", 1, (a, b) => {
                using (var ofd = new OpenFileDialog { Filter = "PKCS#12 / PFX Certificate (*.pfx;*.p12)|*.pfx;*.p12|All files (*.*)|*.*", Title = "Choose TLS Certificate" }) {
                    if (ofd.ShowDialog(this) == DialogResult.OK) {
                        wc.certPath = ofd.FileName; SaveCfg(); ApplyWeb(); WebShow();
                        Toast("Custom certificate loaded");
                    }
                }
            }));
            certBtns.Controls.Add(Ui.Btn("Export Public Cert (.cer)", 1, (a, b) => {
                if (wsrv == null || wsrv.ServerCert == null) { Toast("TLS server is not active"); return; }
                using (var sfd = new SaveFileDialog { FileName = "errin-library.cer", Filter = "X.509 Certificate (*.cer)|*.cer" }) {
                    if (sfd.ShowDialog(this) == DialogResult.OK) {
                        try { CertHelper.ExportCer(wsrv.ServerCert, sfd.FileName); Toast("Public certificate exported"); }
                        catch (Exception ex) { Toast("Export failed: " + ex.Message); }
                    }
                }
            }));
            wGrid = Ui.Grid(); wGrid.Height = Ui.S(130); wGrid.Width = Ui.S(820); WebRefreshGrid();
            var wBtns = Ui.Flow(false);
            wBtns.Controls.Add(Ui.Btn("+ Pair new device", 0, (a, b) => PairNewDeviceDialog()));
            wBtns.Controls.Add(Ui.Btn("Show QR Code", 1, (a, b) => ShowQrDialog()));
            wBtns.Controls.Add(Ui.Btn("Remove selected", 1, (a, b) => WebRemoveSelected()));
            var webBody = Vbox(
                para("Allows phones, tablets, iPads, and other computers to access ErrinILS over the network using secure device pairing. Paired devices have full circulation, catalog, and student access.", 44),
                wf,
                Ui.Btn("Apply", 0, (a, b) => WebApply(wE.SelectedIndex == 1, (int)wP.Value, wB.SelectedIndex == 1 ? "local" : "network", wSsl.SelectedIndex == 1)),
                ws,
                certBtns,
                new Label { UseMnemonic = false, Text = "Paired Devices", Font = Th.FT, AutoSize = true, ForeColor = Th.Ink, Margin = Ui.S(new Padding(0, 10, 0, 4)) },
                wGrid,
                wBtns,
                Ui.Lbl("Clicking 'Pair new device' shows a code. Type it on the other device's screen to connect it.", true));

            var autoUpdCheck = new CheckBox { Text = "Automatically check for updates on startup", Checked = autoUpdate, AutoSize = true, ForeColor = Th.Ink, BackColor = Color.Transparent };
            autoUpdCheck.CheckedChanged += (chkSender, chkArgs) => { autoUpdate = autoUpdCheck.Checked; SaveCfg(); };
            var updStatus = new Label { UseMnemonic = false, Text = "Current version: v" + Updater.GetCurrentVersion(), Font = Th.F, ForeColor = Th.Mut, AutoSize = true, Margin = Ui.S(new Padding(0, 4, 0, 8)) };
            var chkUpdBtn = Ui.Btn("Check for updates now", 0);
            chkUpdBtn.Click += (btnSender, btnArgs) => {
                chkUpdBtn.Enabled = false;
                updStatus.Text = "Checking GitHub for updates...";
                ThreadPool.QueueUserWorkItem(stObj => {
                    var info = Updater.CheckForUpdate();
                    BeginInvoke(new Action(() => {
                        chkUpdBtn.Enabled = true;
                        if (!string.IsNullOrEmpty(info.Error)) {
                            updStatus.ForeColor = Th.Red;
                            updStatus.Text = "Could not check for updates: " + info.Error;
                        } else if (info.HasUpdate) {
                            updStatus.ForeColor = Th.Yel;
                            updStatus.Text = "New version v" + info.LatestVersion + " is available! (Current: v" + info.CurrentVersion + ")";
                            ShowUpdateDialog(info);
                        } else {
                            updStatus.ForeColor = Th.Green;
                            updStatus.Text = "You are using the latest version (v" + info.CurrentVersion + ").";
                        }
                    }));
                });
            };
            var updateBody = Vbox(para("ErrinILS is updated regularly with new features, bug fixes, and security enhancements. Keep your system up to date.", 44), autoUpdCheck, Spacer(6), chkUpdBtn, updStatus);

            return ScrollPage(Ui.Heading("Settings", "Policies and data management"), Ui.Card("Policies", polBody), Ui.Card("Admin password", pwBody),
                Ui.Card("Remote Access", webBody),
                Ui.Card("Software updates", updateBody),
                Ui.Card("Data retention", para("Student records (with their loan and fine history) are deleted " + Lib.RET_YEARS + " years after their last activity, provided they have no unreturned items or unpaid fines. This happens automatically when ErrinILS starts.", 44)),
                Ui.Card("Switching systems?", Vbox(para("Export every catalogued item as MARC 21, MARCXML, MarcEdit text, JSON and CSV to move to another ILS. To bring records in, use Import MARC on the Catalog page.", 44), Ui.Btn("Export catalog", 0, (a, b) => ExportDialog()))),
                Ui.Card("z39.50 server", zBody),
                Ui.Card("Local database", Vbox(para("Every change is written to the database file on this computer the instant you make it. No save button needed.", 24), Ui.Lbl("Database file: " + store.DataFile, true))));
        }

        /* ---------- Remote Access (Web server settings + lifecycle) ---------- */
        class WebCfg { public bool enabled; public int port = 8585; public string bind = "network"; public bool ssl = false; public string certPath = ""; public string certPass = ""; public List<Web.Device> devices = new List<Web.Device>(); }
        WebCfg wc = new WebCfg(); Web.Server wsrv; string werr = ""; Label wStatusLabel; DataGridView wGrid; bool autoUpdate = true;
        void ApplyWeb()
        {
            if (wsrv != null) { wsrv.Stop(); wsrv = null; }
            werr = ""; if (!wc.enabled) return;
            NetHelper.EnsureFirewallRule();
            try
            {
                X509Certificate2 serverCert = null;
                if (wc.ssl)
                {
                    try { serverCert = CertHelper.GetOrCreateCertificate(wc.certPath, wc.certPass, store.Dir); }
                    catch (Exception ex) { werr = "TLS cert error: " + ex.Message; return; }
                }
                wsrv = Web.StartServer(wc.port, wc.bind == "local", wc.devices,
                    getDb: () => db,
                    getLib: () => lib,
                    saveDb: Save,
                    runOnUi: act =>
                    {
                        if (InvokeRequired) Invoke(act);
                        else act();
                    },
                    onRemoteWrite: msg =>
                    {
                        Commit(msg != null ? "Remote: " + msg : null);
                    },
                    onDevicePaired: dev =>
                    {
                        SaveCfg();
                        if (InvokeRequired) BeginInvoke(new Action(() => { WebRefreshGrid(); Toast("Device paired: " + dev.name); }));
                        else { WebRefreshGrid(); Toast("Device paired: " + dev.name); }
                    },
                    log: null,
                    serverCert: serverCert);
            }
            catch (SocketException e)
            {
                werr = e.SocketErrorCode == SocketError.AddressAlreadyInUse ? "Port " + wc.port + " is already in use by another program. Choose a different port."
                    : e.SocketErrorCode == SocketError.AccessDenied ? "Windows did not allow port " + wc.port + ". Choose a different port." : e.Message;
            }
            catch (Exception e) { werr = e.Message; }
        }
        void WebApply(bool on, int port, string bind, bool ssl)
        {
            wc.enabled = on; wc.port = port; wc.bind = bind; wc.ssl = ssl; SaveCfg(); ApplyWeb(); WebShow();
            Toast(werr != "" ? werr : wsrv != null ? (wc.ssl ? "Secure HTTPS Remote Access server is running" : "Remote Access server is running") : "Remote Access server is off");
        }
        void WebShow()
        {
            if (wStatusLabel == null || wStatusLabel.IsDisposed) return;
            if (werr != "") { wStatusLabel.ForeColor = Th.Red; wStatusLabel.Text = "Problem: " + werr; return; }
            if (wsrv != null)
            {
                string scheme = (wsrv != null && wsrv.UseTls) ? "https://" : "http://";
                var addrs = wc.bind == "local" ? new List<string> { "127.0.0.1" } : Z.LanAddresses().Concat(new[] { "127.0.0.1" }).ToList();
                wStatusLabel.ForeColor = Th.Yel;
                wStatusLabel.Text = "\u25CF Running on " + scheme + (addrs.Count > 0 ? addrs[0] : "127.0.0.1") + ":" + wc.port +
                    (addrs.Count > 1 ? "  (also " + scheme + string.Join(":" + wc.port + ", " + scheme, addrs.Skip(1).ToArray()) + ":" + wc.port + ")" : "") +
                    (wsrv.UseTls ? "\n\U0001F512 Encrypted with TLS/SSL: " + CertHelper.GetSummary(wsrv.ServerCert) : "\n\u26A0 Standard HTTP (not encrypted)");
            }
            else { wStatusLabel.ForeColor = Th.Mut; wStatusLabel.Text = "\u25CB Off. Remote access is disabled."; }
        }
        void WebRefreshGrid()
        {
            if (wGrid == null || wGrid.IsDisposed) return;
            string[] headers = new[] { "Name", "Paired", "Last seen" };
            var rows = new List<string[]>();
            var tags = new List<object>();
            foreach (var d in wc.devices)
            {
                rows.Add(new[] { d.name, d.paired, d.seen });
                tags.Add(d);
            }
            Ui.Fill(wGrid, headers, rows, tags, "No devices paired yet. Click '+ Pair new device' to connect one.");
        }
        void WebRemoveSelected()
        {
            if (wGrid == null || wGrid.CurrentRow == null) { Toast("Select a device to remove"); return; }
            var dev = wGrid.CurrentRow.Tag as Web.Device;
            if (dev == null) return;
            if (Ui.Confirm(this, "Disconnect and revoke access for '" + dev.name + "'?\nThis device will no longer be able to access the library system."))
            {
                wc.devices.Remove(dev);
                SaveCfg();
                WebRefreshGrid();
                Toast("Device revoked: " + dev.name);
            }
        }
        void PairNewDeviceDialog()
        {
            if (wsrv == null || !wc.enabled)
            {
                Toast("Please turn Remote Access ON and click Apply first.");
                return;
            }

            var rng = new Random();
            int codeNum = rng.Next(100000, 1000000);
            string codeFormatted = string.Format("{0:D3} {1:D3}", codeNum / 1000, codeNum % 1000);
            string codeClean = codeNum.ToString();

            wsrv.StartPairing(codeClean, 300);

            var ips = NetHelper.GetReachableIpAddresses();
            var addrs = wc.bind == "local" ? new List<string> { "127.0.0.1" } : (ips.Count > 0 ? ips : new List<string> { "127.0.0.1" });
            string selectedIp = addrs.Count > 0 ? addrs[0] : "127.0.0.1";
            Func<string, string> getPairUrl = ip => ((wsrv != null && wsrv.UseTls) ? "https://" : "http://") + ip + ":" + wc.port + "/?code=" + codeClean;

            using (var d = Ui.Dlg("Pair a device (QR Code)", 480, 590))
            {
                var fl = new FlowLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    FlowDirection = FlowDirection.TopDown,
                    WrapContents = false,
                    AutoScroll = true,
                    Padding = Ui.S(new Padding(24, 16, 24, 16)),
                    BackColor = Th.Card
                };

                fl.Controls.Add(new Label
                {
                    UseMnemonic = false,
                    Text = "Scan with your phone or tablet camera to connect instantly:",
                    Font = Th.F,
                    ForeColor = Th.Mut,
                    AutoSize = true,
                    Margin = Ui.S(new Padding(0, 0, 0, 8))
                });

                var qrBox = new PictureBox
                {
                    Size = new Size(Ui.S(160), Ui.S(160)),
                    SizeMode = PictureBoxSizeMode.CenterImage,
                    BackColor = Color.White,
                    Margin = Ui.S(new Padding(125, 0, 0, 8))
                };
                qrBox.Image = Qr.ToBitmap(getPairUrl(selectedIp), 4, 3);
                fl.Controls.Add(qrBox);

                var urlLabel = new Label
                {
                    UseMnemonic = false,
                    Text = getPairUrl(selectedIp),
                    Font = Th.FT,
                    ForeColor = Th.Ink,
                    AutoSize = true,
                    Margin = Ui.S(new Padding(0, 0, 0, 6))
                };

                if (addrs.Count > 1)
                {
                    var ipCombo = Ui.Combo(260, addrs.ToArray());
                    ipCombo.SelectedIndex = 0;
                    ipCombo.SelectedIndexChanged += (s, e) =>
                    {
                        selectedIp = addrs[ipCombo.SelectedIndex];
                        string newUrl = getPairUrl(selectedIp);
                        urlLabel.Text = newUrl;
                        if (qrBox.Image != null) qrBox.Image.Dispose();
                        qrBox.Image = Qr.ToBitmap(newUrl, 4, 3);
                    };
                    var ipField = Ui.Field("Network interface", ipCombo, 280);
                    ipField.Margin = Ui.S(new Padding(0, 0, 0, 8));
                    fl.Controls.Add(ipField);
                }

                fl.Controls.Add(urlLabel);

                var linkBtns = Ui.Flow(false);
                linkBtns.Margin = Ui.S(new Padding(0, 0, 0, 10));
                linkBtns.Controls.Add(Ui.Btn("Copy Link", 1, (s, e) => { Clipboard.SetText(getPairUrl(selectedIp)); Toast("Pairing link copied to clipboard"); }));
                linkBtns.Controls.Add(Ui.Btn("Save QR Image", 1, (s, e) => SaveQrImageDialog(getPairUrl(selectedIp), "errin-pairing-qr.png")));
                fl.Controls.Add(linkBtns);

                fl.Controls.Add(new Label
                {
                    UseMnemonic = false,
                    Text = "Or manually open browser and enter code:",
                    Font = Th.F,
                    ForeColor = Th.Mut,
                    AutoSize = true
                });

                var codeBox = new Label
                {
                    UseMnemonic = false,
                    Text = codeFormatted,
                    Font = Th.FBig,
                    ForeColor = Th.Yel,
                    BackColor = Th.Bg,
                    TextAlign = ContentAlignment.MiddleCenter,
                    Size = new Size(Ui.S(410), Ui.S(48)),
                    Margin = Ui.S(new Padding(0, 4, 0, 8))
                };
                fl.Controls.Add(codeBox);

                int timeLeft = 300;
                var expLbl = new Label
                {
                    UseMnemonic = false,
                    Text = "Code expires in 5:00",
                    Font = Th.FSub,
                    ForeColor = Th.Mut,
                    AutoSize = true,
                    Margin = Ui.S(new Padding(0, 0, 0, 10))
                };
                fl.Controls.Add(expLbl);

                var cancelBtn = Ui.Btn("Close", 1, (s, e) => d.Close());
                fl.Controls.Add(cancelBtn);

                d.Controls.Add(fl);

                var timer = new Timer { Interval = 1000 };
                timer.Tick += (s, e) =>
                {
                    timeLeft--;
                    if (timeLeft <= 0)
                    {
                        timer.Stop();
                        wsrv.CancelPairing();
                        expLbl.Text = "Code expired. Close and click 'Pair new device' again.";
                        expLbl.ForeColor = Th.Red;
                    }
                    else
                    {
                        expLbl.Text = string.Format("Code expires in {0}:{1:D2}", timeLeft / 60, timeLeft % 60);
                        if (wsrv.LastPairedDevice != null)
                        {
                            timer.Stop();
                            codeBox.ForeColor = Th.Green;
                            codeBox.Text = "\u2713 Connected!";
                            expLbl.Text = wsrv.LastPairedDevice.name + " paired successfully.";
                            expLbl.ForeColor = Th.Green;
                            WebRefreshGrid();
                        }
                    }
                };
                timer.Start();

                d.FormClosing += (s, e) =>
                {
                    timer.Stop();
                    timer.Dispose();
                    if (qrBox.Image != null) { qrBox.Image.Dispose(); qrBox.Image = null; }
                    wsrv.CancelPairing();
                };

                d.ShowDialog(this);
            }
        }

        void ShowQrDialog()
        {
            if (wsrv == null || !wc.enabled)
            {
                Toast("Please turn Remote Access ON and click Apply first.");
                return;
            }

            var ips = NetHelper.GetReachableIpAddresses();
            var addrs = wc.bind == "local" ? new List<string> { "127.0.0.1" } : (ips.Count > 0 ? ips : new List<string> { "127.0.0.1" });
            string selectedIp = addrs.Count > 0 ? addrs[0] : "127.0.0.1";
            Func<string, string> getUrl = ip => ((wsrv != null && wsrv.UseTls) ? "https://" : "http://") + ip + ":" + wc.port;

            using (var d = Ui.Dlg("Remote Access QR Code", 460, 490))
            {
                var fl = new FlowLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    FlowDirection = FlowDirection.TopDown,
                    WrapContents = false,
                    AutoScroll = true,
                    Padding = Ui.S(new Padding(24, 20, 24, 20)),
                    BackColor = Th.Card
                };

                fl.Controls.Add(new Label
                {
                    UseMnemonic = false,
                    Text = "Scan with any phone, tablet, or laptop on the school network:",
                    Font = Th.F,
                    ForeColor = Th.Mut,
                    AutoSize = true,
                    Margin = Ui.S(new Padding(0, 0, 0, 10))
                });

                var qrBox = new PictureBox
                {
                    Size = new Size(Ui.S(180), Ui.S(180)),
                    SizeMode = PictureBoxSizeMode.CenterImage,
                    BackColor = Color.White,
                    Margin = Ui.S(new Padding(115, 0, 0, 10))
                };
                qrBox.Image = Qr.ToBitmap(getUrl(selectedIp), 5, 4);
                fl.Controls.Add(qrBox);

                var urlLabel = new Label
                {
                    UseMnemonic = false,
                    Text = getUrl(selectedIp),
                    Font = Th.FT,
                    ForeColor = Th.Ink,
                    AutoSize = true,
                    Margin = Ui.S(new Padding(0, 0, 0, 8))
                };

                if (addrs.Count > 1)
                {
                    var ipCombo = Ui.Combo(260, addrs.ToArray());
                    ipCombo.SelectedIndex = 0;
                    ipCombo.SelectedIndexChanged += (s, e) =>
                    {
                        selectedIp = addrs[ipCombo.SelectedIndex];
                        string newUrl = getUrl(selectedIp);
                        urlLabel.Text = newUrl;
                        if (qrBox.Image != null) qrBox.Image.Dispose();
                        qrBox.Image = Qr.ToBitmap(newUrl, 5, 4);
                    };
                    var ipField = Ui.Field("Network interface", ipCombo, 280);
                    ipField.Margin = Ui.S(new Padding(0, 0, 0, 8));
                    fl.Controls.Add(ipField);
                }

                fl.Controls.Add(urlLabel);

                var btns = Ui.Flow(false);
                btns.Margin = Ui.S(new Padding(0, 6, 0, 0));
                btns.Controls.Add(Ui.Btn("Copy Link", 0, (s, e) => { Clipboard.SetText(getUrl(selectedIp)); Toast("Link copied to clipboard"); }));
                btns.Controls.Add(Ui.Btn("Save QR Image", 1, (s, e) => SaveQrImageDialog(getUrl(selectedIp), "errin-remote-qr.png")));
                btns.Controls.Add(Ui.Btn("Close", 1, (s, e) => d.Close()));
                fl.Controls.Add(btns);

                d.Controls.Add(fl);
                d.FormClosing += (s, e) =>
                {
                    if (qrBox.Image != null) { qrBox.Image.Dispose(); qrBox.Image = null; }
                };
                d.ShowDialog(this);
            }
        }

        void SaveQrImageDialog(string url, string defaultFileName)
        {
            using (var sfd = new SaveFileDialog
            {
                FileName = defaultFileName,
                Filter = "PNG Image (*.png)|*.png|SVG Vector (*.svg)|*.svg"
            })
            {
                if (sfd.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    if (sfd.FileName.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
                    {
                        File.WriteAllText(sfd.FileName, Qr.ToSvg(url, 8, 4), Encoding.UTF8);
                    }
                    else
                    {
                        using (var bmp = Qr.ToBitmap(url, 8, 4))
                        {
                            bmp.Save(sfd.FileName, ImageFormat.Png);
                        }
                    }
                    Toast("QR code image saved");
                }
                catch (Exception ex)
                {
                    Toast("Save failed: " + ex.Message);
                }
            }
        }

        void ShowUpdateBanner(UpdateInfo info)
        {
            if (Ui.Confirm(this, "A new version of ErrinILS (v" + info.LatestVersion + ") is available!\n(You are currently running v" + info.CurrentVersion + ")\n\nWould you like to review and update now?"))
            {
                ShowUpdateDialog(info);
            }
        }

        void ShowUpdateDialog(UpdateInfo info)
        {
            using (var d = Ui.Dlg("Software Update \u00B7 ErrinILS v" + info.LatestVersion, 580, 410))
            {
                d.Controls.Add(new Label { UseMnemonic = false, Text = "new version available", Font = Th.FT, AutoSize = true, Location = new Point(Ui.S(20), Ui.S(14)) });
                d.Controls.Add(new Label { UseMnemonic = false, Text = "ErrinILS v" + info.LatestVersion + " is now available (current: v" + info.CurrentVersion + ").",
                    Font = Th.FB, ForeColor = Th.Yel, AutoSize = true, Location = new Point(Ui.S(20), Ui.S(44)) });

                var rtb = new TextBox {
                    Multiline = true,
                    ReadOnly = true,
                    ScrollBars = ScrollBars.Vertical,
                    BackColor = Th.Bg,
                    ForeColor = Th.Ink,
                    Font = Th.FS,
                    Location = new Point(Ui.S(20), Ui.S(74)),
                    Size = new Size(Ui.S(540), Ui.S(230)),
                    Text = string.IsNullOrEmpty(info.ReleaseNotes) ? "No release notes provided." : info.ReleaseNotes.Replace("\n", "\r\n")
                };
                d.Controls.Add(rtb);

                var prog = new ProgressBar {
                    Location = new Point(Ui.S(20), Ui.S(314)),
                    Size = new Size(Ui.S(540), Ui.S(16)),
                    Visible = false,
                    Minimum = 0,
                    Maximum = 100
                };
                d.Controls.Add(prog);

                var btnRow = Ui.Flow(false);
                btnRow.Location = new Point(Ui.S(20), Ui.S(340));
                var bUpd = Ui.Btn("Update and Restart", 0);
                var bWeb = Ui.Btn("View on GitHub", 1, (s, e) => {
                    if (!string.IsNullOrEmpty(info.ReleaseUrl)) Process.Start(info.ReleaseUrl);
                });
                var bClose = Ui.Btn("Remind Me Later", 1, (s, e) => d.Close());

                bUpd.Click += (s, e) => {
                    if (string.IsNullOrEmpty(info.DownloadUrl)) {
                        if (!string.IsNullOrEmpty(info.ReleaseUrl)) Process.Start(info.ReleaseUrl);
                        d.Close();
                        return;
                    }
                    bUpd.Enabled = false;
                    bWeb.Enabled = false;
                    bClose.Enabled = false;
                    prog.Visible = true;
                    string dest = Path.Combine(Path.GetTempPath(), "ErrinILS-v" + info.LatestVersion + ".exe");
                    ThreadPool.QueueUserWorkItem(stateObj => {
                        try {
                            Updater.DownloadFile(info.DownloadUrl, dest, pct => {
                                BeginInvoke(new Action(() => { prog.Value = Math.Min(100, Math.Max(0, pct)); }));
                            });
                            BeginInvoke(new Action(() => {
                                Toast("Update downloaded, restarting...");
                                Updater.ApplyUpdateAndRestart(dest);
                            }));
                        } catch (Exception ex) {
                            BeginInvoke(new Action(() => {
                                prog.Visible = false;
                                bUpd.Enabled = true;
                                bWeb.Enabled = true;
                                bClose.Enabled = true;
                                MessageBox.Show(d, "Download failed: " + ex.Message + "\nYou can download the update manually from GitHub.", "Update", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            }));
                        }
                    });
                };

                btnRow.Controls.Add(bUpd);
                btnRow.Controls.Add(bWeb);
                btnRow.Controls.Add(bClose);
                d.Controls.Add(btnRow);
                d.ShowDialog(this);
            }
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

                var w = Json.Obj(root, "web");
                if (root.ContainsKey("autoUpdate")) autoUpdate = root["autoUpdate"] is bool && (bool)root["autoUpdate"];
                if (w.ContainsKey("enabled")) wc.enabled = w["enabled"] is bool && (bool)w["enabled"];
                if (w.ContainsKey("port")) wc.port = (int)Json.Num(w, "port");
                if (w.ContainsKey("bind")) wc.bind = Json.Str(w, "bind") == "local" ? "local" : "network";
                if (w.ContainsKey("ssl")) wc.ssl = w["ssl"] is bool && (bool)w["ssl"];
                if (w.ContainsKey("certPath")) wc.certPath = Json.Str(w, "certPath");
                if (w.ContainsKey("certPass")) wc.certPass = Json.Str(w, "certPass");
                wc.devices = new List<Web.Device>();
                foreach (var dObj in Json.Arr(w, "devices"))
                {
                    var d = dObj as Dictionary<string, object>;
                    if (d != null) wc.devices.Add(Web.Device.FromJ(d));
                }
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
                cfg["autoUpdate"] = autoUpdate;

                var wDevs = new List<object>();
                foreach (var d in wc.devices) wDevs.Add(d.ToJ());
                cfg["web"] = new Dictionary<string, object>
                {
                    { "enabled", wc.enabled },
                    { "port", (double)wc.port },
                    { "bind", wc.bind },
                    { "ssl", wc.ssl },
                    { "certPath", wc.certPath },
                    { "certPass", wc.certPass },
                    { "devices", wDevs }
                };

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
                var t = Ui.Txt(340, true); t.Location = new Point(Ui.S(20), Ui.S(76));
                d.Controls.Add(new Label { UseMnemonic = false, Text = "enter password", Font = Th.FT, AutoSize = true, Location = new Point(Ui.S(20), Ui.S(16)), ForeColor = Th.Ink });
                d.Controls.Add(Ui.Lbl("This area is password protected.", true)); d.Controls[d.Controls.Count - 1].Location = new Point(Ui.S(20), Ui.S(44));
                d.Controls.Add(t);
                var ok = Ui.Btn("Unlock", 0); ok.Location = new Point(Ui.S(20), Ui.S(124)); var cancel = Ui.Btn("Cancel", 1); cancel.Location = new Point(Ui.S(110), Ui.S(124));
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
