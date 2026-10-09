// ErrinILS Z39.50 (ISO 23950), written from the protocol spec with no extra libraries.
//   Server: Init, Search (type-1 RPN, Bib-1 attributes), Present, Close. Read-only. Records as USMARC or MARCXML.
//   Client: search another library's Z39.50 target by ISBN / title / author / keyword and retrieve MARC.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace Errin
{
    public class ZErr : Exception
    {
        public int Code; public string Add;
        public ZErr(int code, string add = "") : base("Z39.50 diagnostic " + code + (string.IsNullOrEmpty(add) ? "" : " (" + add + ")")) { Code = code; Add = add ?? ""; }
    }
    class NeedMore : Exception { }

    public class Node
    {
        public int Cls, Tag; public bool Cons; public List<Node> Kids; public byte[] Val;
    }

    public static class Ber
    {
        public const int UNI = 0, CTX = 2;
        public static byte[] Cat(params byte[][] a) { var ms = new MemoryStream(); foreach (var b in a) if (b != null) ms.Write(b, 0, b.Length); return ms.ToArray(); }
        static byte[] TagBytes(int cls, bool cons, int tag)
        {
            int b = (cls << 6) | (cons ? 0x20 : 0);
            if (tag < 31) return new[] { (byte)(b | tag) };
            var t = new List<byte> { (byte)(tag & 127) }; int x = tag >> 7;
            while (x > 0) { t.Insert(0, (byte)((x & 127) | 128)); x >>= 7; }
            t.Insert(0, (byte)(b | 31)); return t.ToArray();
        }
        static byte[] LenBytes(int n)
        {
            if (n < 128) return new[] { (byte)n };
            var a = new List<byte>(); while (n > 0) { a.Insert(0, (byte)(n & 255)); n >>= 8; }
            a.Insert(0, (byte)(128 | a.Count)); return a.ToArray();
        }
        public static byte[] Tlv(int cls, bool cons, int tag, byte[] content) { return Cat(TagBytes(cls, cons, tag), LenBytes(content.Length), content); }
        static byte[] IntBytes(long v)
        {
            var a = new List<byte>(); long x = v;
            do { a.Insert(0, (byte)(x & 255)); x >>= 8; } while (x > 0);
            if ((a[0] & 128) != 0) a.Insert(0, 0); return a.ToArray();
        }
        public static byte[] I(int cls, int tag, long v) { return Tlv(cls, false, tag, IntBytes(v)); }
        public static byte[] S(int cls, int tag, string s) { return Tlv(cls, false, tag, Encoding.UTF8.GetBytes(s ?? "")); }
        public static byte[] B(int cls, int tag, bool v) { return Tlv(cls, false, tag, new byte[] { (byte)(v ? 255 : 0) }); }
        public static byte[] C(int cls, int tag, params byte[][] kids) { return Tlv(cls, true, tag, Cat(kids)); }
        public static byte[] OidContent(string s)
        {
            var p = s.Split('.').Select(int.Parse).ToArray(); var o = new List<byte> { (byte)(p[0] * 40 + p[1]) };
            foreach (int n in p.Skip(2))
            {
                var t = new List<byte> { (byte)(n & 127) }; int x = n >> 7;
                while (x > 0) { t.Insert(0, (byte)((x & 127) | 128)); x >>= 7; }
                o.AddRange(t);
            }
            return o.ToArray();
        }
        public static byte[] Oid(string s) { return Tlv(UNI, false, 6, OidContent(s)); }
        public static string OidStr(byte[] b)
        {
            var o = new List<string> { (b[0] / 40).ToString(), (b[0] % 40).ToString() }; long n = 0;
            for (int k = 1; k < b.Length; k++) { n = n * 128 + (b[k] & 127); if ((b[k] & 128) == 0) { o.Add(n.ToString()); n = 0; } }
            return string.Join(".", o.ToArray());
        }
        public static byte[] Bits(int[] on, int len)
        {
            int nb = (len + 7) / 8; var b = new byte[nb + 1]; b[0] = (byte)(nb * 8 - len);
            foreach (int k in on) b[1 + (k >> 3)] |= (byte)(0x80 >> (k & 7));
            return b;
        }

        // decode one TLV at buf[off]; throws NeedMore when the buffer is incomplete
        public static Node Dec(byte[] buf, ref int off, int limit, int depth)
        {
            if (depth > 40) throw new Exception("nesting too deep");
            if (off >= limit) throw new NeedMore();
            int b = buf[off++], cls = b >> 6; bool cons = (b & 32) != 0; int tag = b & 31;
            if (tag == 31)
            {
                tag = 0;
                for (;;) { if (off >= limit) throw new NeedMore(); int x = buf[off++]; tag = tag * 128 + (x & 127); if ((x & 128) == 0) break; }
            }
            if (off >= limit) throw new NeedMore();
            long l = buf[off++];
            if (l == 0x80)
            {
                if (!cons) throw new Exception("bad indefinite length");
                var kids = new List<Node>();
                for (;;)
                {
                    if (off + 1 >= limit) throw new NeedMore();
                    if (buf[off] == 0 && buf[off + 1] == 0) { off += 2; break; }
                    kids.Add(Dec(buf, ref off, limit, depth + 1));
                }
                return new Node { Cls = cls, Cons = cons, Tag = tag, Kids = kids };
            }
            if ((l & 128) != 0)
            {
                int n = (int)(l & 127); if (n > 4) throw new Exception("length too large");
                if (off + n > limit) throw new NeedMore();
                l = 0; for (int k = 0; k < n; k++) l = l * 256 + buf[off++];
            }
            if (l > 16 * 1024 * 1024) throw new Exception("PDU too large");
            if (off + l > limit) throw new NeedMore();
            int end = (int)(off + l);
            if (cons)
            {
                var kids = new List<Node>();
                while (off < end)
                {
                    try { kids.Add(Dec(buf, ref off, end, depth + 1)); }
                    catch (NeedMore) { throw new Exception("malformed constructed element"); }
                }
                return new Node { Cls = cls, Cons = cons, Tag = tag, Kids = kids };
            }
            var val = new byte[end - off]; Buffer.BlockCopy(buf, off, val, 0, val.Length); off = end;
            return new Node { Cls = cls, Cons = cons, Tag = tag, Val = val };
        }
        public static bool TryDec(byte[] buf, out Node n, out int used)
        {
            int off = 0; n = null; used = 0;
            try { n = Dec(buf, ref off, buf.Length, 0); used = off; return true; }
            catch (NeedMore) { return false; }
        }
        public static Node Kid(Node n, int cls, int tag) { return n == null || n.Kids == null ? null : n.Kids.FirstOrDefault(k => k.Cls == cls && k.Tag == tag); }
        public static Node Ctx(Node n, int tag) { return Kid(n, CTX, tag); }
        public static long NInt(Node n)
        {
            if (n == null || n.Val == null || n.Val.Length == 0) return 0;
            long v = (n.Val[0] & 128) != 0 ? -1 : 0; foreach (byte x in n.Val) v = v * 256 + x; return v;
        }
        public static string NStr(Node n) { return n != null && n.Val != null ? Encoding.UTF8.GetString(n.Val) : ""; }
    }

    public static class Z
    {
        public const string BIB1 = "1.2.840.10003.3.1", USMARC = "1.2.840.10003.5.10", XML = "1.2.840.10003.5.109.10", DIAG = "1.2.840.10003.4.1";
        public const int INIT_REQ = 20, INIT_RES = 21, SEARCH_REQ = 22, SEARCH_RES = 23, PRESENT_REQ = 24, PRESENT_RES = 25, CLOSE = 48;
        const int UNI = Ber.UNI, CTX = Ber.CTX;
        static readonly Dictionary<int, string> DIAGS = new Dictionary<int, string> {
            {1,"Permanent system error"},{2,"Temporary system error"},{3,"Unsupported search"},{13,"Present request out of range"},{30,"Specified result set does not exist"},
            {107,"Query type not supported"},{108,"Malformed query"},{109,"Database unavailable"},{110,"Operator unsupported"},{113,"Unsupported attribute type"},
            {114,"Unsupported Use attribute"},{123,"Unsupported attribute combination"},{125,"Malformed search term"},{128,"Illegal result set name"},
            {235,"Database does not exist"},{238,"Record not available in requested syntax"},{239,"Record syntax not supported"} };
        public static string DiagText(int c, string a) { return (DIAGS.ContainsKey(c) ? DIAGS[c] : "Diagnostic " + c) + (string.IsNullOrEmpty(a) ? "" : ": " + a); }

        /* ================= search semantics (Bib-1 subset) ================= */
        static string Norm(string s)
        {
            s = (s ?? "").Normalize(NormalizationForm.FormD);
            s = Regex.Replace(s, "[\u0300-\u036f]", "").ToLowerInvariant();
            return Regex.Replace(s, "[^a-z0-9\u0080-\uffff]+", " ").Trim();
        }
        static string Compact(string s) { return Regex.Replace((s ?? "").ToLowerInvariant(), "[^a-z0-9]", ""); }
        public static string Isbn13(string s)
        {
            s = Compact(s).ToUpperInvariant();
            if (Regex.IsMatch(s, @"^\d{9}[\dX]$"))
            {
                string t = "978" + s.Substring(0, 9); int c = 0;
                for (int k = 0; k < 12; k++) c += (t[k] - '0') * (k % 2 == 1 ? 3 : 1);
                return t + ((10 - c % 10) % 10);
            }
            return s.ToLowerInvariant();
        }
        static readonly Dictionary<string, Func<Item, string[]>> FIELD = new Dictionary<string, Func<Item, string[]>> {
            {"author", i => new[]{i.author}}, {"title", i => new[]{i.title, i.sub}}, {"isbn", i => new[]{i.isbn}}, {"subject", i => new[]{i.subject}},
            {"call", i => new[]{i.call}}, {"publisher", i => new[]{i.publisher, i.place}}, {"year", i => new[]{i.year}}, {"local", i => new[]{i.ctl, i.barcode, i.acq}},
            {"any", i => new[]{i.title, i.sub, i.author, i.isbn, i.publisher, i.place, i.year, i.subject, i.call, i.ctl, i.barcode, i.acq}} };
        static readonly Dictionary<int, string> USE = new Dictionary<int, string> {
            {1,"author"},{2,"author"},{1003,"author"},{1004,"author"},{1005,"author"},{4,"title"},{5,"title"},{6,"title"},{7,"isbn"},{1007,"isbn"},{21,"subject"},
            {13,"call"},{16,"call"},{19,"call"},{12,"local"},{1018,"publisher"},{31,"year"},{1016,"any"},{1035,"any"} };

        static Func<Item, bool> Matcher(string term, Dictionary<int, int> a)
        {
            int use = a.ContainsKey(1) ? a[1] : 1016;
            if (!USE.ContainsKey(use)) throw new ZErr(114, use.ToString());
            string fld = USE[use];
            foreach (int t in a.Keys) if (t < 1 || t > 6) throw new ZErr(113, t.ToString());
            var get = FIELD[fld]; int rel = a.ContainsKey(2) ? a[2] : 3; int str = a.ContainsKey(4) ? a[4] : 0; int trunc = a.ContainsKey(5) ? a[5] : -1;
            if (rel < 1 || rel > 6) throw new ZErr(117, rel.ToString());
            if (trunc != -1 && trunc != 1 && trunc != 2 && trunc != 3 && trunc != 100) throw new ZErr(120, trunc.ToString());
            if (fld == "isbn") { string k = Isbn13(term); return i => get(i).Any(v => !string.IsNullOrEmpty(v) && Isbn13(v) == k); }
            if (fld == "local") { string k = Compact(term); return i => get(i).Any(v => !string.IsNullOrEmpty(v) && Compact(v) == k); }
            if (fld == "year" && rel != 3)
            {
                var ym = Regex.Match(term, @"\d{4}"); if (!ym.Success) throw new ZErr(125, term);
                int y = int.Parse(ym.Value);
                return i =>
                {
                    var m = Regex.Match(i.year ?? "", @"\d{4}"); if (!m.Success) return false; int v = int.Parse(m.Value);
                    return rel == 1 ? v < y : rel == 2 ? v <= y : rel == 4 ? v >= y : rel == 5 ? v > y : v != y;
                };
            }
            var words = Norm(term).Split(' ').Where(w => w != "").ToArray();
            if (words.Length == 0) throw new ZErr(125, "empty term");
            bool pre = trunc == 1, subm = trunc == 2 || trunc == 3, phrase = str == 1 || str == 108;
            return i =>
            {
                string v = " " + string.Join(" ", get(i).Select(Norm).Where(x => x != "").ToArray()) + " ";
                if (phrase) { string p = string.Join(" ", words); return subm ? v.Contains(p) : pre ? v.Contains(" " + p) : v.Contains(" " + p + " "); }
                return words.All(w => subm ? v.Contains(w) : pre ? v.Contains(" " + w) : v.Contains(" " + w + " "));
            };
        }

        // RPN tree -> list of items. sets: result sets by name
        static List<Item> EvalRpn(Node node, List<Item> items, Dictionary<string, List<Item>> sets)
        {
            if (node.Cls == CTX && node.Tag == 0)                                   // Operand
            {
                var op = node.Kids[0];
                if (op.Cls == CTX && op.Tag == 102)                                // AttributesPlusTerm
                {
                    var list = Ber.Kid(op, CTX, 44) ?? Ber.Kid(op, UNI, 16); var t = op.Kids.FirstOrDefault(k => k.Cls == CTX && (k.Tag == 45 || k.Tag == 216));
                    if (list == null || t == null) throw new ZErr(108, "bad operand");
                    var a = new Dictionary<int, int>();
                    foreach (var e in list.Kids)
                    {
                        var ty = Ber.Ctx(e, 120); var va = Ber.Ctx(e, 121);
                        if (ty == null) throw new ZErr(108, "attribute");
                        if (va == null) throw new ZErr(123, "complex attribute value");
                        a[(int)Ber.NInt(ty)] = (int)Ber.NInt(va);
                    }
                    var f = Matcher(Ber.NStr(t), a); return items.Where(f).ToList();
                }
                if (op.Cls == UNI && (op.Tag == 26 || op.Tag == 4))                // result set reference
                {
                    string n = Ber.NStr(op).ToLowerInvariant();
                    if (!sets.ContainsKey(n)) throw new ZErr(30, Ber.NStr(op));
                    return sets[n].ToList();
                }
                throw new ZErr(3, "operand type");
            }
            if (node.Cls == CTX && node.Tag == 1 && node.Kids.Count == 3)
            {
                var l = EvalRpn(node.Kids[0], items, sets); var r = EvalRpn(node.Kids[1], items, sets);
                var opn = node.Kids[2]; var o = opn.Kids != null && opn.Kids.Count > 0 ? opn.Kids[0] : null;
                if (o == null || o.Cls != CTX) throw new ZErr(110);
                var rs = new HashSet<Item>(r);
                if (o.Tag == 0) return l.Where(x => rs.Contains(x)).ToList();
                if (o.Tag == 1) { var ls = new HashSet<Item>(l); return l.Concat(r.Where(x => !ls.Contains(x))).ToList(); }
                if (o.Tag == 2) return l.Where(x => !rs.Contains(x)).ToList();
                throw new ZErr(110, "proximity");
            }
            throw new ZErr(108, "RPN structure");
        }

        /* ================= PDU builders ================= */
        static byte[] WithRef(byte[] r, params byte[][] p) { return Ber.Cat(r != null ? Ber.Tlv(CTX, false, 2, r) : null, Ber.Cat(p)); }
        class Out { public byte[] Bytes; public bool Xml; }
        static byte[] RecordsOf(List<Out> recs, string dbName)
        {
            return Ber.C(CTX, 28, recs.Select(r => Ber.C(UNI, 16, Ber.S(CTX, 0, dbName), Ber.C(CTX, 1, Ber.C(CTX, 1,
                Ber.C(UNI, 8, Ber.Oid(r.Xml ? XML : USMARC), Ber.Tlv(CTX, false, 1, r.Bytes)))))).ToArray());
        }
        static byte[] DiagRec(int code, string add) { return Ber.C(CTX, 130, Ber.Oid(DIAG), Ber.I(UNI, 2, code), Ber.S(UNI, 26, add ?? "")); }
        static byte[] InitRes(byte[] r, bool ok, int[] ver, int[] opts, long size, string info)
        {
            return Ber.C(CTX, INIT_RES, WithRef(r), Ber.Tlv(CTX, false, 3, Ber.Bits(ver, 3)), Ber.Tlv(CTX, false, 4, Ber.Bits(opts, 15)),
                Ber.I(CTX, 5, size), Ber.I(CTX, 6, size), Ber.B(CTX, 12, ok), Ber.S(CTX, 110, "ErrinILS"), Ber.S(CTX, 111, "ErrinILS Z39.50 server"), Ber.S(CTX, 112, info ?? "1.0"));
        }

        /* ================= server ================= */
        public class Server
        {
            public Func<List<Item>> GetItems; public Action<string> Log = m => { }; public string DbName = "errin";
            TcpListener lis; readonly List<TcpClient> conns = new List<TcpClient>(); int served; volatile bool stopping;
            public int Connections { get { lock (conns) return conns.Count; } }
            public int Served { get { return served; } }

            public void Start(int port, bool local)
            {
                lis = new TcpListener(local ? IPAddress.Loopback : IPAddress.Any, port); lis.Start();
                var th = new Thread(AcceptLoop) { IsBackground = true, Name = "z3950-accept" }; th.Start();
            }
            public void Stop()
            {
                stopping = true;
                try { lis.Stop(); } catch { }
                lock (conns) { foreach (var c in conns.ToArray()) { try { c.Close(); } catch { } } conns.Clear(); }
            }
            void AcceptLoop()
            {
                while (!stopping)
                {
                    TcpClient c;
                    try { c = lis.AcceptTcpClient(); } catch { break; }
                    bool full; lock (conns) full = conns.Count >= 25;
                    if (full) { try { c.Close(); } catch { } continue; }
                    lock (conns) conns.Add(c);
                    Interlocked.Increment(ref served);
                    var t = new Thread(() => Serve(c)) { IsBackground = true, Name = "z3950-conn" }; t.Start();
                }
            }
            void Serve(TcpClient c)
            {
                try
                {
                    c.NoDelay = true; var ns = c.GetStream(); ns.ReadTimeout = 10 * 60 * 1000;
                    var sess = new Session(this, ns);
                    byte[] buf = new byte[0]; var tmp = new byte[16384];
                    for (;;)
                    {
                        int n; try { n = ns.Read(tmp, 0, tmp.Length); } catch (IOException) { try { sess.CloseWith(7, "idle timeout"); } catch { } break; }
                        if (n <= 0) break;
                        buf = Ber.Cat(buf, SubArr(tmp, n));
                        if (buf.Length > 8 * 1024 * 1024) break;
                        for (;;)
                        {
                            Node pdu; int used;
                            try { if (!Ber.TryDec(buf, out pdu, out used)) break; }
                            catch (Exception e) { Log("protocol error: " + e.Message); sess.CloseWith(6, "protocol error"); return; }
                            buf = SubArr(buf, used, buf.Length - used);
                            try { sess.Handle(pdu); } catch (Exception e) { Log("PDU error: " + e.Message); sess.CloseWith(6, Trunc(e.Message, 80)); return; }
                            if (sess.Done) return;
                        }
                    }
                }
                catch { }
                finally { lock (conns) conns.Remove(c); try { c.Close(); } catch { } }
            }
        }
        static string Trunc(string s, int n) { return s.Length > n ? s.Substring(0, n) : s; }
        static byte[] SubArr(byte[] a, int n) { return SubArr(a, 0, n); }
        static byte[] SubArr(byte[] a, int off, int n) { var r = new byte[n]; Buffer.BlockCopy(a, off, r, 0, n); return r; }

        class Session
        {
            readonly Server srv; readonly NetworkStream ns; public bool Done;
            bool inited; long size = 1048576; bool v3 = true; readonly Dictionary<string, List<Item>> sets = new Dictionary<string, List<Item>>(); readonly List<string> setOrder = new List<string>();
            public Session(Server s, NetworkStream n) { srv = s; ns = n; }
            void Send(byte[] b) { try { ns.Write(b, 0, b.Length); ns.Flush(); } catch { Done = true; } }
            public void CloseWith(int reason, string msg)
            {
                Send(Ber.C(CTX, CLOSE, Ber.I(CTX, 211, reason), msg != null ? Ber.S(CTX, 3, msg) : null)); Done = true;
            }
            string Fmt(Node n) { var p = Ber.Ctx(n, 104); return p != null ? Ber.OidStr(p.Val) : USMARC; }
            List<Out> Pick(List<Item> items, int start, int count, string syn, long max)
            {
                var o = new List<Out>(); long used = 2048;
                if (syn != USMARC && syn != XML) throw new ZErr(239, syn);
                for (int k = start - 1; k < items.Count && o.Count < count; k++)
                {
                    var it = items[k];
                    byte[] bytes = syn == XML ? Encoding.UTF8.GetBytes(Marc.RecordXml(it)) : Marc.RecordBytes(it);
                    if (o.Count > 0 && used + bytes.Length + 80 > max) break;
                    o.Add(new Out { Bytes = bytes, Xml = syn == XML }); used += bytes.Length + 80;
                }
                return o;
            }
            public void Handle(Node p)
            {
                var rnode = Ber.Ctx(p, 2); byte[] rf = rnode != null ? rnode.Val : null;
                if (p.Cls != CTX) throw new Exception("bad PDU class");
                if (p.Tag == INIT_REQ)
                {
                    var vb = Ber.Ctx(p, 3); int cv = vb != null && vb.Val != null && vb.Val.Length > 1 ? vb.Val[1] : 0;
                    var have = new List<int>(); for (int k = 0; k < 3; k++) if ((cv & (0x80 >> k)) != 0) have.Add(k);
                    var ps = Ber.Ctx(p, 5); if (ps != null) size = Math.Max(4096, Math.Min(Ber.NInt(ps), 4194304));
                    inited = true; v3 = have.Contains(2);
                    if (have.Count == 0) { Send(InitRes(rf, false, new int[0], new int[0], size, null)); CloseWith(0, null); return; }
                    Send(InitRes(rf, true, have.ToArray(), v3 ? new[] { 0, 1, 14 } : new[] { 0, 1 }, size, "1.1")); return;
                }
                if (!inited) throw new Exception("Init must come first");
                if (p.Tag == SEARCH_REQ)
                {
                    long small = Ber.NInt(Ber.Ctx(p, 13)), large = Ber.NInt(Ber.Ctx(p, 14)), med = Ber.NInt(Ber.Ctx(p, 15));
                    string rsn = Ber.NStr(Ber.Ctx(p, 17)); rsn = (rsn == "" ? "default" : rsn).ToLowerInvariant();
                    var dbn = Ber.Ctx(p, 18); var dbs = dbn != null && dbn.Kids != null ? dbn.Kids.Select(Ber.NStr).ToList() : new List<string>();
                    string syn = Fmt(p);
                    Action<int, string> fail = (code, add) => Send(Ber.C(CTX, SEARCH_RES, WithRef(rf), Ber.I(CTX, 23, 0), Ber.I(CTX, 24, 0), Ber.I(CTX, 25, 0), Ber.B(CTX, 22, false), DiagRec(code, add)));
                    try
                    {
                        if (dbs.Count == 0 || !dbs.All(d => string.Equals(d, srv.DbName, StringComparison.OrdinalIgnoreCase) || string.Equals(d, "default", StringComparison.OrdinalIgnoreCase)))
                            throw new ZErr(235, dbs.FirstOrDefault(d => !string.Equals(d, srv.DbName, StringComparison.OrdinalIgnoreCase)) ?? "");
                        var rep = Ber.Ctx(p, 16);
                        if (rep == null || rep.Val == null || rep.Val.Length == 0 || rep.Val[0] == 0) { if (sets.ContainsKey(rsn)) throw new ZErr(128, rsn); }
                        var q = Ber.Ctx(p, 21); if (q == null || q.Kids == null || q.Kids.Count == 0) throw new ZErr(108, "no query");
                        var t1 = q.Kids[0]; if (t1.Cls != CTX || t1.Tag != 1) throw new ZErr(107, "only type-1 (RPN) queries");
                        var asn = t1.Kids[0]; if (asn == null || asn.Tag != 6 || Ber.OidStr(asn.Val) != BIB1) throw new ZErr(121, asn != null && asn.Val != null ? Ber.OidStr(asn.Val) : "");
                        var res = EvalRpn(t1.Kids[1], srv.GetItems(), sets);
                        if (!sets.ContainsKey(rsn)) setOrder.Add(rsn);
                        sets[rsn] = res;
                        if (sets.Count > 20) { sets.Remove(setOrder[0]); setOrder.RemoveAt(0); }
                        int n = res.Count; var recs = new List<Out>(); int want = 0;
                        if (n > 0 && n <= small) want = n; else if (n > 0 && n <= large && med > 0) want = (int)Math.Min(med, n);
                        if (want > 0) { if (syn != USMARC && syn != XML) throw new ZErr(239, syn); recs = Pick(res, 1, want, syn, size); }
                        Send(Ber.C(CTX, SEARCH_RES, WithRef(rf), Ber.I(CTX, 23, n), Ber.I(CTX, 24, recs.Count), Ber.I(CTX, 25, recs.Count > 0 ? recs.Count + 1 : (n > 0 ? 1 : 0)), Ber.B(CTX, 22, true),
                            recs.Count > 0 ? Ber.I(CTX, 27, recs.Count < want ? 2 : 0) : null, recs.Count > 0 ? RecordsOf(recs, srv.DbName) : null));
                    }
                    catch (ZErr e) { fail(e.Code, e.Add); }
                    catch (Exception e) { srv.Log("search error: " + e.Message); fail(2, "internal error"); }
                    return;
                }
                if (p.Tag == PRESENT_REQ)
                {
                    string rsn = Ber.NStr(Ber.Ctx(p, 31)); string key = (rsn == "" ? "default" : rsn).ToLowerInvariant();
                    long start = Ber.NInt(Ber.Ctx(p, 30)), cnt = Ber.NInt(Ber.Ctx(p, 29));
                    Action<int, string> bad = (code, add) => Send(Ber.C(CTX, PRESENT_RES, WithRef(rf), Ber.I(CTX, 24, 0), Ber.I(CTX, 25, 0), Ber.I(CTX, 27, 5), DiagRec(code, add)));
                    if (!sets.ContainsKey(key)) { bad(30, rsn); return; }
                    var set = sets[key];
                    if (start < 1 || start > set.Count || cnt < 0) { bad(13, start.ToString()); return; }
                    try
                    {
                        int want = (int)Math.Min(cnt, set.Count - start + 1);
                        var recs = Pick(set, (int)start, want, Fmt(p), size);
                        Send(Ber.C(CTX, PRESENT_RES, WithRef(rf), Ber.I(CTX, 24, recs.Count), Ber.I(CTX, 25, start + recs.Count > set.Count ? 0 : start + recs.Count),
                            Ber.I(CTX, 27, recs.Count < want ? 2 : 0), recs.Count > 0 ? RecordsOf(recs, srv.DbName) : null));
                    }
                    catch (ZErr e) { bad(e.Code, e.Add); }
                    return;
                }
                if (p.Tag == CLOSE) { Send(Ber.C(CTX, CLOSE, WithRef(rf), Ber.I(CTX, 211, 0))); Done = true; return; }
                CloseWith(6, "unsupported service");
            }
        }

        public static Server StartServer(int port, bool local, string dbName, Func<List<Item>> getItems, Action<string> log)
        {
            var s = new Server { GetItems = getItems, DbName = string.IsNullOrEmpty(dbName) ? "errin" : dbName, Log = log ?? (m => { }) };
            s.Start(port, local); return s;
        }
        public static List<string> LanAddresses()
        {
            var a = new List<string>();
            try
            {
                foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus != OperationalStatus.Up || ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                    foreach (var ua in ni.GetIPProperties().UnicastAddresses)
                        if (ua.Address.AddressFamily == AddressFamily.InterNetwork) a.Add(ua.Address.ToString());
                }
            }
            catch { }
            return a;
        }

        /* ================= client ================= */
        class Conn : IDisposable
        {
            readonly TcpClient c; readonly NetworkStream ns; byte[] buf = new byte[0]; readonly string host; readonly int port;
            public Conn(string host, int port, int ms)
            {
                this.host = host; this.port = port; c = new TcpClient();
                IAsyncResult ar;
                try { ar = c.BeginConnect(host, port, null, null); }
                catch (SocketException e) { throw Wrap(e); }
                if (!ar.AsyncWaitHandle.WaitOne(ms)) { c.Close(); throw new Exception("Timed out waiting for the Z39.50 server"); }
                try { c.EndConnect(ar); } catch (SocketException e) { throw Wrap(e); }
                c.ReceiveTimeout = ms; c.SendTimeout = ms; ns = c.GetStream();
            }
            Exception Wrap(SocketException e)
            {
                if (e.SocketErrorCode == SocketError.HostNotFound || e.SocketErrorCode == SocketError.TryAgain) return new Exception("Host not found: " + host);
                if (e.SocketErrorCode == SocketError.ConnectionRefused) return new Exception("Connection refused by " + host + ":" + port);
                return new Exception(e.Message);
            }
            public void Send(byte[] b) { ns.Write(b, 0, b.Length); ns.Flush(); }
            public Node Recv()
            {
                var tmp = new byte[16384];
                for (;;)
                {
                    Node n; int used;
                    if (buf.Length > 0 && Ber.TryDec(buf, out n, out used)) { buf = SubArr(buf, used, buf.Length - used); return n; }
                    int r;
                    try { r = ns.Read(tmp, 0, tmp.Length); }
                    catch (IOException) { throw new Exception("Timed out waiting for the Z39.50 server"); }
                    if (r <= 0) throw new Exception("The server closed the connection without replying. Check the host, port and database name.");
                    buf = Ber.Cat(buf, SubArr(tmp, r));
                }
            }
            public void Dispose() { try { c.Close(); } catch { } }
        }
        static readonly Dictionary<string, int> FIELDS = new Dictionary<string, int> { { "isbn", 7 }, { "title", 4 }, { "author", 1003 }, { "subject", 21 }, { "any", 1016 }, { "lccn", 9 } };
        static Tuple<int, string> DiagFrom(Node n)
        {
            if (n == null || n.Tag != 130 || n.Kids == null) return null;
            var c = n.Kids.FirstOrDefault(x => x.Cls == UNI && x.Tag == 2); var a = n.Kids.FirstOrDefault(x => x.Cls == UNI && (x.Tag == 26 || x.Tag == 27));
            return Tuple.Create((int)Ber.NInt(c), Ber.NStr(a));
        }
        static Node FindExternal(Node n)
        {
            if (n.Cls == UNI && n.Tag == 8 && n.Cons) return n;
            if (n.Kids != null) foreach (var k in n.Kids) { var r = FindExternal(k); if (r != null) return r; }
            return null;
        }
        class RawRec { public string Syn; public byte[] Data; }
        static List<RawRec> ExtRecords(Node recs)
        {
            var o = new List<RawRec>(); if (recs == null || recs.Kids == null) return o;
            foreach (var npr in recs.Kids)
            {
                var e = FindExternal(npr); if (e == null) continue;
                var oid = e.Kids.FirstOrDefault(k => k.Cls == UNI && k.Tag == 6); string syn = oid != null ? Ber.OidStr(oid.Val) : "";
                byte[] data = null;
                foreach (var k in e.Kids)
                {
                    if (k.Cls == CTX && k.Tag == 1 && k.Val != null) data = k.Val;
                    else if (k.Cls == CTX && k.Tag == 0 && k.Kids != null && k.Kids.Count > 0 && k.Kids[0].Val != null) data = k.Kids[0].Val;
                }
                if (data != null) o.Add(new RawRec { Syn = syn, Data = data });
            }
            return o;
        }

        public class SearchOpts { public string Host = "", Db = "Default", Field = "any", Term = "", User = "", Password = ""; public int Port = 210, Max = 20, Timeout = 15000; }
        public class SearchResult { public int Count; public List<Item> Items = new List<Item>(); public int Skipped; }

        /// <summary>The Search request as real servers (YAZ, Voyager, Koha, Zebra...) expect it. Tested against yaz-ztest.</summary>
        public static byte[] BuildSearch(SearchOpts opt)
        {
            int use = FIELDS.ContainsKey(opt.Field ?? "") ? FIELDS[opt.Field] : 1016;
            // AttributeList is context tag [44]; each database name is context tag [105] (a plain SEQUENCE / VisibleString is rejected as "malformed")
            var term = Ber.C(CTX, 102, Ber.C(CTX, 44, Ber.C(UNI, 16, Ber.I(CTX, 120, 1), Ber.I(CTX, 121, use))), Ber.S(CTX, 45, opt.Term));
            var query = Ber.C(CTX, 21, Ber.C(CTX, 1, Ber.Oid(BIB1), Ber.C(CTX, 0, term)));
            var fset = Ber.C(CTX, 100, Ber.S(CTX, 0, "F"));
            return Ber.C(CTX, SEARCH_REQ, Ber.I(CTX, 13, 0), Ber.I(CTX, 14, 1), Ber.I(CTX, 15, 0), Ber.B(CTX, 16, true), Ber.S(CTX, 17, "default"),
                Ber.C(CTX, 18, Ber.S(CTX, 105, string.IsNullOrEmpty(opt.Db) ? "Default" : opt.Db)), fset, Ber.Tlv(CTX, false, 104, Ber.OidContent(USMARC)), query);
        }
        static readonly string[] CLOSE_REASONS = { "finished", "shutting down", "system problem", "cost limit", "resources", "security violation", "protocol error", "no activity", "peer abort", "unspecified" };
        /// <summary>Check a reply is the expected kind; if the server ended the session instead, say why.</summary>
        static void Expect(Node n, int tag, string doing)
        {
            if (n.Cls == CTX && n.Tag == tag) return;
            if (n.Cls == CTX && n.Tag == CLOSE)
            {
                int r = (int)Ber.NInt(Ber.Ctx(n, 211)); string info = Ber.NStr(Ber.Ctx(n, 3));
                throw new Exception("The server ended the session while " + doing + " (" + (r >= 0 && r < CLOSE_REASONS.Length ? CLOSE_REASONS[r] : "reason " + r) + (info != "" ? ": " + info : "") + "). Check the host, port and database name.");
            }
            throw new Exception("The server sent an unexpected reply while " + doing + ". It may not be a Z39.50 server.");
        }

        public static SearchResult Search(SearchOpts opt)
        {
            int ms = opt.Timeout > 0 ? opt.Timeout : 15000, max = Math.Max(1, Math.Min(opt.Max, 50));
            if (string.IsNullOrEmpty(opt.Host) || opt.Port <= 0) throw new Exception("Host and port are required");
            int use = FIELDS.ContainsKey(opt.Field ?? "") ? FIELDS[opt.Field] : 1016;
            if (string.IsNullOrWhiteSpace(opt.Term)) throw new Exception("Nothing to search for");
            using (var c = new Conn(opt.Host, opt.Port, ms))
            {
                byte[] idAuth = !string.IsNullOrEmpty(opt.User) ? Ber.C(CTX, 7, Ber.S(CTX, 0, opt.User + "/" + (opt.Password ?? ""))) : null;
                c.Send(Ber.C(CTX, INIT_REQ, Ber.Tlv(CTX, false, 3, Ber.Bits(new[] { 0, 1, 2 }, 3)), Ber.Tlv(CTX, false, 4, Ber.Bits(new[] { 0, 1 }, 15)), Ber.I(CTX, 5, 1048576), Ber.I(CTX, 6, 1048576), idAuth,
                    Ber.S(CTX, 110, "ErrinILS"), Ber.S(CTX, 111, "ErrinILS"), Ber.S(CTX, 112, "1.2")));
                var ir = c.Recv();
                Expect(ir, INIT_RES, "connecting");
                var ok = Ber.Ctx(ir, 12);
                if (ok == null || ok.Val[0] == 0) throw new Exception("The server refused the connection" + (Ber.Ctx(ir, 11) != null ? "" : " (it may need a user name and password)"));
                c.Send(BuildSearch(opt));
                var sr = c.Recv();
                Expect(sr, SEARCH_RES, "searching");
                var status = Ber.Ctx(sr, 22); int count = (int)Ber.NInt(Ber.Ctx(sr, 23));
                if (status == null || status.Val[0] == 0)
                {
                    var d = DiagFrom(Ber.Ctx(sr, 130)); var m = Ber.Ctx(sr, 205);
                    throw new Exception("Search refused: " + (d != null ? DiagText(d.Item1, d.Item2) : m != null ? "multiple diagnostics" : "unknown reason"));
                }
                var got = new List<RawRec>(); got.AddRange(ExtRecords(Ber.Ctx(sr, 28)));
                int next = got.Count + 1;
                while (got.Count < Math.Min(count, max))
                {
                    int want = Math.Min(count, max) - got.Count;
                    c.Send(Ber.C(CTX, PRESENT_REQ, Ber.S(CTX, 31, "default"), Ber.I(CTX, 30, next), Ber.I(CTX, 29, want), Ber.C(CTX, 19, Ber.S(CTX, 0, "F")), Ber.Tlv(CTX, false, 104, Ber.OidContent(USMARC))));
                    var pr = c.Recv(); Expect(pr, PRESENT_RES, "fetching records");
                    int before = got.Count; got.AddRange(ExtRecords(Ber.Ctx(pr, 28)));
                    var d = DiagFrom(Ber.Ctx(pr, 130)); if (d != null && got.Count == before) throw new Exception("Retrieval refused: " + DiagText(d.Item1, d.Item2));
                    if (got.Count == before) break;
                    next += got.Count - before;
                }
                try { c.Send(Ber.C(CTX, CLOSE, Ber.I(CTX, 211, 0))); } catch { }
                var res = new SearchResult { Count = count };
                foreach (var r in got.Take(max))
                {
                    ParseResult p = null;
                    try
                    {
                        if (r.Syn == XML) p = Marc.ParseXml(Encoding.UTF8.GetString(r.Data));
                        else if (r.Syn == USMARC || r.Syn == "1.2.840.10003.5.1") p = Marc.ParseMrc(r.Data);
                    }
                    catch { }
                    if (p != null && p.Records.Count > 0) foreach (var x in p.Records) res.Items.Add(Marc.ToItem(x)); else res.Skipped++;
                }
                return res;
            }
        }
    }
}
