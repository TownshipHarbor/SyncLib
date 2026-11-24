using System;
using System.Collections.Generic;
using UnityEngine;
using Il2Cpp;
using Il2CppAlta.Crafting;
using Il2CppAlta.Impact;
using Il2CppAlta.Networking;

namespace SyncLib
{
    internal static class Attrs
    {
        internal static int Unknown;

        static List<Component> Hits(Component c)
        {
            List<Component> list = new List<Component>();
            foreach (Component x in c.GetComponentsInChildren(Il2CppInterop.Runtime.Il2CppType.Of<IToolHitDependant>(), true)) if (x != null) list.Add(x);
            return list;
        }

        static T Is<T>(Component c) where T : Component
        {
            return c == null ? null : c.TryCast<T>();
        }

        internal static void Run(Component c)
        {
            if (c == null) return;
            NetworkPrefab np = Is<NetworkPrefab>(c);
            if (np != null) { Prefab(np); return; }
            NetworkEntity ne = Is<NetworkEntity>(c);
            if (ne != null) { Entity(ne); return; }
            NetworkRigidbody nr = Is<NetworkRigidbody>(c);
            if (nr != null) { Rigidbody(nr); Behaviour(nr); return; }
            Pickup pk = Is<Pickup>(c);
            if (pk != null) { Pickup(pk); Behaviour(pk); return; }
            CraftingPart cp = Is<CraftingPart>(c);
            if (cp != null) { Crafting(cp); Behaviour(cp); return; }
            DurabilityModule dm = Is<DurabilityModule>(c);
            if (dm != null) { Behaviour(dm); return; }
            PhysicalMaterialPart pm = Is<PhysicalMaterialPart>(c);
            if (pm != null) { Material(pm); Behaviour(pm); return; }
            ImpactTool it = Is<ImpactTool>(c);
            if (it != null) { Tool(it); return; }
            Impactor im = Is<Impactor>(c);
            if (im != null) { Impactor(im); return; }
            if (Is<ImpactorMap>(c) != null) return;
            NetworkEntityBehaviour nb = Is<NetworkEntityBehaviour>(c);
            Unknown++;
            if (nb != null) Behaviour(nb);
        }

        static void Prefab(NetworkPrefab p)
        {
            NetworkEntity e = Plat.Get<NetworkEntity>(p);
            if (e != null) p.entity = e;
        }

        static void Entity(NetworkEntity e)
        {
            NetworkPrefab p = Plat.Get<NetworkPrefab>(e);
            List<MonoBehaviour> behaviours = new List<MonoBehaviour>();
            foreach (MonoBehaviour m in Plat.Here<MonoBehaviour>(e)) if (Plat.IsBehaviour(m)) behaviours.Add(m);
            Pickup pickup = Plat.Get<Pickup>(e);
            Interactable inter = Plat.Get<Interactable>(e);
            ImpactTool tool = Plat.Get<ImpactTool>(e);
            if (p != null) e.prefab = p;
            e.hasNetworkBehaviours = behaviours.Count > 0;
            e.networkEntityBehaviours = Plat.Array(behaviours);
            if (pickup != null) e.commonPickup = pickup;
            if (inter != null) e.commonInteractable = inter;
            if (tool != null) e.commonImpactTool = tool;
        }

        static void Behaviour(NetworkEntityBehaviour b)
        {
            NetworkEntity e = Plat.Get<NetworkEntity>(b);
            if (e != null) b.entity = e;
        }

        static void Rigidbody(NetworkRigidbody r)
        {
            r.cachedTransform = r.transform;
        }

        static void Pickup(Pickup p)
        {
            List<Collider> colliders = Plat.Children<Collider>(p, false);
            NetworkRigidbody nr = Plat.Get<NetworkRigidbody>(p);
            CraftingPart cp = Plat.Get<CraftingPart>(p);
            Rigidbody rb = Plat.Get<Rigidbody>(p);
            DurabilityModule dm = Plat.Get<DurabilityModule>(p);
            List<MonoBehaviour> interfere = new List<MonoBehaviour>();
            foreach (MonoBehaviour m in Plat.Children<MonoBehaviour>(p, false)) if (Plat.InterferesWithDocking(m)) interfere.Add(m);
            p.childrenColliders = Plat.Array(colliders);
            if (nr != null) p.networkRigidbody = nr;
            if (cp != null) p.craftingPart = cp;
            if (rb != null) p.rigidbody = rb;
            if (dm != null) p.durabilityModule = dm;
            p.interfereWithDockingObjects = Plat.Array(interfere);
        }

        static void Crafting(CraftingPart c)
        {
            NetworkRigidbody nr = Plat.Get<NetworkRigidbody>(c);
            DurabilityModule dm = Plat.Get<DurabilityModule>(c);
            ImpactTool it = Plat.Get<ImpactTool>(c);
            Pickup pk = Plat.Get<Pickup>(c);
            List<JointSlot> slots = Plat.Children<JointSlot>(c, true);
            JointInsert ins = Plat.Child<JointInsert>(c, true);
            List<ImpactorMap> maps = Plat.Children<ImpactorMap>(c, false);
            List<Component> hits = Hits(c);
            if (nr != null) c.networkRigidbody = nr;
            if (dm != null) c.durabilityModule = dm;
            if (it != null) c.impactTool = it;
            if (pk != null) c.pickup = pk;
            c.slots = Plat.Array(slots);
            if (ins != null) c.insert = ins;
            c.impactorMaps = Plat.Array(maps);
            c.hitDependants = Plat.Array(hits);
        }

        static void Material(PhysicalMaterialPart m)
        {
            List<ImpactGenerator> gens = Plat.Children<ImpactGenerator>(m, false);
            DurabilityModule dm = Plat.Up<DurabilityModule>(m);
            m.impactors = Plat.Array(gens);
            if (dm != null) m.durabilityModule = dm;
        }

        static void Tool(ImpactTool t)
        {
            NetworkEntity e = Plat.Get<NetworkEntity>(t);
            Pickup pk = Plat.Up<Pickup>(t);
            Rigidbody rb = Plat.Get<Rigidbody>(t);
            List<ImpactorMap> maps = Plat.Children<ImpactorMap>(t, true);
            List<Component> hits = Hits(t);
            if (e != null) t.entity = e;
            if (pk != null) t.pickup = pk;
            if (rb != null) t.rigidbody = rb;
            t.maps = Plat.Array(maps);
            t.hitDependants = Plat.Array(hits);
            t.cachedTransform = t.transform;
            t.hasPickup = t.pickup != null;
        }

        static void Impactor(Impactor i)
        {
            ImpactTool tool = Plat.Up<ImpactTool>(i);
            Pickup pk = Plat.Up<Pickup>(i);
            if (tool != null) i.impactTool = tool;
            if (pk != null) i.pickup = pk;
            i.cachedTransform = i.transform;
        }

    }
}
