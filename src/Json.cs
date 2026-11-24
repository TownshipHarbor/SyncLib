using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace SyncLib
{
    internal static class Json
    {
        internal static object Parse(string s)
        {
            int i = 0;
            if (s.Length > 0 && s[0] == '\uFEFF') i = 1;
            object v = Value(s, ref i);
            Space(s, ref i);
            if (i != s.Length) throw new FormatException("trailing text at " + i);
            return v;
        }

        static void Space(string s, ref int i)
        {
            while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
        }

        static object Value(string s, ref int i)
        {
            Space(s, ref i);
            if (i >= s.Length) throw new FormatException("unexpected end");
            char c = s[i];
            if (c == '{')
            {
                i++;
                Dictionary<string, object> d = new Dictionary<string, object>(StringComparer.Ordinal);
                Space(s, ref i);
                if (i < s.Length && s[i] == '}') { i++; return d; }
                while (true)
                {
                    Space(s, ref i);
                    string k = Str(s, ref i);
                    Space(s, ref i);
                    if (i >= s.Length || s[i] != ':') throw new FormatException("expected : at " + i);
                    i++;
                    d[k] = Value(s, ref i);
                    Space(s, ref i);
                    if (i < s.Length && s[i] == ',') { i++; continue; }
                    if (i < s.Length && s[i] == '}') { i++; return d; }
                    throw new FormatException("expected , or } at " + i);
                }
            }
            if (c == '[')
            {
                i++;
                List<object> l = new List<object>();
                Space(s, ref i);
                if (i < s.Length && s[i] == ']') { i++; return l; }
                while (true)
                {
                    l.Add(Value(s, ref i));
                    Space(s, ref i);
                    if (i < s.Length && s[i] == ',') { i++; continue; }
                    if (i < s.Length && s[i] == ']') { i++; return l; }
                    throw new FormatException("expected , or ] at " + i);
                }
            }
            if (c == '"') return Str(s, ref i);
            if (s.Length - i >= 4 && string.CompareOrdinal(s, i, "null", 0, 4) == 0) { i += 4; return null; }
            if (s.Length - i >= 4 && string.CompareOrdinal(s, i, "true", 0, 4) == 0) { i += 4; return true; }
            if (s.Length - i >= 5 && string.CompareOrdinal(s, i, "false", 0, 5) == 0) { i += 5; return false; }
            int start = i;
            while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0) i++;
            if (i == start) throw new FormatException("unexpected '" + c + "' at " + i);
            return double.Parse(s.Substring(start, i - start), NumberStyles.Float, CultureInfo.InvariantCulture);
        }

        static string Str(string s, ref int i)
        {
            if (i >= s.Length || s[i] != '"') throw new FormatException("expected string at " + i);
            i++;
            StringBuilder b = new StringBuilder();
            while (i < s.Length)
            {
                char c = s[i++];
                if (c == '"') return b.ToString();
                if (c != '\\') { b.Append(c); continue; }
                if (i >= s.Length) break;
                char e = s[i++];
                switch (e)
                {
                    case 'n': b.Append('\n'); break;
                    case 'r': b.Append('\r'); break;
                    case 't': b.Append('\t'); break;
                    case 'b': b.Append('\b'); break;
                    case 'f': b.Append('\f'); break;
                    case 'u':
                        if (i + 4 > s.Length) throw new FormatException("bad escape");
                        b.Append((char)int.Parse(s.Substring(i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                        i += 4;
                        break;
                    default: b.Append(e); break;
                }
            }
            throw new FormatException("unterminated string");
        }

        internal static string Text(Dictionary<string, object> d, string key)
        {
            object v;
            return d != null && d.TryGetValue(key, out v) ? v as string : null;
        }

        internal static int Int(Dictionary<string, object> d, string key, int fallback)
        {
            object v;
            if (d == null || !d.TryGetValue(key, out v) || !(v is double)) return fallback;
            return (int)(double)v;
        }
    }
}
