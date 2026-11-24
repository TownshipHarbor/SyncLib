using System;
using System.Collections.Generic;
using System.IO;
using AssetsTools.NET;
using AssetsTools.NET.Extra;

namespace SyncLib
{
    internal sealed class WorkshopMark
    {
        internal string Kind;
        internal int Order;
        internal int[] Path;
        internal string[] Names;
        internal Dictionary<string, object> Fields;

        internal string Where()
        {
            return Names.Length == 0 ? "(root)" : string.Join("/", Names);
        }
    }

    internal sealed class WorkshopPrefab
    {
        internal string Name;
        internal readonly List<WorkshopMark> Marks = new List<WorkshopMark>();

        internal List<WorkshopMark> Of(string kind)
        {
            List<WorkshopMark> list = new List<WorkshopMark>();
            foreach (WorkshopMark m in Marks) if (m.Kind == kind) list.Add(m);
            list.Sort(Compare);
            return list;
        }

        internal WorkshopMark Root(string kind)
        {
            foreach (WorkshopMark m in Of(kind)) if (m.Path.Length == 0) return m;
            return null;
        }

        static int Compare(WorkshopMark a, WorkshopMark b)
        {
            int n = Math.Min(a.Path.Length, b.Path.Length);
            for (int i = 0; i < n; i++) if (a.Path[i] != b.Path[i]) return a.Path[i].CompareTo(b.Path[i]);
            if (a.Path.Length != b.Path.Length) return a.Path.Length.CompareTo(b.Path.Length);
            return a.Order.CompareTo(b.Order);
        }
    }

    internal sealed class WorkshopBundle
    {
        internal const string Namespace = "ATT_Workshop_Utilities";
        internal int Platform;
        internal string UnityVersion;
        internal readonly Dictionary<string, WorkshopPrefab> Prefabs = new Dictionary<string, WorkshopPrefab>(StringComparer.Ordinal);
        internal readonly List<string> Notes = new List<string>();

        internal WorkshopPrefab Prefab(string name)
        {
            WorkshopPrefab p;
            return name != null && Prefabs.TryGetValue(name, out p) ? p : null;
        }

        sealed class Node
        {
            internal long Go;
            internal long Father;
            internal readonly List<long> Children = new List<long>();
        }

        internal static WorkshopBundle Read(byte[] data)
        {
            WorkshopBundle wb = new WorkshopBundle();
            AssetsManager am = new AssetsManager();
            try
            {
                BundleFileInstance bun = am.LoadBundleFile(new MemoryStream(data, false), "bundle", true);
                int files = bun.file.BlockAndDirInfo.DirectoryInfos.Count;
                for (int i = 0; i < files; i++)
                {
                    if (!bun.file.IsAssetsFile(i)) continue;
                    AssetsFileInstance inst = am.LoadAssetsFileFromBundle(bun, i, false);
                    wb.ReadFile(am, inst);
                }
            }
            finally
            {
                am.UnloadAll(true);
            }
            return wb;
        }

        void ReadFile(AssetsManager am, AssetsFileInstance inst)
        {
            AssetsFile file = inst.file;
            Platform = (int)file.Metadata.TargetPlatform;
            UnityVersion = file.Metadata.UnityVersion;
            Dictionary<long, string> goNames = new Dictionary<long, string>();
            Dictionary<long, long> goTransform = new Dictionary<long, long>();
            Dictionary<long, Node> nodes = new Dictionary<long, Node>();
            Dictionary<long, string[]> scripts = new Dictionary<long, string[]>();
            Dictionary<long, int> order = new Dictionary<long, int>();

            foreach (AssetFileInfo info in file.GetAssetsOfType(AssetClassID.GameObject))
            {
                AssetTypeValueField f = am.GetBaseField(inst, info);
                goNames[info.PathId] = f["m_Name"].AsString;
                int k = 0;
                foreach (AssetTypeValueField pair in f["m_Component"]["Array"].Children)
                {
                    AssetTypeValueField c = pair["component"];
                    if (!c.IsDummy) order[Local(c)] = k;
                    k++;
                }
            }
            foreach (AssetFileInfo info in Transforms(file))
            {
                AssetTypeValueField f = am.GetBaseField(inst, info);
                Node n = new Node();
                n.Go = Local(f["m_GameObject"]);
                n.Father = Local(f["m_Father"]);
                foreach (AssetTypeValueField c in f["m_Children"]["Array"].Children) n.Children.Add(Local(c));
                nodes[info.PathId] = n;
                if (n.Go != 0) goTransform[n.Go] = info.PathId;
            }
            foreach (AssetFileInfo info in file.GetAssetsOfType(AssetClassID.MonoScript))
            {
                AssetTypeValueField f = am.GetBaseField(inst, info);
                scripts[info.PathId] = new string[] { f["m_Namespace"].AsString, f["m_ClassName"].AsString, f["m_AssemblyName"].AsString };
            }
            foreach (AssetFileInfo info in file.GetAssetsOfType(AssetClassID.MonoBehaviour))
            {
                AssetTypeValueField f;
                try
                {
                    f = am.GetBaseField(inst, info);
                }
                catch (Exception e)
                {
                    Notes.Add("MonoBehaviour " + info.PathId + " unreadable: " + e.Message);
                    continue;
                }
                AssetTypeValueField script = f["m_Script"];
                if (script.IsDummy) continue;
                int fileId = script["m_FileID"].AsInt;
                long scriptId = script["m_PathID"].AsLong;
                string[] s;
                if (fileId != 0 || !scripts.TryGetValue(scriptId, out s))
                {
                    if (fileId != 0) Notes.Add("MonoBehaviour " + info.PathId + " has its script in another file (" + fileId + ")");
                    continue;
                }
                if (s[0] != Namespace) continue;
                long go = Local(f["m_GameObject"]);
                long tr;
                if (go == 0 || !goTransform.TryGetValue(go, out tr))
                {
                    Notes.Add(s[1] + " " + info.PathId + " has no GameObject transform");
                    continue;
                }
                List<int> path = new List<int>();
                List<string> names = new List<string>();
                long cur = tr;
                long root = tr;
                bool broken = false;
                int guard = 0;
                while (true)
                {
                    Node n;
                    if (!nodes.TryGetValue(cur, out n)) { broken = true; break; }
                    if (n.Father == 0) { root = cur; break; }
                    Node parent;
                    if (!nodes.TryGetValue(n.Father, out parent)) { broken = true; break; }
                    int index = parent.Children.IndexOf(cur);
                    if (index < 0) { broken = true; break; }
                    path.Insert(0, index);
                    string nm;
                    names.Insert(0, goNames.TryGetValue(n.Go, out nm) ? nm : "?");
                    cur = n.Father;
                    if (++guard > 256) { broken = true; break; }
                }
                if (broken)
                {
                    Notes.Add(s[1] + " " + info.PathId + " sits in a broken hierarchy");
                    continue;
                }
                string rootName;
                if (!goNames.TryGetValue(nodes[root].Go, out rootName)) rootName = "?";
                WorkshopPrefab p;
                if (!Prefabs.TryGetValue(rootName, out p))
                {
                    p = new WorkshopPrefab();
                    p.Name = rootName;
                    Prefabs[rootName] = p;
                }
                WorkshopMark m = new WorkshopMark();
                m.Kind = s[1];
                int at;
                m.Order = order.TryGetValue(info.PathId, out at) ? at : 0;
                m.Path = path.ToArray();
                m.Names = names.ToArray();
                m.Fields = Fields(f);
                p.Marks.Add(m);
            }
        }

        static IEnumerable<AssetFileInfo> Transforms(AssetsFile file)
        {
            foreach (AssetFileInfo i in file.GetAssetsOfType(AssetClassID.Transform)) yield return i;
            foreach (AssetFileInfo i in file.GetAssetsOfType(AssetClassID.RectTransform)) yield return i;
        }

        static long Local(AssetTypeValueField pptr)
        {
            if (pptr == null || pptr.IsDummy) return 0;
            if (pptr["m_FileID"].AsInt != 0) return 0;
            return pptr["m_PathID"].AsLong;
        }

        static readonly HashSet<string> Skip = new HashSet<string>(StringComparer.Ordinal) { "m_GameObject", "m_Enabled", "m_Script", "m_Name", "m_EditorHideFlags", "m_EditorClassIdentifier", "m_ObjectHideFlags", "m_CorrespondingSourceObject", "m_PrefabInstance", "m_PrefabAsset" };

        static Dictionary<string, object> Fields(AssetTypeValueField f)
        {
            Dictionary<string, object> d = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (AssetTypeValueField c in f.Children)
            {
                if (Skip.Contains(c.FieldName)) continue;
                d[c.FieldName] = Value(c);
            }
            return d;
        }

        static object Value(AssetTypeValueField f)
        {
            AssetTypeTemplateField t = f.TemplateField;
            if (t.IsArray)
            {
                List<object> list = new List<object>();
                foreach (AssetTypeValueField c in f.Children) list.Add(Value(c));
                return list;
            }
            if (t.ValueType == AssetValueType.String) return f.AsString;
            if (f.Children == null || f.Children.Count == 0)
            {
                switch (t.ValueType)
                {
                    case AssetValueType.Bool: return f.AsBool;
                    case AssetValueType.Float: return (double)f.AsFloat;
                    case AssetValueType.Double: return f.AsDouble;
                    case AssetValueType.Int8:
                    case AssetValueType.UInt8:
                    case AssetValueType.Int16:
                    case AssetValueType.UInt16:
                    case AssetValueType.Int32:
                    case AssetValueType.UInt32:
                    case AssetValueType.Int64:
                    case AssetValueType.UInt64:
                        return f.AsLong;
                    default:
                        return null;
                }
            }
            if (f.Children.Count == 1 && f.Children[0].TemplateField.IsArray) return Value(f.Children[0]);
            Dictionary<string, object> d = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (AssetTypeValueField c in f.Children) d[c.FieldName] = Value(c);
            return d;
        }
    }

    internal static class Fields
    {
        internal static bool Bool(Dictionary<string, object> d, string k, bool fallback)
        {
            object v;
            if (d == null || !d.TryGetValue(k, out v) || v == null) return fallback;
            if (v is bool) return (bool)v;
            if (v is long) return (long)v != 0;
            return fallback;
        }

        internal static int Int(Dictionary<string, object> d, string k, int fallback)
        {
            object v;
            if (d == null || !d.TryGetValue(k, out v) || v == null) return fallback;
            if (v is long) return (int)(long)v;
            if (v is double) return (int)(double)v;
            if (v is bool) return (bool)v ? 1 : 0;
            Dictionary<string, object> inner = v as Dictionary<string, object>;
            if (inner != null && inner.ContainsKey("m_Bits")) return Int(inner, "m_Bits", fallback);
            return fallback;
        }

        internal static float Float(Dictionary<string, object> d, string k, float fallback)
        {
            object v;
            if (d == null || !d.TryGetValue(k, out v) || v == null) return fallback;
            if (v is double) return (float)(double)v;
            if (v is long) return (long)v;
            return fallback;
        }

        internal static float[] Vec(Dictionary<string, object> d, string k, float[] fallback)
        {
            object v;
            if (d == null || !d.TryGetValue(k, out v)) return fallback;
            Dictionary<string, object> x = v as Dictionary<string, object>;
            if (x == null) return fallback;
            return new float[] { Float(x, "x", 0f), Float(x, "y", 0f), Float(x, "z", 0f) };
        }

        internal static Dictionary<string, object> Obj(Dictionary<string, object> d, string k)
        {
            object v;
            if (d == null || !d.TryGetValue(k, out v)) return null;
            return v as Dictionary<string, object>;
        }

        internal static List<object> List(Dictionary<string, object> d, string k)
        {
            object v;
            if (d == null || !d.TryGetValue(k, out v)) return null;
            return v as List<object>;
        }

        internal static List<int> Ints(Dictionary<string, object> d, string k)
        {
            List<int> r = new List<int>();
            List<object> l = List(d, k);
            if (l == null) return r;
            foreach (object o in l)
            {
                if (o is long) r.Add((int)(long)o);
                else if (o is double) r.Add((int)(double)o);
            }
            return r;
        }
    }
}
