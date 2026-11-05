using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;

namespace Errin
{
    /// <summary>Minimal ZIP writer (deflate), so exports need no extra libraries.</summary>
    public class ZipOut
    {
        class E { public string Name; public uint Crc; public int Csize, Usize, Off; public ushort Method; }
        readonly MemoryStream ms = new MemoryStream(); readonly List<E> ents = new List<E>();
        static uint[] tab;
        static uint Crc(byte[] d)
        {
            if (tab == null) { tab = new uint[256]; for (uint i = 0; i < 256; i++) { uint c = i; for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1; tab[i] = c; } }
            uint r = 0xFFFFFFFFu; foreach (byte b in d) r = tab[(r ^ b) & 0xFF] ^ (r >> 8); return r ^ 0xFFFFFFFFu;
        }
        void U16(Stream s, int v) { s.WriteByte((byte)v); s.WriteByte((byte)(v >> 8)); }
        void U32(Stream s, uint v) { U16(s, (int)(v & 0xFFFF)); U16(s, (int)(v >> 16)); }
        public void Add(string name, byte[] data)
        {
            byte[] comp; using (var o = new MemoryStream()) { using (var d = new DeflateStream(o, CompressionMode.Compress, true)) d.Write(data, 0, data.Length); comp = o.ToArray(); }
            var e = new E { Name = name, Crc = Crc(data), Usize = data.Length, Off = (int)ms.Length };
            if (comp.Length >= data.Length) { comp = data; e.Method = 0; } else e.Method = 8; e.Csize = comp.Length;
            var nb = Encoding.UTF8.GetBytes(name);
            U32(ms, 0x04034b50); U16(ms, 20); U16(ms, 0x0800); U16(ms, e.Method); U16(ms, 0); U16(ms, 0x21); U32(ms, e.Crc); U32(ms, (uint)e.Csize); U32(ms, (uint)e.Usize); U16(ms, nb.Length); U16(ms, 0);
            ms.Write(nb, 0, nb.Length); ms.Write(comp, 0, comp.Length); ents.Add(e);
        }
        public byte[] Finish()
        {
            int start = (int)ms.Length;
            foreach (var e in ents)
            {
                var nb = Encoding.UTF8.GetBytes(e.Name);
                U32(ms, 0x02014b50); U16(ms, 20); U16(ms, 20); U16(ms, 0x0800); U16(ms, e.Method); U16(ms, 0); U16(ms, 0x21); U32(ms, e.Crc); U32(ms, (uint)e.Csize); U32(ms, (uint)e.Usize);
                U16(ms, nb.Length); U16(ms, 0); U16(ms, 0); U16(ms, 0); U16(ms, 0); U32(ms, 0); U32(ms, (uint)e.Off); ms.Write(nb, 0, nb.Length);
            }
            int size = (int)ms.Length - start;
            U32(ms, 0x06054b50); U16(ms, 0); U16(ms, 0); U16(ms, ents.Count); U16(ms, ents.Count); U32(ms, (uint)size); U32(ms, (uint)start); U16(ms, 0);
            return ms.ToArray();
        }
    }

    /// <summary>Minimal ZIP extractor (deflate + stored), zero external dependencies.</summary>
    public static class ZipIn
    {
        public static Dictionary<string, byte[]> Extract(byte[] b)
        {
            var res = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
            if (b == null || b.Length < 22) return res;

            int eocd = -1;
            for (int i = b.Length - 22; i >= Math.Max(0, b.Length - 65557); i--)
            {
                if (b[i] == 0x50 && b[i + 1] == 0x4b && b[i + 2] == 0x05 && b[i + 3] == 0x06)
                {
                    eocd = i;
                    break;
                }
            }
            if (eocd < 0) return res;

            ushort totalEntries = BitConverter.ToUInt16(b, eocd + 10);
            uint cdOffset = BitConverter.ToUInt32(b, eocd + 16);
            int pos = (int)cdOffset;

            for (int e = 0; e < totalEntries && pos + 46 <= b.Length; e++)
            {
                uint sig = BitConverter.ToUInt32(b, pos);
                if (sig != 0x02014b50) break;

                ushort method = BitConverter.ToUInt16(b, pos + 10);
                uint compSize = BitConverter.ToUInt32(b, pos + 20);
                uint uncompSize = BitConverter.ToUInt32(b, pos + 24);
                ushort nameLen = BitConverter.ToUInt16(b, pos + 28);
                ushort extraLen = BitConverter.ToUInt16(b, pos + 30);
                ushort commLen = BitConverter.ToUInt16(b, pos + 32);
                uint locOffset = BitConverter.ToUInt32(b, pos + 42);
                string name = Encoding.UTF8.GetString(b, pos + 46, nameLen);

                if (locOffset + 30 <= b.Length)
                {
                    ushort locNameLen = BitConverter.ToUInt16(b, (int)locOffset + 26);
                    ushort locExtraLen = BitConverter.ToUInt16(b, (int)locOffset + 28);
                    int dataPos = (int)locOffset + 30 + locNameLen + locExtraLen;

                    if (dataPos + compSize <= b.Length)
                    {
                        if (method == 0) // Stored
                        {
                            byte[] d = new byte[compSize];
                            Array.Copy(b, dataPos, d, 0, (int)compSize);
                            res[name] = d;
                        }
                        else if (method == 8) // Deflated
                        {
                            try
                            {
                                using (var ms = new MemoryStream(b, dataPos, (int)compSize))
                                using (var ds = new DeflateStream(ms, CompressionMode.Decompress))
                                using (var outMs = new MemoryStream((int)uncompSize))
                                {
                                    byte[] buf = new byte[4096];
                                    int r;
                                    while ((r = ds.Read(buf, 0, buf.Length)) > 0) outMs.Write(buf, 0, r);
                                    res[name] = outMs.ToArray();
                                }
                            }
                            catch { }
                        }
                    }
                }
                pos += 46 + nameLen + extraLen + commLen;
            }
            return res;
        }
    }

    /// <summary>Pure C# reader for Excel (.xlsx) and CSV/TSV spreadsheets without third-party libraries.</summary>
    public static class ExcelReader
    {
        public static List<List<string>> ReadFile(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return new List<List<string>>();
            string ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext == ".xlsx")
            {
                try { return ParseXlsx(File.ReadAllBytes(path)); }
                catch { }
            }
            // Fallback / CSV / TSV / TXT
            try { return ParseCsv(File.ReadAllText(path, Encoding.UTF8)); }
            catch { }
            try { return ParseCsv(File.ReadAllText(path, Encoding.Default)); }
            catch { }
            return new List<List<string>>();
        }

        public static List<List<string>> ParseXlsx(byte[] bytes)
        {
            var res = new List<List<string>>();
            var zip = ZipIn.Extract(bytes);
            if (zip.Count == 0) return res;

            // 1. Shared strings table
            var ss = new List<string>();
            foreach (var kv in zip)
            {
                if (kv.Key.IndexOf("sharedstrings", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    string xml = Encoding.UTF8.GetString(kv.Value);
                    foreach (Match m in Regex.Matches(xml, @"<(?:\w+:)?si\b[^>]*>([\s\S]*?)</(?:\w+:)?si>"))
                    {
                        var sb = new StringBuilder();
                        foreach (Match tm in Regex.Matches(m.Groups[1].Value, @"<(?:\w+:)?t\b[^>]*>([\s\S]*?)</(?:\w+:)?t>"))
                        {
                            sb.Append(UnescapeXml(tm.Groups[1].Value));
                        }
                        ss.Add(sb.ToString());
                    }
                    break;
                }
            }

            // 2. Locate worksheet (sheet1.xml)
            string sheetXml = null;
            foreach (var kv in zip)
            {
                if (kv.Key.IndexOf("worksheets/sheet1", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    sheetXml = Encoding.UTF8.GetString(kv.Value);
                    break;
                }
            }
            if (sheetXml == null)
            {
                foreach (var kv in zip)
                {
                    if (kv.Key.IndexOf("sheet", StringComparison.OrdinalIgnoreCase) >= 0 && kv.Key.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                    {
                        sheetXml = Encoding.UTF8.GetString(kv.Value);
                        break;
                    }
                }
            }
            if (sheetXml == null) return res;

            // 3. Parse rows and cells
            foreach (Match rm in Regex.Matches(sheetXml, @"<(?:\w+:)?row\b[^>]*>([\s\S]*?)</(?:\w+:)?row>"))
            {
                var rowMap = new Dictionary<int, string>();
                int maxCol = -1;

                foreach (Match cm in Regex.Matches(rm.Groups[1].Value, @"<(?:\w+:)?c\b([^>]*)(?:/>|>([\s\S]*?)</(?:\w+:)?c>)"))
                {
                    string attrs = cm.Groups[1].Value;
                    string body = cm.Groups[2].Success ? cm.Groups[2].Value : "";

                    // Column from cell ref (e.g. "A1", "BC12")
                    var rMatch = Regex.Match(attrs, @"\br=""([A-Za-z]+)\d+""");
                    int col = -1;
                    if (rMatch.Success)
                    {
                        col = ColNameToIndex(rMatch.Groups[1].Value);
                    }
                    if (col < 0) col = maxCol + 1;
                    if (col > maxCol) maxCol = col;

                    string type = "";
                    var tMatch = Regex.Match(attrs, @"\bt=""([^""]+)""");
                    if (tMatch.Success) type = tMatch.Groups[1].Value;

                    string val = "";
                    if (type == "s") // Shared string index
                    {
                        var vm = Regex.Match(body, @"<(?:\w+:)?v>([\s\S]*?)</(?:\w+:)?v>");
                        if (vm.Success)
                        {
                            int idx;
                            if (int.TryParse(vm.Groups[1].Value.Trim(), out idx) && idx >= 0 && idx < ss.Count)
                                val = ss[idx];
                        }
                    }
                    else if (type == "inlineStr") // Inline string
                    {
                        var tm = Regex.Match(body, @"<(?:\w+:)?t\b[^>]*>([\s\S]*?)</(?:\w+:)?t>");
                        if (tm.Success) val = UnescapeXml(tm.Groups[1].Value);
                    }
                    else // Numbers, raw strings, booleans
                    {
                        var vm = Regex.Match(body, @"<(?:\w+:)?v>([\s\S]*?)</(?:\w+:)?v>");
                        if (vm.Success) val = UnescapeXml(vm.Groups[1].Value.Trim());
                        else
                        {
                            var tm = Regex.Match(body, @"<(?:\w+:)?t\b[^>]*>([\s\S]*?)</(?:\w+:)?t>");
                            if (tm.Success) val = UnescapeXml(tm.Groups[1].Value);
                        }
                    }

                    rowMap[col] = val;
                }

                if (maxCol >= 0)
                {
                    var rowList = new List<string>();
                    for (int c = 0; c <= maxCol; c++)
                    {
                        string cellVal;
                        rowList.Add(rowMap.TryGetValue(c, out cellVal) ? cellVal : "");
                    }
                    // Only add if at least one cell has content
                    if (rowList.Exists(s => !string.IsNullOrEmpty(s.Trim())))
                    {
                        res.Add(rowList);
                    }
                }
            }

            return res;
        }

        static int ColNameToIndex(string name)
        {
            int col = 0;
            name = (name ?? "").ToUpperInvariant();
            for (int i = 0; i < name.Length; i++)
            {
                if (name[i] >= 'A' && name[i] <= 'Z')
                    col = col * 26 + (name[i] - 'A' + 1);
            }
            return col - 1;
        }

        static string UnescapeXml(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            s = s.Replace("&amp;", "&").Replace("&lt;", "<").Replace("&gt;", ">").Replace("&quot;", "\"").Replace("&apos;", "'");
            s = Regex.Replace(s, "&#x([0-9a-fA-F]+);", m => char.ConvertFromUtf32(Convert.ToInt32(m.Groups[1].Value, 16)));
            s = Regex.Replace(s, "&#(\\d+);", m => char.ConvertFromUtf32(int.Parse(m.Groups[1].Value)));
            return s;
        }

        public static List<List<string>> ParseCsv(string text)
        {
            var res = new List<List<string>>();
            if (string.IsNullOrEmpty(text)) return res;

            // Detect delimiter: comma, tab, or semicolon
            char delim = ',';
            int commas = 0, tabs = 0, semis = 0;
            for (int i = 0; i < Math.Min(text.Length, 2000); i++)
            {
                if (text[i] == ',') commas++;
                else if (text[i] == '\t') tabs++;
                else if (text[i] == ';') semis++;
            }
            if (tabs > commas && tabs > semis) delim = '\t';
            else if (semis > commas && semis > tabs) delim = ';';

            var curRow = new List<string>();
            var curField = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '"')
                {
                    if (inQuotes && i + 1 < text.Length && text[i + 1] == '"')
                    {
                        curField.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }
                }
                else if (c == delim && !inQuotes)
                {
                    curRow.Add(curField.ToString().Trim());
                    curField.Length = 0;
                }
                else if ((c == '\r' || c == '\n') && !inQuotes)
                {
                    if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                    curRow.Add(curField.ToString().Trim());
                    curField.Length = 0;
                    if (curRow.Exists(s => !string.IsNullOrEmpty(s)))
                        res.Add(curRow);
                    curRow = new List<string>();
                }
                else
                {
                    curField.Append(c);
                }
            }

            if (curField.Length > 0 || curRow.Count > 0)
            {
                curRow.Add(curField.ToString().Trim());
                if (curRow.Exists(s => !string.IsNullOrEmpty(s)))
                    res.Add(curRow);
            }

            return res;
        }
    }
}
