using System;
using System.Globalization;
using System.Text;

namespace SyncLib
{
    internal sealed class Chunks
    {
        internal const string Registry = "SLCH1|";
        internal const string Report = "SLPF1|";
        internal const int Size = 384;
        internal const int Max = 2097152;
        static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);

        string id;
        string digest;
        int length;
        int received;
        byte[][] parts;
        bool done;

        internal int Received { get { return received; } }
        internal int Total { get { return parts == null ? 0 : parts.Length; } }

        internal bool Accept(string envelope, out string json)
        {
            json = null;
            if (envelope == null || envelope.Length > 800 || !envelope.StartsWith(Registry, StringComparison.Ordinal)) throw new FormatException("not a SyncLib chunk");
            string[] a = envelope.Split('|');
            int index, count, len;
            if (a.Length != 7 || !Hex(a[1], 32) || !int.TryParse(a[2], NumberStyles.None, CultureInfo.InvariantCulture, out index) || !int.TryParse(a[3], NumberStyles.None, CultureInfo.InvariantCulture, out count) || !int.TryParse(a[4], NumberStyles.None, CultureInfo.InvariantCulture, out len) || len <= 0 || len > Max || count != (len + Size - 1) / Size || index < 0 || index >= count || !Hex(a[5], 64)) throw new FormatException("bad chunk header");
            byte[] bytes = Convert.FromBase64String(a[6]);
            if (bytes.Length != Math.Min(Size, len - index * Size)) throw new FormatException("bad chunk length");
            if (parts == null)
            {
                id = a[1];
                digest = a[5];
                length = len;
                parts = new byte[count][];
            }
            if (id != a[1] || digest != a[5] || length != len || parts.Length != count) throw new FormatException("chunk from another transfer");
            if (done) return false;
            if (parts[index] != null)
            {
                if (Convert.ToBase64String(parts[index]) != a[6]) throw new FormatException("two different copies of one chunk");
                return false;
            }
            parts[index] = bytes;
            received++;
            if (received != count) return false;
            byte[] all = new byte[length];
            for (int i = 0; i < count; i++) Buffer.BlockCopy(parts[i], 0, all, i * Size, parts[i].Length);
            if (!string.Equals(digest, Digest(all), StringComparison.OrdinalIgnoreCase)) throw new FormatException("registry checksum does not match");
            json = Utf8.GetString(all);
            done = true;
            parts = new byte[count][];
            return true;
        }

        internal static string[] Encode(string prefix, string json, string transfer)
        {
            byte[] bytes = Utf8.GetBytes(json);
            if (bytes.Length == 0 || bytes.Length > Max) throw new ArgumentException("report size out of range");
            string d = Digest(bytes);
            int count = (bytes.Length + Size - 1) / Size;
            string[] r = new string[count];
            for (int i = 0; i < count; i++)
            {
                int at = i * Size;
                r[i] = prefix + transfer + "|" + i.ToString(CultureInfo.InvariantCulture) + "|" + count.ToString(CultureInfo.InvariantCulture) + "|" + bytes.Length.ToString(CultureInfo.InvariantCulture) + "|" + d + "|" + Convert.ToBase64String(bytes, at, Math.Min(Size, bytes.Length - at));
            }
            return r;
        }

        internal static string NewTransfer()
        {
            return Guid.NewGuid().ToString("N");
        }

        static bool Hex(string s, int len)
        {
            if (s == null || s.Length != len) return false;
            foreach (char c in s) if (!Uri.IsHexDigit(c)) return false;
            return true;
        }

        internal static string Digest(byte[] bytes)
        {
            byte[] h = Sha.Hash(bytes);
            StringBuilder b = new StringBuilder(64);
            foreach (byte x in h) b.Append(x.ToString("X2", CultureInfo.InvariantCulture));
            return b.ToString();
        }
    }
}
