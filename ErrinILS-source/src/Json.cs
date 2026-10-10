// Minimal JSON reader/writer (no external libraries). Objects -> Dictionary<string,object>, arrays -> List<object>.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Errin
{
    public static class Json
    {
        public static object Parse(string s)
        {
            int i = 0;
            if (s.Length > 0 && s[0] == '\ufeff') i = 1;
            object v = Val(s, ref i);
            Ws(s, ref i);
            if (i < s.Length) throw new FormatException("Unexpected data after JSON value");
            return v;
        }
        static void Ws(string s, ref int i) { while (i < s.Length && char.IsWhiteSpace(s[i])) i++; }
        static void Expect(string s, ref int i, char c)
        {
            Ws(s, ref i);
            if (i >= s.Length || s[i] != c) throw new FormatException("Expected '" + c + "' at " + i);
            i++;
        }
        static object Val(string s, ref int i)
        {
            Ws(s, ref i);
            if (i >= s.Length) throw new FormatException("Unexpected end of JSON");
            char c = s[i];
            if (c == '{')
            {
                i++; var d = new Dictionary<string, object>(); Ws(s, ref i);
                if (i < s.Length && s[i] == '}') { i++; return d; }
                for (;;)
                {
                    Ws(s, ref i); string k = Str(s, ref i); Expect(s, ref i, ':'); d[k] = Val(s, ref i); Ws(s, ref i);
                    if (i < s.Length && s[i] == ',') { i++; continue; }
                    if (i < s.Length && s[i] == '}') { i++; break; }
                    throw new FormatException("Bad object at " + i);
                }
                return d;
            }
            if (c == '[')
            {
                i++; var l = new List<object>(); Ws(s, ref i);
                if (i < s.Length && s[i] == ']') { i++; return l; }
                for (;;)
                {
                    l.Add(Val(s, ref i)); Ws(s, ref i);
                    if (i < s.Length && s[i] == ',') { i++; continue; }
                    if (i < s.Length && s[i] == ']') { i++; break; }
                    throw new FormatException("Bad array at " + i);
                }
                return l;
            }
            if (c == '"') return Str(s, ref i);
            if (string.CompareOrdinal(s, i, "true", 0, 4) == 0) { i += 4; return true; }
            if (string.CompareOrdinal(s, i, "false", 0, 5) == 0) { i += 5; return false; }
            if (string.CompareOrdinal(s, i, "null", 0, 4) == 0) { i += 4; return null; }
            int st = i;
            while (i < s.Length && (char.IsDigit(s[i]) || s[i] == '-' || s[i] == '+' || s[i] == '.' || s[i] == 'e' || s[i] == 'E')) i++;
            if (i == st) throw new FormatException("Unexpected character at " + i);
            return double.Parse(s.Substring(st, i - st), CultureInfo.InvariantCulture);
        }
        static string Str(string s, ref int i)
        {
            if (i >= s.Length || s[i] != '"') throw new FormatException("Expected string at " + i);
            i++; var sb = new StringBuilder();
            while (i < s.Length)
            {
                char c = s[i++];
                if (c == '"') return sb.ToString();
                if (c != '\\') { sb.Append(c); continue; }
                if (i >= s.Length) break;
                char e = s[i++];
                switch (e)
                {
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    case 't': sb.Append('\t'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'u': sb.Append((char)Convert.ToInt32(s.Substring(i, 4), 16)); i += 4; break;
                    default: sb.Append(e); break;
                }
            }
            throw new FormatException("Unterminated string");
        }

        public static string Quote(string s)
        {
            var sb = new StringBuilder("\"");
            foreach (char c in s ?? "")
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 32) sb.Append("\\u" + ((int)c).ToString("x4")); else sb.Append(c);
                        break;
                }
            }
            return sb.Append('"').ToString();
        }

        public static string Write(object o) { var sb = new StringBuilder(); W(sb, o); return sb.ToString(); }
        static void W(StringBuilder sb, object o)
        {
            if (o == null) { sb.Append("null"); return; }
            if (o is string) { sb.Append(Quote((string)o)); return; }
            if (o is bool) { sb.Append((bool)o ? "true" : "false"); return; }
            if (o is double) { sb.Append(((double)o).ToString("R", CultureInfo.InvariantCulture)); return; }
            if (o is int || o is long) { sb.Append(Convert.ToString(o, CultureInfo.InvariantCulture)); return; }
            var d = o as IDictionary<string, object>;
            if (d != null)
            {
                sb.Append('{'); bool first = true;
                foreach (var kv in d) { if (!first) sb.Append(','); first = false; sb.Append(Quote(kv.Key)).Append(':'); W(sb, kv.Value); }
                sb.Append('}'); return;
            }
            var l = o as IEnumerable;
            if (l != null)
            {
                sb.Append('['); bool first = true;
                foreach (object x in l) { if (!first) sb.Append(','); first = false; W(sb, x); }
                sb.Append(']'); return;
            }
            sb.Append(Quote(o.ToString()));
        }

        // typed accessors used by the model classes
        public static string Str(IDictionary<string, object> d, string k)
        {
            object v; if (d == null || !d.TryGetValue(k, out v) || v == null) return "";
            if (v is double) return ((double)v).ToString("R", CultureInfo.InvariantCulture);
            return v.ToString();
        }
        public static double Num(IDictionary<string, object> d, string k)
        {
            object v; if (d == null || !d.TryGetValue(k, out v) || v == null) return 0;
            if (v is double) return (double)v;
            double r; return double.TryParse(v.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out r) ? r : 0;
        }
        public static List<object> Arr(IDictionary<string, object> d, string k)
        {
            object v; if (d != null && d.TryGetValue(k, out v)) { var l = v as List<object>; if (l != null) return l; }
            return new List<object>();
        }
        public static Dictionary<string, object> Obj(IDictionary<string, object> d, string k)
        {
            object v; if (d != null && d.TryGetValue(k, out v)) { var o = v as Dictionary<string, object>; if (o != null) return o; }
            return new Dictionary<string, object>();
        }
    }
}
