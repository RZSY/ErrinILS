// Simplified View: just the circulation part (check out and return), stripped down to the essentials.
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
            sBannerBox.BackColor = kind == "ok" ? Th.Green : kind == "bad" ? Th.Red : kind == "warn" ? Th.Yel : Th.Card;
            sBanner.ForeColor = kind == "warn" ? Th.Dark : (kind == "" ? Th.Ink : Color.White); sBanner.Text = text;
        }
        void SLog(string who, string what, string kind)
        {
            sLog.Insert(0, new[] { DateTime.Now.ToString("HH:mm"), who, what, kind == "ok" ? "Done" : kind == "warn" ? "Done (late)" : "Not done" });
            if (sLog.Count > 8) sLog.RemoveAt(sLog.Count - 1);
            if (sGrid != null && !sGrid.IsDisposed) Ui.Fill(sGrid, new[] { "Time", "Student", "Book", "Result" }, sLog, null, "Nothing yet. Scan a book to begin.");
        }

        Control SimplePage()
        {
            Func<TextBox> big = () => { var t = Ui.Txt(330); t.Font = new Font("Segoe UI", 14f); return t; };
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
                var it = lib.ItemByBarcode(b); var l = lib.OpenLoan(it.id); Save();
                SBanner("Checked out: " + Short(it.title) + "\nDue back " + Nice(l.due), "ok"); SLog(who, "Borrowed: " + Short(it.title), "ok"); sBc.Clear(); sBc.Focus();
            };
            Action doRet = () =>
            {
                string b = sRb.Text.Trim(); if (b == "") return; string m;
                var it0 = lib.ItemByBarcode(b); var l0 = it0 != null ? lib.OpenLoan(it0.id) : null; string who = l0 != null ? lib.PName(l0.pid) : "";
                var err = lib.Return(b, out m);
                if (err != null) { SBanner(Lib.Friendly(err), err == "Item is not on loan" ? "warn" : "bad"); SLog(who, "Return: " + b, err == "Item is not on loan" ? "warn" : "bad"); sRb.SelectAll(); return; }
                bool late = m.Contains("Late"); Save();
                SBanner("Returned: " + Short(it0.title) + (late ? "\nThis book was late. A fine has been added." : "\nThank you!"), late ? "warn" : "ok"); SLog(who, "Returned: " + Short(it0.title), late ? "warn" : "ok");
                sRb.Clear(); sRb.Focus();
            };
            Func<Action, KeyEventHandler> enter = a => (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; a(); } };
            sIc.KeyDown += enter(doCard); sBc.KeyDown += enter(doCo); sRb.KeyDown += enter(doRet);

            var coBtns = Ui.Flow(false); coBtns.Controls.Add(Ui.Btn("Check out", 0, (s, e) => doCo())); coBtns.Controls.Add(Ui.Btn("Next student", 1, (s, e) => { sIc.Clear(); sBc.Clear(); SBanner("Ready. Scan a library card.", ""); sIc.Focus(); }));
            var c1 = Ui.Card("Check out", Vbox(Ui.Caption("Library card (IC number)"), sIc, Ui.Caption("Book barcode"), sBc, coBtns));
            var c2 = Ui.Card("Return", Vbox(Ui.Caption("Book barcode"), sRb, Ui.Btn("Return", 0, (s, e) => doRet())));

            sBanner = new Label { UseMnemonic = false, Font = new Font("Segoe UI", 15f, FontStyle.Bold), Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(18, 0, 18, 0) };
            sBannerBox = new Panel { Height = 86, Margin = new Padding(0, 0, 0, 14) }; sBannerBox.Controls.Add(sBanner);
            SBanner("Ready. Scan a library card.", "");

            sGrid = Ui.Grid(); Ui.Fill(sGrid, new[] { "Time", "Student", "Book", "Result" }, sLog, null, "Nothing yet. Scan a book to begin.");
            var recent = Ui.Card("Done so far", sGrid, 230);
            var exit = Ui.Flow(false); exit.Margin = new Padding(0, 0, 0, 10); exit.Controls.Add(Ui.Btn("Exit Simplified View", 1, (s, e) => Go("dash")));
            BeginInvoke(new Action(() => { if (sIc != null && !sIc.IsDisposed) sIc.Focus(); }));
            return ScrollPage(Ui.Heading("Simplified View", "Just check out and return. Scan the library card, then scan the book."), Cols(250, c1, c2), sBannerBox, recent, exit);
        }
    }
}
