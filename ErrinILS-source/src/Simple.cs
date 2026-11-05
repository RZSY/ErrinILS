// Simplified View: child-friendly circulation view (check out and return).
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Errin
{
    public partial class MainForm
    {
        TextBox sIc, sBc, sRb; Label sBanner; Panel sBannerBox; DataGridView sGrid;
        readonly List<string[]> sLog = new List<string[]>();          // what happened this session (newest first)

        static string Nice(string iso) { try { return Lib.Dt(iso).ToString("d MMM yyyy", System.Globalization.CultureInfo.InvariantCulture); } catch { return iso; } }
        static string Short(string t) { t = t ?? ""; return t.Length > 50 ? t.Substring(0, 48) + "\u2026" : t; }

        void SBanner(string text, string kind)
        {
            if (sBanner == null || sBanner.IsDisposed) return;
            string icon = kind == "ok" ? "\u2705 " : kind == "bad" ? "\uD83D\uDCA1 " : kind == "warn" ? "\u23F0 " : "\uD83D\uDC4B ";
            sBannerBox.BackColor = kind == "ok" ? Color.FromArgb(0x13, 0x4e, 0x3a) :
                                   kind == "bad" ? Color.FromArgb(0x5c, 0x1d, 0x1d) :
                                   kind == "warn" ? Color.FromArgb(0x5c, 0x43, 0x0c) :
                                   Color.FromArgb(0x1e, 0x3a, 0x5f);
            sBanner.ForeColor = kind == "warn" ? Color.FromArgb(0xfd, 0xee, 0xb2) : Color.White;
            sBanner.Text = icon + text;
        }

        void SLog(string who, string what, string kind)
        {
            sLog.Insert(0, new[] { DateTime.Now.ToString("HH:mm"), who, what, kind == "ok" ? "Done" : kind == "warn" ? "Done (late)" : "Not done" });
            if (sLog.Count > 8) sLog.RemoveAt(sLog.Count - 1);
            if (sGrid != null && !sGrid.IsDisposed) Ui.Fill(sGrid, new[] { "Time", "Student", "Book", "Result" }, sLog, null, "Nothing yet. Scan a book to begin.");
        }

        static Panel ChildCard(string title, string emoji, Color accent, Control body, int height = 0)
        {
            var p = new Panel { BackColor = Color.FromArgb(0x27, 0x29, 0x2e), Padding = Ui.S(new Padding(18, 14, 18, 14)), Margin = Ui.S(new Padding(0, 0, 0, 14)), Dock = DockStyle.Fill };
            var bar = new Panel { Dock = DockStyle.Top, Height = Math.Max(2, Ui.S(4)), BackColor = accent };
            var tl = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, BackColor = Color.Transparent, RowCount = 2, Margin = new Padding(0) };
            tl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            var hBox = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Margin = Ui.S(new Padding(0, 4, 0, 10)) };
            var emLbl = new Label { UseMnemonic = false, Text = emoji + " ", Font = new Font("Segoe UI", 14f), AutoSize = true, ForeColor = accent };
            var tLbl = new Label { UseMnemonic = false, Text = title, Font = new Font("Segoe UI", 13f, FontStyle.Bold), AutoSize = true, ForeColor = Th.Ink };
            hBox.Controls.Add(emLbl);
            hBox.Controls.Add(tLbl);
            tl.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            tl.Controls.Add(hBox, 0, 0);

            if (height > 0) { tl.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); body.Dock = DockStyle.Fill; p.Height = Ui.S(height); }
            else { tl.RowStyles.Add(new RowStyle(SizeType.AutoSize)); body.Dock = DockStyle.Top; p.AutoSize = true; p.AutoSizeMode = AutoSizeMode.GrowAndShrink; tl.AutoSize = true; tl.Dock = DockStyle.Top; }
            tl.Controls.Add(body, 0, 1);
            p.Controls.Add(tl);
            p.Controls.Add(bar);
            return p;
        }

        Control SimplePage()
        {
            Func<TextBox> big = () => { var t = Ui.Txt(330); t.Font = new Font("Segoe UI", 15f); return t; };
            sIc = big(); sBc = big(); sRb = big();

            Action doCard = () =>
            {
                var p = lib.PatronById(sIc.Text.Trim());
                if (p == null) { SBanner(Lib.Friendly("Student not found"), "bad"); sIc.SelectAll(); return; }
                sIc.Text = p.id; SBanner("Hello, " + p.name + ". Now scan the book.", "ok"); sBc.Focus();
            };
            Action doCo = () =>
            {
                string pid = sIc.Text.Trim(), b = sBc.Text.Trim();
                if (pid == "") { SBanner("Scan or type the library card first.", "warn"); sIc.Focus(); return; }
                if (b == "") { sBc.Focus(); return; }
                var p = lib.PatronById(pid); string who = p != null ? p.name : pid; string m;
                var err = lib.Checkout(pid, b, out m);
                if (err != null) { SBanner(Lib.Friendly(err), "bad"); SLog(who, "Borrow: " + b, "bad"); sBc.SelectAll(); return; }
                var it = lib.ItemByBarcode(b); var l = it != null ? lib.OpenLoan(it.id) : null; Save();
                SBanner("Checked out: " + Short(it != null ? it.title : b) + (l != null ? "\nDue back " + Nice(l.due) : ""), "ok"); SLog(who, "Borrowed: " + Short(it != null ? it.title : b), "ok"); sBc.Clear(); sBc.Focus();
            };
            Action doRet = () =>
            {
                string b = sRb.Text.Trim(); if (b == "") return; string m;
                var it0 = lib.ItemByBarcode(b); var l0 = it0 != null ? lib.OpenLoan(it0.id) : null; string who = l0 != null ? lib.PName(l0.pid) : "";
                var err = lib.Return(b, out m);
                if (err != null) { SBanner(Lib.Friendly(err), err == "Item is not on loan" ? "warn" : "bad"); SLog(who, "Return: " + b, err == "Item is not on loan" ? "warn" : "bad"); sRb.SelectAll(); return; }
                bool late = m != null && m.Contains("Late"); Save();
                string title = it0 != null ? it0.title : b;
                SBanner("Returned: " + Short(title) + (late ? "\nThis book was late. A fine has been added." : "\nThank you!"), late ? "warn" : "ok"); SLog(who, "Returned: " + Short(title), late ? "warn" : "ok");
                sRb.Clear(); sRb.Focus();
            };
            Func<Action, KeyEventHandler> enter = a => (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; a(); } };
            sIc.KeyDown += enter(doCard); sBc.KeyDown += enter(doCo); sRb.KeyDown += enter(doRet);

            Func<string, Color, Color, EventHandler, Button> kidBtn = (text, bg, fg, click) =>
            {
                var b = new Button
                {
                    Text = text,
                    FlatStyle = FlatStyle.Flat,
                    Font = new Font("Segoe UI", 11.5f, FontStyle.Bold),
                    AutoSize = true,
                    AutoSizeMode = AutoSizeMode.GrowAndShrink,
                    Padding = Ui.S(new Padding(16, 6, 16, 6)),
                    Margin = Ui.S(new Padding(0, 0, 10, 0)),
                    Cursor = Cursors.Hand,
                    BackColor = bg,
                    ForeColor = fg,
                    UseVisualStyleBackColor = false
                };
                b.FlatAppearance.BorderSize = 0;
                b.MinimumSize = new Size(Ui.S(110), Ui.S(38));
                if (click != null) b.Click += click;
                return b;
            };

            var coBtns = Ui.Flow(false);
            coBtns.Controls.Add(kidBtn("Check out", Color.FromArgb(0x2f, 0x6b, 0xff), Color.White, (s, e) => doCo()));
            coBtns.Controls.Add(kidBtn("Next student", Color.FromArgb(0x3e, 0x40, 0x47), Th.Ink, (s, e) => { sIc.Clear(); sBc.Clear(); SBanner("Ready. Scan a library card.", ""); sIc.Focus(); }));

            var retBtn = kidBtn("Return", Color.FromArgb(0x2f, 0xb3, 0x6b), Color.White, (s, e) => doRet());

            var c1 = ChildCard("Check out", "\uD83D\uDCD6", Color.FromArgb(0x38, 0xbd, 0xf8),
                Vbox(Ui.Caption("\uD83C\uDF92 Library card (IC number)"), sIc, Ui.Caption("\uD83D\uDCDA Book barcode"), sBc, coBtns));
            var c2 = ChildCard("Return", "\uD83C\uDF81", Color.FromArgb(0x34, 0xd3, 0x99),
                Vbox(Ui.Caption("\uD83D\uDD16 Book barcode"), sRb, retBtn));

            sBanner = new Label { UseMnemonic = false, Font = new Font("Segoe UI", 15f, FontStyle.Bold), Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Padding = Ui.S(new Padding(18, 0, 18, 0)) };
            sBannerBox = new Panel { Height = Ui.S(86), Margin = Ui.S(new Padding(0, 0, 0, 14)) }; sBannerBox.Controls.Add(sBanner);
            SBanner("Ready. Scan a library card.", "");

            sGrid = Ui.Grid(); Ui.Fill(sGrid, new[] { "Time", "Student", "Book", "Result" }, sLog, null, "Nothing yet. Scan a book to begin.");
            var recent = ChildCard("Done so far", "\u2B50", Color.FromArgb(0xfb, 0xbf, 0x24), sGrid, 230);
            var exit = Ui.Flow(false); exit.Margin = Ui.S(new Padding(0, 0, 0, 10)); exit.Controls.Add(Ui.Btn("Exit Simplified View", 1, (s, e) => Go("dash")));
            BeginInvoke(new Action(() => { if (sIc != null && !sIc.IsDisposed) sIc.Focus(); }));
            return ScrollPage(Ui.Heading("Simplified View", "Just check out and return. Scan the library card, then scan the book."), Cols(250, c1, c2), sBannerBox, recent, exit);
        }
    }
}
