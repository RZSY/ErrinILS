// ErrinILS dialogs: record editor, weeding, MARC view, labels, students, export/import, Z39.50 search.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace Errin
{
    public partial class MainForm
    {
        /// <summary>Run work on a background thread, then continue on the UI thread (if the owner is still open).</summary>
        void Bg<T>(Control owner, Func<T> work, Action<T> done)
        {
            var t = new Thread(() =>
            {
                T r; try { r = work(); } catch (Exception e) { r = default(T); lastBgError = e.Message; }
                try { if (!owner.IsDisposed) owner.BeginInvoke(new Action(() => { if (!owner.IsDisposed) done(r); })); } catch { }
            }) { IsBackground = true }; t.Start();
        }
        string lastBgError = "";

        /* ---------- weeding dialog ---------- */
        void WeedDialog(Item i)
        {
            if (lib.OpenLoan(i.id) != null)
            {
                Toast("Item is currently on loan. Please return it before weeding.");
                return;
            }

            bool alreadyWeeded = !string.IsNullOrEmpty(i.weeded);
            string title = (alreadyWeeded ? "Weeded record: " : "Weed record: ") + i.title;
            using (var d = Ui.Dlg(title, 560, alreadyWeeded ? 390 : 440))
            {
                var fl = new FlowLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    FlowDirection = FlowDirection.TopDown,
                    WrapContents = false,
                    AutoScroll = true,
                    Padding = Ui.S(new Padding(20, 16, 20, 16)),
                    BackColor = Th.Card
                };

                fl.Controls.Add(new Label
                {
                    UseMnemonic = false,
                    Text = alreadyWeeded ? "weeded record details" : "weed / withdraw item",
                    Font = Th.FT,
                    AutoSize = true,
                    ForeColor = Th.Ink,
                    Margin = Ui.S(new Padding(0, 0, 0, 4))
                });

                fl.Controls.Add(new Label
                {
                    UseMnemonic = false,
                    Text = i.title + (string.IsNullOrEmpty(i.author) ? "" : " \u00B7 " + i.author) +
                           "\nBarcode: " + (string.IsNullOrEmpty(i.barcode) ? "(none)" : i.barcode) +
                           " \u00B7 Call no.: " + (string.IsNullOrEmpty(i.call) ? "(none)" : i.call) +
                           " \u00B7 Control: " + (string.IsNullOrEmpty(i.ctl) ? "(none)" : i.ctl),
                    Font = Th.FB,
                    AutoSize = true,
                    ForeColor = Th.Mut,
                    Margin = Ui.S(new Padding(0, 0, 0, 10))
                });

                if (alreadyWeeded)
                {
                    fl.Controls.Add(new Label
                    {
                        UseMnemonic = false,
                        Text = "This record has been weeded and withdrawn from active circulation.",
                        Font = Th.F,
                        ForeColor = Th.Yel,
                        AutoSize = true,
                        Margin = Ui.S(new Padding(0, 0, 0, 8))
                    });

                    var pInfo = new Panel
                    {
                        Width = Ui.S(500),
                        Height = Ui.S(74),
                        BackColor = Th.Bg,
                        Padding = Ui.S(new Padding(12, 10, 12, 10)),
                        Margin = Ui.S(new Padding(0, 0, 0, 14))
                    };
                    pInfo.Controls.Add(new Label
                    {
                        UseMnemonic = false,
                        Dock = DockStyle.Fill,
                        Text = "Weeded date: " + (string.IsNullOrEmpty(i.weeded) ? Lib.Today() : i.weeded) + "\nReason: " + (string.IsNullOrEmpty(i.weed_reason) ? "No reason specified" : i.weed_reason),
                        Font = Th.F,
                        ForeColor = Th.Ink
                    });
                    fl.Controls.Add(pInfo);

                    var bRow = Ui.Flow(false);
                    bRow.Margin = Ui.S(new Padding(0, 8, 0, 0));

                    var btnRestore = Ui.Btn("Restore to active catalog", 0, (s, e) =>
                    {
                        lib.Unweed(i);
                        d.Close();
                        Commit("Restored to active catalog: " + i.title);
                    });

                    var btnDel = Ui.Btn("Delete permanently", 2, (s, e) =>
                    {
                        d.Close();
                        DelItem(i);
                    });

                    var btnClose = Ui.Btn("Close", 1, (s, e) => d.Close());

                    bRow.Controls.Add(btnRestore);
                    bRow.Controls.Add(btnDel);
                    bRow.Controls.Add(btnClose);
                    fl.Controls.Add(bRow);
                }
                else
                {
                    fl.Controls.Add(new Label
                    {
                        UseMnemonic = false,
                        Text = "Weeding marks this copy as withdrawn from the library while keeping its catalog history. Withdrawn copies cannot be borrowed.",
                        Font = Th.FS,
                        ForeColor = Th.Mut,
                        AutoSize = false,
                        Size = new Size(Ui.S(500), Ui.S(34)),
                        Margin = Ui.S(new Padding(0, 0, 0, 8))
                    });

                    var cbReason = Ui.Combo(340,
                        "Damaged / worn out (condition beyond repair)",
                        "Outdated / inaccurate / superseded information",
                        "Lost / missing copy",
                        "Low circulation / duplicate copy",
                        "Curriculum change / no longer relevant",
                        "Other (specify below)");

                    var tbNotes = Ui.Txt(340);
                    var tbDate = Ui.Txt(160);
                    tbDate.Text = Lib.Today();

                    fl.Controls.Add(Ui.Field("Primary weeding reason", cbReason, 360));
                    fl.Controls.Add(Ui.Field("Specific notes / condition details (optional)", tbNotes, 360));
                    fl.Controls.Add(Ui.Field("Date weeded", tbDate, 180));

                    var bRow = Ui.Flow(false);
                    bRow.Margin = Ui.S(new Padding(0, 10, 0, 0));

                    var btnWeed = Ui.Btn("Weed & withdraw copy", 0, (s, e) =>
                    {
                        string reason = cbReason.SelectedItem as string ?? "Weeded";
                        string note = tbNotes.Text.Trim();
                        if (note != "") reason += " (" + note + ")";
                        string date = tbDate.Text.Trim();
                        if (date == "") date = Lib.Today();

                        i.weeded = date;
                        i.weed_reason = reason;
                        d.Close();
                        Commit("Item weeded: " + i.title);
                    });

                    var btnCancel = Ui.Btn("Cancel", 1, (s, e) => d.Close());
                    bRow.Controls.Add(btnWeed);
                    bRow.Controls.Add(btnCancel);
                    fl.Controls.Add(bRow);
                }

                d.Controls.Add(fl);
                d.ShowDialog(this);
            }
        }

        /* ---------- record editor ---------- */
        static readonly string[][] IF = { new[] { "title", "245 $a Title" }, new[] { "sub", "245 $b Subtitle" }, new[] { "author", "100 $a Author" }, new[] { "isbn", "020 $a ISBN" }, new[] { "publisher", "260 $b Publisher" },
            new[] { "place", "260 $a Place" }, new[] { "year", "260 $c Year" }, new[] { "pages", "300 $a Pages" }, new[] { "subject", "650 $a Subject" }, new[] { "call", "082 $a Call number" },
            new[] { "barcode", "852 $p Barcode" }, new[] { "ctl", "001 Control number" }, new[] { "acq", "541 $e Acquisition number" } };
        void ItemForm(Item existing)
        {
            var src = existing ?? new Item { barcode = lib.NextBarcode(), ctl = lib.NextCtl(), acq = lib.NextAcq() };
            using (var d = Ui.Dlg((existing != null ? "Edit" : "New") + " bibliographic record", 840, 720))
            {
                var boxes = new Dictionary<string, TextBox>();
                var root = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, Padding = Ui.S(new Padding(20, 14, 10, 10)), BackColor = Th.Card };
                root.Controls.Add(new Label { UseMnemonic = false, Text = (existing != null ? "edit" : "new") + " bibliographic record", Font = Th.FT, AutoSize = true, ForeColor = Th.Ink, Margin = Ui.S(new Padding(0, 0, 0, 8)) });

                if (existing != null && !string.IsNullOrEmpty(existing.weeded))
                {
                    root.Controls.Add(new Label
                    {
                        UseMnemonic = false,
                        Text = "\u26A0 This record is weeded (" + existing.weed_reason + " \u00B7 " + existing.weeded + ") and withdrawn from circulation.",
                        Font = Th.FB,
                        ForeColor = Th.Yel,
                        AutoSize = true,
                        Margin = Ui.S(new Padding(0, 0, 0, 6))
                    });
                }

                var lk = Ui.Txt(420); var lks = Ui.Lbl("", true); lks.MaximumSize = new Size(Ui.S(740), 0);
                var lkBtn = Ui.Btn("Look up", 1); lkBtn.Margin = Ui.S(new Padding(8, 18, 0, 0));
                var lkSrc = Ui.Combo(250, new[] { "All sources (best match)" }.Concat(Lookup.SourceNames).ToArray());
                var lrow = Ui.Flow(false); lrow.Controls.Add(Ui.Field("Copy cataloguing: scan or type an ISBN", lk, 330)); lrow.Controls.Add(Ui.Field("Search in", lkSrc, 270)); lrow.Controls.Add(lkBtn);
                root.Controls.Add(lrow); root.Controls.Add(lks);
                Action<Item> fill = r => { foreach (var k in new[] { "title", "sub", "author", "isbn", "publisher", "place", "year", "pages", "subject", "call" }) if (r.Get(k) != "") boxes[k].Text = r.Get(k); };
                Action doLookup = () =>
                {
                    string raw = lk.Text.Trim(); if (raw == "") return; string c = Lib.Isb(raw);
                    var own = db.Items.Where(i => i.isbn != "" && Lib.Isb(i.isbn) == c).ToList();
                    if (own.Count > 0) { fill(own[0]); lks.Text = "Already in your catalog (" + own.Count + " cop" + (own.Count > 1 ? "ies" : "y") + "). Details copied. Enter a new barcode for this copy."; return; }
                    lks.Text = "Searching\u2026"; lkBtn.Enabled = false;
                    int only = lkSrc.SelectedIndex - 1;
                    Bg(d, () => Lookup.Find(raw, only), r =>
                    {
                        lkBtn.Enabled = true;
                        if (r == null) { lks.Text = "Lookup failed."; return; }
                        if (r.Ok) { fill(r.Rec); lks.Text = "Found via " + string.Join(" + ", r.Src.ToArray()) + ". Please check the details and the call number before saving."; }
                        else lks.Text = r.Err == "invalid" ? "That is not a valid ISBN. Please check the digits." : r.Err == "offline" ? "Could not reach the lookup services (no internet?). Enter the details by hand." : r.Err == "notfound" ? "No record found for this ISBN" + (only >= 0 ? " in " + Lookup.SourceNames[only] + ". Try \u201CAll sources\u201D, or enter the details by hand." : " in any source. Enter the details by hand.") : "Lookup failed.";
                    });
                };
                lkBtn.Click += (s, e) => doLookup(); lk.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; doLookup(); } };
                var secs = new[] { new { t = "Bibliographic description", sub = "", keys = new[] { "title", "sub", "author", "isbn", "publisher", "place", "year", "pages", "subject" } },
                    new { t = "Acquisition & control", sub = "The control number identifies this record and must be unique. The acquisition number records how this copy entered the library.", keys = new[] { "ctl", "acq" } },
                    new { t = "Local holdings", sub = "", keys = new[] { "call", "barcode" } } };
                foreach (var sc in secs)
                {
                    root.Controls.Add(new Label { UseMnemonic = false, Text = sc.t.ToLowerInvariant(), Font = Th.FB, AutoSize = true, ForeColor = sc.t[0] == 'A' ? Th.Yel : Th.Ink, Margin = Ui.S(new Padding(0, 10, 0, 2)) });
                    if (sc.sub != "") root.Controls.Add(new Label { UseMnemonic = false, Text = sc.sub, AutoSize = true, MaximumSize = new Size(Ui.S(740), 0), ForeColor = Th.Mut, Font = Th.FS, Margin = Ui.S(new Padding(0, 0, 0, 4)) });
                    var fl = Ui.Flow(true); fl.MaximumSize = new Size(Ui.S(780), 0);
                    foreach (var k in sc.keys) { var tb = Ui.Txt(220); tb.Text = src.Get(k); boxes[k] = tb; fl.Controls.Add(Ui.Field(IF.First(x => x[0] == k)[1], tb, 236)); }
                    root.Controls.Add(fl);
                }
                var save = Ui.Btn("Save", 0); var cancel = Ui.Btn("Cancel", 1); var br = Ui.Flow(false); br.Margin = Ui.S(new Padding(0, 14, 0, 0)); br.Controls.Add(save); br.Controls.Add(cancel); root.Controls.Add(br);
                cancel.Click += (s, e) => d.Close();
                save.Click += (s, e) =>
                {
                    var o = new Dictionary<string, string>(); foreach (var f in IF) o[f[0]] = boxes[f[0]].Text.Trim();
                    if (o["title"] == "" || o["barcode"] == "") { Toast("Title and barcode are required"); return; }
                    if (o["ctl"] == "") o["ctl"] = lib.NextCtl();
                    if (db.Items.Any(i => string.Equals(i.ctl, o["ctl"], StringComparison.OrdinalIgnoreCase) && i != existing)) { Toast("Control number already used"); return; }
                    if (db.Items.Any(i => i.barcode == o["barcode"] && i != existing)) { Toast("Barcode already used"); return; }
                    var tgt = existing ?? new Item { id = Lib.Uid() }; foreach (var kv in o) tgt.Set(kv.Key, kv.Value); if (existing == null) db.Items.Add(tgt);
                    d.Close(); Commit("Record saved");
                };
                d.Controls.Add(root); if (existing == null) d.Shown += (s, e) => lk.Focus();
                d.ShowDialog(this);
            }
        }

        void MarcView(Item i)
        {
            using (var d = Ui.Dlg("MARC 21 record", 760, 450))
            {
                d.Controls.Add(new TextBox { Multiline = true, ReadOnly = true, Font = new Font("Consolas", 10.5f), BackColor = Th.Bg, ForeColor = Th.Ink, BorderStyle = BorderStyle.None, Location = new Point(Ui.S(16), Ui.S(16)), Size = new Size(Ui.S(728), Ui.S(360)), ScrollBars = ScrollBars.Vertical, Text = Marc.ToText(i) });
                var c = Ui.Btn("Close", 1); c.Location = new Point(Ui.S(16), Ui.S(390)); c.Click += (s, e) => d.Close(); d.Controls.Add(c); d.ShowDialog(this);
            }
        }

        void LabelDialog(Item i)
        {
            using (var d = Ui.Dlg("Print label", 460, 350))
            {
                d.Controls.Add(new Label { UseMnemonic = false, Text = "print label", Font = Th.FT, AutoSize = true, Location = new Point(Ui.S(20), Ui.S(14)) });
                var pv = new Panel { BackColor = Color.White, Location = new Point(Ui.S(20), Ui.S(46)), Size = new Size(Ui.S(415), Ui.S(190)) };
                pv.Paint += (s, e) => {
                    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    e.Graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
                    float sc = 1.3f * Ui.DpiScale;
                    e.Graphics.ScaleTransform(sc, sc);
                    Labels.Draw(e.Graphics, 12, 12, i);
                };
                d.Controls.Add(pv);
                var n = Ui.Num(80, 1, 50, 1); var f = Ui.Field("Copies", n, 100); f.Location = new Point(Ui.S(20), Ui.S(246)); d.Controls.Add(f);
                var pr = Ui.Btn("Print", 0); pr.Location = new Point(Ui.S(140), Ui.S(266)); var cl = Ui.Btn("Close", 1); cl.Location = new Point(Ui.S(232), Ui.S(266));
                pr.Click += (s, e) => { if (Labels.Print(d, new List<Item> { i }, (int)n.Value)) { d.Close(); Toast("Sent to printer"); } };
                cl.Click += (s, e) => d.Close(); d.Controls.Add(pr); d.Controls.Add(cl); d.ShowDialog(this);
            }
        }

        void SpineDialog(Item i)
        {
            if (SpineArt.Lines(i.call).Count == 0) { Toast("This item has no call number yet"); return; }
            using (var d = Ui.Dlg("Spine label", 480, 370))
            {
                d.Controls.Add(new Label { UseMnemonic = false, Text = "spine label", Font = Th.FT, AutoSize = true, Location = new Point(Ui.S(20), Ui.S(14)) });
                var pv = new Panel { BackColor = Color.White, Location = new Point(Ui.S(20), Ui.S(46)), Size = new Size(Ui.S(180), Ui.S(272)) };
                pv.Paint += (s, e) => {
                    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    e.Graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
                    float sc = 1.5f * Ui.DpiScale;
                    e.Graphics.ScaleTransform(sc, sc);
                    SpineArt.Draw(e.Graphics, 12, 12, i);
                };
                d.Controls.Add(pv);
                d.Controls.Add(new Label { UseMnemonic = false, Text = i.title + "\nCall number: " + i.call, ForeColor = Th.Mut, AutoSize = false, Size = new Size(Ui.S(230), Ui.S(80)), Location = new Point(Ui.S(220), Ui.S(46)) });
                d.Controls.Add(new Label { UseMnemonic = false, Text = "Label size 1 x 1.5 inch (25 x 38 mm). Stick it on the spine, about 2 cm from the bottom.", ForeColor = Th.Mut, Font = Th.FS, AutoSize = false, Size = new Size(Ui.S(230), Ui.S(54)), Location = new Point(Ui.S(220), Ui.S(134)) });
                var n = Ui.Num(80, 1, 50, 1); var f = Ui.Field("Copies", n, 100); f.Location = new Point(Ui.S(220), Ui.S(200)); d.Controls.Add(f);
                var pr = Ui.Btn("Print", 0); pr.Location = new Point(Ui.S(220), Ui.S(270)); var cl = Ui.Btn("Close", 1); cl.Location = new Point(Ui.S(305), Ui.S(270));
                pr.Click += (s, e) => { if (SpineArt.Print(d, new List<Item> { i }, (int)n.Value)) { d.Close(); Toast("Sent to printer"); } };
                cl.Click += (s, e) => d.Close(); d.Controls.Add(pr); d.Controls.Add(cl); d.ShowDialog(this);
            }
        }

        /* ---------- students ---------- */
        void PatForm()
        {
            using (var d = Ui.Dlg("Register student", 560, 310))
            {
                var ic = Ui.Txt(240); var nm = Ui.Txt(240); var cl = Ui.Txt(240); var ct = Ui.Txt(240);
                var f1 = Ui.Field("IC number", ic, 250); f1.Location = new Point(Ui.S(20), Ui.S(20)); var f2 = Ui.Field("Full name", nm, 250); f2.Location = new Point(Ui.S(286), Ui.S(20));
                var f3 = Ui.Field("Class / grade", cl, 250); f3.Location = new Point(Ui.S(20), Ui.S(102)); var f4 = Ui.Field("Guardian contact", ct, 250); f4.Location = new Point(Ui.S(286), Ui.S(102));
                d.Controls.Add(f1); d.Controls.Add(f2); d.Controls.Add(f3); d.Controls.Add(f4);
                d.Controls.Add(new Label { UseMnemonic = false, Text = "Type the 12 digits from the student's IC or MyKid. The dashes are added for you. The IC number is the student's library ID and is printed on the library card.",
                    ForeColor = Th.Mut, Font = Th.FS, AutoSize = false, Size = new Size(Ui.S(510), Ui.S(42)), Location = new Point(Ui.S(20), Ui.S(184)) });
                ic.Leave += (s, e) => { var n = Lib.NormIc(ic.Text); if (n != null) ic.Text = n; };
                var ok = Ui.Btn("Register", 0); ok.Location = new Point(Ui.S(20), Ui.S(246)); var ca = Ui.Btn("Cancel", 1); ca.Location = new Point(Ui.S(124), Ui.S(246)); ca.Click += (s, e) => d.Close();
                ok.Click += (s, e) =>
                {
                    string n = Lib.NormIc(ic.Text), name = nm.Text.Trim();
                    if (n == null || !Lib.IcOk(n)) { MessageBox.Show(d, "Please type a valid IC number with 12 digits.", "ErrinILS", MessageBoxButtons.OK, MessageBoxIcon.Information); ic.Focus(); return; }
                    if (name == "") { MessageBox.Show(d, "Please type the student's full name.", "ErrinILS", MessageBoxButtons.OK, MessageBoxIcon.Information); nm.Focus(); return; }
                    if (lib.PatronById(n) != null) { MessageBox.Show(d, "This IC number is already registered.", "ErrinILS", MessageBoxButtons.OK, MessageBoxIcon.Information); ic.Focus(); return; }
                    var p = new Patron { id = n, name = name, cls = cl.Text.Trim(), contact = ct.Text.Trim(), reg = Lib.Today(), last = Lib.Today() };
                    db.Patrons.Add(p); d.Close(); Commit(name + " registered");
                    if (Ui.Confirm(this, "Print a library card for " + name + " now?")) CardDialog(p);
                };
                d.Controls.Add(ok); d.Controls.Add(ca); d.Shown += (s, e) => ic.Focus(); d.ShowDialog(this);
            }
        }
        void CardDialog(Patron p)
        {
            using (var d = Ui.Dlg("Library card", 500, 460))
            {
                d.Controls.Add(new Label { UseMnemonic = false, Text = "library card", Font = Th.FT, AutoSize = true, Location = new Point(Ui.S(20), Ui.S(14)) });
                var pv = new Panel { BackColor = Color.White, Location = new Point(Ui.S(20), Ui.S(46)), Size = new Size(Ui.S(455), Ui.S(292)) };
                string sch = db.Set.school;
                pv.Paint += (s, e) => {
                    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    e.Graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
                    float sc = 1.25f * Ui.DpiScale;
                    e.Graphics.ScaleTransform(sc, sc);
                    CardArt.Draw(e.Graphics, 15, 15, p, sch);
                };
                d.Controls.Add(pv);
                d.Controls.Add(new Label { UseMnemonic = false, Text = "Card size 85.6 x 54 mm. Print on card stock or thick paper and cut out. The barcode holds the IC number so the student can scan the card to borrow.", ForeColor = Th.Mut, Font = Th.FS, AutoSize = false, Size = new Size(Ui.S(455), Ui.S(38)), Location = new Point(Ui.S(20), Ui.S(348)) });
                var pr = Ui.Btn("Print card", 0); pr.Location = new Point(Ui.S(20), Ui.S(398)); var cl = Ui.Btn("Close", 1); cl.Location = new Point(Ui.S(134), Ui.S(398));
                pr.Click += (s, e) => { if (PrintCardList(new List<Patron> { p })) { d.Close(); Toast("Sent to printer"); } };
                cl.Click += (s, e) => d.Close(); d.Controls.Add(pr); d.Controls.Add(cl); d.ShowDialog(this);
            }
        }
        void PatView(Patron p)
        {
            using (var d = Ui.Dlg(p.name + " \u00B7 " + p.id, 740, 510))
            {
                var fs = db.Fines.Where(f => f.pid == p.id && f.paid == "").ToList(); string later = Lib.AddD(string.CompareOrdinal(p.reg, p.last) > 0 ? p.reg : p.last, 365 * Lib.RET_YEARS);
                d.Controls.Add(new Label { UseMnemonic = false, Text = p.name.ToLowerInvariant(), Font = Th.FT, AutoSize = true, Location = new Point(Ui.S(20), Ui.S(14)) });
                d.Controls.Add(new Label { UseMnemonic = false, Text = "IC " + p.id + " \u00B7 " + p.cls + " \u00B7 " + p.contact + " \u00B7 registered " + p.reg + "\nRecord scheduled for deletion after " + later + " if inactive", ForeColor = Th.Mut, AutoSize = true, Location = new Point(Ui.S(20), Ui.S(42)) });
                d.Controls.Add(new Label { UseMnemonic = false, Text = "Unpaid fines: " + Lib.Money(fs.Sum(f => f.amt)), Font = Th.FB, AutoSize = true, Location = new Point(Ui.S(20), Ui.S(86)) });
                var ls = db.Loans.Where(l => l.pid == p.id).Reverse().ToList();
                var g = MakeGrid(new[] { "Title", "Out", "Due", "Returned" }, ls.Select(l => new[] { lib.Title(l.iid), l.@out, l.due, l.ret ?? "\u2014" }).ToList(), null, "No borrowing history");
                g.Location = new Point(Ui.S(20), Ui.S(116)); g.Size = new Size(Ui.S(695), Ui.S(330)); g.Dock = DockStyle.None; d.Controls.Add(g);
                var c = Ui.Btn("Close", 1); c.Location = new Point(Ui.S(20), Ui.S(458)); c.Click += (s, e) => d.Close(); d.Controls.Add(c);
                var pc = Ui.Btn("Print library card", 0); pc.Location = new Point(Ui.S(114), Ui.S(458)); pc.Click += (s, e) => CardDialog(p); d.Controls.Add(pc);
                d.ShowDialog(this);
            }
        }

        /* ---------- export ---------- */
        void ExportDialog()
        {
            using (var d = Ui.Dlg("Transfer catalog", 620, 350))
            {
                d.Controls.Add(new Label { UseMnemonic = false, Text = "transfer catalog to another system", Font = Th.FT, AutoSize = true, Location = new Point(Ui.S(20), Ui.S(14)) });
                d.Controls.Add(new Label { UseMnemonic = false, Text = "Exports all " + db.Items.Count + " bibliographic records (with control and acquisition numbers) so they can be imported into another ILS (Koha, Follett Destiny, Alma, etc.).\n\nFull export (.zip) contains: MARC 21 binary (.mrc), MARCXML, MarcEdit text (.mrk), MARC-in-JSON and a spreadsheet CSV. Most systems accept the .mrc file directly.",
                    AutoSize = false, Size = new Size(Ui.S(570), Ui.S(150)), Location = new Point(Ui.S(20), Ui.S(46)), ForeColor = Th.Ink });
                var z = Ui.Btn("Download full export (.zip)", 0); z.Location = new Point(Ui.S(20), Ui.S(218)); var c = Ui.Btn("Spreadsheet only (.csv)", 1); c.Location = new Point(Ui.S(260), Ui.S(218)); var x = Ui.Btn("Close", 1); x.Location = new Point(Ui.S(20), Ui.S(276));
                z.Click += (s, e) => DoExport(true); c.Click += (s, e) => DoExport(false); x.Click += (s, e) => d.Close();
                d.Controls.Add(z); d.Controls.Add(c); d.Controls.Add(x); d.ShowDialog(this);
            }
        }
        void DoExport(bool zip)
        {
            var it = db.Items; if (it.Count == 0) { Toast("Catalog is empty"); return; } string day = Lib.Today(); var u8 = new UTF8Encoding(false);
            using (var d = new SaveFileDialog { FileName = "errin-catalog-" + day + (zip ? ".zip" : ".csv"), Filter = zip ? "Zip archive (*.zip)|*.zip" : "CSV (*.csv)|*.csv" })
            {
                if (d.ShowDialog(this) != DialogResult.OK) { Toast("Download cancelled"); return; }
                try
                {
                    byte[] csv = new UTF8Encoding(true).GetPreamble().Concat(u8.GetBytes(Lib.ToCsv(it))).ToArray();
                    if (!zip) File.WriteAllBytes(d.FileName, csv);
                    else
                    {
                        var z = new ZipOut(); z.Add("errin-catalog.mrc", Marc.ToMrcBytes(it)); z.Add("errin-catalog.xml", u8.GetBytes(Marc.ToXml(it))); z.Add("errin-catalog.mrk", u8.GetBytes(Marc.ToMrk(it)));
                        z.Add("errin-catalog.json", u8.GetBytes(Marc.ToJson(it))); z.Add("errin-catalog.csv", csv);
                        z.Add("README.txt", u8.GetBytes("Errin catalog export, " + day + "\r\n" + it.Count + " records, UTF-8.\r\n.mrc = MARC 21 (ISO 2709), .xml = MARCXML, .mrk = MarcEdit text, .json = MARC-in-JSON, .csv = spreadsheet.\r\nControl number is in 001, acquisition number in 541$e, item barcode in 852$p.\r\n"));
                        File.WriteAllBytes(d.FileName, z.Finish());
                    }
                    Toast("Export ready");
                }
                catch (Exception e) { Toast("Export failed: " + e.Message); }
            }
        }

        /* ---------- MARC import ---------- */
        void PickImport()
        {
            using (var d = new OpenFileDialog { Filter = "MARC files|*.mrc;*.marc;*.dat;*.xml;*.mrk;*.txt|All files|*.*", Title = "Import MARC records" }) { if (d.ShowDialog(this) == DialogResult.OK) ImportFile(d.FileName); }
        }
        void ImportFile(string path)
        {
            ParseResult r; try { r = Marc.ParseAny(File.ReadAllBytes(path)); } catch { Toast("Could not read that file"); return; }
            if (r.Records.Count == 0) { Toast("No MARC records found in that file"); return; }
            ImportPreview(r.Records.Select(Marc.ToItem).ToList(), r.Bad, Path.GetFileName(path) + " \u00B7 " + r.Format);
        }
        void ImportPreview(List<Item> items, int bad, string src)
        {
            var ok = items.Where(i => i.title != "").ToList(); int skip = items.Count - ok.Count; var dup = lib.DupSet(ok);
            using (var d = Ui.Dlg("Import MARC records", 740, 580))
            {
                d.Controls.Add(new Label { UseMnemonic = false, Text = "import marc records", Font = Th.FT, AutoSize = true, Location = new Point(Ui.S(20), Ui.S(14)) });
                d.Controls.Add(new Label { UseMnemonic = false, Text = src, ForeColor = Th.Mut, AutoSize = true, Location = new Point(Ui.S(20), Ui.S(42)) });
                d.Controls.Add(new Label { UseMnemonic = false, Text = items.Count + " records read \u00B7 " + (ok.Count - dup.Count) + " new \u00B7 " + dup.Count + " already in the catalogue" + (skip > 0 ? " \u00B7 " + skip + " skipped (no title in 245 $a)" : "") + (bad > 0 ? " \u00B7 " + bad + " damaged and ignored" : ""),
                    Font = Th.FB, AutoSize = false, Size = new Size(Ui.S(695), Ui.S(24)), Location = new Point(Ui.S(20), Ui.S(68)) });
                int y = 98; ComboBox mode = null;
                if (dup.Count > 0) { mode = Ui.Combo(340, "Skip them", "Add them as another copy (new barcode)"); var f = Ui.Field("Records already in the catalogue", mode, 360); f.Location = new Point(Ui.S(20), Ui.S(y)); d.Controls.Add(f); y += 62; }
                var acq = new CheckBox { Text = "Give records that have no acquisition number a new one (ACQ-nnnnnn)", Checked = true, AutoSize = true, Location = new Point(Ui.S(20), Ui.S(y)), ForeColor = Th.Ink, BackColor = Color.Transparent }; d.Controls.Add(acq); y += 30;
                d.Controls.Add(new Label { UseMnemonic = false, Text = "Control numbers and barcodes from the file are kept when they are free; otherwise new ones are assigned.", ForeColor = Th.Mut, Font = Th.FS, AutoSize = true, Location = new Point(Ui.S(20), Ui.S(y)) }); y += 28;
                var g = MakeGrid(new[] { "Title", "Author", "ISBN" }, ok.Take(8).Select(i => new[] { i.title, i.author, i.isbn }).ToList(), null, "Nothing to import");
                g.Dock = DockStyle.None; g.Location = new Point(Ui.S(20), Ui.S(y)); g.Size = new Size(Ui.S(695), Ui.S(580 - y - 95)); d.Controls.Add(g);
                if (ok.Count > 8) d.Controls.Add(new Label { UseMnemonic = false, Text = "\u2026and " + (ok.Count - 8) + " more", ForeColor = Th.Mut, AutoSize = true, Location = new Point(Ui.S(20), Ui.S(580 - 84)) });
                var go = Ui.Btn("Import", 0); go.Location = new Point(Ui.S(20), Ui.S(580 - 54)); go.Enabled = ok.Count > 0; var ca = Ui.Btn("Cancel", 1); ca.Location = new Point(Ui.S(115), Ui.S(580 - 54)); ca.Click += (s, e) => d.Close();
                go.Click += (s, e) =>
                {
                    int skipped; int added = lib.AddItems(ok, mode != null && mode.SelectedIndex == 1 ? "copy" : "skip", acq.Checked, dup, out skipped); d.Close();
                    Commit(added + " record(s) imported" + (skipped > 0 ? ", " + skipped + " skipped" : ""));
                };
                d.Controls.Add(go); d.Controls.Add(ca); d.ShowDialog(this);
            }
        }

        /* ---------- Z39.50 client ---------- */
        void ZDialog()
        {
            var presets = new List<ZTarget> { new ZTarget { n = "Library of Congress", host = "lx2.loc.gov", port = 210, db = "LCDB" } }; int nPre = presets.Count;
            using (var d = Ui.Dlg("Search Z39.50 libraries", 880, 660))
            {
                d.Controls.Add(new Label { UseMnemonic = false, Text = "search z39.50 libraries", Font = Th.FT, AutoSize = true, Location = new Point(Ui.S(20), Ui.S(14)) });
                d.Controls.Add(new Label { UseMnemonic = false, Text = "Find a record in another library's catalogue and copy it into yours. Needs an internet connection.", ForeColor = Th.Mut, AutoSize = true, Location = new Point(Ui.S(20), Ui.S(40)) });
                Func<List<ZTarget>> all = () => presets.Concat(db.Set.zt).ToList();
                var cbT = new ComboBox { Width = Ui.S(320), DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat, BackColor = Th.Bg, ForeColor = Th.Ink, Font = Th.F };
                var cbF = Ui.Combo(200, "ISBN", "Title", "Author", "Any word"); string[] fkeys = { "isbn", "title", "author", "any" };
                var q = Ui.Txt(520); var go = Ui.Btn("Search", 0);
                var fT = Ui.Field("Library", cbT, 336); fT.Location = new Point(Ui.S(20), Ui.S(68)); var fF = Ui.Field("Search by", cbF, 216); fF.Location = new Point(Ui.S(380), Ui.S(68));
                var fQ = Ui.Field("Search for", q, 540); fQ.Location = new Point(Ui.S(20), Ui.S(132)); go.Location = new Point(Ui.S(595), Ui.S(150));
                var gb = new GroupBox { Text = "Connection details", ForeColor = Th.Mut, Location = new Point(Ui.S(20), Ui.S(196)), Size = new Size(Ui.S(835), Ui.S(132)), BackColor = Color.Transparent };
                var h = Ui.Txt(180); var p = Ui.Num(80, 1, 65535, 210); var dbn = Ui.Txt(120); var us = Ui.Txt(110); var pw = Ui.Txt(110, true);
                Action<Control, string, int> put = (c, cap, x) => { var f = Ui.Field(cap, c, c.Width + 14); f.Location = new Point(Ui.S(x), Ui.S(18)); gb.Controls.Add(f); };
                us.Width = Ui.S(150); pw.Width = Ui.S(150); put(h, "Host", 10); put(p, "Port", 210); put(dbn, "Database", 310); put(us, "User name (optional)", 456); put(pw, "Password (optional)", 634);
                var sv = Ui.Btn("Save this library", 1); sv.Location = new Point(Ui.S(10), Ui.S(88)); sv.MinimumSize = new Size(0, Ui.S(28)); var rm = Ui.Btn("Remove saved library", 1); rm.Location = new Point(Ui.S(175), Ui.S(88)); rm.MinimumSize = new Size(0, Ui.S(28)); gb.Controls.Add(sv); gb.Controls.Add(rm);
                var st = new Label { UseMnemonic = false, Text = "", ForeColor = Th.Mut, AutoSize = false, Size = new Size(Ui.S(835), Ui.S(22)), Location = new Point(Ui.S(20), Ui.S(336)) };
                var res = new List<Item>(); var grid = Ui.Grid(); grid.Location = new Point(Ui.S(20), Ui.S(364)); grid.Size = new Size(Ui.S(835), Ui.S(218)); grid.Dock = DockStyle.None;
                Ui.Fill(grid, new[] { "Title", "Author", "Publisher", "Year", "ISBN" }, new List<string[]>(), null, "No results yet");
                var add = Ui.Btn("Add selected to catalogue", 0); add.Location = new Point(Ui.S(20), Ui.S(594)); var close = Ui.Btn("Close", 1); close.Location = new Point(Ui.S(280), Ui.S(594)); close.Click += (s, e) => d.Close();
                Action reload = () => { cbT.Items.Clear(); foreach (var t in all()) cbT.Items.Add(t.n); cbT.Items.Add("Another library\u2026"); };
                Action pick = () =>
                {
                    int k = cbT.SelectedIndex; var l = all(); ZTarget t = k >= 0 && k < l.Count ? l[k] : new ZTarget();
                    h.Text = t.host; p.Value = Math.Max(1, Math.Min(65535, t.port <= 0 ? 210 : t.port)); dbn.Text = t.db; rm.Visible = k >= nPre && k < l.Count;
                };
                reload(); cbT.SelectedIndexChanged += (s, e) => pick(); cbT.SelectedIndex = 0;
                sv.Click += (s, e) =>
                {
                    string hh = h.Text.Trim(); int pp = (int)p.Value; if (hh == "") { Toast("Enter host and port first"); return; }
                    db.Set.zt.RemoveAll(x => x.host == hh && x.port == pp); db.Set.zt.Add(new ZTarget { n = hh, host = hh, port = pp, db = dbn.Text.Trim() }); Save(); Toast("Library saved"); reload(); cbT.SelectedIndex = all().Count - 1;
                };
                rm.Click += (s, e) => { int k = cbT.SelectedIndex - nPre; if (k >= 0 && k < db.Set.zt.Count) { db.Set.zt.RemoveAt(k); Save(); reload(); cbT.SelectedIndex = 0; } };
                Action search = () =>
                {
                    string term = q.Text.Trim(); if (term == "") return; if (h.Text.Trim() == "") { Toast("Enter the library host and port"); return; }
                    st.ForeColor = Th.Mut; st.Text = "Searching\u2026"; go.Enabled = false;
                    var o = new Z.SearchOpts { Host = h.Text.Trim(), Port = (int)p.Value, Db = dbn.Text.Trim(), Field = fkeys[Math.Max(0, cbF.SelectedIndex)], Term = term, Max = 20, User = us.Text.Trim(), Password = pw.Text };
                    lastBgError = "";
                    Bg(d, () => Z.Search(o), r =>
                    {
                        go.Enabled = true; res.Clear();
                        if (r == null) { st.ForeColor = Th.Red; st.Text = lastBgError != "" ? lastBgError : "Search failed"; Ui.Fill(grid, new[] { "Title", "Author", "Publisher", "Year", "ISBN" }, new List<string[]>(), null, "No results"); return; }
                        res.AddRange(r.Items);
                        if (r.Count == 0) { st.Text = "No records found."; Ui.Fill(grid, new[] { "Title", "Author", "Publisher", "Year", "ISBN" }, new List<string[]>(), null, "No results"); return; }
                        st.Text = r.Count + " found" + (r.Count > res.Count ? ", showing the first " + res.Count : "") + (r.Skipped > 0 ? ". " + r.Skipped + " record(s) were in a format ErrinILS cannot read" : "") + ".";
                        Ui.Fill(grid, new[] { "Title", "Author", "Publisher", "Year", "ISBN" }, res.Select(i => new[] { i.title, i.author, i.publisher, i.year, i.isbn }).ToList(), res.Cast<object>().ToList(), "No results");
                    });
                };
                go.Click += (s, e) => search(); q.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; search(); } };
                Action addSel = () =>
                {
                    var it = Ui.Sel<Item>(grid); if (it == null) { Toast("Select a record first"); return; } if (it.title == "") { Toast("This record has no title"); return; }
                    int n = it.isbn != "" ? db.Items.Count(i => Lib.Isb(i.isbn) == Lib.Isb(it.isbn)) : 0;
                    if (n > 0 && !Ui.Confirm(d, "You already have " + n + " cop" + (n > 1 ? "ies" : "y") + " of this title. Add another copy?")) return;
                    int sk; lib.AddItems(new List<Item> { it }, "copy", true, null, out sk); Save(); Toast("Record added to the catalogue");
                };
                add.Click += (s, e) => addSel(); grid.CellDoubleClick += (s, e) => addSel();
                foreach (Control c in new Control[] { fT, fF, fQ, go, gb, st, grid, add, close }) d.Controls.Add(c);
                d.Shown += (s, e) => q.Focus(); d.ShowDialog(this);
            }
            if (view == "cat") Render();
        }
    }
}
