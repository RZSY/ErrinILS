// ErrinILS zero-dependency QR code generator (ISO/IEC 18004 compliant).
// Generates standard QR codes (Versions 1-10, Error Correction Level M) in pure C# using only System.Drawing.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text;

namespace Errin
{
    public static class Qr
    {
        public struct VersionInfo
        {
            public int Version;
            public int Size;
            public int TotalCodewords;
            public int EcCodewordsPerBlock;
            public int Group1Blocks;
            public int Group1Data;
            public int Group2Blocks;
            public int Group2Data;
            public int RemainderBits;
            public int[] Alignments;
        }

        // Error Correction Level M parameters for Versions 1 through 10
        public static readonly VersionInfo[] Versions = new VersionInfo[]
        {
            new VersionInfo { Version = 1, Size = 21, TotalCodewords = 26, EcCodewordsPerBlock = 10, Group1Blocks = 1, Group1Data = 16, Group2Blocks = 0, Group2Data = 0, RemainderBits = 0, Alignments = new int[0] },
            new VersionInfo { Version = 2, Size = 25, TotalCodewords = 44, EcCodewordsPerBlock = 16, Group1Blocks = 1, Group1Data = 28, Group2Blocks = 0, Group2Data = 0, RemainderBits = 7, Alignments = new[] { 6, 18 } },
            new VersionInfo { Version = 3, Size = 29, TotalCodewords = 70, EcCodewordsPerBlock = 26, Group1Blocks = 1, Group1Data = 44, Group2Blocks = 0, Group2Data = 0, RemainderBits = 7, Alignments = new[] { 6, 22 } },
            new VersionInfo { Version = 4, Size = 33, TotalCodewords = 100, EcCodewordsPerBlock = 18, Group1Blocks = 2, Group1Data = 32, Group2Blocks = 0, Group2Data = 0, RemainderBits = 7, Alignments = new[] { 6, 26 } },
            new VersionInfo { Version = 5, Size = 37, TotalCodewords = 134, EcCodewordsPerBlock = 24, Group1Blocks = 2, Group1Data = 43, Group2Blocks = 0, Group2Data = 0, RemainderBits = 7, Alignments = new[] { 6, 30 } },
            new VersionInfo { Version = 6, Size = 41, TotalCodewords = 172, EcCodewordsPerBlock = 16, Group1Blocks = 4, Group1Data = 27, Group2Blocks = 0, Group2Data = 0, RemainderBits = 7, Alignments = new[] { 6, 34 } },
            new VersionInfo { Version = 7, Size = 45, TotalCodewords = 196, EcCodewordsPerBlock = 18, Group1Blocks = 4, Group1Data = 31, Group2Blocks = 0, Group2Data = 0, RemainderBits = 0, Alignments = new[] { 6, 22, 38 } },
            new VersionInfo { Version = 8, Size = 49, TotalCodewords = 242, EcCodewordsPerBlock = 22, Group1Blocks = 2, Group1Data = 38, Group2Blocks = 2, Group2Data = 39, RemainderBits = 0, Alignments = new[] { 6, 24, 42 } },
            new VersionInfo { Version = 9, Size = 53, TotalCodewords = 292, EcCodewordsPerBlock = 22, Group1Blocks = 3, Group1Data = 36, Group2Blocks = 2, Group2Data = 37, RemainderBits = 0, Alignments = new[] { 6, 26, 46 } },
            new VersionInfo { Version = 10, Size = 57, TotalCodewords = 346, EcCodewordsPerBlock = 26, Group1Blocks = 4, Group1Data = 43, Group2Blocks = 1, Group2Data = 44, RemainderBits = 0, Alignments = new[] { 6, 28, 50 } }
        };

        static readonly byte[] Exp = new byte[512];
        static readonly byte[] Log = new byte[256];

        static Qr()
        {
            int x = 1;
            for (int i = 0; i < 255; i++)
            {
                Exp[i] = (byte)x;
                Exp[i + 255] = (byte)x;
                Log[x] = (byte)i;
                x <<= 1;
                if ((x & 0x100) != 0) x ^= 0x11D; // Primitive polynomial x^8 + x^4 + x^3 + x^2 + 1
            }
        }

        static byte GfMul(int a, int b)
        {
            if (a == 0 || b == 0) return 0;
            return Exp[Log[a] + Log[b]];
        }

        static byte[] GenPoly(int deg)
        {
            byte[] g = new byte[] { 1 };
            for (int i = 0; i < deg; i++)
            {
                byte[] next = new byte[g.Length + 1];
                byte factor = Exp[i];
                for (int j = 0; j < g.Length; j++)
                {
                    next[j] ^= g[j];
                    next[j + 1] ^= GfMul(g[j], factor);
                }
                g = next;
            }
            return g;
        }

        static byte[] ReedSolomon(byte[] data, int ecCount)
        {
            byte[] g = GenPoly(ecCount);
            byte[] res = new byte[data.Length + ecCount];
            Array.Copy(data, res, data.Length);
            for (int i = 0; i < data.Length; i++)
            {
                byte coef = res[i];
                if (coef != 0)
                {
                    for (int j = 0; j < g.Length; j++)
                    {
                        res[i + j] ^= GfMul(coef, g[j]);
                    }
                }
            }
            byte[] ec = new byte[ecCount];
            Array.Copy(res, data.Length, ec, 0, ecCount);
            return ec;
        }

        public static VersionInfo PickVersion(int dataBytes)
        {
            foreach (var v in Versions)
            {
                int totalData = v.Group1Blocks * v.Group1Data + v.Group2Blocks * v.Group2Data;
                int headerBits = 4 + (v.Version < 10 ? 8 : 16);
                if (dataBytes * 8 + headerBits <= totalData * 8) return v;
            }
            throw new ArgumentException("Data exceeds maximum QR capacity (" + dataBytes + " bytes)");
        }

        public static bool[,] GenerateMatrix(string text)
        {
            byte[] utf8 = Encoding.UTF8.GetBytes(text ?? "");
            var vi = PickVersion(utf8.Length);
            int totalData = vi.Group1Blocks * vi.Group1Data + vi.Group2Blocks * vi.Group2Data;

            // 1. Bitstream assembly
            var bits = new List<bool>();
            // Mode 0100 (Byte mode)
            bits.Add(false); bits.Add(true); bits.Add(false); bits.Add(false);
            // Character count indicator (8 bits for v1-9, 16 bits for v10+)
            int countBits = vi.Version < 10 ? 8 : 16;
            for (int i = countBits - 1; i >= 0; i--) bits.Add(((utf8.Length >> i) & 1) != 0);
            // Data bytes
            foreach (byte b in utf8)
            {
                for (int i = 7; i >= 0; i--) bits.Add(((b >> i) & 1) != 0);
            }
            // Terminator (up to 4 zeroes)
            int term = Math.Min(4, totalData * 8 - bits.Count);
            for (int i = 0; i < term; i++) bits.Add(false);
            // Pad to byte boundary
            while ((bits.Count % 8) != 0) bits.Add(false);
            // Pad bytes (0xEC, 0x11)
            byte[] padBytes = new byte[] { 0xEC, 0x11 };
            int padIdx = 0;
            while (bits.Count < totalData * 8)
            {
                byte pb = padBytes[padIdx % 2];
                padIdx++;
                for (int i = 7; i >= 0; i--) bits.Add(((pb >> i) & 1) != 0);
            }

            // Convert data bits to byte codewords
            byte[] dataCodewords = new byte[totalData];
            for (int i = 0; i < totalData; i++)
            {
                byte val = 0;
                for (int b = 0; b < 8; b++)
                {
                    if (bits[i * 8 + b]) val |= (byte)(1 << (7 - b));
                }
                dataCodewords[i] = val;
            }

            // 2. Block division and Reed-Solomon computation
            int totalBlocks = vi.Group1Blocks + vi.Group2Blocks;
            byte[][] dataBlocks = new byte[totalBlocks][];
            byte[][] ecBlocks = new byte[totalBlocks][];

            int offset = 0;
            for (int i = 0; i < totalBlocks; i++)
            {
                int blockSize = (i < vi.Group1Blocks) ? vi.Group1Data : vi.Group2Data;
                dataBlocks[i] = new byte[blockSize];
                Array.Copy(dataCodewords, offset, dataBlocks[i], 0, blockSize);
                offset += blockSize;
                ecBlocks[i] = ReedSolomon(dataBlocks[i], vi.EcCodewordsPerBlock);
            }

            // 3. Interleaving data and EC codewords
            var finalCodewords = new List<byte>();
            int maxDataLen = Math.Max(vi.Group1Data, vi.Group2Data);
            for (int j = 0; j < maxDataLen; j++)
            {
                for (int i = 0; i < totalBlocks; i++)
                {
                    if (j < dataBlocks[i].Length) finalCodewords.Add(dataBlocks[i][j]);
                }
            }
            for (int j = 0; j < vi.EcCodewordsPerBlock; j++)
            {
                for (int i = 0; i < totalBlocks; i++)
                {
                    finalCodewords.Add(ecBlocks[i][j]);
                }
            }

            // Convert interleaved codewords to bit sequence + remainder bits
            var finalBits = new List<bool>();
            foreach (byte cw in finalCodewords)
            {
                for (int i = 7; i >= 0; i--) finalBits.Add(((cw >> i) & 1) != 0);
            }
            for (int i = 0; i < vi.RemainderBits; i++) finalBits.Add(false);

            // 4. Matrix creation & Function patterns
            int size = vi.Size;
            bool[,] matrix = new bool[size, size];
            bool[,] isFunc = new bool[size, size];

            // Finder patterns
            Action<int, int> placeFinder = (r, c) =>
            {
                for (int dr = -1; dr <= 7; dr++)
                {
                    for (int dc = -1; dc <= 7; dc++)
                    {
                        int nr = r + dr, nc = c + dc;
                        if (nr >= 0 && nr < size && nc >= 0 && nc < size)
                        {
                            isFunc[nr, nc] = true;
                            if (dr >= 0 && dr <= 6 && dc >= 0 && dc <= 6)
                            {
                                bool dark = (dr == 0 || dr == 6 || dc == 0 || dc == 6 || (dr >= 2 && dr <= 4 && dc >= 2 && dc <= 4));
                                matrix[nr, nc] = dark;
                            }
                            else
                            {
                                matrix[nr, nc] = false; // separator
                            }
                        }
                    }
                }
            };
            placeFinder(0, 0);
            placeFinder(0, size - 7);
            placeFinder(size - 7, 0);

            // Timing patterns
            for (int c = 8; c < size - 8; c++)
            {
                isFunc[6, c] = true;
                matrix[6, c] = (c % 2 == 0);
            }
            for (int r = 8; r < size - 8; r++)
            {
                isFunc[r, 6] = true;
                matrix[r, 6] = (r % 2 == 0);
            }

            // Alignment patterns
            if (vi.Alignments.Length > 0)
            {
                foreach (int ar in vi.Alignments)
                {
                    foreach (int ac in vi.Alignments)
                    {
                        if ((ar <= 8 && ac <= 8) || (ar <= 8 && ac >= size - 9) || (ar >= size - 9 && ac <= 8))
                            continue;

                        for (int dr = -2; dr <= 2; dr++)
                        {
                            for (int dc = -2; dc <= 2; dc++)
                            {
                                isFunc[ar + dr, ac + dc] = true;
                                bool dark = (Math.Abs(dr) == 2 || Math.Abs(dc) == 2 || (dr == 0 && dc == 0));
                                matrix[ar + dr, ac + dc] = dark;
                            }
                        }
                    }
                }
            }

            // Dark module
            isFunc[4 * vi.Version + 9, 8] = true;
            matrix[4 * vi.Version + 9, 8] = true;

            // Reserve Format Information areas
            for (int i = 0; i <= 8; i++)
            {
                if (i != 6) { isFunc[8, i] = true; isFunc[i, 8] = true; }
            }
            for (int i = 0; i < 8; i++)
            {
                isFunc[8, size - 1 - i] = true;
                isFunc[size - 1 - i, 8] = true;
            }

            // Reserve Version Information areas (version >= 7)
            if (vi.Version >= 7)
            {
                for (int r = 0; r < 6; r++)
                {
                    for (int c = 0; c < 3; c++)
                    {
                        isFunc[size - 11 + c, r] = true;
                        isFunc[r, size - 11 + c] = true;
                    }
                }
            }

            // 5. Data placement (zig-zag traversal)
            int bitIdx = 0;
            int col = size - 1;
            bool upward = true;
            while (col > 0)
            {
                if (col == 6) col--; // Skip vertical timing column
                for (int i = 0; i < size; i++)
                {
                    int r = upward ? (size - 1 - i) : i;
                    for (int c = col; c >= col - 1; c--)
                    {
                        if (!isFunc[r, c])
                        {
                            if (bitIdx < finalBits.Count)
                            {
                                matrix[r, c] = finalBits[bitIdx++];
                            }
                        }
                    }
                }
                upward = !upward;
                col -= 2;
            }

            // 6. Find best mask
            int bestMask = 0;
            int minPenalty = int.MaxValue;
            bool[,] bestMatrix = null;

            for (int mask = 0; mask < 8; mask++)
            {
                bool[,] masked = (bool[,])matrix.Clone();
                ApplyMask(masked, isFunc, size, mask);
                WriteFormatInfo(masked, size, mask);
                if (vi.Version >= 7) WriteVersionInfo(masked, size, vi.Version);

                int penalty = CalculatePenalty(masked, size);
                if (penalty < minPenalty)
                {
                    minPenalty = penalty;
                    bestMask = mask;
                    bestMatrix = masked;
                }
            }

            return bestMatrix;
        }

        static void ApplyMask(bool[,] m, bool[,] isFunc, int size, int mask)
        {
            for (int r = 0; r < size; r++)
            {
                for (int c = 0; c < size; c++)
                {
                    if (isFunc[r, c]) continue;
                    bool invert = false;
                    switch (mask)
                    {
                        case 0: invert = ((r + c) % 2 == 0); break;
                        case 1: invert = (r % 2 == 0); break;
                        case 2: invert = (c % 3 == 0); break;
                        case 3: invert = ((r + c) % 3 == 0); break;
                        case 4: invert = ((r / 2 + c / 3) % 2 == 0); break;
                        case 5: invert = (((r * c) % 2 + (r * c) % 3) == 0); break;
                        case 6: invert = ((((r * c) % 2 + (r * c) % 3) % 2) == 0); break;
                        case 7: invert = ((((r + c) % 2 + (r * c) % 3) % 2) == 0); break;
                    }
                    if (invert) m[r, c] = !m[r, c];
                }
            }
        }

        static void WriteFormatInfo(bool[,] m, int size, int mask)
        {
            // Level M = 00
            int data = (0 << 3) | mask;
            int rem = data << 10;
            int gen = 0x537;
            for (int i = 4; i >= 0; i--)
            {
                if (((rem >> (i + 10)) & 1) != 0) rem ^= (gen << i);
            }
            int format = ((data << 10) | rem) ^ 0x5412;

            // Write around top-left
            int[] r1 = new int[] { 0, 1, 2, 3, 4, 5, 7, 8, 8, 8, 8, 8, 8, 8, 8 };
            int[] c1 = new int[] { 8, 8, 8, 8, 8, 8, 8, 8, 7, 5, 4, 3, 2, 1, 0 };
            for (int i = 0; i < 15; i++)
            {
                m[r1[i], c1[i]] = ((format >> i) & 1) != 0;
            }

            // Write around bottom-left & top-right
            for (int i = 0; i < 7; i++)
            {
                m[size - 1 - i, 8] = ((format >> i) & 1) != 0;
            }
            for (int i = 7; i < 15; i++)
            {
                m[8, size - 15 + i] = ((format >> i) & 1) != 0;
            }
        }

        static void WriteVersionInfo(bool[,] m, int size, int version)
        {
            int rem = version << 12;
            int gen = 0x1F25;
            for (int i = 5; i >= 0; i--)
            {
                if (((rem >> (i + 12)) & 1) != 0) rem ^= (gen << i);
            }
            int vdata = (version << 12) | rem;

            for (int i = 0; i < 18; i++)
            {
                bool bit = ((vdata >> i) & 1) != 0;
                int r = i / 3;
                int c = i % 3;
                m[size - 11 + r, c] = bit;
                m[c, size - 11 + r] = bit;
            }
        }

        static int CalculatePenalty(bool[,] m, int size)
        {
            int penalty = 0;
            for (int r = 0; r < size; r++)
            {
                int count = 1;
                for (int c = 1; c < size; c++)
                {
                    if (m[r, c] == m[r, c - 1])
                    {
                        count++;
                        if (count == 5) penalty += 3;
                        else if (count > 5) penalty++;
                    }
                    else count = 1;
                }
            }
            for (int c = 0; c < size; c++)
            {
                int count = 1;
                for (int r = 1; r < size; r++)
                {
                    if (m[r, c] == m[r - 1, c])
                    {
                        count++;
                        if (count == 5) penalty += 3;
                        else if (count > 5) penalty++;
                    }
                    else count = 1;
                }
            }
            for (int r = 0; r < size - 1; r++)
            {
                for (int c = 0; c < size - 1; c++)
                {
                    bool b = m[r, c];
                    if (m[r + 1, c] == b && m[r, c + 1] == b && m[r + 1, c + 1] == b)
                    {
                        penalty += 3;
                    }
                }
            }
            for (int r = 0; r < size; r++)
            {
                for (int c = 0; c <= size - 11; c++)
                {
                    if (IsFinderLike(m, r, c, true)) penalty += 40;
                }
            }
            for (int c = 0; c < size; c++)
            {
                for (int r = 0; r <= size - 11; r++)
                {
                    if (IsFinderLike(m, r, c, false)) penalty += 40;
                }
            }
            int darkCount = 0;
            for (int r = 0; r < size; r++)
                for (int c = 0; c < size; c++)
                    if (m[r, c]) darkCount++;
            double percent = (double)darkCount * 100.0 / (size * size);
            int prev5 = (int)(Math.Abs(percent - 50) / 5);
            penalty += prev5 * 10;
            return penalty;
        }

        static bool IsFinderLike(bool[,] m, int r, int c, bool horizontal)
        {
            int[] pat1 = new int[] { 1, 0, 1, 1, 1, 0, 1, 0, 0, 0, 0 };
            int[] pat2 = new int[] { 0, 0, 0, 0, 1, 0, 1, 1, 1, 0, 1 };
            bool m1 = true, m2 = true;
            for (int i = 0; i < 11; i++)
            {
                bool bit = horizontal ? m[r, c + i] : m[r + i, c];
                if (bit != (pat1[i] == 1)) m1 = false;
                if (bit != (pat2[i] == 1)) m2 = false;
                if (!m1 && !m2) return false;
            }
            return true;
        }

        public static Bitmap ToBitmap(string text, int moduleSize, int quietZone)
        {
            if (moduleSize < 1) moduleSize = 4;
            if (quietZone < 0) quietZone = 4;
            bool[,] m = GenerateMatrix(text);
            int size = m.GetLength(0);
            int totalModules = size + quietZone * 2;
            int imgSize = totalModules * moduleSize;

            Bitmap bmp = new Bitmap(imgSize, imgSize, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.White);
                using (SolidBrush brush = new SolidBrush(Color.Black))
                {
                    for (int r = 0; r < size; r++)
                    {
                        for (int c = 0; c < size; c++)
                        {
                            if (m[r, c])
                            {
                                g.FillRectangle(brush, (c + quietZone) * moduleSize, (r + quietZone) * moduleSize, moduleSize, moduleSize);
                            }
                        }
                    }
                }
            }
            return bmp;
        }

        public static byte[] ToPng(string text, int moduleSize, int quietZone)
        {
            using (var bmp = ToBitmap(text, moduleSize, quietZone))
            using (var ms = new MemoryStream())
            {
                bmp.Save(ms, ImageFormat.Png);
                return ms.ToArray();
            }
        }

        public static string ToSvg(string text, int moduleSize, int quietZone)
        {
            if (moduleSize < 1) moduleSize = 4;
            if (quietZone < 0) quietZone = 4;
            bool[,] m = GenerateMatrix(text);
            int size = m.GetLength(0);
            int totalModules = size + quietZone * 2;
            int imgSize = totalModules * moduleSize;

            var sb = new StringBuilder();
            sb.AppendFormat("<svg xmlns=\"http://www.w3.org/2000/svg\" version=\"1.1\" viewBox=\"0 0 {0} {0}\" width=\"{0}\" height=\"{0}\">", imgSize);
            sb.AppendFormat("<rect width=\"{0}\" height=\"{0}\" fill=\"#ffffff\"/>", imgSize);
            sb.Append("<path fill=\"#000000\" d=\"");
            for (int r = 0; r < size; r++)
            {
                for (int c = 0; c < size; c++)
                {
                    if (m[r, c])
                    {
                        sb.AppendFormat("M{0},{1}h{2}v{2}h-{2}z ", (c + quietZone) * moduleSize, (r + quietZone) * moduleSize, moduleSize);
                    }
                }
            }
            sb.Append("\"/></svg>");
            return sb.ToString();
        }
    }
}
