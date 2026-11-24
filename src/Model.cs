using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace SyncLib.Items
{
    public class HashRegistryFile
    {
        public int FormatVersion = 1;
        public int NextHashId = 1;
        public int CraftingProtocolVersion;
        public List<ModHashRegistry> Mods = new List<ModHashRegistry>();

        internal static HashRegistryFile Parse(string json)
        {
            Dictionary<string, object> root = global::SyncLib.Json.Parse(json) as Dictionary<string, object>;
            if (root == null) throw new FormatException("the registry is not a JSON object");
            HashRegistryFile r = new HashRegistryFile();
            r.FormatVersion = global::SyncLib.Json.Int(root, "FormatVersion", 1);
            r.NextHashId = global::SyncLib.Json.Int(root, "NextHashId", 1);
            r.CraftingProtocolVersion = global::SyncLib.Json.Int(root, "CraftingProtocolVersion", 0);
            if (r.CraftingProtocolVersion < 0 || r.CraftingProtocolVersion > 1) throw new FormatException("unknown crafting protocol " + r.CraftingProtocolVersion);
            object mods;
            if (!root.TryGetValue("Mods", out mods) || !(mods is List<object>)) throw new FormatException("the registry has no Mods list");
            foreach (object o in (List<object>)mods)
            {
                Dictionary<string, object> m = o as Dictionary<string, object>;
                if (m == null) throw new FormatException("a Mods entry is not an object");
                string id = global::SyncLib.Json.Text(m, "ModId");
                if (string.IsNullOrEmpty(id)) throw new FormatException("a Mods entry has no ModId");
                ModHashRegistry mr = new ModHashRegistry(id, global::SyncLib.Json.Text(m, "LastKnownVersion") ?? "");
                object inst;
                mr.Installed = !m.TryGetValue("Installed", out inst) || !(inst is bool) || (bool)inst;
                Numbers(m, "ItemHashes", mr.ItemHashes);
                Numbers(m, "RecipeHashes", mr.RecipeHashes);
                object rd;
                if (m.TryGetValue("RecipeDescriptors", out rd) && rd is Dictionary<string, object>)
                {
                    foreach (KeyValuePair<string, object> kv in (Dictionary<string, object>)rd)
                    {
                        string v = kv.Value as string;
                        if (v != null) mr.RecipeDescriptors[kv.Key] = v;
                    }
                }
                r.Mods.Add(mr);
            }
            return r;
        }

        static void Numbers(Dictionary<string, object> m, string key, Dictionary<string, int> into)
        {
            object v;
            if (!m.TryGetValue(key, out v) || v == null) return;
            Dictionary<string, object> d = v as Dictionary<string, object>;
            if (d == null) throw new FormatException(key + " is not an object");
            foreach (KeyValuePair<string, object> kv in d)
            {
                if (!(kv.Value is double)) throw new FormatException(key + " has a non number for " + kv.Key);
                double x = (double)kv.Value;
                if (x < int.MinValue || x > int.MaxValue || Math.Floor(x) != x) throw new FormatException(key + " has a bad number for " + kv.Key);
                into[kv.Key] = (int)x;
            }
        }

        internal string Owner(string key, out int hash)
        {
            hash = 0;
            foreach (ModHashRegistry m in Mods)
            {
                int h;
                if (m.ItemHashes.TryGetValue(key, out h))
                {
                    hash = h;
                    return m.ModId;
                }
            }
            return null;
        }

        internal static string Report(IList<int> hashes, bool crafting, IDictionary<uint, string> recipes)
        {
            StringBuilder b = new StringBuilder();
            if (crafting) b.Append("{\"SchemaVersion\":1,\"PrefabHashes\":");
            b.Append('[');
            for (int i = 0; i < hashes.Count; i++)
            {
                if (i > 0) b.Append(',');
                b.Append(hashes[i].ToString(CultureInfo.InvariantCulture));
            }
            b.Append(']');
            if (crafting)
            {
                b.Append(",\"RecipeDescriptors\":{");
                bool first = true;
                if (recipes != null)
                {
                    foreach (KeyValuePair<uint, string> kv in recipes)
                    {
                        if (!first) b.Append(',');
                        first = false;
                        b.Append('"').Append(kv.Key.ToString(CultureInfo.InvariantCulture)).Append("\":\"").Append(kv.Value).Append('"');
                    }
                }
                b.Append("}}");
            }
            return b.ToString();
        }
    }

    public class ModHashRegistry
    {
        public string ModId { get; set; }
        public string LastKnownVersion { get; set; }
        public bool Installed { get; set; }
        public Dictionary<string, int> ItemHashes { get; set; }
        public Dictionary<string, int> RecipeHashes { get; set; }
        public Dictionary<string, string> RecipeDescriptors { get; set; }

        public ModHashRegistry(string modId, string version)
        {
            ModId = modId;
            LastKnownVersion = version;
            Installed = true;
            ItemHashes = new Dictionary<string, int>();
            RecipeHashes = new Dictionary<string, int>();
            RecipeDescriptors = new Dictionary<string, string>();
        }
    }
}
