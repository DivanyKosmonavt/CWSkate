using System.Collections.Generic;
using UnityEngine;

namespace CWSkate
{
    // Hitting with the board. Players fall over (little or no damage), monsters get knocked back and fall
    // for a moment (they can't be killed this way) and give a bit of aura.
    internal static class Melee
    {
        private static readonly Collider[] hits = new Collider[128]; // (big monsters are made of lots of colliders)
        private static readonly HashSet<Player> struck = new HashSet<Player>();
        private static readonly Dictionary<int, float> auraCooldown = new Dictionary<int, float>();
        private const float MonsterAuraCooldown = 5f;

        public static void Strike(Player me)
        {
            if (me == null || me.data.dead) return;
            var cam = MainCamera.instance != null ? MainCamera.instance.transform : null;
            Vector3 eye = cam != null ? cam.position : me.Center();
            Vector3 look = me.data.lookDirection.normalized;
            float range = SkatePlugin.SwingRange.Value;

            // Everything around you, checked by the nearest point of each body part, not by the body's centre:
            // a big monster's centre can be far away while its leg is right in your face.
            struck.Clear();
            int n = Physics.OverlapSphereNonAlloc(eye, range + 0.4f, hits, ~(1 << 29), QueryTriggerInteraction.Ignore);
            bool any = false;
            Vector3 hitPoint = eye + look * range * 0.6f;
            int wallMask = HelperFunctions.GetMask(HelperFunctions.LayerType.TerrainProp);
            for (int i = 0; i < n; i++)
            {
                var c = hits[i];
                if (c == null) continue;
                var p = c.GetComponentInParent<Player>();
                if (p == null)
                {
                    // rocket fuel / a rocket lying on the floor: kaboom
                    var pickup = c.GetComponentInParent<Pickup>();
                    if (pickup != null && Vector3.Dot((c.bounds.center - eye).normalized, look) > 0.2f && Boom.TryExplodePickup(pickup))
                        hitPoint = Nearest(c, eye);
                    continue;
                }
                if (p == me || p.data.dead || struck.Contains(p)) continue;
                if (!InReach(c, eye, look, range, out Vector3 at)) continue;
                // not through walls (the monster's own colliders don't count as a wall)
                Vector3 to = at - eye;
                if (to.magnitude > 0.1f && Physics.Linecast(eye, at - to.normalized * 0.05f, out RaycastHit wall, wallMask, QueryTriggerInteraction.Ignore)
                    && wall.collider.GetComponentInParent<Player>() != p) continue;
                if (!p.ai && !SkatePlugin.HitPlayers.Value) continue;
                struck.Add(p);
                Hit(me, p, look);
                hitPoint = at;
                any = true;
            }

            if (any)
            {
                AudioSource.PlayClipAtPoint(SkateSounds.Hit, hitPoint, SkatePlugin.Volume.Value);
                try { GamefeelHandler.instance.perlin.AddShake(4f, 0.25f); } catch { }
                // smacking things wears the board out too; the last hit snaps it in your hands
                BoardWear.Damage(2.5f);
                if (BoardWear.LocalBroken)
                {
                    var hand = me.refs.ragdoll.GetBodypart(BodypartType.Hand_R);
                    Vector3 at = hand != null ? hand.transform.position : eye + look * 0.6f;
                    BoardWear.Snap(me, at, Quaternion.LookRotation(look, Vector3.up), look * 3f, 1f);
                }
            }
        }

        // Does the swing reach this body part? Yes if it's in the crosshair (a fat line along your look),
        // or close and roughly in front of you. `at` = where the board lands on it.
        private static bool InReach(Collider c, Vector3 eye, Vector3 look, float range, out Vector3 at)
        {
            // along the crosshair line, every 25 cm: is the body part within half a metre of it?
            for (float t = 0.25f; t <= range + 0.01f; t += 0.25f)
            {
                Vector3 onLine = eye + look * t;
                at = Nearest(c, onLine);
                if ((at - onLine).sqrMagnitude < 0.5f * 0.5f) return true;
            }
            // off to the side: close enough and inside a wide cone in front
            at = Nearest(c, eye);
            Vector3 to = at - eye;
            float d = to.magnitude;
            if (d < 0.05f) return true; // we're basically touching it
            return d <= range && Vector3.Dot(to / d, look) >= 0.35f;
        }

        // Nearest point of a collider (concave mesh colliders can't do that, so their box is used).
        private static Vector3 Nearest(Collider c, Vector3 from)
        {
            if (c is MeshCollider mc && !mc.convex) return c.ClosestPointOnBounds(from);
            return c.ClosestPoint(from);
        }

        private static void Hit(Player me, Player target, Vector3 look)
        {
            Vector3 dir = Vector3.ProjectOnPlane(look, Vector3.up).normalized;
            Vector3 force = (dir + Vector3.up * 0.35f).normalized * SkatePlugin.KnockForce.Value;
            float fall = SkatePlugin.KnockdownTime.Value;
            if (target.ai)
            {
                // At max aura (AURA GOD) the monster gets launched far away, and you celebrate with a dance.
                bool god = SkatePlugin.AuraGodSmack.Value && Aura.IsMaxRank(Progress.Aura);
                if (god)
                {
                    float k = Mathf.Max(1f, SkatePlugin.KnockForce.Value);
                    Vector3 launch = dir * Mathf.Max(28f, k * 8f) + Vector3.up * Mathf.Max(14f, k * 4f);
                    // it lies there helpless for the whole dance
                    target.CallTakeDamageAndAddForceAndFall(0f, launch, Mathf.Max(fall, MockTime));
                    Mockery.Start(target);
                    if (target.refs.view != null) NetSync.SendMock(target.refs.view.ViewID);
                    danceTarget = target;
                    godVictim = target; // if it kills you later, you're humbled
                    godVictimAt = Time.time;
                    SkateActivity.Local(SkateAct.GodSmack);
                    SkateHUD.Popup(Lang.T("УДАР БОГА АУРЫ!!!", "AURA GOD SMACK!!!"), Color.HSVToRGB(Time.time * 0.6f % 1f, 0.6f, 1f), 3f);
                    try { GamefeelHandler.instance.perlin.AddShake(10f, 0.4f); } catch { }
                    danceAt = Time.time + 0.45f; // right after the swing
                }
                else
                {
                    // monsters: knocked back harder, no damage (they can't be killed with a board)
                    target.CallTakeDamageAndAddForceAndFall(0f, force * 1.4f, fall);
                    SkateActivity.Local(SkateAct.SmackMonster);
                }
                Progress.AddStat("smacks", 1);
                int aura = Mathf.RoundToInt(SkatePlugin.MonsterHitAura.Value * SkatePlugin.AuraMultiplier.Value);
                int id = target.refs.view != null ? target.refs.view.ViewID : target.GetInstanceID();
                if (aura > 0 && (!auraCooldown.TryGetValue(id, out float until) || Time.time >= until))
                {
                    auraCooldown[id] = Time.time + MonsterAuraCooldown;
                    Progress.Aura += aura;
                    SkateHUD.Popup(Lang.T("МОНСТР ПОЛУЧИЛ! +", "MONSTER SMACKED! +") + aura + Lang.T(" АУРЫ", " AURA"), new Color(1f, 0.55f, 0.9f), 2f, small: true);
                }
            }
            else
            {
                target.CallTakeDamageAndAddForceAndFall(SkatePlugin.PlayerHitDamage.Value, force, fall);
                SkateActivity.Local(SkateAct.SmackPlayer);
            }
            SkatePlugin.Log.LogInfo("Board hit: " + (target.ai ? "monster " : "player ") + target.name);
        }

        // ------------------------------------------------------------ victory dance (one of the game's own dance emotes)

        public const float MockTime = 6f;
        private static float danceAt = -1f;
        private static Player danceTarget;
        private static Item danceEmote;
        private static bool danceSearched;

        // ------------------------------------------------------------ humbled: the monster you god-smacked killed you

        private const float HumbleWindow = 180f;  // how long the monster holds a grudge (s)
        private const float KillerRange = 7f;     // it was right there when you died
        private static Player godVictim;
        private static float godVictimAt;
        private static bool wasDead;

        private static void TickHumbled()
        {
            var me = Player.localPlayer;
            if (me == null || me.data == null) { wasDead = false; return; }
            bool dead = me.data.dead;
            if (dead == wasDead) return;
            wasDead = dead;
            if (!dead) return;

            // just died: was it the monster we smacked at AURA GOD?
            var victim = godVictim;
            godVictim = null;
            if (!SkatePlugin.HumbleOnDeath.Value || victim == null || victim.data == null || victim.data.dead) return;
            if (Time.time - godVictimAt > HumbleWindow) return;
            Vector3 body = me.Center();
            var bot = Monsters.BotOf(victim);
            float d = Vector3.Distance(bot != null ? bot.Center() : victim.Center(), body);
            bool huntingMe = bot != null && bot.targetPlayer == me;
            if (d > KillerRange && !(huntingMe && d < KillerRange * 2f)) return;

            long lost = Progress.Aura;
            if (lost <= 0) return;
            Progress.Aura = 0;
            Progress.AddStat("humbled", 1);
            SkateHUD.Popup(Lang.T("ОПУЩЕН", "HUMBLED"), new Color(0.75f, 0.75f, 0.8f), 4f);
            SkateHUD.Popup("-" + lost.ToString("N0") + Lang.T(" АУРЫ", " AURA"), new Color(1f, 0.4f, 0.35f), 4f, small: true);
            // the more aura it cost, the funnier the video
            SkateActivity.Local(SkateAct.Humbled, 0, "", Mathf.Clamp(1.5f + Mathf.Log10(lost + 1f) * 0.2f, 1.5f, 3f));
            SkatePlugin.Log.LogInfo("Humbled by " + victim.name + ": lost " + lost + " aura");
        }

        // Called every frame by the manager.
        public static void Tick()
        {
            TickHumbled();
            if (danceAt < 0f || Time.time < danceAt) return;
            danceAt = -1f;
            try
            {
                var me = Player.localPlayer;
                if (me == null || me.data.dead || me.Ragdoll()) return;
                // turn to face the monster: the dance is for it
                if (danceTarget != null)
                {
                    Vector3 to = danceTarget.Center() - me.Center();
                    to.y = 0f;
                    if (to.sqrMagnitude > 0.01f) me.data.playerLookValues.x = HelperFunctions.DirectionToLook(to.normalized).x;
                }
                danceTarget = null;
                var emote = FindDance();
                var emotes = me.GetComponent<PlayerEmotes>();
                if (emote == null || emotes == null) return;
                emotes.PlayEmote(emote); // the game plays it for everyone (and it counts as content on camera)
            }
            catch (System.Exception e) { SkatePlugin.Log.LogWarning("Victory dance failed: " + e.Message); }
        }

        // a dance emote from the game (prefers anything called "dance"; otherwise any emote)
        private static Item FindDance()
        {
            if (danceSearched) return danceEmote;
            danceSearched = true;
            var all = new List<Item>();
            foreach (var it in Zorro.Core.SingletonAsset<ItemDatabase>.Instance.Objects)
                if (it != null && it.emoteInfo != null && !string.IsNullOrEmpty(it.emoteInfo.animationName)) all.Add(it);
            var names = new List<string>();
            foreach (var it in all) names.Add(it.name + " (" + it.emoteInfo.animationName + ")");
            SkatePlugin.Log.LogInfo("Emotes in the game: " + string.Join(", ", names));
            foreach (var it in all)
            {
                string n = (it.name + " " + it.displayName + " " + it.emoteInfo.displayName + " " + it.emoteInfo.animationName).ToLowerInvariant();
                if (n.Contains("dance")) { danceEmote = it; break; }
            }
            if (danceEmote == null && all.Count > 0) danceEmote = all[0];
            SkatePlugin.Log.LogInfo("Victory dance: " + (danceEmote != null ? danceEmote.name : "none found"));
            return danceEmote;
        }
    }
}
