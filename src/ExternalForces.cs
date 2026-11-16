using HarmonyLib;
using UnityEngine;

namespace CWSkate
{
    // Collects everything that pushes the local rider (black holes, shots, punches, explosions, monsters grabbing you),
    // so the skate physics can react to it instead of quietly cancelling it out.
    internal static class ExternalForces
    {
        public static Vector3 Impulse; // m/s, instant kicks
        public static Vector3 Accel;   // m/s², continuous pulls (summed since the last physics step)
        public static float Damage;
        public static bool Self;       // true while our own code pushes the body

        private static bool fromTrampoline;

        private static bool IsRider(PlayerRagdoll rag)
        {
            var sc = SkateController.Local;
            return sc != null && sc.Riding && sc.player != null && sc.player.refs.ragdoll == rag;
        }

        private static void Add(Vector3 force, ForceMode mode, float scale)
        {
            if (Self || fromTrampoline) return;
            switch (mode)
            {
                case ForceMode.VelocityChange: Impulse += force * scale; break;
                case ForceMode.Acceleration: Accel += force * scale; break;
                case ForceMode.Impulse: Impulse += force * (scale / 60f); break; // the body weighs about 60
                case ForceMode.Force: Accel += force * (scale / 60f); break;
            }
        }

        public static void Clear()
        {
            Impulse = Accel = Vector3.zero;
            Damage = 0f;
        }

        [HarmonyPatch(typeof(PlayerRagdoll), nameof(PlayerRagdoll.AddForce), new[] { typeof(Vector3), typeof(ForceMode) })]
        private static class Patch_AddForce
        {
            private static void Prefix(PlayerRagdoll __instance, Vector3 force, ForceMode forceMode)
            {
                if (IsRider(__instance)) Add(force, forceMode, 1f);
            }
        }

        [HarmonyPatch(typeof(PlayerRagdoll), nameof(PlayerRagdoll.AddForce), new[] { typeof(Vector3), typeof(ForceMode), typeof(Vector3), typeof(float) })]
        private static class Patch_AddForceFalloff
        {
            private static void Prefix(PlayerRagdoll __instance, Vector3 force, ForceMode forceMode)
            {
                if (IsRider(__instance)) Add(force, forceMode, 0.6f);
            }
        }

        // shots hit single body parts
        [HarmonyPatch(typeof(Player), nameof(Player.RPCA_AddForceToBodyParts))]
        private static class Patch_BodyParts
        {
            private static void Prefix(Player __instance, Vector3[] vector3s)
            {
                if (vector3s == null || !IsRider(__instance.refs.ragdoll)) return;
                Vector3 biggest = Vector3.zero;
                foreach (var v in vector3s) if (v.sqrMagnitude > biggest.sqrMagnitude) biggest = v;
                Add(biggest, ForceMode.VelocityChange, 0.4f);
            }
        }

        [HarmonyPatch(typeof(Player), "TakeDamage")]
        private static class Patch_Damage
        {
            private static void Prefix(Player __instance, float damage)
            {
                // (damage that doesn't happen - insurance invulnerability, the developer's NoDamage - doesn't knock you off either)
                if (SponsorItems.Invulnerable) return;
                if (!fromTrampoline && IsRider(__instance.refs.ragdoll)) Damage += damage;
            }
        }

        // The player's own controller pushes the body too (the "stand up" force while on the ground,
        // strongest right after landing). That's not something hitting you, so ignore it.
        [HarmonyPatch(typeof(PlayerController), "FixedUpdate")]
        private static class Patch_OwnController
        {
            private static void Prefix(out bool __state)
            {
                __state = Self;
                Self = true;
            }

            private static void Finalizer(bool __state) => Self = __state;
        }

        // black holes pull on the hip directly
        [HarmonyPatch(typeof(BlackHole), "FixedUpdate")]
        private static class Patch_BlackHole
        {
            private static void Postfix(BlackHole __instance)
            {
                try
                {
                    var p = Player.localPlayer;
                    if (p == null || !IsRider(p.refs.ragdoll) || __instance.scale == null || __instance.inner == null) return;
                    float range = __instance.scale.localScale.x * 0.5f;
                    float innerRange = range * __instance.inner.localScale.x;
                    range = range * 1.2f + 20f;
                    Vector3 c = p.Center();
                    float dist = Vector3.Distance(__instance.transform.position, c);
                    if (dist > range) return;
                    float n = Mathf.InverseLerp(range, innerRange, dist);
                    Accel += (__instance.transform.position - c) * (__instance.force * n * n * 0.5f);
                }
                catch { }
            }
        }

        // The trampoline launches walking players by itself. On the board we do our own bouncing,
        // so it shouldn't grab the rider (that's what made it bounce you without jumping).
        [HarmonyPatch(typeof(Trampoline), nameof(Trampoline.CollideWithSurface))]
        private static class Patch_TrampolineCollide
        {
            private static bool Prefix(Bodypart part)
            {
                try
                {
                    var pl = part != null ? part.GetComponentInParent<Player>() : null;
                    return pl == null || !IsRider(pl.refs.ragdoll);
                }
                catch { return true; }
            }
        }

        [HarmonyPatch(typeof(Trampoline), "FixedUpdate")]
        private static class Patch_TrampolineFixed
        {
            private static void Prefix() => fromTrampoline = true;
            private static void Finalizer() => fromTrampoline = false;
        }
    }
}
