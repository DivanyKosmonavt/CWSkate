using System.Collections.Generic;
using UnityEngine;

namespace CWSkate
{
    // Dancing on the board (the game's own emote key while riding) and getting away from monsters on it.
    internal partial class SkateController
    {
        // ---------------------------------------------------------------- dance

        private float danceTime, danceBlend, danceTick;

        public bool Dancing => danceTime > 0f;

        private void TickDance(float dt, bool dancing)
        {
            danceBlend = Mathf.MoveTowards(danceBlend, dancing && !airborne ? 1f : 0f, dt * 5f);
            if (dancing)
            {
                danceTime += dt;
                if (combo.Active) combo.KeepAlive(); // the dance joins the combo you're in
                danceTick -= dt;
                if (danceTick <= 0f)
                {
                    danceTick = 0.5f;
                    // dancing while rolling fast is worth more on video
                    SkateActivity.Local(SkateAct.BoardDance, combo.Count, "Board Dance", Mathf.Clamp(1f + speed * 0.05f, 1f, 1.8f));
                }
                return;
            }
            if (danceTime > 0f)
            {
                // the emote is over: it counts as a trick (and cashes in as aura with the combo)
                if (!bailed && danceTime > 0.6f)
                {
                    combo.Add("Board Dance", 120f + danceTime * 80f);
                    Progress.AddStat("dances", 1, save: false);
                }
                danceTime = 0f;
                danceTick = 0f;
            }
        }

        // ---------------------------------------------------------------- escape

        private float escapeCheck, escapeTime, escapeClosest = 999f, escapeLostFor, chaserDistance, lastChaseSample;
        private Player chaser;
        private readonly Dictionary<Bot, Vector3> botLastPos = new Dictionary<Bot, Vector3>();
        private readonly Dictionary<Bot, float> botRunAt = new Dictionary<Bot, float>();
        private const float ChaseRunSpeed = 1.5f; // m/s towards you = it's coming for you

        public bool Escaping => escapeTime > 0f && chaser != null;
        public Player Chaser => Escaping ? chaser : null;
        public float ChaserDistance => chaserDistance;
        public bool Chased => !bailed && chaserDistance > 0f && chaserDistance < 25f; // a monster is after you right now
        public float EscapeTime => escapeTime;

        private void ResetDanceAndEscape()
        {
            danceTime = danceTick = 0f;
            escapeTime = escapeLostFor = 0f;
            escapeClosest = 999f;
            chaser = null;
        }

        // A monster that's after you (the game tells everyone who each monster is hunting), and you ride away from it.
        private void TickEscape(float dt)
        {
            escapeCheck -= dt;
            if (escapeCheck > 0f) return;
            const float step = 0.25f;
            escapeCheck = step;

            if (bailed)
            {
                // it got you, or you crashed: the chase is over
                escapeTime = escapeLostFor = 0f;
                escapeClosest = 999f;
                chaser = null;
                return;
            }

            Vector3 me = Hip().position;
            Player best = null;
            float bestD = float.MaxValue;
            float now = Time.time, elapsed = Mathf.Max(0.05f, now - lastChaseSample);
            lastChaseSample = now;
            // (monsters aren't in the game's player list: they're found through its list of bots)
            foreach (var bot in Monsters.Bots)
            {
                if (bot == null) continue;
                Vector3 pos = bot.Center();
                bool hadPos = botLastPos.TryGetValue(bot, out Vector3 last);
                botLastPos[bot] = pos;
                if (bot.targetPlayer != player) continue;
                var m = Monsters.PlayerOf(bot);
                if (m == null || m.data == null || m.data.dead) continue;
                // Picking you as its target isn't a chase yet (the eye guy just stares first):
                // it has to actually run at you. Once it has, short stops still count.
                Vector3 toMe = me - pos;
                if (hadPos && toMe.sqrMagnitude > 0.01f && Vector3.Dot((pos - last) / elapsed, toMe.normalized) > ChaseRunSpeed)
                    botRunAt[bot] = now;
                if (!botRunAt.TryGetValue(bot, out float ranAt) || now - ranAt > 1.5f) continue;
                float d = toMe.magnitude;
                if (d < bestD) { bestD = d; best = m; }
            }
            if (botLastPos.Count > 128) { botLastPos.Clear(); botRunAt.Clear(); }
            chaserDistance = best != null ? bestD : 0f;

            float spd = airborne ? new Vector3(airVel.x, 0f, airVel.z).magnitude : speed;
            bool chased = best != null && bestD < 25f;
            if (chased)
            {
                escapeLostFor = 0f;
                chaser = best;
                if (spd > 5f)
                {
                    if (escapeTime <= 0f && SkatePlugin.ShowChaseHUD.Value) SkateHUD.Popup(Lang.T("ОН ГОНИТСЯ ЗА ТОБОЙ - ГОНИ!", "IT'S CHASING YOU - RIDE!"), new Color(1f, 0.45f, 0.35f), 1.8f, small: true);
                    escapeTime += step;
                    escapeClosest = Mathf.Min(escapeClosest, bestD);
                    // faster and closer = better video
                    float power = Mathf.Clamp(1f + (spd - 5f) * 0.06f, 1f, 1.8f) * (bestD < 6f ? 1.5f : bestD < 12f ? 1.2f : 1f);
                    SkateActivity.Local(SkateAct.Escape, combo.Count, "", power);
                }
                return;
            }

            if (escapeTime <= 0f) return;
            // it's not after you any more (or it's far behind): give it a moment, then see if that was a getaway
            escapeLostFor += step;
            if (escapeLostFor < 1f) return;
            if (escapeTime >= 2f && escapeClosest < 12f)
            {
                int bonus = Mathf.RoundToInt(SkatePlugin.EscapeAura.Value * SkatePlugin.AuraMultiplier.Value);
                if (bonus > 0)
                {
                    Progress.Aura += bonus;
                    SkateHUD.Popup(Lang.T("ОТОРВАЛСЯ! +", "ESCAPED! +") + bonus.ToString("N0") + Lang.T(" АУРЫ", " AURA"), new Color(0.5f, 1f, 0.6f), 2.4f);
                }
                else SkateHUD.Popup(Lang.T("ОТОРВАЛСЯ!", "ESCAPED!"), new Color(0.5f, 1f, 0.6f), 2.4f);
                Play(SkateSounds.Chime, 0.9f);
                Progress.AddStat("escapes", 1, save: false);
                // the getaway itself is the big moment
                SkateActivity.Local(SkateAct.Escape, combo.Count, "Escaped", Mathf.Clamp(1.4f + escapeTime * 0.1f, 1.4f, 2.5f));
            }
            escapeTime = escapeLostFor = 0f;
            escapeClosest = 999f;
            chaser = null;
        }
    }

    // The game keeps monsters out of its player list; they live in the bot list.
    internal static class Monsters
    {
        private static readonly List<Bot> none = new List<Bot>();
        private static readonly Dictionary<Bot, Player> owner = new Dictionary<Bot, Player>();
        private static readonly Dictionary<Player, Bot> botOf = new Dictionary<Player, Bot>();

        public static List<Bot> Bots => BotHandler.instance != null && BotHandler.instance.bots != null ? BotHandler.instance.bots : none;

        public static Player PlayerOf(Bot bot)
        {
            if (bot == null) return null;
            if (owner.TryGetValue(bot, out var p) && p != null) return p;
            p = bot.GetComponentInParent<Player>();
            if (p == null) p = bot.transform.root.GetComponentInChildren<Player>();
            if (owner.Count > 128) owner.Clear();
            owner[bot] = p;
            return p;
        }

        public static Bot BotOf(Player monster)
        {
            if (monster == null) return null;
            if (botOf.TryGetValue(monster, out var b) && b != null) return b;
            b = monster.GetComponentInChildren<Bot>();
            if (b == null) b = monster.transform.root.GetComponentInChildren<Bot>();
            if (botOf.Count > 128) botOf.Clear();
            botOf[monster] = b;
            return b;
        }

        // nearest live monster to a point (float.MaxValue if none)
        public static float Nearest(Vector3 pos)
        {
            float best = float.MaxValue;
            foreach (var bot in Bots)
            {
                if (bot == null) continue;
                var m = PlayerOf(bot);
                if (m != null && m.data != null && m.data.dead) continue;
                float d = Vector3.Distance(bot.Center(), pos);
                if (d < best) best = d;
            }
            return best;
        }
    }
}
