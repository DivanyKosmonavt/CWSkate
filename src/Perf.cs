using System.Collections.Generic;
using UnityEngine;

namespace CWSkate
{
    // Things that make the mod lighter for older computers.
    internal static class Perf
    {
        // the "Low-end PC mode" setting: fewer and lighter effects (see where it's used)
        public static bool Low => SkatePlugin.PerformanceMode != null && SkatePlugin.PerformanceMode.Value;

        // how many particles an effect gets in that mode
        public static float ParticleMul => Low ? 0.35f : 1f;
    }

    // What a collider belongs to (a player, a trampoline, a bed...), looked up through its parents.
    // The physics checks ask this for the same floor and walls many times a second, so for still scenery
    // (no rigidbody: it never moves to another parent) the answer is kept. Anything with a rigidbody
    // (players, monsters, items lying around) is looked up every time, since it can be picked up or carried.
    internal static class Owner<T> where T : Component
    {
        private static readonly Dictionary<int, T> known = new Dictionary<int, T>();

        public static T Of(Collider c)
        {
            if (c == null) return null;
            if (c.attachedRigidbody != null) return c.GetComponentInParent<T>();
            int id = c.GetInstanceID();
            if (known.TryGetValue(id, out var t)) return t;
            t = c.GetComponentInParent<T>();
            if (known.Count > 4000) known.Clear(); // (a new level: old ids are gone anyway)
            known[id] = t;
            return t;
        }
    }
}
