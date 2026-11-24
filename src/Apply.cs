using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using Il2Cpp;
using Il2CppAlta;
using Il2CppAlta.Crafting;
using Il2CppAlta.Impact;
using Il2CppAlta.Inventory;
using Il2CppAlta.Loot;
using Il2CppAlta.Networking;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Object = UnityEngine.Object;

namespace SyncLib
{
    internal static class Apply
    {
        internal static GameObject JointSlotReference;
        internal static GameObject JointInsertReference;

        internal static T Find<T>(string name, bool contains) where T : Object
        {
            if (name == null) return null;
            name = Regex.Replace(name, "(?<=[a-z0-9])(?<!_)([A-Z])", " $1");
            name = name.Replace("__", "-");
            foreach (Object o in Plat.Loaded<T>())
            {
                if (o == null) continue;
                string n = o.name;
                if (contains ? n.Contains(name) : n == name) return Plat.As<T>(o);
            }
            return null;
        }

        static Vector3 V(float[] f)
        {
            return f == null ? Vector3.zero : new Vector3(f[0], f[1], f[2]);
        }

        static bool ActiveChain(Transform from, Transform to)
        {
            for (Transform t = to; t != null; t = t.parent)
            {
                if (!t.gameObject.activeSelf) return false;
                if (t == from) return true;
            }
            return false;
        }

        static Transform Resolve(GameObject root, WorkshopMark m, List<string> notes)
        {
            Transform t = root.transform;
            for (int i = 0; i < m.Path.Length; i++)
            {
                int k = m.Path[i];
                if (k < 0 || k >= t.childCount)
                {
                    notes.Add(m.Kind + " at " + m.Where() + ": the prefab has no child " + k + " under " + t.name);
                    return null;
                }
                t = t.GetChild(k);
                if (t.name != m.Names[i]) notes.Add(m.Kind + " at " + m.Where() + ": child " + k + " is named " + t.name);
            }
            return t;
        }

        sealed class Placed
        {
            internal WorkshopMark Mark;
            internal Transform At;
        }

        static List<Placed> Marks(GameObject root, WorkshopPrefab wp, string kind, Transform under, Dictionary<WorkshopMark, Transform> where)
        {
            List<Placed> list = new List<Placed>();
            if (wp == null) return list;
            foreach (WorkshopMark m in wp.Of(kind))
            {
                Transform t;
                if (!where.TryGetValue(m, out t) || t == null) continue;
                if (!ActiveChain(under, t)) continue;
                Placed p = new Placed();
                p.Mark = m;
                p.At = t;
                list.Add(p);
            }
            return list;
        }

        internal static void All(GameObject root, NetworkEntity entity, NetworkPrefab prefab, List<Component> all, WorkshopPrefab wp, List<string> notes)
        {
            Dictionary<WorkshopMark, Transform> where = new Dictionary<WorkshopMark, Transform>();
            if (wp != null) foreach (WorkshopMark m in wp.Marks) where[m] = Resolve(root, m, notes);
            Impactors(root, wp, where, all);
            ChildEntities(root, wp, where, all);
            CraftingParts(root, wp, where, all);
            Joints(root, prefab, wp, where, all);
            Pickups(root, wp, where, all);
            Durability(root, wp, where, all);
        }

        static void Impactors(GameObject root, WorkshopPrefab wp, Dictionary<WorkshopMark, Transform> where, List<Component> all)
        {
            foreach (Placed p in Marks(root, wp, "ImpactTool", root.transform, where))
            {
                Dictionary<string, object> f = p.Mark.Fields;
                ImpactTool t = Plat.Add<ImpactTool>(p.At.gameObject);
                LayerMask mask = new LayerMask();
                mask.m_Mask = Fields.Int(f, "BlockableLayers", 1);
                t.damage = Fields.Float(f, "damage", 1f);
                t.blockableLayers = mask;
                t.timeToSelfHit = Fields.Float(f, "timeToSelfHit", 1.5f);
                t.isMedHandle = Fields.Bool(f, "isMedHandle", false);
                t.singleHandedMultiplier = Fields.Float(f, "singleHandedMultiplier", 1f);
                t.dualHandedMultiplier = Fields.Float(f, "dualHandedMultiplier", 1f);
                t.lengthAxis = V(Fields.Vec(f, "lengthAxis", new float[] { 0f, 1f, 0f }));
                t.isIgnoringVelocityCheck = Fields.Bool(f, "isIgnoringVelocityCheck", false);
                t.requiredBlockLevel = (BlockLevel)Fields.Int(f, "RequiredBlockLevel", 2);
                t.resetConditions = (ImpactTool.ResetConditions)Fields.Int(f, "ResetConditions", 2);
                all.Add(t);
            }
            foreach (Placed p in Marks(root, wp, "ImpactorMap", root.transform, where))
            {
                ImpactorMap map = Plat.Add<ImpactorMap>(p.At.gameObject);
                foreach (Placed q in Marks(root, wp, "Impactor", p.At, where))
                {
                    Impactor imp = Plat.Add<Impactor>(q.At.gameObject);
                    int values = Fields.Int(q.Mark.Fields, "ImpactorValues", 0);
                    int settings = Fields.Int(q.Mark.Fields, "ImpactorSettings", 0);
                    if (values != 0)
                    {
                        ImpactorValues v = Find<ImpactorValues>(Names.Of(Names.ImpactorValues, values), true);
                        if (v != null) imp.impactorValues = v;
                    }
                    if (settings != 0)
                    {
                        ImpactorSettings s = Find<ImpactorSettings>(Names.Of(Names.ImpactorSettings, settings), true);
                        if (s != null) imp.impactorSettings = s;
                    }
                    all.Add(imp);
                }
                map.ForceValidate();
            }
        }

        static void ChildEntities(GameObject root, WorkshopPrefab wp, Dictionary<WorkshopMark, Transform> where, List<Component> all)
        {
            foreach (Placed p in Marks(root, wp, "ChildNetworkEntity", root.transform, where))
            {
                all.Add(Plat.Add<NetworkEntity>(p.At.gameObject));
            }
        }

        static void CraftingParts(GameObject root, WorkshopPrefab wp, Dictionary<WorkshopMark, Transform> where, List<Component> all)
        {
            foreach (Placed p in Marks(root, wp, "CraftingPart", root.transform, where))
            {
                bool addon = Fields.Bool(p.Mark.Fields, "isAddon", false);
                Component c = addon ? (Component)Plat.Add<AddonCraftingPart>(p.At.gameObject) : Plat.Add<CraftingPart>(p.At.gameObject);
                all.Add(c);
            }
        }

        static void Joints(GameObject root, NetworkPrefab prefab, WorkshopPrefab wp, Dictionary<WorkshopMark, Transform> where, List<Component> all)
        {
            foreach (Placed p in Marks(root, wp, "JointSlot", root.transform, where))
            {
                if (JointSlotReference == null)
                {
                    Core.Warn("no stick joint slot to copy; the slot at " + p.Mark.Where() + " is left out");
                    continue;
                }
                GameObject made = Plat.Copy(JointSlotReference);
                Transform tr = made.transform;
                tr.parent = p.At.parent;
                tr.localPosition = p.At.localPosition;
                tr.localScale = p.At.localScale;
                tr.localRotation = p.At.localRotation;
                JointSlot slot = Plat.Get<JointSlot>(made);
                NetworkEntity se = Plat.Get<NetworkEntity>(made);
                if (slot == null || se == null) continue;
                se.OnValidate();
                Game.AddEmbedded(prefab, se);
                List<JointSlotType> types = new List<JointSlotType>();
                foreach (int k in Fields.Ints(p.Mark.Fields, "JointSlotTypes"))
                {
                    JointSlotType jt = Find<JointSlotType>(Names.Of(Names.JointSlotType, k), false);
                    if (jt != null) types.Add(jt);
                }
                Game.SetSlotTypes(slot, types);
                all.Add(se);
                p.At.gameObject.SetActive(false);
            }
            foreach (Placed p in Marks(root, wp, "JointInsert", root.transform, where))
            {
                if (JointInsertReference == null)
                {
                    Core.Warn("no flint joint insert to copy; the insert at " + p.Mark.Where() + " is left out");
                    continue;
                }
                GameObject made = Plat.Copy(JointInsertReference);
                Transform tr = made.transform;
                tr.parent = p.At.parent;
                tr.localPosition = p.At.localPosition;
                tr.localScale = p.At.localScale;
                tr.localEulerAngles = p.At.localEulerAngles;
                JointInsert ins = Plat.Get<JointInsert>(made);
                NetworkEntity se = Plat.Get<NetworkEntity>(made);
                if (ins == null || se == null) continue;
                se.OnValidate();
                Game.AddEmbedded(prefab, se);
                JointInsertType jt = Find<JointInsertType>(Names.Of(Names.JointSlotType, Fields.Int(p.Mark.Fields, "InsertType", 0)), false);
                if (jt != null) ins.type = jt;
                all.Add(se);
                p.At.gameObject.SetActive(false);
            }
        }

        static void Pickups(GameObject root, WorkshopPrefab wp, Dictionary<WorkshopMark, Transform> where, List<Component> all)
        {
            foreach (Placed p in Marks(root, wp, "Pickup", root.transform, where))
            {
                Dictionary<string, object> f = p.Mark.Fields;
                Pickup pk = Plat.Add<Pickup>(p.At.gameObject);
                int allowance = Fields.Int(f, "interactionAllowance", 0);
                pk.isTightGrab = Fields.Bool(f, "isTightGrab", true);
                pk.isSwingable = Fields.Bool(f, "isSwingable", false);
                pk.isCumbersome = Fields.Bool(f, "isCumbersome", false);
                pk.blockLevel = (BlockLevel)Fields.Int(f, "blockLevel", 1);
                pk.jointType = (PickUpJointType)Fields.Int(f, "jointType", 2);
                pk.grabPointsScale = Fields.Float(f, "grabPointsScale", 1f);
                pk.bypassMed1h = Fields.Bool(f, "bypassMed1h", false);
                if (allowance != 0) pk.multipleAllowance = (MultipleInteractionAllowance)allowance;
                List<GrabPoint> points = new List<GrabPoint>();
                foreach (Placed g in Marks(root, wp, "GrabPoint", p.At, where))
                {
                    Dictionary<string, object> gf = g.Mark.Fields;
                    GrabPoint gp = Game.FreshGrabPoint();
                    Transform gt = g.At;
                    gp.rotationMode = (RotationMode)Fields.Int(gf, "rotationMode", 0);
                    gp.transform = gt;
                    gp.position = gt.localPosition;
                    gp.rotationEuler = gt.localEulerAngles;
                    gp.poleForward = V(Fields.Vec(gf, "PoleForward", new float[] { 0f, 0f, 1f }));
                    gp.linear = V(Fields.Vec(gf, "Linear", null));
                    gp.refuseMultipleHands = Fields.Bool(gf, "RefuseMultipleHands", false);
                    gp.offset = Fields.Float(gf, "Offset", 0f);
                    gp.isDisabled = Fields.Bool(gf, "IsDisabled", false);
                    gp.nonDockedRestriction = (PickupFromDockRestriction)Fields.Int(gf, "nonDockedRestriction", 0);
                    gp.dockedRestriction = (PickupFromDockRestriction)Fields.Int(gf, "DockedRestriction", 0);
                    gp.thumbPosition = (ThumbPosition)Fields.Int(gf, "ThumbPosition", 0);
                    points.Add(gp);
                }
                pk.grabPoints = Plat.Array(points);
                all.Add(pk);
            }
        }

        static void Durability(GameObject root, WorkshopPrefab wp, Dictionary<WorkshopMark, Transform> where, List<Component> all)
        {
            foreach (Placed p in Marks(root, wp, "DurabilityModule", root.transform, where))
            {
                all.Add(Plat.Add<DurabilityModule>(p.At.gameObject));
            }
        }

        internal static Item NewItem(string name, WorkshopMark mark)
        {
            Dictionary<string, object> f = mark.Fields;
            Item item = Game.CreateItem();
            item.name = name;
            int cat = Fields.Int(f, "LootCategory", 0);
            if (cat != 0)
            {
                Category c = Find<Category>(Names.Of(Names.LootCategory, cat), false);
                if (c != null) Game.SetItemCategory(item, c);
            }
            int value = Fields.Int(f, "LootValue", 0);
            if (value != 0)
            {
                LootValue v = Find<LootValue>(Names.Of(Names.LootValue, value), true);
                if (v != null) Game.SetItemValue(item, v);
            }
            List<int> tagIds = Fields.Ints(f, "PickupTags");
            if (tagIds.Count > 0)
            {
                List<PickupTag> tags = new List<PickupTag>();
                foreach (int t in tagIds)
                {
                    PickupTag tag = Find<PickupTag>(Names.Of(Names.PickupTag, t), false);
                    if (tag != null) tags.Add(tag);
                }
                if (tags.Count > 0) Game.SetItemTags(item, tags);
            }
            Dictionary<string, object> scaled = Fields.Obj(f, "scaledPositioning");
            Dictionary<string, object> normal = Fields.Obj(f, "normalPositioning");
            List<Vector3[]> custom = Spots(Fields.List(f, "customPositions"));
            List<Vector3[]> crafted = Spots(Fields.List(f, "customPositionsWhenCrafted"));
            Game.SetItemDocks(item,
                V(Fields.Vec(f, "StoreScale", new float[] { 0.5f, 0.5f, 0.5f })),
                V(Fields.Vec(scaled, "storePosition", null)), V(Fields.Vec(scaled, "storeEuler", null)),
                V(Fields.Vec(normal, "storePosition", null)), V(Fields.Vec(normal, "storeEuler", null)),
                custom, crafted);
            bool visual = Fields.Bool(f, "isOverridingDefaultVisualScale", false);
            item.isStackable = Fields.Bool(f, "isStackable", false);
            item.isStackableWhenCrafted = Fields.Bool(f, "isStackableWhenCrafted", false);
            item.size = Fields.Int(f, "size", 0);
            item.dockedStackSize = Fields.Int(f, "dockedStackSize", 30);
            item.weight = Fields.Float(f, "weight", 1f);
            item.isUsingShrunkenVisual = Fields.Bool(f, "isUsingShrunkenVisual", true);
            item.isOverridingDefaultShrunkenScale = visual;
            item.overrideShrunkenScale = Fields.Float(f, "overrideShrunkenScale", 1f);
            item.isOverridingDefaultVisualScale = visual;
            item.overrideScaleInsideVisual = Fields.Float(f, "overrideScaleInsideVisual", 1f);
            item.isSnappingToHand = Fields.Bool(f, "isSnappingToHand", false);
            item.isAssistGrabBlocked = Fields.Bool(f, "isAssistGrabBlocked", false);
            return item;
        }

        static List<Vector3[]> Spots(List<object> list)
        {
            List<Vector3[]> r = new List<Vector3[]>();
            if (list == null) return r;
            foreach (object o in list)
            {
                Dictionary<string, object> d = o as Dictionary<string, object>;
                r.Add(new Vector3[] { V(Fields.Vec(d, "storePosition", null)), V(Fields.Vec(d, "storeEuler", null)) });
            }
            return r;
        }

        internal static void Material(NetworkPrefab prefab, WorkshopMark mark)
        {
            int pm = Fields.Int(mark.Fields, "PhysicalMaterial", 0);
            if (pm == 0) return;
            PhysicalMaterial m = Find<PhysicalMaterial>(Names.Of(Names.PhysicalMaterial, pm), false);
            if (m == null) return;
            PhysicalMaterialPart part = Plat.Add<PhysicalMaterialPart>(prefab.gameObject);
            part.physicalMaterial = m;
            Attrs.Run(part);
        }
    }
}
