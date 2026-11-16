using UnityEngine;

namespace CWSkate
{
    // Mid-air the body getting slowed down isn't proof of a wall: at rocket speed it can scrape a sloped roof,
    // or bump an invisible collider box on a building. So before throwing you off, look what's actually ahead.
    internal partial class SkateController
    {
        private static readonly Renderer[] noRenderers = new Renderer[0];

        // 0 = nothing there (the body just lagged behind), 1 = not a crash (a slope to land on, a glancing touch,
        // an invisible collider), 2 = a real wall
        private int AirWallAhead(Rigidbody hip, out RaycastHit hit)
        {
            hit = default;
            Vector3 dir = new Vector3(airVel.x, 0f, airVel.z);
            if (dir.sqrMagnitude < 0.25f) return 0;
            dir.Normalize();
            if (crashMask == -1)
                crashMask = LayerMask.GetMask("Terrain", "Prop", "Default", "Player", "Interactable");

            // start a bit behind, so something we're already touching is still found
            Vector3 back = -dir * 0.4f;
            Vector3 p1 = hip.position + back - Vector3.up * 0.45f, p2 = hip.position + back + Vector3.up * 0.6f;
            float dist = 0.4f + 0.6f + airVel.magnitude * Time.fixedDeltaTime * 2f;
            int c = Physics.CapsuleCastNonAlloc(p1, p2, 0.25f, dir, castHits, dist, crashMask, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            for (int i = 0; i < c; i++)
            {
                var h = castHits[i];
                if (h.collider == null || h.distance <= 0f || h.distance >= best) continue;
                if (board != null && h.collider.transform.IsChildOf(board.transform)) continue;
                if (h.collider.GetComponentInParent<SkateboardItemBehaviour>() != null) continue;
                var hp = h.collider.GetComponentInParent<Player>();
                if (hp == player) continue;
                var rb = h.rigidbody;
                if (rb != null && !rb.isKinematic && rb.mass < 10f && hp == null) continue; // small loose stuff
                best = h.distance;
                hit = h;
            }
            if (best == float.MaxValue) return 0;
            if (hit.normal.y > 0.55f) return 1;                  // a roof slope / a ramp: you land on it, not crash
            if (Vector3.Dot(hit.normal, -dir) < 0.3f) return 1;  // just scraping along the side
            if (Invisible(hit)) return 1;                        // an invisible box: it stops you, but no crash
            return 2;
        }

        // Is there anything you can see where we hit? (level builders put plain collider boxes on buildings;
        // a crash into something you can't see feels like a bug)
        private static bool Invisible(RaycastHit h)
        {
            foreach (var r in VisibleAround(h.collider))
                if (r != null && r.enabled && r.bounds.SqrDistance(h.point) < 0.6f * 0.6f) return false;
            return true;
        }

        // The models near a collider (up to 3 parents up). For still scenery the list is kept:
        // a whole building can have hundreds of parts, and this is asked every physics step next to a wall.
        private static readonly System.Collections.Generic.Dictionary<int, Renderer[]> aroundCache = new System.Collections.Generic.Dictionary<int, Renderer[]>();
        private static readonly System.Collections.Generic.List<Renderer> gather = new System.Collections.Generic.List<Renderer>();
        private static readonly System.Collections.Generic.HashSet<Renderer> gatherSeen = new System.Collections.Generic.HashSet<Renderer>();

        private static Renderer[] VisibleAround(Collider c)
        {
            bool still = c.attachedRigidbody == null;
            int id = c.GetInstanceID();
            if (still && aroundCache.TryGetValue(id, out var known)) return known;
            gather.Clear();
            gatherSeen.Clear();
            Transform t = c.transform;
            for (int up = 0; up < 3 && t != null; up++, t = t.parent)
            {
                Renderer[] rs;
                try { rs = t.GetComponentsInChildren<Renderer>(false); } catch { rs = noRenderers; }
                foreach (var r in rs)
                {
                    if (r == null || r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
                    if (gatherSeen.Add(r)) gather.Add(r);
                }
            }
            var list = gather.ToArray();
            if (still)
            {
                if (aroundCache.Count > 2000) aroundCache.Clear();
                aroundCache[id] = list;
            }
            return list;
        }

        // what we crashed into goes to the log (BepInEx\LogOutput.log), to find odd colliders on the maps
        private static void LogCrash(string kind, RaycastHit h)
        {
            if (h.collider == null) { SkatePlugin.Log.LogInfo("Crash (" + kind + "): nothing found ahead"); return; }
            string path = h.collider.name;
            for (Transform t = h.collider.transform.parent; t != null; t = t.parent) path = t.name + "/" + path;
            SkatePlugin.Log.LogInfo("Crash (" + kind + ") into " + path + " [" + h.collider.GetType().Name + ", layer "
                                    + LayerMask.LayerToName(h.collider.gameObject.layer) + ", normal " + h.normal.ToString("0.00")
                                    + (Invisible(h) ? ", INVISIBLE" : "") + "]");
        }
    }
}
