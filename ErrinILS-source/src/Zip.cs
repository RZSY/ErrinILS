using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;

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
}
