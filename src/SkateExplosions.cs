using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;
using Zorro.Core;

namespace CWSkate
{
    // Kaboom. Rocket fuel and rocket boosters lying on the floor blow up when you smack them with the board or when
    // they hit something hard (ExplosiveFuel); the rocket on your board can blow up when you crash while it burns
    // (RocketCanExplode). An explosion throws and hurts whoever is close (the closer, the worse), knocks monsters
    // around and sets off other fuel lying nearby. Everyone sees and hears it.
    internal static class Boom
    {
        public const float Radius = 6f;       // for power 1 (a fuel canister)
        public const float MaxDamage = 55f;   // right in the middle
        public const float Push = 14f;

        private static readonly HashSet<int> blown = new HashSet<int>(); // pickups already gone up
        private struct Pending { public float at; public Pickup pickup; }
        private static readonly List<Pending> chain = new List<Pending>();

        // fuel = 1, a rocket booster = a bit more
        public static bool IsExplosive(Item it, out float power)
        {
            power = 0f;
            var d = SkateParts.DefOf(it);
            if (d == null) return false;
            if (d.Kind == PartKind.Fuel) { power = 1f; return true; }
            if (d.Kind == PartKind.Rocket) { power = 1.3f; return true; }
            return false;
        }

        // A fuel canister / a rocket lying on the floor: it's gone and it goes off. True if it was one.
        public static bool TryExplodePickup(Pickup p)
        {
            if (!SkatePlugin.ExplosiveFuel.Value || p == null || p.itemInstance == null) return false;
            if (!IsExplosive(p.itemInstance.item, out float power)) return false;
            int id = p.m_photonView != null ? p.m_photonView.ViewID : p.GetInstanceID();
            if (!blown.Add(id)) return true;
            if (blown.Count > 500) blown.Clear();
            Vector3 at = p.itemInstance.transform.position;
            try { if (p.m_photonView != null) p.m_photonView.RPC("RPC_Remove", RpcTarget.MasterClient); } // (only the host can remove pickups)
            catch (System.Exception e) { SkatePlugin.Log.LogWarning("Boom: " + e.Message); }
            Explode(at, power);
            return true;
        }

        // here and on everyone else's screen
        public static void Explode(Vector3 at, float power)
        {
            SkatePlugin.Log.LogInfo("Boom at " + at + " (power " + power.ToString("0.0") + ")");
            Effects(at, power);
            NetSync.SendBoom(at, power);
        }

        // what an explosion does on this computer (also for one that happened on someone else's)
        public static void Effects(Vector3 at, float power)
        {
            SkateFx.Explosion(at, power);
            AudioSource.PlayClipAtPoint(SkateSounds.Boom, at, Mathf.Clamp01(SkatePlugin.Volume.Value * 1.3f));
            float r = Radius * power;

            // us: thrown and hurt (each player does this for themselves)
            var me = Player.localPlayer;
            if (me != null && me.data != null && !me.data.dead)
            {
                Vector3 c = me.Center();
                float dist = Vector3.Distance(c, at);
                if (dist < r * 3f)
                {
                    try { GamefeelHandler.instance.perlin.AddShake(Mathf.Lerp(20f, 3f, dist / (r * 3f)), 0.6f); } catch { }
                    SkateActivity.Local(SkateAct.Kaboom, 0, "", dist < r ? 1.5f + power : 1f); // an explosion on camera
                }
                if (dist < r)
                {
                    float k = 1f - dist / r;
                    me.CallTakeDamageAndAddForceAndFall(MaxDamage * power * k, Away(c, at) * (Push * power * k) + Vector3.up * (2f + 7f * k) * power, 1.2f + k);
                }
            }

            // the host does the monsters and the chain reaction (it owns them and the things lying around)
            if (!PhotonNetwork.IsMasterClient) return;
            foreach (var bot in Monsters.Bots)
            {
                if (bot == null) continue;
                var m = Monsters.PlayerOf(bot);
                if (m == null || m.data == null || m.data.dead) continue;
                Vector3 c = bot.Center();
                float dist = Vector3.Distance(c, at);
                if (dist > r) continue;
                float k = 1f - dist / r;
                m.CallTakeDamageAndAddForceAndFall(0f, Away(c, at) * (Push * 1.5f * power * k) + Vector3.up * (8f * k * power), 2f + k);
            }
            if (!SkatePlugin.ExplosiveFuel.Value) return;
            var handler = Singleton<PickupHandler>.Instance;
            if (handler == null || handler.m_pickup == null) return;
            foreach (var p in handler.m_pickup)
            {
                if (p == null || p.itemInstance == null || !IsExplosive(p.itemInstance.item, out _)) continue;
                float dist = Vector3.Distance(p.itemInstance.transform.position, at);
                if (dist < 0.3f || dist > r * 0.8f) continue;
                chain.Add(new Pending { at = Time.time + 0.15f + dist * 0.06f, pickup = p }); // the next one goes off a moment later
            }
        }

        private static Vector3 Away(Vector3 from, Vector3 at)
        {
            Vector3 d = from - at;
            d.y = 0f;
            return d.sqrMagnitude > 0.01f ? d.normalized : Vector3.forward;
        }

        // Called every frame by the manager: the chain reaction, one after another.
        public static void Tick()
        {
            for (int i = chain.Count - 1; i >= 0; i--)
            {
                if (Time.time < chain[i].at) continue;
                var p = chain[i].pickup;
                chain.RemoveAt(i);
                TryExplodePickup(p);
            }
        }
    }
}
