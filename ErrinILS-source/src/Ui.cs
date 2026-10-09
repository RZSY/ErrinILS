// Shared UI helpers: dark theme, controls, grid, charts, Code 128 labels and a small ZIP writer.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Printing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace Errin
{
    public static class Th
    {
        public static readonly Color Bg = Color.FromArgb(0x23, 0x24, 0x27), Card = Color.FromArgb(0x2c, 0x2d, 0x31), Ink = Color.FromArgb(0xf1, 0xef, 0xe8), Mut = Color.FromArgb(0x9a, 0x9a, 0x94),
            Dim = Color.FromArgb(0x44, 0x45, 0x4a), Blue = Color.FromArgb(0x2f, 0x6b, 0xff), Red = Color.FromArgb(0xe8, 0x39, 0x2f), Yel = Color.FromArgb(0xf6, 0xc6, 0x1b), Green = Color.FromArgb(0x2f, 0xb3, 0x6b), Dark = Color.FromArgb(0x1b, 0x1b, 0x1b);
        public static readonly Font F = new Font("Segoe UI", 9.75f), FB = new Font("Segoe UI", 9.75f, FontStyle.Bold), FS = new Font("Segoe UI", 8.5f),
            FH = new Font("Segoe UI", 20f, FontStyle.Bold), FBig = new Font("Segoe UI", 20f, FontStyle.Bold), FT = new Font("Segoe UI", 11.5f, FontStyle.Bold), FLogo = new Font("Segoe UI", 26f, FontStyle.Bold);
    }

    public static class Ui
    {
        public static Label Lbl(string t, bool muted = false, Font f = null)
        {
            return new Label { UseMnemonic = false, Text = t, AutoSize = true, ForeColor = muted ? Th.Mut : Th.Ink, BackColor = Color.Transparent, Font = f ?? Th.F, Margin = new Padding(0, 2, 0, 2) };
        }
        public static Label Caption(string t) { return new Label { UseMnemonic = false, Text = System.Text.RegularExpressions.Regex.Replace(t.ToUpperInvariant(), @"\$([A-Z])", m => "$" + m.Groups[1].Value.ToLowerInvariant()), AutoSize = true, ForeColor = Th.Mut, BackColor = Color.Transparent, Font = new Font("Segoe UI", 8f, FontStyle.Bold), Margin = new Padding(0, 4, 0, 0) }; }
        public static Button Btn(string t, int kind = 0, EventHandler click = null)   // 0 primary, 1 secondary, 2 danger
        {
            var b = new Button { Text = t, FlatStyle = FlatStyle.Flat, Font = Th.FB, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new Padding(10, 3, 10, 3), Margin = new Padding(0, 0, 8, 0), Cursor = Cursors.Hand, UseVisualStyleBackColor = false };
            b.MinimumSize = new Size(0, 32);
            if (kind == 0) { b.BackColor = Th.Yel; b.ForeColor = Th.Dark; b.FlatAppearance.BorderColor = Th.Yel; b.FlatAppearance.MouseOverBackColor = Color.FromArgb(0xff, 0xd8, 0x4a); }
            else if (kind == 1) { b.BackColor = Th.Card; b.ForeColor = Th.Ink; b.FlatAppearance.BorderColor = Th.Ink; b.FlatAppearance.MouseOverBackColor = Th.Blue; }
            else { b.BackColor = Th.Red; b.ForeColor = Color.White; b.FlatAppearance.BorderColor = Th.Red; b.FlatAppearance.MouseOverBackColor = Color.FromArgb(0xff, 0x5a, 0x4f); }
            b.FlatAppearance.BorderSize = 2;
            if (click != null) b.Click += click;
            return b;
        }
        public static TextBox Txt(int w = 200, bool pw = false)
        {
            return new TextBox { Width = w, BackColor = Th.Bg, ForeColor = Th.Ink, BorderStyle = BorderStyle.FixedSingle, Font = Th.F, UseSystemPasswordChar = pw, Margin = new Padding(0, 2, 0, 8) };
        }
        public static ComboBox Combo(int w, params string[] items)
        {
            var c = new ComboBox { Width = w, DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat, BackColor = Th.Bg, ForeColor = Th.Ink, Font = Th.F, Margin = new Padding(0, 2, 0, 8) };
            c.Items.AddRange(items); if (items.Length > 0) c.SelectedIndex = 0; return c;
        }
        public static NumericUpDown Num(int w, decimal min, decimal max, decimal val, int dec = 0, decimal inc = 1)
        {
            return new NumericUpDown { Width = w, Minimum = min, Maximum = max, Value = val, DecimalPlaces = dec, Increment = inc, BackColor = Th.Bg, ForeColor = Th.Ink, Font = Th.F, BorderStyle = BorderStyle.FixedSingle, Margin = new Padding(0, 2, 0, 8) };
        }
        /// <summary>A caption above a control.</summary>
        public static Panel Field(string caption, Control c, int w)
        {
            var p = new Panel { Width = w, Height = 24 + c.Height + 8, Margin = new Padding(0, 0, 14, 0), BackColor = Color.Transparent };
            var l = Caption(caption); l.Location = new Point(0, 0); c.Location = new Point(0, 18); c.Width = w - 4; p.Controls.Add(l); p.Controls.Add(c); return p;
        }
        public static FlowLayoutPanel Flow(bool wrap = true, FlowDirection d = FlowDirection.LeftToRight)
        {
            return new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = wrap, FlowDirection = d, BackColor = Color.Transparent, Margin = new Padding(0), Padding = new Padding(0) };
        }
        public static Panel Card(string title, Control body, int height = 0)
        {
            var p = new Panel { BackColor = Th.Card, Padding = new Padding(18, 12, 14, 12), Margin = new Padding(0, 0, 0, 14), Dock = DockStyle.Fill };
            var bar = new Panel { Dock = DockStyle.Left, Width = 2, BackColor = Th.Ink };
            var tl = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, BackColor = Color.Transparent, RowCount = 2, Margin = new Padding(0) };
            tl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            if (title != null) { tl.RowStyles.Add(new RowStyle(SizeType.AutoSize)); var t = new Label { UseMnemonic = false, Text = title.ToLowerInvariant(), Font = Th.FT, ForeColor = Th.Ink, AutoSize = true, Margin = new Padding(0, 0, 0, 8) }; tl.Controls.Add(t, 0, 0); }
            else tl.RowStyles.Add(new RowStyle(SizeType.Absolute, 0));
            if (height > 0) { tl.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); body.Dock = DockStyle.Fill; p.Height = height; }
            else { tl.RowStyles.Add(new RowStyle(SizeType.AutoSize)); body.Dock = DockStyle.Top; p.AutoSize = true; p.AutoSizeMode = AutoSizeMode.GrowAndShrink; tl.AutoSize = true; tl.Dock = DockStyle.Top; }
            tl.Controls.Add(body, 0, 1);
            p.Controls.Add(tl); p.Controls.Add(bar);
            return p;
        }
        public static TableLayoutPanel Stack(Control[] rows, int fill = -1)
        {
            var t = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = rows.Length, BackColor = Color.Transparent, Margin = new Padding(0) };
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            for (int k = 0; k < rows.Length; k++)
            {
                t.RowStyles.Add(k == fill ? new RowStyle(SizeType.Percent, 100) : new RowStyle(SizeType.AutoSize));
                rows[k].Dock = DockStyle.Fill; rows[k].Margin = new Padding(0, 0, 0, rows[k].Margin.Bottom); t.Controls.Add(rows[k], 0, k);
            }
            return t;
        }
        public static Control Heading(string title, string sub)
        {
            var p = new Panel { Height = 66, BackColor = Color.Transparent, Margin = new Padding(0) };
            var h = new Label { UseMnemonic = false, Text = title.ToLowerInvariant(), Font = Th.FH, ForeColor = Th.Ink, AutoSize = true, Location = new Point(0, 0) };
            var dot = new Label { UseMnemonic = false, Text = ".", Font = Th.FH, ForeColor = Th.Yel, AutoSize = true };
            p.Controls.Add(h); p.Controls.Add(dot); int tw = TextRenderer.MeasureText(h.Text, Th.FH, new Size(2000, 60), TextFormatFlags.NoPadding).Width; dot.Location = new Point(tw - 2, 0);
            p.Controls.Add(new Label { UseMnemonic = false, Text = sub, Font = new Font("Georgia", 9.75f), ForeColor = Th.Mut, AutoSize = false, Location = new Point(2, 40), Size = new Size(1100, 22), Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right });
            return p;
        }
        public static Panel Stat(string value, string label, Color accent)
        {
            var p = new Panel { Size = new Size(154, 78), BackColor = Th.Card, Margin = new Padding(0, 0, 12, 14) };
            p.Controls.Add(new Panel { Dock = DockStyle.Left, Width = 8, BackColor = accent });
            p.Controls.Add(new Label { UseMnemonic = false, Text = value, Font = Th.FBig, ForeColor = Th.Ink, AutoSize = false, Location = new Point(18, 6), Size = new Size(132, 38), AutoEllipsis = true });
            p.Controls.Add(new Label { UseMnemonic = false, Text = label, Font = Th.FS, ForeColor = Th.Mut, AutoSize = false, Location = new Point(20, 46), Size = new Size(130, 22), AutoEllipsis = true });
            return p;
        }

        /* ---------- grid ---------- */
        public static DataGridView Grid()
        {
            var g = new DataGridView
            {
                ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false, AllowUserToResizeRows = false, RowHeadersVisible = false, MultiSelect = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect, BackgroundColor = Th.Card, BorderStyle = BorderStyle.None, EnableHeadersVisualStyles = false,
                CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal, GridColor = Th.Dim, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, Dock = DockStyle.Fill,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing, ColumnHeadersHeight = 30, ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single,
                Font = Th.F, ShowCellToolTips = true, ScrollBars = ScrollBars.Vertical
            };
            g.RowTemplate.Height = 28;
            g.DefaultCellStyle.BackColor = Th.Card; g.DefaultCellStyle.ForeColor = Th.Ink; g.DefaultCellStyle.SelectionBackColor = Th.Blue; g.DefaultCellStyle.SelectionForeColor = Color.White;
            g.DefaultCellStyle.Padding = new Padding(4, 0, 4, 0);
            g.ColumnHeadersDefaultCellStyle.BackColor = Th.Card; g.ColumnHeadersDefaultCellStyle.ForeColor = Th.Mut; g.ColumnHeadersDefaultCellStyle.SelectionBackColor = Th.Card;
            g.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 8f, FontStyle.Bold); g.ColumnHeadersDefaultCellStyle.Padding = new Padding(4, 0, 4, 0);
            try { typeof(DataGridView).GetProperty("DoubleBuffered", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(g, true, null); } catch { }
            return g;
        }
        public static void Fill(DataGridView g, string[] headers, List<string[]> rows, List<object> tags, string empty)
        {
            g.SuspendLayout(); g.Rows.Clear(); g.Columns.Clear();
            foreach (var h in headers) g.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = h, MinimumWidth = 30, SortMode = DataGridViewColumnSortMode.Automatic });
            for (int k = 0; k < rows.Count; k++) { int ix = g.Rows.Add((object[])rows[k]); g.Rows[ix].Tag = tags != null ? tags[k] : null; }
            Label el = null; foreach (Control c in g.Controls) if (c is Label && c.Name == "emptyMsg") el = (Label)c;
            if (el == null) { el = new Label { UseMnemonic = false, Name = "emptyMsg", AutoSize = true, ForeColor = Th.Mut, BackColor = Th.Card, Font = Th.F, Location = new Point(8, g.ColumnHeadersHeight + 8) }; g.Controls.Add(el); }
            el.Text = empty ?? ""; el.Visible = rows.Count == 0;
            g.ResumeLayout();
            try { g.CurrentCell = null; g.ClearSelection(); } catch { }
            if (!g.IsHandleCreated) g.HandleCreated += (s, e) => { try { g.CurrentCell = null; g.ClearSelection(); } catch { } };
        }
        public static T Sel<T>(DataGridView g) where T : class
        {
            if (g.SelectedRows.Count == 0) return null; return g.SelectedRows[0].Tag as T;
        }

        /* ---------- dialogs ---------- */
        public static Form Dlg(string title, int w, int h)
        {
            return new Form { Text = title, ClientSize = new Size(w, h), StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false,
                ShowInTaskbar = false, BackColor = Th.Card, ForeColor = Th.Ink, Font = Th.F };
        }
        public static bool Confirm(IWin32Window o, string msg) { return MessageBox.Show(o, msg, "ErrinILS", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes; }
        public static void Info(IWin32Window o, string msg) { MessageBox.Show(o, msg, "ErrinILS", MessageBoxButtons.OK, MessageBoxIcon.Information); }
    }

    /// <summary>Simple bar chart: horizontal bars with labels, or vertical columns.</summary>
    public class BarChart : Control
    {
        public List<KeyValuePair<string, double>> Data = new List<KeyValuePair<string, double>>();
        public bool Vertical;
        public BarChart() { SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true); BackColor = Th.Card; }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; g.Clear(Th.Card); g.SmoothingMode = SmoothingMode.None;
            if (Data.Count == 0) { TextRenderer.DrawText(g, "No data yet", Th.F, new Point(2, 4), Th.Mut); return; }
            double mx = Math.Max(1, Data.Max(d => d.Value));
            if (Vertical)
            {
                int n = Data.Count, gap = 10, w = Math.Max(10, (Width - gap * (n + 1)) / n), top = 4, bot = 38, hmax = Height - top - bot;
                for (int k = 0; k < n; k++)
                {
                    int x = gap + k * (w + gap), h = (int)(Data[k].Value / mx * hmax);
                    using (var b = new SolidBrush(Th.Yel)) g.FillRectangle(b, x, top + hmax - h, w, h);
                    var r = new Rectangle(x - 6, top + hmax + 2, w + 12, 34); var f = TextFormatFlags.HorizontalCenter | TextFormatFlags.WordBreak;
                    TextRenderer.DrawText(g, Data[k].Value.ToString("0") + "\n" + Data[k].Key, Th.FS, r, Th.Mut, f);
                }
            }
            else
            {
                int rowH = 26, labW = Math.Min(170, Width / 3), valW = 40;
                for (int k = 0; k < Data.Count; k++)
                {
                    int y = 4 + k * rowH; if (y + rowH > Height + rowH) break;
                    TextRenderer.DrawText(g, Data[k].Key, Th.F, new Rectangle(0, y, labW - 6, rowH), Th.Ink, TextFormatFlags.EndEllipsis | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
                    int bw = (int)(Data[k].Value / mx * Math.Max(10, Width - labW - valW - 10));
                    using (var b = new SolidBrush(k % 2 == 0 ? Th.Blue : Th.Yel)) g.FillRectangle(b, labW, y + 5, Math.Max(3, bw), 16);
                    TextRenderer.DrawText(g, Data[k].Value.ToString("0"), Th.F, new Point(labW + Math.Max(3, bw) + 6, y + 3), Th.Ink);
                }
            }
        }
    }

    /// <summary>Code 128 (set B) barcode labels, drawn in 1/100 inch units for both preview and printing.</summary>
    public static class Labels
    {
        const string P = "212222 222122 222221 121223 121322 131222 122213 122312 132212 221213 221312 231212 112232 122132 122231 113222 123122 123221 223211 221132 221231 213212 223112 312131 311222 321122 321221 312212 322112 322211 212123 212321 232121 111323 131123 131321 112313 132113 132311 211313 231113 231311 112133 112331 132131 113123 113321 133121 313121 211331 231131 213113 213311 213131 311123 311321 331121 312113 312311 332111 314111 221411 431111 111224 111422 121124 121421 141122 141221 112214 112412 122114 122411 142112 142211 241211 221114 413111 241112 134111 111242 121142 121241 114212 124112 124211 411212 421112 421211 212141 214121 412121 111143 111341 131141 114113 114311 411113 411311 113141 114131 311141 411131 211412 211214 211232 2331112";
        static readonly string[] C128 = P.Split(' ');
        public const int W = 276, H = 118;            // 70 x 30 mm
        public static List<int> Modules(string text, out int total)
        {
            var v = new List<int> { 104 };
            foreach (char ch in text ?? "") v.Add(ch >= 32 && ch <= 126 ? ch - 32 : 0);
            int sum = 104; for (int i = 1; i < v.Count; i++) sum += v[i] * i;
            v.Add(sum % 103); v.Add(106);
            var w = new List<int>(); total = 0;
            foreach (int k in v) foreach (char c in C128[k]) { w.Add(c - '0'); total += c - '0'; }
            return w;
        }
        static Font WF(float pt, FontStyle s) { return new Font("Arial", pt / 72f * 100f, s, GraphicsUnit.World); }
        public static void Draw(Graphics g, float x, float y, Item it)
        {
            using (var pen = new Pen(Color.FromArgb(150, 150, 150)) { DashStyle = DashStyle.Dash }) g.DrawRectangle(pen, x, y, W, H);
            using (var fc = WF(15f, FontStyle.Bold)) using (var ft = WF(6.5f, FontStyle.Regular)) using (var fx = WF(8f, FontStyle.Regular))
            {
                float pad = 8, cw = 94;
                var call = string.Join("\n", (string.IsNullOrEmpty(it.call) ? "\u2014" : it.call).Split(' '));
                var sfC = new StringFormat { LineAlignment = StringAlignment.Center };
                g.DrawString(call, fc, Brushes.Black, new RectangleF(x + pad, y, cw, H), sfC);
                float rx = x + pad + cw + 10, rw = W - pad - cw - 10 - pad;
                var sfT = new StringFormat { Alignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap };
                g.DrawString(it.title ?? "", ft, Brushes.Black, new RectangleF(rx, y + 12, rw, 18), sfT);
                int total; var mod = Modules(it.barcode, out total); float sc = rw / (total + 20f), cx = rx + 10 * sc; bool bar = true;
                foreach (int m in mod) { if (bar) g.FillRectangle(Brushes.Black, cx, y + 32, m * sc, 50); cx += m * sc; bar = !bar; }
                g.DrawString(it.barcode ?? "", fx, Brushes.Black, new RectangleF(rx, y + 85, rw, 20), new StringFormat { Alignment = StringAlignment.Center });
            }
        }
        /// <summary>Lay out many identical-size items on pages and print them after the user chooses a printer.</summary>
        public static bool PrintSheet<T>(IWin32Window owner, List<T> all, int w, int h, int margin, Action<Graphics, float, float, T> draw, string name)
        {
            if (all.Count == 0) return false;
            var doc = new PrintDocument { DocumentName = name }; doc.DefaultPageSettings.Margins = new Margins(margin, margin, margin, margin); int pos = 0;
            doc.PrintPage += (s, e) =>
            {
                var m = e.MarginBounds; int gap = 12, cols = Math.Max(1, (m.Width + gap) / (w + gap)), rows = Math.Max(1, (m.Height + gap) / (h + gap));
                for (int r = 0; r < rows && pos < all.Count; r++) for (int c = 0; c < cols && pos < all.Count; c++, pos++) draw(e.Graphics, m.Left + c * (w + gap), m.Top + r * (h + gap), all[pos]);
                e.HasMorePages = pos < all.Count;
            };
            using (var pd = new PrintDialog { Document = doc, UseEXDialog = true }) { if (pd.ShowDialog(owner) != DialogResult.OK) return false; }
            doc.Print(); return true;
        }
        /// <summary>Print labels (item repeated n times).</summary>
        public static bool Print(IWin32Window owner, List<Item> list, int copies)
        {
            var all = new List<Item>(); foreach (var i in list) for (int k = 0; k < Math.Min(copies, 50); k++) all.Add(i);
            return PrintSheet(owner, all, W, H, 100, (g, x, y, it) => Draw(g, x, y, it), "ErrinILS labels");
        }
    }

    /// <summary>Spine label: just the call number, one part per line (e.g. "523.1" over "HAW"), 1 x 1.5 inch. Units: 1/100 inch.</summary>
    public static class SpineArt
    {
        public const int W = 100, H = 150;
        public static List<string> Lines(string call) { return (call ?? "").Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).ToList(); }
        public static void Draw(Graphics g, float x, float y, Item it)
        {
            using (var pen = new Pen(Color.FromArgb(150, 150, 150)) { DashStyle = DashStyle.Dash }) g.DrawRectangle(pen, x, y, W, H);
            var lines = Lines(it.call); if (lines.Count == 0) return;
            var sf = new StringFormat(StringFormatFlags.NoWrap) { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.None };
            float pt = 18f; Font f = null; float lh = 0;
            for (; pt >= 6f; pt -= 1f)                               // largest size at which every line fits the label
            {
                if (f != null) f.Dispose();
                f = new Font("Arial", pt / 72f * 100f, FontStyle.Bold, GraphicsUnit.World);
                float wmax = 0; foreach (var l in lines) wmax = Math.Max(wmax, g.MeasureString(l, f, 1000, sf).Width);
                lh = f.GetHeight(g) * 1.05f;
                if (wmax <= W - 8 && lh * lines.Count <= H - 20) break;
            }
            using (f)
            {
                float top = y + (H - lh * lines.Count) / 2;
                for (int i = 0; i < lines.Count; i++) g.DrawString(lines[i], f, Brushes.Black, new RectangleF(x, top + i * lh, W, lh), sf);
            }
            sf.Dispose();
        }
        public static bool Print(IWin32Window owner, List<Item> list, int copies)
        {
            var all = new List<Item>(); foreach (var i in list) if (Lines(i.call).Count > 0) for (int k = 0; k < Math.Min(copies, 50); k++) all.Add(i);
            return Labels.PrintSheet(owner, all, W, H, 100, (g, x, y, it) => Draw(g, x, y, it), "ErrinILS spine labels");
        }
    }

    /// <summary>Library card (credit-card size, 85.6 x 54 mm) with a Code 128 barcode of the IC number. Units: 1/100 inch.</summary>
    public static class CardArt
    {
        public const int W = 337, H = 213;
        static Font WF(float pt, FontStyle s) { return new Font("Arial", pt / 72f * 100f, s, GraphicsUnit.World); }
        public static void Draw(Graphics g, float x, float y, Patron p, string school)
        {
            g.FillRectangle(Brushes.White, x, y, W, H);
            using (var pen = new Pen(Color.FromArgb(120, 120, 120))) g.DrawRectangle(pen, x, y, W, H);
            using (var blue = new SolidBrush(Th.Blue)) g.FillRectangle(blue, x, y, W, 50);
            using (var yel = new SolidBrush(Th.Yel)) g.FillRectangle(yel, x, y + 50, W, 5);
            using (var f1 = WF(19f, FontStyle.Bold)) using (var f2 = WF(7.5f, FontStyle.Bold)) using (var f3 = WF(15f, FontStyle.Bold)) using (var f4 = WF(9f, FontStyle.Regular)) using (var f5 = WF(8f, FontStyle.Regular)) using (var f6 = WF(6.5f, FontStyle.Regular))
            {
                g.DrawString("errin.", f1, Brushes.White, x + 12, y + 7);
                var right = new StringFormat { Alignment = StringAlignment.Far, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap };
                bool hasSchool = !string.IsNullOrWhiteSpace(school);
                if (hasSchool) g.DrawString(school.ToUpperInvariant(), f2, Brushes.White, new RectangleF(x + 100, y + 12, W - 112, 16), right);
                g.DrawString("LIBRARY CARD", f2, Brushes.White, new RectangleF(x + 100, hasSchool ? y + 28 : y + 20, W - 112, 16), right);
                var left = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap };
                g.DrawString(p.name ?? "", f3, Brushes.Black, new RectangleF(x + 14, y + 62, W - 28, 24), left);
                g.DrawString(string.IsNullOrEmpty(p.cls) ? " " : p.cls, f4, Brushes.DimGray, new RectangleF(x + 14, y + 86, W - 28, 16), left);
                g.DrawString("IC: " + p.id, f4, Brushes.Black, new RectangleF(x + 14, y + 102, W - 28, 16), left);
                float rw = 250, rx = x + (W - rw) / 2; int total; var mod = Labels.Modules(p.id, out total); float sc = rw / (total + 20f), cx = rx + 10 * sc; bool bar = true;
                foreach (int m in mod) { if (bar) g.FillRectangle(Brushes.Black, cx, y + 124, m * sc, 44); cx += m * sc; bar = !bar; }
                g.DrawString(p.id ?? "", f5, Brushes.Black, new RectangleF(x, y + 170, W, 16), new StringFormat { Alignment = StringAlignment.Center });
                g.DrawString("Please bring this card when you borrow books.", f6, Brushes.DimGray, new RectangleF(x, y + 191, W, 16), new StringFormat { Alignment = StringAlignment.Center });
            }
        }
    }
}
