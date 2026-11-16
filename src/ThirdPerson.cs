using HarmonyLib;
using UnityEngine;
using UnityEngine.Rendering;

namespace CWSkate
{
    // F5: third-person camera behind the player (works on and off the board).
    internal static class ThirdPerson
    {
        public static bool Enabled;
        private const int LocalHiddenLayer = 29; // layer the game uses for "local player can't see this"

        private static Player appliedPlayer;
        private static Camera appliedCam;
        private static bool appliedState;
        private static float currentDist;
        private static int camMask = -1;

        public static void Toggle()
        {
            Enabled = !Enabled;
            currentDist = 0.3f;
        }

        // Called every frame by the manager: shows / hides the parts of your own body the game hides in first person.
        public static void Tick()
        {
            if (Input.GetKeyDown(SkatePlugin.ThirdPersonKey.Value) && GlobalInputHandler.CanTakeInput())
                Toggle();

            var p = Player.localPlayer;
            var cam = MainCamera.instance != null ? MainCamera.instance.Cam : null;
            bool want = Enabled && Active(p);
            if (p == appliedPlayer && cam == appliedCam && want == appliedState) return;

            if (cam != null)
            {
                if (want) cam.cullingMask |= 1 << LocalHiddenLayer;
                else cam.cullingMask &= ~(1 << LocalHiddenLayer);
            }
            if (p != null)
            {
                foreach (var t in p.GetComponentsInChildren<ObjectToggler>(true))
                {
                    if (t.hideWhen != ObjectToggler.HideWhen.Local) continue;
                    if (t.hideType == ObjectToggler.HideType.DisableRenderers)
                    {
                        foreach (var r in t.GetComponentsInChildren<Renderer>(true)) r.enabled = want;
                    }
                    else if (t.hideType == ObjectToggler.HideType.SwitchToShadowOnly)
                    {
                        foreach (var r in t.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                            r.shadowCastingMode = want ? ShadowCastingMode.On : ShadowCastingMode.ShadowsOnly;
                    }
                }
            }
            appliedPlayer = p;
            appliedCam = cam;
            appliedState = want;
        }

        private static bool Active(Player p)
        {
            if (p == null || p.data == null) return false;
            if (p.data.dead) return false;
            if (p.HasLockedInput()) return false; // terminals need the normal view
            return true;
        }

        public static void PlaceCamera(Transform camT)
        {
            var p = Player.localPlayer;
            if (!Enabled || !Active(p)) return;
            if (camMask == -1) camMask = LayerMask.GetMask("Terrain", "Prop", "Default");

            firstPersonPos = camT.position;
            hasFirstPersonPos = true;
            Vector3 pivot = camT.position + Vector3.up * 0.15f;
            Vector3 dir = (-camT.forward * SkatePlugin.ThirdPersonDistance.Value + Vector3.up * SkatePlugin.ThirdPersonHeight.Value);
            float want = dir.magnitude;
            dir /= want;
            float dist = want;
            int n = Physics.SphereCastNonAlloc(pivot, 0.2f, dir, camHits, want, camMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var h = camHits[i];
                if (h.collider == null || h.distance <= 0f || h.distance >= dist) continue;
                if (!BlocksCamera(h.collider, p)) continue;
                dist = Mathf.Max(0.2f, h.distance - 0.05f);
            }
            // zoom in instantly when blocked, ease back out
            currentDist = dist < currentDist ? dist : Mathf.MoveTowards(currentDist, dist, Time.deltaTime * 6f);
            camT.position = pivot + dir * currentDist;
        }

        private static readonly RaycastHit[] camHits = new RaycastHit[16];

        // Only solid world geometry pushes the camera in. Your own body and gear (the oxygen tank on your back),
        // other players, monsters and loose objects that move around don't.
        private static bool BlocksCamera(Collider c, Player self)
        {
            if (c.GetComponentInParent<Player>() != null) return false;
            var rb = c.attachedRigidbody;
            if (rb != null && !rb.isKinematic) return false;
            if (c.GetComponentInParent<BoardVisual>() != null || c.GetComponentInParent<SkateboardItemBehaviour>() != null) return false;
            // tiny things (cables, bottles, decorations)
            if (c.bounds.size.sqrMagnitude < 0.3f * 0.3f) return false;
            return true;
        }

        private static Vector3 firstPersonPos;
        private static bool hasFirstPersonPos;

        // Interacting (E) shoots a 3 m ray from the camera. In third person the camera is behind you,
        // so for that check we put it back at your head for a moment.
        [HarmonyPatch(typeof(PlayerInteraction), "Update")]
        private static class Patch_Interaction
        {
            private static void Prefix(out Vector3 __state)
            {
                __state = Vector3.zero;
                var mc = MainCamera.instance;
                if (mc == null || !Enabled || !hasFirstPersonPos || !Active(Player.localPlayer)) return;
                __state = mc.transform.position;
                mc.transform.position = firstPersonPos;
            }

            private static void Postfix(Vector3 __state)
            {
                var mc = MainCamera.instance;
                if (mc != null && __state != Vector3.zero) mc.transform.position = __state;
            }
        }

        [HarmonyPatch(typeof(MainCameraMovement), "LateUpdate")]
        private static class Patch_CameraMovement
        {
            private static void Postfix(MainCameraMovement __instance)
            {
                try { PlaceCamera(__instance.transform); }
                catch (System.Exception e) { SkatePlugin.Log.LogError(e); }
            }
        }
    }
}
