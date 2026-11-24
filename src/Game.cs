using System;
using System.Collections.Generic;
using UnityEngine;
using Il2Cpp;
using Il2CppAlta;
using Il2CppAlta.Crafting;
using Il2CppAlta.Inventory;
using Il2CppAlta.Loot;
using Il2CppAlta.Networking;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Object = UnityEngine.Object;

namespace SyncLib
{
    internal static class Game
    {
        internal static void SetPrefabHash(NetworkPrefab p, int hash)
        {
            p.hash = hash;
        }

        internal static void AddEmbedded(NetworkPrefab p, NetworkEntity e)
        {
            p.embeddedEntities.Add(e);
        }

        internal static int EmbeddedCount(NetworkPrefab p)
        {
            return p.embeddedEntities == null ? 0 : p.embeddedEntities.Count;
        }

        internal static void SetSlotTypes(JointSlot s, List<JointSlotType> types)
        {
            var list = s.types;
            list.Clear();
            foreach (JointSlotType t in types) list.Add(t);
        }

        static GrabPoint _grip;
        static Item.DockPositioning _dock;
        static Item.CustomDockPositioning _spot;

        internal static void ForgetDonors()
        {
            _grip = null;
            _dock = null;
            _spot = null;
        }

        static GrabPoint GripDonor()
        {
            if (_grip != null) return _grip;
            foreach (Object o in Plat.Loaded<Pickup>())
            {
                Pickup p = Plat.As<Pickup>(o);
                if (p == null) continue;
                Il2CppReferenceArray<GrabPoint> points = p.grabPoints;
                if (points == null) continue;
                for (int i = 0; i < points.Length; i++)
                {
                    GrabPoint g = points[i];
                    if (g != null && g.enabledFromDocks != null && g.enabledFromDocks.Count == 0) { _grip = g; return g; }
                }
            }
            throw new InvalidOperationException("no loaded pickup lends a grab point");
        }

        internal static GrabPoint FreshGrabPoint()
        {
            GrabPoint g = Plat.Clone(GripDonor());
            g.isDisabled = false;
            g.transform = null;
            g.isUsingLocal = true;
            g.position = Vector3.zero;
            g.rotationEuler = Vector3.zero;
            g.linear = Vector3.zero;
            g.rotationMode = RotationMode.Fixed;
            g.poleForward = Vector3.forward;
            g.offset = 0f;
            g.isSetupWithLeft = false;
            g.dockedRestriction = (PickupFromDockRestriction)0;
            g.nonDockedRestriction = (PickupFromDockRestriction)0;
            g.isIgnoringAngle = false;
            g.thumbPosition = (ThumbPosition)0;
            g.refuseMultipleHands = false;
            g.interactor = null;
            return g;
        }

        internal static Item CreateItem()
        {
            ScriptableObject so = ScriptableObject.CreateInstance(Il2CppType.Of<Item>());
            Item item = so == null ? null : so.TryCast<Item>();
            if (item == null) throw new InvalidOperationException("the game did not make a new Item");
            return item;
        }

        internal static void SetItemCategory(Item item, Category c)
        {
            item.lootCategory = c;
        }

        internal static void SetItemValue(Item item, LootValue v)
        {
            item.lootValue = v;
        }

        internal static void SetItemTags(Item item, List<PickupTag> tags)
        {
            var list = item.tags;
            list.Clear();
            foreach (PickupTag t in tags) list.Add(t);
        }

        static Item.DockPositioning DockDonor()
        {
            if (_dock != null) return _dock;
            foreach (Item i in Plat.AllItems())
            {
                Item.DockPositioning d = i.scaledPositioning;
                if (d != null) { _dock = d; return d; }
            }
            throw new InvalidOperationException("no loaded item lends a dock position");
        }

        static Item.CustomDockPositioning SpotDonor()
        {
            if (_spot != null) return _spot;
            foreach (Item i in Plat.AllItems())
            {
                Il2CppReferenceArray<Item.CustomDockPositioning> spots = i.customPositions;
                if (spots == null) continue;
                for (int k = 0; k < spots.Length; k++) if (spots[k] != null) { _spot = spots[k]; return _spot; }
            }
            throw new InvalidOperationException("no loaded item lends a custom dock position");
        }

        static Item.DockPositioning Dock(Vector3 pos, Vector3 euler)
        {
            Item.DockPositioning d = Plat.Clone(DockDonor());
            d.storePosition = pos;
            d.storeEuler = euler;
            return d;
        }

        static Item.CustomDockPositioning Spot(Vector3 pos, Vector3 euler)
        {
            Item.CustomDockPositioning s = Plat.Clone(SpotDonor());
            s.dockType = null;
            s.storePosition = pos;
            s.storeEuler = euler;
            return s;
        }

        internal static void SetItemDocks(Item item, Vector3 storeScale, Vector3 sPos, Vector3 sEuler, Vector3 nPos, Vector3 nEuler, List<Vector3[]> custom, List<Vector3[]> crafted)
        {
            List<Item.CustomDockPositioning> a = new List<Item.CustomDockPositioning>();
            foreach (Vector3[] v in custom) a.Add(Spot(v[0], v[1]));
            List<Item.CustomDockPositioning> b = new List<Item.CustomDockPositioning>();
            foreach (Vector3[] v in crafted) b.Add(Spot(v[0], v[1]));
            item.storeScale = storeScale;
            item.scaledPositioning = Dock(sPos, sEuler);
            item.normalPositioning = Dock(nPos, nEuler);
            item.customPositions = Plat.Array(a);
            item.customPositionsWhenCrafted = Plat.Array(b);
        }

        internal static void BindItem(Item item, NetworkPrefab prefab, int hash)
        {
            item.Prefab = prefab;
            item.hash = hash;
        }
    }
}
