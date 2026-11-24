using System;
using System.Collections.Generic;
using System.Reflection;
using MelonLoader;
using UnityEngine;
using Il2Cpp;
using Il2CppAlta;
using Il2CppAlta.Crafting;
using Il2CppAlta.Inventory;
using Il2CppAlta.Networking;
using Object = UnityEngine.Object;

namespace SyncLib.Items
{
    public sealed class WorkshopItem
    {
        public string Name { get; internal set; }
        public Vector3 StoreScale { get; internal set; }
        public bool isStackable { get; internal set; }
        public int size { get; internal set; }
        public int dockedStackSize { get; internal set; }
        public float weight { get; internal set; }
        public int LootValue { get; internal set; }
        public int LootCategory { get; internal set; }
        public int PhysicalMaterial { get; internal set; }
        public int[] PickupTags { get; internal set; }
        public IDictionary<string, object> Fields { get; internal set; }
    }

    public static class NetworkPrefabRegistry
    {
        public static Dictionary<int, NetworkPrefab> RegisteredCustomPrefabs = new Dictionary<int, NetworkPrefab>();
        public static Dictionary<Item, WorkshopItem> RegisteredCustomItems = new Dictionary<Item, WorkshopItem>();
        public static event Action OnItemsFinishedRegistering;
        public static event Action RegisterPrefabs;

        public static HashRegistryFile ServerRegistry { get; private set; }
        public static string ServerJson { get; private set; }
        public static bool ServerUsesChunks { get; private set; }
        public static readonly List<string> MissingMods = new List<string>();

        internal static bool hasRegistered;
        static bool inRegistryProcess;
        static readonly Dictionary<long, NetworkPrefab> Applied = new Dictionary<long, NetworkPrefab>();
        static readonly Dictionary<long, Item> Made = new Dictionary<long, Item>();
        static readonly List<KeyValuePair<uint, Item>> ItemsAdded = new List<KeyValuePair<uint, Item>>();

        internal static void RegisterAll(HashRegistryFile reg, string json, bool chunked)
        {
            if (inRegistryProcess) return;
            UnregisterAll();
            ServerRegistry = reg;
            ServerJson = json;
            ServerUsesChunks = chunked;
            DateTime start = DateTime.UtcNow;
            PrefabManager.PrepareSpawnSetups();
            HashedGeneralValue<Item>.CheckItems();
            Game.ForgetDonors();
            Joints();
            int before = 0;
            inRegistryProcess = true;
            try
            {
                Action handlers = RegisterPrefabs;
                if (handlers != null)
                {
                    foreach (Delegate d in handlers.GetInvocationList())
                    {
                        before = RegisteredCustomPrefabs.Count;
                        try
                        {
                            ((Action)d)();
                        }
                        catch (Exception e)
                        {
                            Core.Error("[FROM: '" + Who(d) + "'] RegisterPrefabs threw after " + (RegisteredCustomPrefabs.Count - before) + " prefab(s): " + e);
                        }
                    }
                }
            }
            finally
            {
                inRegistryProcess = false;
            }
            hasRegistered = true;
            Core.Log("registered " + RegisteredCustomPrefabs.Count + " prefab(s) and " + RegisteredCustomItems.Count + " item(s) for this server in " + (int)(DateTime.UtcNow - start).TotalMilliseconds + " ms");
            Action done = OnItemsFinishedRegistering;
            if (done != null)
            {
                foreach (Delegate d in done.GetInvocationList())
                {
                    try { ((Action)d)(); }
                    catch (Exception e) { Core.Error("[FROM: '" + Who(d) + "'] OnItemsFinishedRegistering threw: " + e); }
                }
            }
        }

        internal static List<int> Present()
        {
            List<int> list = new List<int>();
            MissingMods.Clear();
            if (ServerRegistry == null) return list;
            Dictionary<string, string> missing = new Dictionary<string, string>();
            foreach (ModHashRegistry m in ServerRegistry.Mods)
            {
                foreach (int h in m.ItemHashes.Values)
                {
                    if (RegisteredCustomPrefabs.ContainsKey(h) || Plat.MapHas((uint)h)) { if (!list.Contains(h)) list.Add(h); }
                    else missing[m.ModId] = m.LastKnownVersion;
                }
            }
            foreach (KeyValuePair<string, string> kv in missing) MissingMods.Add(kv.Key + " (VERSION: " + kv.Value + ")");
            return list;
        }

        internal static int Expected()
        {
            int n = 0;
            if (ServerRegistry == null) return 0;
            foreach (ModHashRegistry m in ServerRegistry.Mods) n += m.ItemHashes.Count;
            return n;
        }

        static string Who(Delegate d)
        {
            MethodInfo m = d.Method;
            return m == null || m.DeclaringType == null ? "Unknown" : m.DeclaringType.Assembly.GetName().Name;
        }

        static void Joints()
        {
            if (Apply.JointSlotReference == null)
            {
                Item stick = HashedGeneralValue<Item>.Get(10650u);
                JointSlot s = stick == null || stick.Prefab == null ? null : Plat.Child<JointSlot>(stick.Prefab, false);
                if (s != null) Apply.JointSlotReference = s.gameObject;
            }
            if (Apply.JointInsertReference == null)
            {
                Item flint = HashedGeneralValue<Item>.Get(42570u);
                JointInsert i = flint == null || flint.Prefab == null ? null : Plat.Child<JointInsert>(flint.Prefab, false);
                if (i != null) Apply.JointInsertReference = i.gameObject;
            }
        }

        internal static void UnregisterAll()
        {
            int n = RegisteredCustomPrefabs.Count;
            foreach (KeyValuePair<int, NetworkPrefab> kv in RegisteredCustomPrefabs) Plat.MapRemove((uint)kv.Key, kv.Value);
            foreach (KeyValuePair<uint, Item> kv in ItemsAdded) Plat.ItemRemove(kv.Key, kv.Value);
            RegisteredCustomPrefabs.Clear();
            RegisteredCustomItems.Clear();
            ItemsAdded.Clear();
            MissingMods.Clear();
            ServerRegistry = null;
            ServerJson = null;
            ServerUsesChunks = false;
            if (hasRegistered && n > 0) Core.Log("removed " + n + " custom prefab(s) from the last server");
            hasRegistered = false;
        }

        static void Fail(string mod, string message)
        {
            Core.Error("[FROM: '" + (mod ?? "Unknown") + "'] " + message);
        }

        public static NetworkPrefab RegisterCustomPrefab(this MelonMod mod, GameObject prefab, string CustomPrefabId, params Type[] AdditionalComponents)
        {
            if (mod == null)
            {
                Fail(null, "Attempted to register a custom prefab. However the 'mod' parameter was null.");
                return null;
            }
            return RegisterCustomPrefab(mod.Info.Name, prefab, CustomPrefabId, AdditionalComponents);
        }

        public static NetworkPrefab RegisterCustomPrefab(string modName, GameObject prefab, string CustomPrefabId, params Type[] AdditionalComponents)
        {
            if (!inRegistryProcess)
            {
                Fail(modName, "Attempted to register a custom prefab too late.");
                return null;
            }
            if (string.IsNullOrEmpty(modName))
            {
                Fail(modName, "Attempted to register a custom prefab without a mod name.");
                return null;
            }
            if (string.IsNullOrWhiteSpace(CustomPrefabId) || CustomPrefabId.Length < 3)
            {
                Fail(modName, "Attempted to register a custom prefab with an invalid CustomPrefabId. Please make sure your CustomPrefabId has at least three characters.");
                return null;
            }
            string key = CustomPrefabId + " (" + modName + ")";
            int hash = 0;
            string owner = ServerRegistry == null ? null : ServerRegistry.Owner(key, out hash);
            if (owner == null) return null;
            if (prefab == null)
            {
                Fail(modName, "Attempted to register a custom prefab that was null.");
                return null;
            }
            if (hash <= 0 || hash > 65535)
            {
                Fail(modName, "The server gave '" + key + "' the number " + hash + ", which is out of range.");
                return null;
            }
            foreach (ModHashRegistry m in ServerRegistry.Mods)
            {
                if (m.ModId == owner) continue;
                if (m.ItemHashes.ContainsValue(hash))
                {
                    Fail(modName, "Hash '" + hash + "' for '" + prefab.name + "' is already registered by '" + m.ModId + "'");
                    return null;
                }
            }
            if (RegisteredCustomPrefabs.ContainsKey(hash))
            {
                Fail(modName, "'" + key + "' was registered twice on this server.");
                return null;
            }
            NetworkPrefab taken = Plat.MapGet((uint)hash);
            long k = Plat.Key(prefab);
            NetworkPrefab np;
            Applied.TryGetValue(k, out np);
            if (taken != null && !Plat.Same(taken, np))
            {
                Fail(modName, "Number " + hash + " for '" + key + "' is already the game prefab '" + taken.name + "' here.");
                return null;
            }
            if (np != null)
            {
                Game.SetPrefabHash(np, hash);
            }
            else
            {
                np = Plat.Get<NetworkPrefab>(prefab);
                if (np == null) np = Plat.Add<NetworkPrefab>(prefab);
                NetworkEntity entity = Plat.Get<NetworkEntity>(prefab);
                if (entity == null) entity = Plat.Add<NetworkEntity>(prefab);
                List<Component> all = new List<Component>();
                all.Add(np);
                all.Add(entity);
                if (AdditionalComponents != null)
                {
                    foreach (Type t in AdditionalComponents)
                    {
                        if (t == null) continue;
                        all.Add(Plat.Add(prefab, t));
                    }
                }
                if (Plat.Get<Rigidbody>(prefab) != null) all.Add(Plat.Add<NetworkRigidbody>(prefab));
                WorkshopPrefab wp = Bundles.Workshop(prefab);
                List<string> notes = new List<string>();
                int unknown = Attrs.Unknown;
                Apply.All(prefab, entity, np, all, wp, notes);
                foreach (Component c in all) Attrs.Run(c);
                foreach (string n in notes) Core.Warn(key + ": " + n);
                if (Plat.Quest && Attrs.Unknown > unknown) Core.Warn(key + ": " + (Attrs.Unknown - unknown) + " added component(s) of a type SyncLib does not know; their [GetComponent] fields are not filled on Quest");
                int fixedShaders = Shaders.Fix(prefab);
                if (fixedShaders > 0) Core.Log(key + ": " + fixedShaders + " material(s) had a shader this headset cannot draw; now Township Tale/Cheap/SimpleStandard");
                Game.SetPrefabHash(np, hash);
                np.Initialize();
                entity.OnValidate();
                Applied[k] = np;
                Core.Log("built " + prefab.name + (wp == null ? " (no Workshop settings)" : " (" + wp.Marks.Count + " Workshop settings, " + Game.EmbeddedCount(np) + " embedded)"));
            }
            Plat.MapAdd((uint)hash, np);
            RegisteredCustomPrefabs[hash] = np;
            Core.Log("Successfully registered " + prefab.name + " as a NetworkPrefab with HashId of '" + hash + "'!");
            return np;
        }

        public static Item RegisterCustomItem(this NetworkPrefab prefab)
        {
            if (prefab == null) return null;
            WorkshopPrefab wp = Bundles.Workshop(prefab.gameObject);
            WorkshopMark mark = wp == null ? null : wp.Root("Item");
            if (mark == null)
            {
                Core.Error("Please use ATT Workshop to register custom items. (" + prefab.name + " has no Workshop Item on its root)");
                return null;
            }
            uint hash = prefab.Hash;
            Item item;
            long k = Plat.Key(prefab);
            if (!Made.TryGetValue(k, out item) || item == null)
            {
                Apply.Material(prefab, mark);
                item = Apply.NewItem(prefab.name, mark);
                item.hideFlags = HideFlags.DontUnloadUnusedAsset;
                Pickup pk = Plat.Get<Pickup>(prefab);
                Game.BindItem(item, prefab, (int)hash);
                if (pk != null) pk.Item = item;
                Made[k] = item;
            }
            else
            {
                Game.BindItem(item, prefab, (int)hash);
            }
            Item there = Plat.ItemGet(hash);
            if (there != null && !Plat.Same(there, item))
            {
                Core.Error("item number " + hash + " for " + prefab.name + " is already the game item '" + there.name + "'");
                return null;
            }
            if (there == null)
            {
                Plat.ItemAdd(hash, item);
                ItemsAdded.Add(new KeyValuePair<uint, Item>(hash, item));
            }
            RegisteredCustomItems[item] = Describe(prefab.name, mark);
            Core.Log("Successfully registered " + prefab.name + " as a custom item!");
            return item;
        }

        static WorkshopItem Describe(string name, WorkshopMark m)
        {
            Dictionary<string, object> f = m.Fields;
            WorkshopItem w = new WorkshopItem();
            w.Name = name;
            float[] s = global::SyncLib.Fields.Vec(f, "StoreScale", new float[] { 0.5f, 0.5f, 0.5f });
            w.StoreScale = new Vector3(s[0], s[1], s[2]);
            w.isStackable = global::SyncLib.Fields.Bool(f, "isStackable", false);
            w.size = global::SyncLib.Fields.Int(f, "size", 0);
            w.dockedStackSize = global::SyncLib.Fields.Int(f, "dockedStackSize", 30);
            w.weight = global::SyncLib.Fields.Float(f, "weight", 1f);
            w.LootValue = global::SyncLib.Fields.Int(f, "LootValue", 0);
            w.LootCategory = global::SyncLib.Fields.Int(f, "LootCategory", 0);
            w.PhysicalMaterial = global::SyncLib.Fields.Int(f, "PhysicalMaterial", 0);
            w.PickupTags = global::SyncLib.Fields.Ints(f, "PickupTags").ToArray();
            w.Fields = f;
            return w;
        }

        public static void ExecuteAllGetComponentAtts(params Component[] comps)
        {
            if (comps == null) return;
            foreach (Component c in comps) Attrs.Run(c);
        }

        public static T FindScriptableObjectOfName<T>(string name, bool contains = false) where T : Object
        {
            return Apply.Find<T>(name, contains);
        }

        public static AssetBundle LoadAssetBundle(Assembly assembly, string embeddedPath)
        {
            return Bundles.Bundle(assembly, embeddedPath);
        }

        public static GameObject GetGameObjectFromAssetBundle(Assembly assembly, string embeddedPath, string objectName)
        {
            return Bundles.Prefab(assembly, embeddedPath, objectName);
        }
    }
}
