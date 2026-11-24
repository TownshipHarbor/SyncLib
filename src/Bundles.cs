using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace SyncLib
{
    internal static class Bundles
    {
        sealed class Entry
        {
            internal AssetBundle Bundle;
            internal WorkshopBundle Data;
        }

        static readonly Dictionary<string, Entry> ByResource = new Dictionary<string, Entry>(StringComparer.Ordinal);
        static readonly Dictionary<long, WorkshopPrefab> ByObject = new Dictionary<long, WorkshopPrefab>();
        static readonly List<WorkshopBundle> All = new List<WorkshopBundle>();

        internal static int Expected
        {
            get { return Plat.Quest ? 13 : 19; }
        }

        static Entry Load(Assembly asm, string path)
        {
            if (asm == null) throw new ArgumentNullException("asm");
            if (string.IsNullOrEmpty(path)) throw new ArgumentException("no resource name");
            string key = asm.FullName + "|" + path;
            Entry e;
            if (ByResource.TryGetValue(key, out e) && e.Bundle != null) return e;
            byte[] data;
            using (Stream s = asm.GetManifestResourceStream(path))
            {
                if (s == null) throw new FileNotFoundException(asm.GetName().Name + " has no embedded resource '" + path + "'; set its Build Action to Embedded Resource");
                data = new byte[s.Length];
                int read = 0;
                while (read < data.Length)
                {
                    int n = s.Read(data, read, data.Length - read);
                    if (n <= 0) break;
                    read += n;
                }
            }
            e = new Entry();
            try
            {
                e.Data = WorkshopBundle.Read(data);
            }
            catch (Exception x)
            {
                Core.Warn("could not read the Workshop settings in " + path + ": " + x.Message + "; its prefabs register without them");
                e.Data = new WorkshopBundle();
            }
            foreach (string n in e.Data.Notes) Core.Warn(path + ": " + n);
            if (e.Data.Platform != 0 && e.Data.Platform != Expected && !(e.Data.Platform == 5 && !Plat.Quest))
                Core.Warn(path + " was built for platform " + e.Data.Platform + ", this game needs " + Expected + (Plat.Quest ? " (Android)" : " (StandaloneWindows64)"));
            string file = Path.Combine(Plat.CacheFolder(), Safe(asm.GetName().Name) + "-" + Chunks.Digest(data).Substring(0, 16) + ".bundle");
            if (!File.Exists(file) || new FileInfo(file).Length != data.Length)
            {
                string tmp = file + ".tmp";
                File.WriteAllBytes(tmp, data);
                if (File.Exists(file)) File.Delete(file);
                File.Move(tmp, file);
            }
            e.Bundle = Plat.BundleFromFile(file);
            if (e.Bundle == null) throw new InvalidOperationException("Unity refused " + path + (Plat.Quest ? "; Quest needs a bundle built for Android with Unity 2020.3" : ""));
            ByResource[key] = e;
            All.Add(e.Data);
            Core.Log("loaded bundle " + path + " (" + data.Length + " bytes, " + e.Data.Prefabs.Count + " Workshop prefab(s))");
            return e;
        }

        static string Safe(string s)
        {
            char[] c = s.ToCharArray();
            for (int i = 0; i < c.Length; i++) if (!char.IsLetterOrDigit(c[i]) && c[i] != '.' && c[i] != '-') c[i] = '_';
            return new string(c);
        }

        internal static AssetBundle Bundle(Assembly asm, string path)
        {
            return Load(asm, path).Bundle;
        }

        internal static GameObject Prefab(Assembly asm, string path, string name)
        {
            Entry e = Load(asm, path);
            GameObject go = Plat.LoadPrefab(e.Bundle, name);
            if (go == null) return null;
            WorkshopPrefab wp = e.Data.Prefab(go.name) ?? e.Data.Prefab(name);
            if (wp != null) ByObject[Plat.Key(go)] = wp;
            return go;
        }

        internal static WorkshopPrefab Workshop(GameObject go)
        {
            if (go == null) return null;
            WorkshopPrefab wp;
            if (ByObject.TryGetValue(Plat.Key(go), out wp)) return wp;
            WorkshopPrefab found = null;
            foreach (WorkshopBundle b in All)
            {
                WorkshopPrefab p = b.Prefab(go.name);
                if (p == null) continue;
                if (found != null) return null;
                found = p;
            }
            if (found != null) ByObject[Plat.Key(go)] = found;
            return found;
        }
    }
}
