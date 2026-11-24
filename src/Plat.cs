using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Il2Cpp;
using Il2CppAlta;
using Il2CppAlta.Inventory;
using Il2CppAlta.Networking;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using GameStream = Il2CppAlta.Serialization.Stream;
using Object = UnityEngine.Object;

namespace SyncLib
{
    internal static class Plat
    {
        internal const bool Quest = true;

        internal static T Get<T>(Component c) where T : Component
        {
            if (c == null) return null;
            Component x = c.GetComponent(Il2CppType.Of<T>());
            return x == null ? null : x.TryCast<T>();
        }

        internal static T Get<T>(GameObject g) where T : Component
        {
            return g == null ? null : Get<T>(g.transform);
        }

        internal static T Add<T>(GameObject g) where T : Component
        {
            return g.AddComponent(Il2CppType.Of<T>()).TryCast<T>();
        }

        internal static Component Add(GameObject g, Type t)
        {
            return g.AddComponent(Il2CppType.From(t));
        }

        internal static bool Has(GameObject g, Type t)
        {
            return g.GetComponent(Il2CppType.From(t)) != null;
        }

        internal static List<T> Children<T>(Component c, bool inactive) where T : Component
        {
            List<T> list = new List<T>();
            if (c == null) return list;
            foreach (Component x in c.GetComponentsInChildren(Il2CppType.Of<T>(), inactive))
            {
                T t = x == null ? null : x.TryCast<T>();
                if (t != null) list.Add(t);
            }
            return list;
        }

        internal static T Child<T>(Component c, bool inactive) where T : Component
        {
            if (c == null) return null;
            Component x = c.GetComponentInChildren(Il2CppType.Of<T>(), inactive);
            return x == null ? null : x.TryCast<T>();
        }

        internal static List<T> Here<T>(Component c) where T : Component
        {
            List<T> list = new List<T>();
            if (c == null) return list;
            foreach (Component x in c.GetComponents(Il2CppType.Of<T>()))
            {
                T t = x == null ? null : x.TryCast<T>();
                if (t != null) list.Add(t);
            }
            return list;
        }

        internal static T Up<T>(Component c) where T : Component
        {
            for (Transform t = c == null ? null : c.transform; t != null; t = t.parent)
            {
                T x = Get<T>(t);
                if (x != null) return x;
            }
            return null;
        }

        internal static bool IsBehaviour(MonoBehaviour m)
        {
            return m != null && m.TryCast<INetworkEntityBehaviour>() != null;
        }

        internal static bool InterferesWithDocking(MonoBehaviour m)
        {
            return m != null && m.TryCast<IInterfereWithDocking>() != null;
        }

        internal static long Key(Object o)
        {
            return o == null ? 0 : (long)o.Pointer;
        }

        internal static bool Same(Object a, Object b)
        {
            if (a == null || b == null) return false;
            return Key(a) == Key(b);
        }

        internal static GameObject Copy(GameObject g)
        {
            Object o = Object.Instantiate((Object)g);
            return o == null ? null : o.TryCast<GameObject>();
        }

        internal static T Clone<T>(T o) where T : Il2CppSystem.Object
        {
            if (o == null) return null;
            Il2CppSystem.Object x = o.MemberwiseClone();
            return x == null ? null : x.Cast<T>();
        }

        internal static Il2CppReferenceArray<T> Array<T>(List<T> list) where T : Il2CppSystem.Object
        {
            Il2CppSystem.Array arr = Il2CppSystem.Array.CreateInstance(Il2CppType.Of<T>(), list.Count);
            Il2CppReferenceArray<T> r = new Il2CppReferenceArray<T>(arr.Pointer);
            for (int i = 0; i < list.Count; i++) r[i] = list[i];
            return r;
        }

        internal static List<Object> Loaded<T>() where T : Object
        {
            List<Object> list = new List<Object>();
            foreach (Object o in Resources.FindObjectsOfTypeAll(Il2CppType.Of<T>())) list.Add(o);
            return list;
        }

        internal static T As<T>(Object o) where T : Object
        {
            return o == null ? null : o.TryCast<T>();
        }

        internal static AssetBundle BundleFromFile(string path)
        {
            return AssetBundle.LoadFromFile(path);
        }

        internal static GameObject LoadPrefab(AssetBundle b, string name)
        {
            if (b == null) return null;
            Object o = b.LoadAsset(name);
            return As<GameObject>(o);
        }

        internal static string CacheFolder()
        {
            string root = Application.persistentDataPath;
            if (string.IsNullOrEmpty(root)) root = Path.GetTempPath();
            string dir = Path.Combine(root, "SyncLibBundles");
            Directory.CreateDirectory(dir);
            return dir;
        }

        internal static SerializeConnectionMethod ConnectionMethod(Action<Connection, GameStream> a)
        {
            return DelegateSupport.ConvertDelegate<SerializeConnectionMethod>(a);
        }

        internal static ConnectionChannel Reliable(Connection c)
        {
            ConnectionChannel d = c.DefaultChannel;
            if (d != null && d.IsReliable) return d;
            Il2CppReferenceArray<ConnectionChannel> all = c.channels;
            if (all == null) return null;
            for (int i = 0; i < all.Length; i++) if (all[i] != null && all[i].IsReliable) return all[i];
            return null;
        }

        internal static bool MapHas(uint hash)
        {
            PrefabManager.PrepareSpawnSetups();
            return PrefabManager.prefabMap.ContainsKey(hash);
        }

        internal static NetworkPrefab MapGet(uint hash)
        {
            PrefabManager.PrepareSpawnSetups();
            var map = PrefabManager.prefabMap;
            return map.ContainsKey(hash) ? map[hash] : null;
        }

        internal static void MapAdd(uint hash, NetworkPrefab p)
        {
            PrefabManager.PrepareSpawnSetups();
            PrefabManager.prefabMap.Add(hash, p);
        }

        internal static void MapRemove(uint hash, NetworkPrefab mine)
        {
            NetworkPrefab now = MapGet(hash);
            if (now == null || !Same(now, mine)) return;
            PrefabManager.prefabMap.Remove(hash);
        }

        internal static Item ItemGet(uint hash)
        {
            var items = HashedGeneralValue<Item>.items;
            return items != null && items.ContainsKey(hash) ? items[hash] : null;
        }

        internal static void ItemAdd(uint hash, Item item)
        {
            HashedGeneralValue<Item>.items.Add(hash, item);
            HashedGeneralValue<Item>.itemsByName = null;
        }

        internal static void ItemRemove(uint hash, Item mine)
        {
            Item now = ItemGet(hash);
            if (now == null || !Same(now, mine)) return;
            HashedGeneralValue<Item>.items.Remove(hash);
            HashedGeneralValue<Item>.itemsByName = null;
        }

        internal static List<Item> AllItems()
        {
            List<Item> list = new List<Item>();
            var items = HashedGeneralValue<Item>.items;
            if (items == null) return list;
            foreach (var kv in items) if (kv.Value != null) list.Add(kv.Value);
            return list;
        }

    }

}
