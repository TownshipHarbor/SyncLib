using System;
using System.Collections.Generic;
using System.IO;
using AssetsTools.NET;
using AssetsTools.NET.Extra;

static class Program
{
    const int Android = 13;

    static int Main(string[] args)
    {
        if (args.Length < 1 || args.Length > 2)
        {
            Console.WriteLine("usage: QuestBundle <PC Workshop bundle> [out file]");
            Console.WriteLine("Writes a copy marked for Android with DXT textures turned into RGBA32, LZ4 compressed.");
            return 2;
        }
        string input = args[0];
        string output = args.Length > 1 ? args[1] : input + ".quest";
        AssetsManager am = new AssetsManager();
        BundleFileInstance bun = am.LoadBundleFile(input, true);
        int files = bun.file.BlockAndDirInfo.DirectoryInfos.Count;
        int changed = 0;
        Dictionary<string, int> types = new Dictionary<string, int>();
        for (int i = 0; i < files; i++)
        {
            if (!bun.file.IsAssetsFile(i)) continue;
            AssetsFileInstance inst = am.LoadAssetsFileFromBundle(bun, i, false);
            AssetsFile f = inst.file;
            Console.WriteLine(bun.file.GetFileName(i) + ": platform " + f.Metadata.TargetPlatform + ", unity " + f.Metadata.UnityVersion + ", " + f.AssetInfos.Count + " objects");
            foreach (AssetFileInfo info in f.AssetInfos)
            {
                string n = ((AssetClassID)info.TypeId).ToString();
                int c;
                types[n] = types.TryGetValue(n, out c) ? c + 1 : 1;
            }
            foreach (AssetFileInfo info in f.GetAssetsOfType(AssetClassID.Texture2D)) changed += Texture(am, inst, bun, info);
            f.Metadata.TargetPlatform = Android;
            bun.file.BlockAndDirInfo.DirectoryInfos[i].SetNewData(f);
        }
        foreach (KeyValuePair<string, int> kv in types) Console.WriteLine("  " + kv.Key + " x" + kv.Value);
        string raw = output + ".raw";
        using (AssetsFileWriter w = new AssetsFileWriter(raw)) bun.file.Write(w);
        am.UnloadAll(true);
        AssetBundleFile plain = new AssetBundleFile();
        using (FileStream rs = File.OpenRead(raw))
        {
            plain.Read(new AssetsFileReader(rs));
            using (AssetsFileWriter w = new AssetsFileWriter(output)) plain.Pack(w, AssetBundleCompressionType.LZ4);
            plain.Close();
        }
        File.Delete(raw);
        Console.WriteLine("wrote " + output + " (" + new FileInfo(output).Length + " bytes, " + changed + " texture(s) decoded, platform Android)");
        return 0;
    }

    static int Texture(AssetsManager am, AssetsFileInstance inst, BundleFileInstance bun, AssetFileInfo info)
    {
        AssetTypeValueField t = am.GetBaseField(inst, info);
        int fmt = t["m_TextureFormat"].AsInt;
        string name = t["m_Name"].AsString;
        if (fmt == 4 || fmt == 3 || fmt == 5 || fmt == 1)
        {
            Console.WriteLine("  texture " + name + " format " + fmt + " kept");
            return 0;
        }
        if (fmt != 10 && fmt != 12) throw new InvalidDataException("texture " + name + " uses format " + fmt + "; rebuild the bundle for Android in Unity instead");
        int w = t["m_Width"].AsInt, h = t["m_Height"].AsInt;
        byte[] data = Image(bun, t);
        byte[] rgba = Dxt(data, w, h, fmt);
        t["m_TextureFormat"].AsInt = 4;
        t["m_MipCount"].AsInt = 1;
        t["m_CompleteImageSize"].AsInt = rgba.Length;
        t["image data"].AsByteArray = rgba;
        AssetTypeValueField sd = t["m_StreamData"];
        if (!sd.IsDummy)
        {
            sd["offset"].AsLong = 0;
            sd["size"].AsLong = 0;
            sd["path"].AsString = "";
        }
        info.SetNewData(t);
        Console.WriteLine("  texture " + name + " " + w + "x" + h + " format " + fmt + " -> RGBA32");
        return 1;
    }

    static byte[] Image(BundleFileInstance bun, AssetTypeValueField t)
    {
        AssetTypeValueField sd = t["m_StreamData"];
        if (sd.IsDummy || sd["size"].AsLong == 0) return t["image data"].AsByteArray;
        string path = sd["path"].AsString;
        string file = path.Substring(path.LastIndexOf('/') + 1);
        int idx = bun.file.GetFileIndex(file);
        if (idx < 0) throw new FileNotFoundException("texture stream " + file);
        bun.file.GetFileRange(idx, out long start, out long length);
        AssetsFileReader r = bun.file.DataReader;
        r.Position = start + sd["offset"].AsLong;
        return r.ReadBytes((int)sd["size"].AsLong);
    }

    static byte[] Dxt(byte[] data, int w, int h, int fmt)
    {
        byte[] rgba = new byte[w * h * 4];
        int bw = (w + 3) / 4, bh = (h + 3) / 4, size = fmt == 10 ? 8 : 16;
        byte[] block = new byte[64];
        for (int by = 0; by < bh; by++)
        {
            for (int bx = 0; bx < bw; bx++)
            {
                int p = (by * bw + bx) * size;
                if (fmt == 10) Bc1(data, p, block, true);
                else { Bc1(data, p + 8, block, false); Bc3Alpha(data, p, block); }
                for (int y = 0; y < 4; y++)
                {
                    int py = by * 4 + y;
                    if (py >= h) break;
                    for (int x = 0; x < 4; x++)
                    {
                        int px = bx * 4 + x;
                        if (px >= w) break;
                        Buffer.BlockCopy(block, (y * 4 + x) * 4, rgba, (py * w + px) * 4, 4);
                    }
                }
            }
        }
        return rgba;
    }

    static void Bc1(byte[] d, int p, byte[] o, bool alpha)
    {
        ushort c0 = BitConverter.ToUInt16(d, p), c1 = BitConverter.ToUInt16(d, p + 2);
        int[,] c = new int[4, 4];
        Rgb565(c0, c, 0);
        Rgb565(c1, c, 1);
        bool four = c0 > c1 || !alpha;
        for (int k = 0; k < 3; k++)
        {
            if (four)
            {
                c[2, k] = (2 * c[0, k] + c[1, k]) / 3;
                c[3, k] = (c[0, k] + 2 * c[1, k]) / 3;
            }
            else
            {
                c[2, k] = (c[0, k] + c[1, k]) / 2;
                c[3, k] = 0;
            }
        }
        c[0, 3] = c[1, 3] = c[2, 3] = 255;
        c[3, 3] = four ? 255 : 0;
        uint bits = BitConverter.ToUInt32(d, p + 4);
        for (int i = 0; i < 16; i++)
        {
            int s = (int)((bits >> (2 * i)) & 3);
            o[i * 4] = (byte)c[s, 0];
            o[i * 4 + 1] = (byte)c[s, 1];
            o[i * 4 + 2] = (byte)c[s, 2];
            if (alpha) o[i * 4 + 3] = (byte)c[s, 3];
        }
    }

    static void Rgb565(ushort v, int[,] c, int i)
    {
        int r = (v >> 11) & 31, g = (v >> 5) & 63, b = v & 31;
        c[i, 0] = (r << 3) | (r >> 2);
        c[i, 1] = (g << 2) | (g >> 4);
        c[i, 2] = (b << 3) | (b >> 2);
    }

    static void Bc3Alpha(byte[] d, int p, byte[] o)
    {
        int a0 = d[p], a1 = d[p + 1];
        int[] a = new int[8];
        a[0] = a0;
        a[1] = a1;
        if (a0 > a1) for (int i = 1; i < 7; i++) a[i + 1] = ((7 - i) * a0 + i * a1) / 7;
        else
        {
            for (int i = 1; i < 5; i++) a[i + 1] = ((5 - i) * a0 + i * a1) / 5;
            a[6] = 0;
            a[7] = 255;
        }
        ulong bits = 0;
        for (int i = 0; i < 6; i++) bits |= (ulong)d[p + 2 + i] << (8 * i);
        for (int i = 0; i < 16; i++) o[i * 4 + 3] = (byte)a[(int)((bits >> (3 * i)) & 7)];
    }
}
