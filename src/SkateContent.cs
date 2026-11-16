using System.Collections.Generic;
using System.IO;
using System.Text;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Localization.Settings;
using Zorro.Core.Serizalization;

namespace CWSkate
{
    // What a skater just did (for the camera / video comments).
    internal enum SkateAct : byte
    {
        Cruise, Ollie, Flip, Grab, Spin, Grind, Manual, BigAir, Combo, Bail, MonsterBonk, SmackPlayer, SmackMonster,
        GodSmack, // max aura: the monster flies away and you dance (new kinds always go at the end)
        Wallride, Wallplant, DropIn, BoardThrow,
        BoardDance, // an emote while riding
        Escape,     // a monster is chasing you and you ride away ("Escaped" = you got away)
        HippieJump, // you jump, the board rolls on under something, you land back on it
        BoardSnap,  // the worn-out board broke in half
        WornBoard,  // riding a board that squeaks and creaks like it's about to go
        MonsterGrind, // riding on a monster's back
        Skitch,       // holding on to a running monster / player and getting towed
        Humbled,      // AURA GOD smacked a monster, then that monster killed them: all the aura is gone
        Train,        // a skate train: both hands on the shoulders of the skater in front
        RocketRide,   // the rocket booster is burning
        Insured,      // an insurance policy saved them from dying
        Kaboom        // rocket fuel / a rocket blew up next to them (the last one: NetSync checks the range)
    }

    // Remembers the recent skate moments of every player (yours directly, others' come over the network),
    // so a camera that films them can turn them into video content.
    internal static class SkateActivity
    {
        private struct Rec { public float time; public int combo; public string trick; public float power; }

        private static readonly Dictionary<long, Rec> recent = new Dictionary<long, Rec>();
        private const float Window = 2.2f; // how long a moment stays "on camera"

        private static long Key(int actor, SkateAct kind) => (long)actor * 64 + (byte)kind;

        public static void Record(int actor, SkateAct kind, int combo, string trick, float power = 1f)
        {
            recent[Key(actor, kind)] = new Rec { time = Time.time, combo = combo, trick = Ascii(trick), power = Mathf.Clamp(power, 1f, 5f) };
        }

        // something we did ourselves: remember it and tell the others.
        // power = how big it was (air time, height, duration...), 1 = normal
        public static void Local(SkateAct kind, int combo = 0, string trick = "", float power = 1f)
        {
            var lp = Player.localPlayer;
            if (lp == null || lp.refs.view == null || lp.refs.view.Owner == null) return;
            power *= TrickBonus(kind, trick);
            if (lp.data != null && !lp.data.dead && lp.data.health < 35f) power *= 1.3f; // skating on the edge of death
            power *= SituationBonus(kind);
            power = Mathf.Clamp(power, 1f, 5f);
            Record(lp.refs.view.Owner.ActorNumber, kind, combo, trick, power);
            NetSync.SendActivity(kind, combo, trick, power);
        }

        public static bool Recent(int actor, SkateAct kind, out int combo, out string trick, out float power)
        {
            combo = 0;
            trick = "";
            power = 1f;
            // a death stays longer: whoever films it may only get to the body a few seconds later
            float window = kind == SkateAct.Humbled ? 10f : Window;
            if (!recent.TryGetValue(Key(actor, kind), out var r) || Time.time - r.time > window) return false;
            combo = r.combo;
            trick = r.trick;
            power = r.power;
            return true;
        }

        private static bool IsTrick(SkateAct k)
        {
            switch (k)
            {
                case SkateAct.Ollie: case SkateAct.Flip: case SkateAct.Grab: case SkateAct.Spin: case SkateAct.Grind:
                case SkateAct.Manual: case SkateAct.BigAir: case SkateAct.Combo: case SkateAct.Wallride: case SkateAct.Wallplant:
                case SkateAct.DropIn: case SkateAct.BoardThrow: case SkateAct.BoardDance: case SkateAct.HippieJump:
                case SkateAct.MonsterGrind: case SkateAct.Skitch: case SkateAct.Train: case SkateAct.RocketRide:
                    return true;
            }
            return false;
        }

        // What's going on while you do it (on top of the trick itself):
        //  - a monster is chasing you: tricks x1.3, dancing x2 (dancing in front of death);
        //  - the board is nearly broken (under 30%): x1.2, every trick could be its last;
        //  - in the air right above a monster: x1.5.
        private static float SituationBonus(SkateAct kind)
        {
            var sc = SkateController.Local;
            if (sc == null || !sc.Riding || !IsTrick(kind)) return 1f;
            float b = 1f;
            if (sc.Escaping) b *= kind == SkateAct.BoardDance ? 2f : 1.3f;
            if (SkatePlugin.WearEnabled.Value && BoardWear.Local < 0.3f) b *= 1.2f;
            if (sc.Airborne && MonsterBelow(sc.HipPos)) b *= 1.5f;
            return b;
        }

        private static bool MonsterBelow(Vector3 hip)
        {
            foreach (var bot in Monsters.Bots)
            {
                if (bot == null) continue;
                var m = Monsters.PlayerOf(bot);
                if (m != null && m.data != null && m.data.dead) continue;
                Vector3 c = bot.Center();
                Vector3 d = c - hip;
                d.y = 0f;
                if (d.magnitude < 2.5f && c.y < hip.y - 0.3f && c.y > hip.y - 6f) return true;
            }
            return false;
        }

        // harder versions of a trick are worth more on video
        public static float TrickBonus(SkateAct kind, string trick)
        {
            if (string.IsNullOrEmpty(trick)) return 1f;
            float b = 1f;
            if (trick.Contains("Triple")) b *= 2.2f;
            else if (trick.Contains("Double")) b *= 1.5f;
            if (trick.Contains("Fakie")) b *= 1.15f;
            if (trick.Contains("Tre Flip") || trick.Contains("Impossible")) b *= 1.3f;
            if (trick.Contains("Boneless")) b *= 1.4f;
            if (trick.Contains("Nose Manual")) b *= 1.4f;
            if (trick.Contains("Boardslide")) b *= 1.3f;
            if (trick.Contains("Revert")) b *= 1.2f;
            if (trick.Contains("Melon")) b *= 1.1f;
            if (trick.Contains("Nosegrab") || trick.Contains("Tailgrab")) b *= 1.2f;
            if (trick.Contains("Stalefish") || trick.Contains("Crail")) b *= 1.25f;
            if (trick.Contains("Airwalk")) b *= 1.5f;
            if (trick.Contains("Superman")) b *= 1.7f;
            if (trick.Contains("Backflip") || trick.Contains("Frontflip")) b *= 1.8f; // head over heels on a board
            if (trick.Contains("Big Drop In")) b *= 1.3f;
            if (kind == SkateAct.Spin)
            {
                string digits = "";
                foreach (char c in trick) if (c >= '0' && c <= '9') digits += c;
                if (int.TryParse(digits, out int deg))
                {
                    if (trick.StartsWith("Kickturn")) { if (deg >= 360) b *= 1.3f; }
                    else b *= deg >= 900 ? 2.2f : deg >= 720 ? 1.7f : deg >= 540 ? 1.3f : 1f;
                }
            }
            return b;
        }

        // comments travel as ASCII text in this game
        private static string Ascii(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var sb = new StringBuilder(s.Length);
            foreach (char c in s) if (c >= 32 && c < 127 && c != '|') sb.Append(c);
            return sb.Length > 40 ? sb.ToString(0, 40) : sb.ToString();
        }
    }

    // One skate moment in a video. Gives views and a viewer comment.
    internal class SkateContentEvent : PlayerBaseEvent
    {
        public const ushort ID = 47123;
        public const string Prefix = "CWSKATE|";

        public SkateAct kind;
        public byte combo;
        public string trick = "";
        // bonus multiplier, only used while recording: the game stores the finished score in the video,
        // so this isn't saved (and old videos still load)
        public float bonus = 1f;

        // base score of each moment (the game's own: a player on camera 10, falling over 20, typical monster 55, Flicker 350)
        public static float BaseValue(SkateAct k)
        {
            switch (k)
            {
                case SkateAct.Cruise: return 15f;
                case SkateAct.Ollie: return 25f;
                case SkateAct.Flip: return 45f;
                case SkateAct.Grab: return 40f;
                case SkateAct.Spin: return 45f;
                case SkateAct.Grind: return 55f;
                case SkateAct.Manual: return 30f;
                case SkateAct.BigAir: return 65f;
                case SkateAct.Combo: return 50f;
                case SkateAct.Bail: return 50f;
                case SkateAct.MonsterBonk: return 130f;
                case SkateAct.SmackPlayer: return 60f;
                case SkateAct.SmackMonster: return 110f;
                case SkateAct.GodSmack: return 300f;
                case SkateAct.Wallride: return 70f;
                case SkateAct.Wallplant: return 60f;
                case SkateAct.DropIn: return 40f;
                case SkateAct.BoardThrow: return 45f;
                case SkateAct.BoardDance: return 60f;
                case SkateAct.Escape: return 90f;
                case SkateAct.HippieJump: return 60f;
                case SkateAct.BoardSnap: return 220f;
                case SkateAct.WornBoard: return 25f;
                case SkateAct.MonsterGrind: return 140f;
                case SkateAct.Skitch: return 70f;
                case SkateAct.Humbled: return 250f;
                case SkateAct.Train: return 80f;
                case SkateAct.RocketRide: return 90f;
                case SkateAct.Insured: return 200f;
                case SkateAct.Kaboom: return 160f;
            }
            return 10f;
        }

        // bigger combos are worth more: +20% per extra trick, up to x3
        public static float ComboFactor(int combo) => 1f + 0.2f * Mathf.Clamp(combo - 1, 0, 10);

        public SkateContentEvent() { }

        public SkateContentEvent(string playerName, int actorNumber, Vector3 worldPosition, SkateAct kind, int combo, string trick)
            : base(playerName, actorNumber, worldPosition)
        {
            this.kind = kind;
            this.combo = (byte)Mathf.Clamp(combo, 0, 255);
            this.trick = trick ?? "";
        }

        public override float GetContentValue() => BaseValue(kind) * ComboFactor(combo) * bonus * SkatePlugin.ContentMultiplier.Value;

        // What's around the skater: monsters close by, other skaters, filming yourself, a famous skater.
        public static float SceneBonus(Player p)
        {
            float b = 1f;
            Vector3 pos = p.HeadPosition();
            float nearest = Monsters.Nearest(pos); // monsters aren't in the player list
            int crew = 0;
            var ph = PlayerHandler.instance;
            if (ph != null && ph.players != null)
            {
                foreach (var o in ph.players)
                {
                    if (o == null || o == p || o.ai || o.data == null || o.data.dead) continue;
                    if (Vector3.Distance(o.HeadPosition(), pos) < 15f && Patch_PlayerContent.IsRiding(o)) crew++;
                }
            }
            if (nearest < 5f) b *= 2f;          // a monster right next to you
            else if (nearest < 12f) b *= 1.5f;  // a monster close by
            if (crew > 0) b *= 1f + 0.2f * Mathf.Min(crew, 3); // skating together
            if (p.IsLocal && p.data.currentItem != null && p.data.currentItem.GetComponent<VideoCamera>() != null)
                b *= 1.5f; // selfie: filming yourself on the board
            long aura = p.IsLocal ? Progress.Aura : NetSync.GetAura(p);
            if (aura > 0) b *= 1f + 0.05f * Mathf.Max(0, Aura.RankIndex(aura) - 1); // famous skater, up to x1.4
            return b;
        }

        public override ushort GetID() => ID;

        public override string GetName() => "Skate_" + kind;

        // different moments of the same player are separate (each can get its own comment)
        public override int GetUniqueID() => actorNumber * 64 + (byte)kind; // (room for 64 kinds of moments)

        public override string[] GetAllComments() => new string[0];

        public override Comment GenerateComment()
        {
            // only a code goes into the video; every viewer sees the text in their own language
            string extra = kind == SkateAct.Combo ? "x" + Mathf.Max(1, (int)combo) : trick;
            return new Comment(Prefix + SkateComments.Section(kind) + "|" + Random.Range(0, 10000) + "|" + extra, playerName);
        }

        public override void Serialize(BinarySerializer serializer)
        {
            base.Serialize(serializer);
            serializer.WriteByte((byte)kind);
            serializer.WriteByte(combo);
            serializer.WriteString(trick ?? "", Encoding.UTF8);
        }

        public override void Deserialize(BinaryDeserializer deserializer)
        {
            base.Deserialize(deserializer);
            kind = (SkateAct)deserializer.ReadByte();
            combo = deserializer.ReadByte();
            trick = deserializer.ReadString(Encoding.UTF8);
        }

        // ------------------------------------------------------------ patches

        // the game builds content events from their id when it reads a video
        [HarmonyPatch(typeof(ContentEventIDMapper), nameof(ContentEventIDMapper.GetContentEvent))]
        private static class Patch_IDMapper
        {
            private static bool Prefix(ushort id, ref ContentEvent __result)
            {
                if (id != ID) return true;
                __result = new SkateContentEvent();
                return false;
            }
        }

        // When a camera sees a player, add what they're doing on the board.
        [HarmonyPatch(typeof(PlayerContentProvider), nameof(PlayerContentProvider.GetContent))]
        private static class Patch_PlayerContent
        {
            private static readonly SkateAct[] Moments =
            {
                SkateAct.Ollie, SkateAct.Flip, SkateAct.Grab, SkateAct.Spin, SkateAct.Grind, SkateAct.Manual, SkateAct.BigAir,
                SkateAct.Combo, SkateAct.Bail, SkateAct.MonsterBonk, SkateAct.SmackPlayer, SkateAct.SmackMonster, SkateAct.GodSmack,
                SkateAct.Wallride, SkateAct.Wallplant, SkateAct.DropIn, SkateAct.BoardThrow,
                SkateAct.BoardDance, SkateAct.Escape, SkateAct.HippieJump, SkateAct.BoardSnap, SkateAct.WornBoard,
                SkateAct.MonsterGrind, SkateAct.Skitch, SkateAct.Humbled, SkateAct.Train, SkateAct.RocketRide, SkateAct.Insured, SkateAct.Kaboom
            };

            private static void Postfix(PlayerContentProvider __instance, List<ContentEventFrame> contentEvents, float seenAmount, float time)
            {
                try
                {
                    if (!SkatePlugin.SkateContent.Value) return;
                    var p = __instance.player;
                    if (p == null) return;
                    // a monster on camera that's chasing a skater: that's the chase (you filming it behind you from the board)
                    if (p.ai) { ChaseOnCamera(p, contentEvents, seenAmount, time); return; }
                    if (p.refs.view == null || p.refs.view.Owner == null) return;
                    string name = p.refs.view.Owner.NickName;
                    int actor = p.refs.view.Owner.ActorNumber;
                    Vector3 pos = p.HeadPosition();
                    bool bonuses = SkatePlugin.ContentBonuses.Value;
                    float scene = -1f; // worked out once, only if needed
                    bool any = false;
                    foreach (var k in Moments)
                    {
                        if (!SkateActivity.Recent(actor, k, out int combo, out string trick, out float power)) continue;
                        if (bonuses && scene < 0f) scene = SceneBonus(p);
                        var ev = new SkateContentEvent(name, actor, pos, k, combo, trick) { bonus = bonuses ? Mathf.Clamp(power * scene, 1f, 10f) : 1f };
                        contentEvents.Add(new ContentEventFrame(ev, seenAmount, time));
                        any = true;
                    }
                    if (!any && IsRiding(p))
                    {
                        var ev = new SkateContentEvent(name, actor, pos, SkateAct.Cruise, 0, "") { bonus = bonuses ? Mathf.Clamp(SceneBonus(p), 1f, 10f) : 1f };
                        contentEvents.Add(new ContentEventFrame(ev, seenAmount, time));
                    }
                }
                catch (System.Exception e) { SkatePlugin.Log.LogWarning("Skate content: " + e.Message); }
            }

            // (monsters belong to the host on the network, so the skater comes from who the monster is after)
            internal static void ChaseOnCamera(Player monster, List<ContentEventFrame> contentEvents, float seenAmount, float time)
            {
                var bot = Monsters.BotOf(monster);
                var target = bot != null ? bot.targetPlayer : null;
                if (target == null || target.ai || target.refs.view == null || target.refs.view.Owner == null) return;
                int actor = target.refs.view.Owner.ActorNumber;
                if (!SkateActivity.Recent(actor, SkateAct.Escape, out int combo, out string trick, out float power)) return;
                bool bonuses = SkatePlugin.ContentBonuses.Value;
                var ev = new SkateContentEvent(target.refs.view.Owner.NickName, actor, monster.HeadPosition(), SkateAct.Escape, combo, trick)
                {
                    bonus = bonuses ? Mathf.Clamp(power * SceneBonus(target), 1f, 10f) : 1f
                };
                contentEvents.Add(new ContentEventFrame(ev, seenAmount, time));
            }

            internal static bool IsRiding(Player p)
            {
                if (p.IsLocal) return SkateController.Local != null && SkateController.Local.Riding;
                return NetSync.GetState(p) == NetSync.StateRiding;
            }
        }

        // Monsters have their own camera content (one class per monster): when the camera sees a monster
        // that's chasing a skater, the chase goes into the video too.
        [HarmonyPatch]
        private static class Patch_MonsterContent
        {
            private static readonly Dictionary<ContentProvider, Player> owners = new Dictionary<ContentProvider, Player>();

            private static IEnumerable<System.Reflection.MethodBase> TargetMethods()
            {
                var seen = new HashSet<System.Reflection.MethodBase>();
                foreach (var t in AccessTools.GetTypesFromAssembly(typeof(MonsterContentProvider).Assembly))
                {
                    if (t == null || !typeof(MonsterContentProvider).IsAssignableFrom(t)) continue;
                    var m = AccessTools.DeclaredMethod(t, "GetContent");
                    if (m != null && !m.IsAbstract && seen.Add(m)) yield return m;
                }
            }

            // (arguments by position: each monster class may name them differently)
            private static void Postfix(ContentProvider __instance, List<ContentEventFrame> __0, float __1, float __3)
            {
                var contentEvents = __0;
                float seenAmount = __1, time = __3;
                try
                {
                    if (!SkatePlugin.SkateContent.Value || __instance == null || contentEvents == null) return;
                    if (!owners.TryGetValue(__instance, out var monster) || monster == null)
                    {
                        monster = __instance.GetComponentInParent<Player>();
                        if (monster == null) monster = __instance.transform.root.GetComponentInChildren<Player>();
                        if (owners.Count > 128) owners.Clear();
                        owners[__instance] = monster;
                    }
                    if (monster != null && monster.ai) Patch_PlayerContent.ChaseOnCamera(monster, contentEvents, seenAmount, time);
                }
                catch (System.Exception e) { SkatePlugin.Log.LogWarning("Chase content: " + e.Message); }
            }
        }

        // Our comments aren't in the game's translation tables: show our own text.
        [HarmonyPatch(typeof(CommentUI), "GetLocalizedText")]
        private static class Patch_CommentText
        {
            private static bool Prefix(Comment comment, ref string __result)
            {
                if (comment == null || comment.Text == null || !comment.Text.StartsWith(SkateContentEvent.Prefix)) return true;
                __result = SkateComments.Resolve(comment.Text, comment.Player);
                return false;
            }
        }
    }

    // Viewer comments about skating. Live in text files next to the mod (SkateComments_ru.txt / _en.txt),
    // so anyone can add their own lines. <playername> = the skater, <trick> = the trick (or the combo size).
    internal static class SkateComments
    {
        // per language: the parsed file and when it was last changed
        private static readonly Dictionary<Lng, Dictionary<string, List<string>>> pools = new Dictionary<Lng, Dictionary<string, List<string>>>();
        private static readonly Dictionary<Lng, System.DateTime> poolTimes = new Dictionary<Lng, System.DateTime>();
        private static readonly Lng[] AllLanguages = { Lng.Ru, Lng.En, Lng.Fr, Lng.Zh };

        public static string Section(SkateAct k) => k.ToString().ToLowerInvariant();

        private static string Folder => Path.GetDirectoryName(typeof(SkatePlugin).Assembly.Location) ?? BepInEx.Paths.PluginPath;
        private static string FileFor(Lng l) => Path.Combine(Folder, "SkateComments_" + l.ToString().ToLowerInvariant() + ".txt");

        private static string Defaults(Lng l)
        {
            switch (l) { case Lng.Ru: return DefaultRu; case Lng.Fr: return DefaultFr; case Lng.Zh: return DefaultZh; default: return DefaultEn; }
        }

        public static void Init()
        {
            try
            {
                foreach (Lng lang in AllLanguages)
                {
                    string path = FileFor(lang), defaults = Defaults(lang);
                    if (!File.Exists(path)) { File.WriteAllText(path, defaults, new UTF8Encoding(true)); continue; }
                    AddMissingSections(path, defaults);
                }
            }
            catch (System.Exception e) { SkatePlugin.Log.LogWarning("Comment files: " + e.Message); }
        }

        // a newer version brought new moments: add their default lines to the player's own file (keeps everything they wrote)
        private static void AddMissingSections(string path, string defaults)
        {
            string text = File.ReadAllText(path, Encoding.UTF8);
            var have = Parse(text);
            var sb = new StringBuilder();
            foreach (var kv in Parse(defaults))
            {
                if (have.ContainsKey(kv.Key)) continue;
                sb.Append("\n[").Append(kv.Key).Append("]\n");
                foreach (var line in kv.Value) sb.Append(line).Append('\n');
            }
            if (sb.Length > 0) File.AppendAllText(path, sb.ToString(), new UTF8Encoding(false));
        }

        // comments have their own language setting (F8 -> HUD & sound -> CommentsLanguage)
        public static Lng Language() => Lang.Comments;

        private static Dictionary<string, List<string>> Pool(Lng lang)
        {
            string path = FileFor(lang);
            System.DateTime stamp = File.Exists(path) ? File.GetLastWriteTimeUtc(path) : System.DateTime.MinValue;
            if (pools.TryGetValue(lang, out var cached) && poolTimes.TryGetValue(lang, out var t) && t == stamp) return cached; // re-read after the file is edited
            var pool = Parse(File.Exists(path) ? File.ReadAllText(path, Encoding.UTF8) : Defaults(lang));
            pools[lang] = pool;
            poolTimes[lang] = stamp;
            return pool;
        }

        private static Dictionary<string, List<string>> Parse(string text)
        {
            var d = new Dictionary<string, List<string>>();
            List<string> cur = null;
            foreach (var raw in text.Split('\n'))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;
                if (line.StartsWith("[") && line.EndsWith("]"))
                {
                    string sec = line.Substring(1, line.Length - 2).Trim().ToLowerInvariant();
                    if (!d.TryGetValue(sec, out cur)) d[sec] = cur = new List<string>();
                    continue;
                }
                cur?.Add(line);
            }
            return d;
        }

        private static readonly HashSet<string> Extras = new HashSet<string> { "fakie", "double", "triple", "big", "fs", "bs" };

        // "Nose Manual" -> "nosemanual", "Kickturn 180" -> "kickturn".
        // plain = without the extras: "Fakie Double Kickflip" -> "kickflip"
        private static string TrickKey(string trick, bool plain)
        {
            if (string.IsNullOrEmpty(trick)) return "";
            var sb = new StringBuilder();
            foreach (var word in trick.ToLowerInvariant().Split(' ', '-'))
            {
                if (plain && Extras.Contains(word)) continue;
                foreach (char c in word) if (c >= 'a' && c <= 'z') sb.Append(c);
            }
            return sb.ToString();
        }

        // "CWSKATE|section|number|trick" -> a comment in the viewer's language
        public static string Resolve(string code, string player)
        {
            var parts = code.Split('|');
            string sec = parts.Length > 1 ? parts[1] : "cruise";
            int n = parts.Length > 2 && int.TryParse(parts[2], out int x) ? x : 0;
            string trick = parts.Length > 3 ? parts[3] : "";
            Lng lang = Language();
            var pool = Pool(lang);
            if (!pool.TryGetValue(sec, out var lines) || lines.Count == 0)
            {
                pool = Parse(Defaults(lang));
                if (!pool.TryGetValue(sec, out lines) || lines.Count == 0) return "!!!";
            }
            // a section just for this trick ([boneless], [revert], [nosemanual]...) wins most of the time
            if (Mathf.Abs(n) % 10 < 7)
            {
                foreach (string key in new[] { TrickKey(trick, false), TrickKey(trick, true) })
                {
                    if (key.Length == 0 || key == sec || !pool.TryGetValue(key, out var special) || special.Count == 0) continue;
                    lines = special;
                    break;
                }
            }
            string s = lines[Mathf.Abs(n) % lines.Count];
            return s.Replace("<playername>", string.IsNullOrEmpty(player) ? Lang.In(lang, "скейтер", "the skater") : player).Replace("<trick>", trick);
        }

        // ------------------------------------------------------------ default comments

        private const string DefaultRu =
@"# Комментарии зрителей про скейт (CW Skate).
# Каждая строка — один комментарий. [раздел] — к какому моменту видео он относится.
# <playername> — имя скейтера, <trick> — название трюка (в [combo] — размер комбо, например x5).
# Добавляй свои строки куда хочешь. Файл подхватывается сам, перезапуск не нужен.

[cruise]
<playername> катается на скейте в старом миру. Я познал эту жизнь
Сзади монстр, какой скейт?
скейтер-ютубер нового уровня
бро просто вышел покататься
Тони Хоук одобряет
лучший канал, катайся дальше
доску где купил, хочу такую же
<playername> продался. видно, что реклама скейта
здравствуйте. Хотели бы вы с нами работать? Мы магазин скейтов ""Ride to Hell"" - продаём качественные скейты. Напишите нам по почте насчёт сотрудничества: shtoto189@gmail.com
чел катается, пока его друзья умирают. база
оператор, сними его покрупнее, не видно даже
лучше бы так в школу ездил
катается увереннее, чем живёт
кто-нибудь, дайте ему спонсора уже

[ollie]
что за Олли ужасный
<playername> прыгнул выше моих ожиданий
прыжок на 10 из 10
у него ноги из пружин, клянусь
этот прыжок я буду видеть во снах
этот прыжок я буду видеть в кошмарах
и это всё что ты можешь?
дефф
легенда, а я даже так не умею
здравствуйте. Хотели бы вы с нами работать? Мы магазин скейтов ""Ride to Hell"" - продаём качественные скейты. Напишите нам по почте насчёт сотрудничества: shtoto189@gmail.com
олли уровня ""я только вчера купил доску""
<playername>, это был прыжок или ты споткнулся?
гравитация в шоке
подпрыгнул выше, чем цены в магазине

[flip]
<trick>?! ЧИСТО
он сделал <trick> в старом мире
<trick> на камеру - это вообще законно?
кто научил <playername> делать <trick>? Ужасно выглядит
<trick> лучше, чем у моей бабушки инвалидки
Ужасный флип. У меня лучше
здравствуйте. Хотели бы вы с нами работать? Мы магазин скейтов ""Ride to Hell"" - продаём качественные скейты. Напишите нам по почте насчёт сотрудничества: shtoto189@gmail.com
<trick> чище, чем моя совесть
доска крутанулась, а я чуть не крутанул голову
<trick>?! покажи ещё раз в замедленной съёмке
мой батя тоже так умеет. наверно
<playername>, научи меня <trick>, плиз

[grab]
грэб как в Skates 3
<playername> схватил доску как мою годовую зарплату
стиль - одно слово!
этот грэб - чистая аура
держи доску крепче, а то ёбнешься
здравствуйте. Хотели бы вы с нами работать? Мы магазин скейтов ""Ride to Hell"" - продаём качественные скейты. Напишите нам по почте насчёт сотрудничества: shtoto189@gmail.com
держит доску крепче, чем свои обещания
грэб такой, что даже монстр захотел автограф
<trick> в старом мире. я видел всё

[spin]
от этого <trick> у меня закружилась голова
он крутится как моя жизнь
<playername>, ты вертолёт?
вращение уровня моггнул бабку
здравствуйте. Хотели бы вы с нами работать? Мы магазин скейтов ""Ride to Hell"" - продаём качественные скейты. Напишите нам по почте насчёт сотрудничества: shtoto189@gmail.com
<trick>, у оператора тоже голова закружилась
крутится лучше, чем земля
вращение такое, что я забыл, где пол

[grind]
гринд по перилам в аду, я в шоке
искры видели?
<playername> скользит как по маслу
этот гринд пусть будет твоим последним
металл заплакал
здравствуйте. Хотели бы вы с нами работать? Мы магазин скейтов ""Ride to Hell"" - продаём качественные скейты. Напишите нам по почте насчёт сотрудничества: shtoto189@gmail.com
перила плачут, а он кайфует
этот гринд был громче моей жизни
<playername> поставил рекорд по гринду в старом мире

[manual]
мэнуал как у профи
баланс 100%
<playername> едет на двух колёсах, а я на двух ногах падаю
чел, я тебе моггну. фигня, а не катание
сынок, это ровно. это ровно, сынок
здравствуйте. Хотели бы вы с нами работать? Мы магазин скейтов ""Ride to Hell"" - продаём качественные скейты. Напишите нам по почте насчёт сотрудничества: shtoto189@gmail.com
едет на двух колёсах будто каждый день
баланс как у алкашей

[bigair]
ЭТО БЫЛ ПОЛЁТ
<playername> улетел в космос
он вообще приземлился??
приземлись на дно гроба пожалуйста
здравствуйте. Хотели бы вы с нами работать? Мы магазин скейтов ""Ride to Hell"" - продаём качественные скейты. Напишите нам по почте насчёт сотрудничества: shtoto189@gmail.com
<playername> полетел в отпуск
пилоты в шоке
полёт длиннее, чем моя речь на экзамене

[combo]
комбо <trick>, я плачу
АУРА ЗАШКАЛИВАЕТ
<playername> фармит ауру прямо на камеру
такое комбо — это уже искусство
сигма-скейтер
кто-нибудь посчитал, сколько тут трюков?
здравствуйте. Хотели бы вы с нами работать? Мы магазин скейтов ""Ride to Hell"" - продаём качественные скейты. Напишите нам по почте насчёт сотрудничества: shtoto189@gmail.com
комбо <trick>, ставлю лайк не глядя
это не комбо, это кайф
я лучше сделаю, лол
аура +бесконечность

[bail]
ХАХАХАХА ОН УПАЛ
асфальт выиграл
<playername> поцеловал землю
минус аура
Он не успеет до колокола доехать
ой. ОЙ.
переиграл и уничтожил сам себя
больно было даже мне
респект в минус, лох
здравствуйте. Хотели бы вы с нами работать? Мы магазин скейтов ""Ride to Hell"" - продаём качественные скейты. Напишите нам по почте насчёт сотрудничества: shtoto189@gmail.com
асфальт: 1, <playername>: 0
падение было красивее, чем трюк
надо поставить это на будильник
лицом в пол, как профи
минус зубы

[monsterbonk]
он врезался в монстра, монстр в шоке
<playername> сбил монстра скейтом, я в слезах
монстр такого не ожидал
лучший момент видео
операторы никогда не умирают
кто кого испугался?
тебе пизда
здравствуйте. Хотели бы вы с нами работать? Мы магазин скейтов ""Ride to Hell"" - продаём качественные скейты. Напишите нам по почте насчёт сотрудничества: shtoto189@gmail.com
монстр подаст в суд
даже монстр в шоке от этого контента
сбил монстра - получил лайк

[smackplayer]
понюхай доску
дружба закончилась
<playername> решил вопрос доской
за что его так?
звук этого удара ты будешь слышать в последние минуты жизни
здравствуйте. Хотели бы вы с нами работать? Мы магазин скейтов ""Ride to Hell"" - продаём качественные скейты. Напишите нам по почте насчёт сотрудничества: shtoto189@gmail.com
и как после этого друзьям доверять
доской по кумполу, классика
это было за всё хорошее
друг больше не друг

[smackmonster]
монстру прилетело доской
монстр получил скейтом по лицу
<playername> бьёт монстров скейтом, лучший контент
скейт - лучшее оружие
теперь монстры боятся скейтеров
этого контента нам явно не хватала
здравствуйте. Хотели бы вы с нами работать? Мы магазин скейтов ""Ride to Hell"" - продаём качественные скейты. Напишите нам по почте насчёт сотрудничества: shtoto189@gmail.com
монстр теперь знает, что такое боль
скейт больнее, чем выглядит

[godsmack]
МОНСТР УЛЕТЕЛ НА ОРБИТУ
<playername> отправил монстра в другую страну и пошёл танцевать
это не скейтер, это бог ауры
от такой ауры моя семья холодная уже 2 часа
после такого можно и потанцевать
аура настолько большая, что монстр не выдержал
<playername> моггнул эту игру
монстра унизили на весь интернет
аура уровня бог

[wallride]
он едет ПО СТЕНЕ
гравитация уволилась
<playername> нашёл новый пол
стены тоже для катания, оказывается
человек-паук - это ты???
Вот бы ты ёбнулся
по стене едет увереннее, чем мой батя во время ДТП
законы физики - это для слабаков

[wallplant]
absolute cinema
<playername> пнул стену. Стена запомнит
уоллплант чистый
стена даже не поняла, что произошло
стена получила пинок, заслуженно
я бы не стерпел, если я был бы стеной
оттолкнулся от стены и от своих проблем

[dropin]
дроп-ин без страха
<playername> съехал оттуда?? я бы не смог
спуск уровня ""мама, смотри""
колени у него из песка
спуск без тормозов
<playername> не боится высоты. А его мама нет. Потому-что её нету

[boardthrow]
кинул доску и догнал, это стиль
<playername> запрыгнул на ходу как в кино
доска сама прибежала к нему, не то что моя бывшая
так вообще можно было?
кинул доску как бумеранг
доска вернулась к хозяину
вернуть отправителю

[boneless]
бонлесс?! это вообще из нулевых
<playername> оттолкнулся ногой и улетел выше крыш
тебе в костюме удобно вообще?
бонлесс в старом мире, классика жанра

[revert]
развернулся красиво, как в кино
ревёрт чистый, кайф
<playername> крутанул доску и поехал дальше, как будто так и надо
разворот уровня ""я безногое уёбище""

[kickturn]
развернулся на задних колёсах, стиль
кикторн как у профи
<playername> поворачивает красивее, чем машины на парковке

[nosemanual]
ноуз-мэнуал?! баланс бога
вот бы в детстве тебя также носом прокатили по полу
<playername> держит баланс лучше, чем свои нервы

[boardslide]
бордслайд по перилам, чистая аура
доска поперёк, а он спокоен
НАУЧИ!!

[stalefish]
стейлфиш?! рука за спиной, стиль за гранью
<playername> схватил доску за пяткой, как настоящая рыба
стейлфиш в старом мире, я в шоке
так изогнуться — это надо уметь
это рыба? нет, это <playername>

[crail]
крейл чистый, нос доски в руке
<playername> дотянулся до носа задней рукой, йога уровня бог
крейл как из старых видео про скейт
рука через всё тело, а он даже не упал

[airwalk]
он идёт ПО ВОЗДУХУ
<playername> гуляет в воздухе, а я по земле нормально не могу
эйрволк?! ноги отдельно, доска отдельно
вот это прогулка
чел просто вышел пройтись. по воздуху

[superman]
ЭТО ПТИЦА? ЭТО САМОЛЁТ? ЭТО <playername>!
суперман на скейте, монстры в шоке
<playername> полетел спасать мир
лежит в воздухе, как на диване
где плащ потерял?

[backflip]
САЛЬТО НАЗАД НА СКЕЙТЕ?!
<playername> перевернулся через голову и даже не упал
бэкфлип в старом мире, я видел всё
у меня от одного вида голова закружилась
<playername>, ты гимнаст или скейтер?
мама, я хочу так же
как таким трюкам научится

[frontflip]
сальто вперёд?! это законно вообще?
<playername> крутанулся через голову, как в мультике
фронтфлип чистый, аура до потолка
перевернулся вперёд и поехал дальше, как будто так и надо
у монстров отвисла челюсть
как твоя мама на инвалидной коляске делаешь. Респект

[boarddance]
он ТАНЦУЕТ на скейте
<playername> устроил дискотеку прямо на доске
танцы на ходу, баланс бога
едет и танцует, а я даже ходить ровно не могу
монстры: мы его убиваем. <playername>: танцует
вайб на максимум
SpookTok уже ждёт это видео
представьте дискотеку на досках?

[escape]
БЕГИ, <playername>, БЕГИ!
монстр гонится, а он на скейте, гениально
кто быстрее: монстр или доска?
он не убегает, он уезжает
крути колёса быстрее!!!
как страшно. Укатывай быстрей
лучшая погоня в старом мире
это не хоррор, это скейт-видео
монстр бежит, а <playername> просто катается

[escaped]
УЕХАЛ! монстр остался ни с чем
<playername> укатил от монстра, легенда
монстр лох
вот зачем нужен скейт в старом мире
монстр проиграл гонку скейтеру
скорость спасла ему жизнь
пока, монстр, увидимся никогда

[hippiejump]
хиппи-джамп?! доска поехала сама, а он сверху
прыгнул, а доска такая: я подожду
<playername> и доска разошлись на секунду и снова сошлись. любовь
доска едет одна, это новый монстр?!
это был прыжок веры
хиппи-джамп в старом мире, олдскул

[hippiejumpover]
ДОСКА ПРОЕХАЛА ПОД НИМ, А ОН ПЕРЕЛЕТЕЛ
<playername> перепрыгнул, доска проехала снизу. физика плачет
вот это хиппи-джамп, все бы так
он перепрыгнул, а я бы сальто сделал
боже, как он ущербно это делает

[boardsnap]
ДОСКА СЛОМАЛАСЬ ПОПОЛАМ
ПОМИНУТЬ ДОСКУ. F
<playername> сломал скейт, теперь только пешком
доска: я устала, я ухожу
сколько же он на ней падал...
это был последний трюк этой доски
F в чат за доску
магазин скейтов уже потирает руки
Вообще уже. Вот раньше мы берегли тщательней

[wornboard]
эта доска сейчас развалится
слышите скрип? это доска плачет
колёса скрипят как дверь в хоррор-фильме
<playername>, купи уже новую доску
доска держится на честном слове
доска держится на соплях видимо
этот скрип страшнее монстров

[monstergrind]
ОН ЕДЕТ НА МОНСТРЕ
<playername> сделал из монстра перила
гринд по монстру?! такого даже в Skate 3 не было
монстр: я тут вообще-то охочусь. <playername>: и что?
монстр теперь такси
катается на монстре, как на эскалаторе
монстр в шоке, оператор в шоке, я в шоке

[skitch]
скитчинг! прицепился и поехал
<playername> едет на прицепе
зачем толкаться, если можно прицепиться
бесплатное такси
держись крепче!

[monsterskitch]
ОН ПРИЦЕПИЛСЯ К МОНСТРУ
Монстр, у вас на хвосту заяц
монстр работает буксиром
это самый смелый скитчинг в истории
монстр: почему мне тяжело бежать?
отпусти его, он же тебя съест!

[humbled]
опущенный
<playername> опущенный
<playername> станцевал над монстром, а потом монстр станцевал над ним. опущенный
от AURA GOD до лоха за минуту
ауры: 0. достоинства: 0. опущенный
монстр отомстил. <playername> пососал по полной
вот это карма
монстр: ""ну и кто теперь AURA GOD?""
вся аура ушла в никуда, опущенный
сначала моггнул, потом сам приготовлен
опустили на весь интернет
F. опущенный
Я так и знал, что ты лох

[train]
ПАРОВОЗИК!!!
чух-чух, следующая остановка - ад
<playername> едет зайцем
паровозик из скейтеров, я плачу
кто впереди, тот и толкается за всех
вот это командная работа
паровозик дружбы в старом мире
держись крепче, а то отцепишься
машинист, не тормози!
монстры: *существуют*. эти: чух-чух

[rocketride]
У НЕГО РАКЕТА НА СКЕЙТЕ
<playername> летит быстрее, чем мой интернет
Илон Маск нервно курит
это не скейт, это баллистическая ракета
куда он так летит??? на тот свет?
тормозов нет, мозгов тоже
<playername> разогнался до скорости света
монстры даже не успели понять, что это было
главное не врезаться. ой
вот это я понимаю апгрейд

[insured]
ОН ЖЕ ДОЛЖЕН БЫЛ УМЕРЕТЬ
страховка решает
<playername> обманул смерть бумажкой
тотем бессмертия как в майнкрафте
Bail Insurance Co., я ваш клиент навсегда
смерть: пришла. полис: отказано
вот почему важно читать договор
у него что, сохранение было?
бумажка сильнее монстра

[kaboom]
КАБУМ
БАБАХ!!!
кто вообще разрешил возить топливо на скейте
<playername> поймал взрыв лицом
Майкл Бэй одобряет
это было громче, чем мой будильник
техника безопасности: 0
монстры: *разлетелись*
он теперь умеет летать без ракеты
";

        private const string DefaultEn =
@"# Viewer Comments About Skateboarding (CW Skate).
# Each line is one comment. [section] — the part of the video the comment refers to.
# <playername> — skater's name, <trick> — trick name (in [combo], the combo size, e.g. x5).
# Feel free to add your own lines wherever you want. The file is picked up automatically — no restart required.

[cruise]
<playername> skating in the old world. bro has found inner peace
monster right behind him, and bro's worried about skateboarding?
next-level skateboarding youtuber
bro just went out for a casual ride
tony hawk approves
best channel ever, keep skating
where did you get that board? i want one
<playername> sold out. you can tell this is a sponsored skateboarding ad
hello! would you be interested in working with us? we're ""Ride to Hell"", a skate shop selling high-quality skateboards. email us about a potential partnership: shtoto189@gmail.com
bro's skating while his friends are dying. based
camera guy, zoom in! i can't even see him
wish he'd skate to school like that
skates more confidently than he lives
someone get this guy a sponsor already

[ollie]
what an awful ollie
<playername> jumped higher than my expectations
10/10 jump
bro's legs are literally springs, i swear
i'll be seeing that jump in my dreams
i'll be seeing that jump in my nightmares
is that all you've got?
def
legend, i can't even do that
hello! would you be interested in working with us? we're ""Ride to Hell"", a skate shop selling high-quality skateboards. email us about a potential partnership: shtoto189@gmail.com
ollie level: ""i bought my first board yesterday""
<playername>, was that a jump or did you just trip?
gravity is shook
jumped higher than the prices at the store

[flip]
<trick>?! THAT WAS CLEAN
he pulled off a <trick> in the old world
is doing a <trick> on camera even legal?
who taught <playername> to do a <trick>? that looked awful
that <trick> was better than my grandma's
terrible flip. i can do better
hello! would you be interested in working with us? we're ""Ride to Hell"", a skate shop selling high-quality skateboards. email us about a potential partnership: shtoto189@gmail.com
that <trick> was cleaner than my conscience
the board spun and i almost spun my head around
<trick>?! show that again in slow motion
my dad can do that too. probably
<playername>, teach me how to do a <trick>, pls

[grab]
grab straight out of Skate 3
<playername> grabbed that board like it was my yearly salary
style. one word!
that grab is pure aura
hold onto the board tighter or you're gonna eat dirt
hello! would you be interested in working with us? we're ""Ride to Hell"", a skate shop selling high-quality skateboards. email us about a potential partnership: shtoto189@gmail.com
holds onto the board tighter than he keeps his promises
that grab was so good even the monster wanted an autograph
<trick> in the old world. i've seen everything

[spin]
that <trick> made my head spin
he spins like my life is falling apart
<playername>, are you a helicopter?
spinning level: mogged grandma
hello! would you be interested in working with us? we're ""Ride to Hell"", a skate shop selling high-quality skateboards. email us about a potential partnership: shtoto189@gmail.com
that <trick> made the camera guy dizzy too
spins better than the Earth
that spin was so crazy i forgot where the floor was

[grind]
grinding on rails in hell, i'm speechless
did you guys see those sparks?
<playername> slides like it's butter
let this be your last grind
the metal is crying
hello! would you be interested in working with us? we're ""Ride to Hell"", a skate shop selling high-quality skateboards. email us about a potential partnership: shtoto189@gmail.com
the rails are crying while he's having the time of his life
that grind was louder than my entire life
<playername> just set the old world's grind record

[manual]
manual like a pro
100% balance
<playername> rides on two wheels while i fall over on two feet
bro, i'll mog you. this skating is nothing special
son, that's straight. that's straight, son
hello! would you be interested in working with us? we're ""Ride to Hell"", a skate shop selling high-quality skateboards. email us about a potential partnership: shtoto189@gmail.com
rides on two wheels like he does it every day
balance like a drunk guy

[bigair]
THAT WAS FLIGHT
<playername> flew into outer space
did he even land??
please land in a coffin
hello! would you be interested in working with us? we're ""Ride to Hell"", a skate shop selling high-quality skateboards. email us about a potential partnership: shtoto189@gmail.com
<playername> just flew off on vacation
pilots are shook
that flight lasted longer than my exam speech

[combo]
that <trick> combo, i'm crying
THE AURA IS OFF THE CHARTS
<playername> is farming aura right on camera
that combo is straight-up art
sigma skater
has anyone counted how many tricks that was?
hello! would you be interested in working with us? we're ""Ride to Hell"", a skate shop selling high-quality skateboards. email us about a potential partnership: shtoto189@gmail.com
that <trick> combo, instant like
that's not a combo, that's pure joy
i could do better lol
aura +infinity

[bail]
HAHAHAHA HE FELL
the pavement won
<playername> kissed the ground
aura: -100
he's not making it back to the bell
oops. OOPS.
bro played himself
that hurt even me
respect: -100, loser
hello! would you be interested in working with us? we're ""Ride to Hell"", a skate shop selling high-quality skateboards. email us about a potential partnership: shtoto189@gmail.com
pavement: 1, <playername>: 0
the bail was prettier than the trick
someone set this as their alarm
face-first into the floor, just like a pro
there go his teeth

[monsterbonk]
he crashed into the monster and the monster is shook
<playername> took out the monster with a skateboard, i'm crying
the monster did NOT see that coming
best moment of the video
camera operators never die
who was more scared?
you're so screwed
hello! would you be interested in working with us? we're ""Ride to Hell"", a skate shop selling high-quality skateboards. email us about a potential partnership: shtoto189@gmail.com
the monster is gonna sue
even the monster is shocked by this content
hit the monster, got a like

[smackplayer]
smell the board
friendship is over
<playername> settled things with a skateboard
what did he do to deserve that?
you'll hear that impact in your final moments
hello! would you be interested in working with us? we're ""Ride to Hell"", a skate shop selling high-quality skateboards. email us about a potential partnership: shtoto189@gmail.com
how are we supposed to trust our friends after this?
a skateboard to the noggin, classic
that was for everything you've ever done
he's not your friend anymore

[smackmonster]
the monster just got hit with a skateboard
the monster got smacked in the face with a board
<playername> hitting monsters with skateboards, best content ever
skateboards are the best weapons
monsters are scared of skaters now
we definitely needed more of this content
hello! would you be interested in working with us? we're ""Ride to Hell"", a skate shop selling high-quality skateboards. email us about a potential partnership: shtoto189@gmail.com
the monster now knows what pain is
skateboards hurt more than they look

[godsmack]
THE MONSTER JUST GOT SENT INTO ORBIT
<playername> sent the monster to another country and started dancing
that's not a skater, that's the god of aura
bro's aura got my whole family freezing for 2 hours
after that, bro deserves a victory dance
the aura was so massive even the monster couldn't handle it
<playername> mogged this game
the monster just got humiliated in front of the entire internet
aura level: god

[wallride]
HE'S RIDING ON THE WALL
gravity just quit
<playername> found a new floor
apparently walls are for skating too
spider-man, is that you???
wish you'd eat pavement
rides on the wall more confidently than my dad drives
the laws of physics are for the weak

[wallplant]
absolute cinema
<playername> kicked the wall. the wall will remember this
clean wallplant
the wall didn't even understand what happened
the wall deserved that kick
i wouldn't stand for that if i were the wall
bounced off the wall and away from my problems

[dropin]
fearless drop-in
<playername> dropped in from THERE?? i could never
drop-in level: ""mom, watch this""
his knees are made of sand
no brakes, just vibes
<playername> isn't afraid of heights. his mom is. because she doesn't exist

[boardthrow]
threw the board and caught up with it, that's style
<playername> jumped onto it mid-motion like in a movie
the board came back to him on its own, unlike my ex
wait, you can actually do that?
threw the board like a boomerang
the board returned to its owner
return to sender

[boneless]
boneless?! that's straight outta the 2000s
<playername> pushed off with one foot and flew above the rooftops
is that suit even comfortable?
boneless in the old world, a true classic

[revert]
turned around beautifully, just like in a movie
clean revert, love it
<playername> spun the board and kept riding like it was nothing
that turn looked like i've never stepped on a skateboard before

[kickturn]
turned on the back wheels, stylish
kickturn like a pro
<playername> turns smoother than cars in a parking lot

[nosemanual]
nose manual?! god-tier balance
wish someone had dragged you across the floor by your nose as a kid
<playername> has better balance than he has nerves

[boardslide]
boardslide on the rails, pure aura
board sideways, and he's completely chill
TEACH ME!!

[stalefish]
stalefish?! hand behind the back, style off the charts
<playername> grabbed the board by the heel like a real fish
stalefish in the old world, i'm shook
you gotta be flexible to bend like that
is that a fish? no, that's <playername>

[crail]
clean crail, nose of the board in his hand
<playername> reached for the nose with his back hand, god-tier yoga
crail straight out of old skate videos
arm across the whole body and he didn't even fall

[airwalk]
HE'S WALKING ON AIR
<playername> is walking in the air while i can't even walk properly on the ground
airwalk?! legs separate, board separate
now that's a stroll
bro just went out for a walk. in the air

[superman]
IS IT A BIRD? IS IT A PLANE? IT'S <playername>!
superman on a skateboard, monsters are shook
<playername> flew off to save the world
just lying in midair like he's on a couch
where'd you lose your cape?

[backflip]
A BACKFLIP ON A SKATEBOARD?!
<playername> flipped over backwards and didn't even fall
backflip in the old world, i've seen everything
my head started spinning just watching that
<playername>, are you a gymnast or a skater?
mom, i wanna do that too
how do you even learn tricks like that?

[frontflip]
a frontflip?! is that even legal?
<playername> flipped head over heels like a cartoon character
clean frontflip, aura through the roof
flipped forward and kept riding like it was nothing
the monsters' jaws dropped
you do it like your mom in a wheelchair. respect

[boarddance]
HE'S DANCING ON A SKATEBOARD
<playername> started a whole disco party on his board
dancing while riding, god-tier balance
he's riding and dancing while i can't even walk straight
monsters: we're killing him. <playername>: *dancing*
maximum vibes
SpookTok is already waiting for this video
imagine a whole disco party on skateboards

[escape]
RUN, <playername>, RUN!
the monster's chasing him and he's on a skateboard. genius
who's faster: the monster or the board?
he's not running away, he's skating away
spin those wheels faster!!!
that's terrifying. skate faster!
best chase scene in the old world
this isn't horror, it's a skate video
the monster's running while <playername> is just cruising

[escaped]
HE GOT AWAY! the monster got nothing
<playername> skated away from the monster, legend
monster's a loser
this is exactly why you need a skateboard in the old world
the monster lost a race to a skater
speed saved his life
see ya, monster. hope we never meet again

[hippiejump]
hippie jump?! the board rolled away on its own while he jumped over it
he jumped and the board was like: i'll wait
<playername> and the board separated for a second and reunited. true love
the board is moving on its own. is that a new monster?!
that was a leap of faith
hippie jump in the old world, old-school

[hippiejumpover]
THE BOARD WENT UNDER HIM AND HE JUMPED OVER IT
<playername> jumped over the board while it rolled underneath. physics is crying
now THAT'S a hippie jump, everyone should do it like that
he jumped over it, i would've done a backflip
god, he's doing this so badly

[boardsnap]
THE BOARD SNAPPED IN HALF
RIP BOARD. F
<playername> broke his skateboard, guess he's walking now
board: i'm tired, i'm leaving
just how many times has he fallen on that thing...
that was the board's final trick
F in chat for the board
the skate shop is already rubbing its hands together
seriously, come on. back in my day, we took better care of our boards

[wornboard]
this board is about to fall apart
hear that creaking? that's the board crying
the wheels squeak like a door in a horror movie
<playername>, just buy a new board already
this board is being held together by sheer willpower
pretty sure this board is held together with duct tape
that squeaking is scarier than the monsters

[monstergrind]
HE'S RIDING ON THE MONSTER
<playername> turned the monster into a rail
grinding on a monster?! even Skate 3 didn't have that
monster: i'm literally hunting you. <playername>: so what?
the monster is a taxi now
riding the monster like it's an escalator
the monster is shook, the cameraman is shook, i'm shook

[skitch]
skitching! grabbed onto something and took off
<playername> is hitching a ride
why push when you can just hold on?
free taxi
hold on tight!

[monsterskitch]
HE'S SKITCHING OFF THE MONSTER
monster, you've got a fare dodger hanging onto you
the monster is working as a tow truck
this is the bravest skitch ever
monster: why is running so hard?
let go of it, it's gonna eat you!
[humbled]
HUMBLED
<playername> got humbled
<playername> danced on the monster, then the monster danced on <playername>
from AURA GOD to zero in a minute
aura: 0. dignity: 0.
the monster got its revenge. <playername> got humbled hard
karma is real
monster: ""who's the aura god now?""
all that aura, gone. humbled
mogged it, then got cooked
humbled in front of the whole internet
F. humbled
I knew you were a loser.

[train]
SKATE TRAIN!!!
choo choo, next stop: hell
<playername> is riding for free
a train of skaters, i'm crying
whoever's in front does all the pushing
now that's teamwork
the friendship train in the old world
hold on tight or you'll fall off
conductor, don't brake!
monsters: *exist*. them: choo choo

[rocketride]
HE PUT A ROCKET ON HIS SKATEBOARD
<playername> is faster than my internet
elon is sweating right now
that's not a skateboard, that's a missile
where is he going so fast??? the afterlife?
no brakes, no brain cells
<playername> just hit light speed
the monsters didn't even see what happened
just don't crash. oh
now THAT'S an upgrade

[insured]
HE WAS SUPPOSED TO DIE
insurance is OP
<playername> cheated death with a piece of paper
totem of undying irl
Bail Insurance Co., take my money
death: arrives. the policy: claim denied
this is why you read the fine print
did he just respawn??
paper beats monster

[kaboom]
KABOOM
BOOM!!!
who allowed rocket fuel on a skateboard
<playername> caught that explosion with his face
michael bay approves
that was louder than my alarm clock
safety rules: 0
monsters: *yeeted*
he can fly without the rocket now
";

        private const string DefaultFr =
@"# Commentaires des spectateurs sur le skate (CW Skate).
# Chaque ligne = un commentaire. [section] = le moment de la vidéo auquel il correspond.
# <playername> = le nom du skateur, <trick> = le nom du trick (dans [combo], la taille du combo, par ex. x5).
# Ajoute tes propres lignes où tu veux. Le fichier est rechargé tout seul, pas besoin de redémarrer.

[cruise]
<playername> fait du skate dans le vieux monde. le gars a trouvé la paix intérieure
y a un monstre juste derrière lui et lui il pense au skate ?
youtubeur skate niveau supérieur
le frérot est juste sorti faire un tour
tony hawk valide
meilleure chaîne, continue de rider
t'as eu la planche où ? j'en veux une pareille
<playername> s'est vendu. ça se voit que c'est une pub pour du skate
bonjour ! seriez-vous intéressé par une collaboration ? nous sommes ""Ride to Hell"", un skateshop qui vend des planches de qualité. écrivez-nous par mail pour un partenariat : shtoto189@gmail.com
le gars ride pendant que ses potes meurent. respect
le caméraman, zoome ! on le voit même pas
j'aimerais qu'il aille à l'école comme ça
il ride avec plus d'assurance qu'il ne vit
que quelqu'un lui trouve un sponsor svp

[ollie]
cet ollie est catastrophique
<playername> a sauté plus haut que mes attentes
saut 10/10
ses jambes c'est des ressorts je jure
je vais rêver de ce saut
je vais faire des cauchemars de ce saut
c'est tout ce que t'as ?
propre
légende, moi je sais même pas faire ça
bonjour ! seriez-vous intéressé par une collaboration ? nous sommes ""Ride to Hell"", un skateshop qui vend des planches de qualité. écrivez-nous par mail pour un partenariat : shtoto189@gmail.com
ollie niveau ""j'ai acheté ma planche hier""
<playername>, c'était un saut ou t'as trébuché ?
la gravité est choquée
il a sauté plus haut que les prix au supermarché

[flip]
<trick> ?! C'ÉTAIT PROPRE
il a sorti un <trick> dans le vieux monde
c'est même légal de faire un <trick> devant une caméra ?
qui a appris le <trick> à <playername> ? c'était affreux
ce <trick> était meilleur que celui de ma grand-mère
flip nul. je fais mieux
bonjour ! seriez-vous intéressé par une collaboration ? nous sommes ""Ride to Hell"", un skateshop qui vend des planches de qualité. écrivez-nous par mail pour un partenariat : shtoto189@gmail.com
ce <trick> était plus propre que ma conscience
la planche a tourné et j'ai failli me dévisser la tête
<trick> ?! remets-le au ralenti
mon père sait faire ça aussi. probablement
<playername>, apprends-moi le <trick> stp

[grab]
grab tout droit sorti de Skate 3
<playername> a agrippé la planche comme mon salaire annuel
du style. un seul mot !
ce grab c'est de l'aura pure
tiens la planche plus fort ou tu vas manger le sol
bonjour ! seriez-vous intéressé par une collaboration ? nous sommes ""Ride to Hell"", un skateshop qui vend des planches de qualité. écrivez-nous par mail pour un partenariat : shtoto189@gmail.com
il tient sa planche mieux que ses promesses
ce grab était tellement beau que même le monstre voulait un autographe
un <trick> dans le vieux monde. j'aurai tout vu

[spin]
ce <trick> m'a fait tourner la tête
il tourne comme ma vie qui part en vrille
<playername>, t'es un hélicoptère ?
niveau rotation : mamie moggée
bonjour ! seriez-vous intéressé par une collaboration ? nous sommes ""Ride to Hell"", un skateshop qui vend des planches de qualité. écrivez-nous par mail pour un partenariat : shtoto189@gmail.com
ce <trick> a donné le tournis au caméraman aussi
il tourne mieux que la Terre
cette rotation était tellement folle que j'ai oublié où était le sol

[grind]
grinder sur des rampes en enfer, je suis sans voix
vous avez vu ces étincelles ?
<playername> glisse comme dans du beurre
que ce soit ton dernier grind
le métal pleure
bonjour ! seriez-vous intéressé par une collaboration ? nous sommes ""Ride to Hell"", un skateshop qui vend des planches de qualité. écrivez-nous par mail pour un partenariat : shtoto189@gmail.com
les rampes pleurent pendant qu'il s'éclate
ce grind était plus bruyant que toute ma vie
<playername> vient de battre le record de grind du vieux monde

[manual]
manual de pro
équilibre 100%
<playername> roule sur deux roues pendant que moi je tombe sur deux pieds
frérot je te mogg, ce skate a rien de spécial
c'est droit, fiston. c'est bien droit
bonjour ! seriez-vous intéressé par une collaboration ? nous sommes ""Ride to Hell"", un skateshop qui vend des planches de qualité. écrivez-nous par mail pour un partenariat : shtoto189@gmail.com
il roule sur deux roues comme si c'était son quotidien
l'équilibre d'un mec bourré

[bigair]
ÇA C'ÉTAIT UN VOL
<playername> est parti dans l'espace
il a atterri au moins ??
atterris dans un cercueil stp
bonjour ! seriez-vous intéressé par une collaboration ? nous sommes ""Ride to Hell"", un skateshop qui vend des planches de qualité. écrivez-nous par mail pour un partenariat : shtoto189@gmail.com
<playername> vient de partir en vacances
les pilotes sont choqués
ce vol a duré plus longtemps que mon oral d'exam

[combo]
ce combo <trick>, je pleure
L'AURA EXPLOSE LES COMPTEURS
<playername> farme de l'aura en direct
ce combo c'est carrément de l'art
skateur sigma
quelqu'un a compté combien de tricks c'était ?
bonjour ! seriez-vous intéressé par une collaboration ? nous sommes ""Ride to Hell"", un skateshop qui vend des planches de qualité. écrivez-nous par mail pour un partenariat : shtoto189@gmail.com
ce combo <trick>, like direct
c'est pas un combo, c'est du bonheur
je ferais mieux mdr
aura +infini

[bail]
MDRRR IL EST TOMBÉ
le bitume a gagné
<playername> a embrassé le sol
aura : -100
il rentrera pas à la cloche celui-là
oups. OUPS.
le gars s'est auto-détruit
même moi j'ai eu mal
respect : -100, loser
bonjour ! seriez-vous intéressé par une collaboration ? nous sommes ""Ride to Hell"", un skateshop qui vend des planches de qualité. écrivez-nous par mail pour un partenariat : shtoto189@gmail.com
bitume : 1, <playername> : 0
la chute était plus belle que le trick
quelqu'un mettez ça en sonnerie de réveil
la tête la première dans le sol, comme un pro
adieu les dents

[monsterbonk]
il a percuté le monstre et le monstre est choqué
<playername> a sorti le monstre avec un skate, je pleure
le monstre l'avait PAS vu venir
meilleur moment de la vidéo
les caméramans ne meurent jamais
qui a eu le plus peur ?
t'es tellement mort
bonjour ! seriez-vous intéressé par une collaboration ? nous sommes ""Ride to Hell"", un skateshop qui vend des planches de qualité. écrivez-nous par mail pour un partenariat : shtoto189@gmail.com
le monstre va porter plainte
même le monstre est choqué par ce contenu
il a tapé le monstre, il a eu un like

[smackplayer]
sens la planche
l'amitié c'est fini
<playername> a réglé ça au skate
il a fait quoi pour mériter ça ?
tu entendras ce bruit à tes derniers instants
bonjour ! seriez-vous intéressé par une collaboration ? nous sommes ""Ride to Hell"", un skateshop qui vend des planches de qualité. écrivez-nous par mail pour un partenariat : shtoto189@gmail.com
comment on peut faire confiance à ses potes après ça ?
un skate dans la tête, un classique
ça c'était pour tout ce que t'as fait
c'est plus ton pote

[smackmonster]
le monstre vient de se prendre un skate
le monstre s'est pris une planche en pleine face
<playername> tape des monstres au skate, meilleur contenu
le skate est la meilleure arme
les monstres ont peur des skateurs maintenant
on avait clairement besoin de plus de ce contenu
bonjour ! seriez-vous intéressé par une collaboration ? nous sommes ""Ride to Hell"", un skateshop qui vend des planches de qualité. écrivez-nous par mail pour un partenariat : shtoto189@gmail.com
le monstre sait ce qu'est la douleur maintenant
un skate ça fait plus mal que ça en a l'air

[godsmack]
LE MONSTRE VIENT D'ÊTRE ENVOYÉ EN ORBITE
<playername> a envoyé le monstre dans un autre pays et s'est mis à danser
c'est pas un skateur, c'est le dieu de l'aura
l'aura du gars a congelé toute ma famille pendant 2 heures
après ça, il mérite sa danse de la victoire
l'aura était tellement énorme que même le monstre a pas tenu
<playername> a moggé ce jeu
le monstre vient de se faire humilier devant tout internet
niveau d'aura : divin

[wallride]
IL ROULE SUR LE MUR
la gravité a démissionné
<playername> a trouvé un nouveau sol
apparemment les murs c'est aussi pour le skate
spider-man, c'est toi ???
j'espère que tu vas manger le bitume
il roule sur le mur mieux que mon père conduit
les lois de la physique c'est pour les faibles

[wallplant]
cinéma absolu
<playername> a mis un coup de pied au mur. le mur s'en souviendra
wallplant propre
le mur a même pas compris ce qui s'est passé
le mur méritait ce coup de pied
à la place du mur je me laisserais pas faire
il a rebondi sur le mur et loin de ses problèmes

[dropin]
drop-in sans peur
<playername> a droppé depuis LÀ-HAUT ?? jamais de la vie moi
drop-in niveau ""maman, regarde""
ses genoux sont en sable
pas de freins, que des vibes
<playername> a pas peur du vide. sa mère si. parce qu'elle existe pas

[boardthrow]
il a lancé la planche et l'a rattrapée, ça c'est du style
<playername> a sauté dessus en marche comme dans un film
la planche est revenue toute seule, pas comme mon ex
attends, on peut vraiment faire ça ?
il a lancé la planche comme un boomerang
la planche est revenue à son propriétaire
retour à l'envoyeur

[boneless]
un boneless ?! tout droit sorti des années 2000
<playername> a poussé d'un pied et s'est envolé au-dessus des toits
elle est confortable au moins cette combi ?
un boneless dans le vieux monde, un vrai classique

[revert]
il s'est retourné magnifiquement, comme dans un film
revert propre, j'adore
<playername> a fait tourner la planche et a continué comme si de rien n'était
ce demi-tour donne l'impression que j'ai jamais touché un skate

[kickturn]
il a tourné sur les roues arrière, stylé
kickturn de pro
<playername> tourne mieux que les voitures sur un parking

[nosemanual]
un nose manual ?! équilibre divin
j'aurais aimé que quelqu'un te traîne par le nez par terre quand t'étais petit
<playername> a plus d'équilibre que de nerfs

[boardslide]
boardslide sur la rampe, aura pure
planche de travers et lui il est tranquille
APPRENDS-MOI !!

[stalefish]
un stalefish ?! main derrière le dos, style hors normes
<playername> a attrapé la planche par le talon comme un vrai poisson
un stalefish dans le vieux monde, je suis choqué
faut être souple pour se plier comme ça
c'est un poisson ? non, c'est <playername>

[crail]
crail propre, le nose de la planche dans la main
<playername> a attrapé le nose avec la main arrière, yoga divin
crail tout droit sorti des vieilles vidéos de skate
le bras en travers du corps et il est même pas tombé

[airwalk]
IL MARCHE DANS LES AIRS
<playername> marche en l'air alors que moi je marche même pas droit par terre
un airwalk ?! les jambes d'un côté, la planche de l'autre
ça c'est une balade
le gars est juste sorti se promener. dans les airs

[superman]
C'EST UN OISEAU ? C'EST UN AVION ? C'EST <playername> !
superman en skate, les monstres sont choqués
<playername> est parti sauver le monde
il est allongé en l'air comme sur un canapé
t'as perdu ta cape où ?

[backflip]
UN BACKFLIP EN SKATE ?!
<playername> a fait un salto arrière et il est même pas tombé
un backflip dans le vieux monde, j'aurai tout vu
j'ai eu le tournis rien qu'en regardant
<playername>, t'es gymnaste ou skateur ?
maman, moi aussi je veux faire ça
comment on apprend des tricks pareils ?

[frontflip]
un frontflip ?! c'est légal ça ?
<playername> a fait la culbute comme un perso de dessin animé
frontflip propre, l'aura crève le plafond
il a fait un salto avant et a continué comme si de rien n'était
les monstres en ont perdu la mâchoire
tu le fais comme ta mère en fauteuil roulant. respect

[boarddance]
IL DANSE SUR SON SKATE
<playername> a lancé une soirée disco sur sa planche
il danse en roulant, équilibre divin
il roule et danse pendant que moi je marche même pas droit
les monstres : on va le tuer. <playername> : *danse*
vibes au max
SpookTok attend déjà cette vidéo
imaginez une soirée disco en skate

[escape]
COURS, <playername>, COURS !
le monstre le poursuit et lui il est en skate. génie
qui est le plus rapide : le monstre ou la planche ?
il fuit pas, il ride
fais tourner ces roues plus vite !!!
c'est terrifiant. ride plus vite !
meilleure course-poursuite du vieux monde
c'est pas de l'horreur, c'est une vidéo de skate
le monstre court pendant que <playername> se balade

[escaped]
IL S'EST ÉCHAPPÉ ! le monstre a rien eu
<playername> a semé le monstre en skate, légende
le monstre c'est un loser
voilà pourquoi il faut un skate dans le vieux monde
le monstre a perdu une course contre un skateur
la vitesse lui a sauvé la vie
salut le monstre. j'espère qu'on se reverra jamais

[hippiejump]
un hippie jump ?! la planche a roulé toute seule pendant qu'il sautait par-dessus
il a sauté et la planche genre : je t'attends
<playername> et la planche se sont séparés une seconde puis retrouvés. le vrai amour
la planche roule toute seule. c'est un nouveau monstre ?!
c'était un saut de la foi
un hippie jump dans le vieux monde, old school

[hippiejumpover]
LA PLANCHE EST PASSÉE SOUS LUI ET IL A SAUTÉ PAR-DESSUS
<playername> a sauté au-dessus de la planche pendant qu'elle roulait dessous. la physique pleure
ÇA c'est un hippie jump, tout le monde devrait faire comme ça
il a sauté par-dessus, moi j'aurais fait un backflip
mon dieu il le fait tellement mal

[boardsnap]
LA PLANCHE S'EST CASSÉE EN DEUX
RIP LA PLANCHE. F
<playername> a cassé son skate, maintenant c'est à pied
la planche : je suis fatiguée, je m'en vais
combien de fois il est tombé avec ce truc...
c'était le dernier trick de la planche
F dans le chat pour la planche
le skateshop se frotte déjà les mains
sérieux, de mon temps on prenait mieux soin de nos planches

[wornboard]
cette planche va tomber en morceaux
vous entendez ce grincement ? c'est la planche qui pleure
les roues grincent comme une porte dans un film d'horreur
<playername>, achète une nouvelle planche
cette planche tient par la seule force de la volonté
je suis sûr que cette planche tient avec du scotch
ce grincement fait plus peur que les monstres

[monstergrind]
IL ROULE SUR LE MONSTRE
<playername> a transformé le monstre en rampe
grinder sur un monstre ?! même Skate 3 avait pas ça
le monstre : je te chasse littéralement. <playername> : et alors ?
le monstre est devenu un taxi
il ride sur le monstre comme sur un escalator
le monstre est choqué, le caméraman est choqué, je suis choqué

[skitch]
du skitch ! il s'est accroché et c'est parti
<playername> fait du stop
pourquoi pousser quand tu peux juste t'accrocher ?
taxi gratuit
accroche-toi bien !

[monsterskitch]
IL SE FAIT TRACTER PAR LE MONSTRE
le monstre, t'as un resquilleur accroché à toi
le monstre bosse comme dépanneuse
c'est le skitch le plus courageux de l'histoire
le monstre : pourquoi c'est si dur de courir ?
lâche-le, il va te manger !

[humbled]
HUMILIÉ
<playername> s'est fait humilier
<playername> a dansé sur le monstre, puis le monstre a dansé sur <playername>
d'AURA GOD à zéro en une minute
aura : 0. dignité : 0.
le monstre s'est vengé. <playername> s'est fait humilier sévère
le karma existe
le monstre : ""c'est qui l'aura god maintenant ?""
toute cette aura, envolée. humilié
il l'a moggé, puis il s'est fait cuire
humilié devant tout internet
F. humilié
je savais que t'étais un loser.

[train]
PETIT TRAIN DE SKATE !!!
tchou tchou, prochain arrêt : l'enfer
<playername> voyage gratuitement
un train de skateurs, je pleure
celui de devant pousse pour tout le monde
ça c'est du travail d'équipe
le train de l'amitié dans le vieux monde
accroche-toi bien ou tu vas tomber
conducteur, freine pas !
les monstres : *existent*. eux : tchou tchou

[rocketride]
IL A MIS UNE FUSÉE SUR SON SKATE
<playername> est plus rapide que ma connexion
elon transpire en ce moment
c'est pas un skate, c'est un missile
il va où aussi vite ??? dans l'au-delà ?
pas de freins, pas de neurones
<playername> vient de passer la vitesse de la lumière
les monstres ont même pas vu ce qui s'est passé
surtout te crashe pas. ah
ÇA c'est une amélioration

[insured]
IL ÉTAIT CENSÉ MOURIR
l'assurance est cheatée
<playername> a triché avec la mort grâce à un bout de papier
totem d'immortalité irl
Bail Insurance Co., prenez mon argent
la mort : arrive. la police d'assurance : demande refusée
voilà pourquoi il faut lire les petites lignes
il vient de respawn ??
le papier bat le monstre

[kaboom]
KABOOM
BOUM !!!
qui a autorisé le carburant de fusée sur un skate
<playername> a pris l'explosion en pleine face
michael bay valide
c'était plus fort que mon réveil
règles de sécurité : 0
les monstres : *propulsés*
il peut voler sans la fusée maintenant
";

        private const string DefaultZh =
@"# CW Skate 观众关于滑板的评论。
# 每一行 = 一条评论。[分区] = 视频里对应的瞬间（分区名不要翻译）。
# <playername> = 滑手的名字，<trick> = 动作名（在 [combo] 里是连招数，比如 x5）。
# 随便加你自己的评论。文件会自动重新读取，不用重启。

[cruise]
<playername> 在旧世界玩滑板。这哥们找到内心的平静了
后面就是怪物，他满脑子还是滑板？
滑板 UP 主天花板
兄弟只是出来溜个弯
托尼·霍克认证
最好的频道，继续滑
滑板哪买的？我也想要一块
<playername> 恰饭了。一看就是滑板广告
您好！有兴趣合作吗？我们是 Ride to Hell，一家卖优质滑板的滑板店。商务合作请发邮件：shtoto189@gmail.com
队友在死，他在滑。respect
摄影师拉近点！根本看不见他
我要是能这样去上学就好了
他滑板比他活得还自信
求求谁给他找个赞助商吧

[ollie]
这个豚跳太灾难了
<playername> 跳得比我的期望还高
这一跳 10/10
他的腿是弹簧做的吧
今晚做梦都是这一跳
今晚噩梦都是这一跳
就这？
干净
传奇，我连这都不会
您好！有兴趣合作吗？我们是 Ride to Hell，一家卖优质滑板的滑板店。商务合作请发邮件：shtoto189@gmail.com
豚跳水平：滑板昨天刚买的
<playername>，你这是跳还是绊了一下？
重力都惊了
他跳得比超市物价涨得还高

[flip]
<trick>？！太干净了
他在旧世界整了个 <trick>
在摄像机前做 <trick> 合法吗？
谁教 <playername> 的 <trick>？太难看了
这个 <trick> 比我奶奶的还好
翻得真烂。我都比他强
您好！有兴趣合作吗？我们是 Ride to Hell，一家卖优质滑板的滑板店。商务合作请发邮件：shtoto189@gmail.com
这个 <trick> 比我的良心还干净
板子一转，我脖子差点拧断
<trick>？！慢放再来一遍
我爸也会。大概吧
<playername>，教我 <trick> 吧

[grab]
这个抓板直接从 Skate 3 里出来的
<playername> 抓板抓得像抓我的年终奖
有型。就一个字！（两个字）
这个抓板全是气场
抓紧点，不然要啃地了
您好！有兴趣合作吗？我们是 Ride to Hell，一家卖优质滑板的滑板店。商务合作请发邮件：shtoto189@gmail.com
他抓板比他守承诺还牢
这个抓板太帅了，怪物都想要签名
在旧世界整 <trick>，我算是开眼了

[spin]
这个 <trick> 把我转晕了
他转得像我的人生一样失控
<playername>，你是直升机吗？
旋转水平：奶奶被碾压了
您好！有兴趣合作吗？我们是 Ride to Hell，一家卖优质滑板的滑板店。商务合作请发邮件：shtoto189@gmail.com
这个 <trick> 把摄影师也转晕了
他转得比地球还溜
转得太离谱了，我都忘了地在哪

[grind]
在地狱的栏杆上滑杆，我无话可说
你们看到那些火花了吗？
<playername> 滑得像切黄油一样顺
希望这是你最后一次滑杆
金属在哭泣
您好！有兴趣合作吗？我们是 Ride to Hell，一家卖优质滑板的滑板店。商务合作请发邮件：shtoto189@gmail.com
栏杆在哭，他在乐
这次滑杆比我整个人生都吵
<playername> 刚刚刷新了旧世界的滑杆纪录

[manual]
职业级 manual
平衡感 100%
<playername> 用两个轮子滑，我用两只脚都能摔
兄弟我碾压你，这滑板也没啥特别的
稳，孩子。真稳
您好！有兴趣合作吗？我们是 Ride to Hell，一家卖优质滑板的滑板店。商务合作请发邮件：shtoto189@gmail.com
他用两个轮子滑得跟日常一样
醉汉的平衡感

[bigair]
这才叫飞
<playername> 上太空了
他落地了吗？？
落进棺材里吧
您好！有兴趣合作吗？我们是 Ride to Hell，一家卖优质滑板的滑板店。商务合作请发邮件：shtoto189@gmail.com
<playername> 刚出发去度假了
飞行员都惊了
这次滞空比我的答辩还长

[combo]
这个 <trick> 连招，我哭了
气场爆表了
<playername> 在直播刷气场
这连招就是艺术
sigma 滑手
有人数了一共几个动作吗？
您好！有兴趣合作吗？我们是 Ride to Hell，一家卖优质滑板的滑板店。商务合作请发邮件：shtoto189@gmail.com
<trick> 连招，直接点赞
这不是连招，这是幸福
我能做得更好 hhh
气场 +∞

[bail]
哈哈哈哈哈他摔了
地面赢了
<playername> 亲吻了大地
气场：-100
这位怕是赶不上上课了
哎呀。哎呀呀。
这哥们自爆了
我看着都疼
尊重 -100，菜
您好！有兴趣合作吗？我们是 Ride to Hell，一家卖优质滑板的滑板店。商务合作请发邮件：shtoto189@gmail.com
地面 1 : 0 <playername>
摔得比动作还好看
谁把这个设成我的起床铃
脸先着地，专业
牙齿再见

[monsterbonk]
他撞上了怪物，怪物都懵了
<playername> 用滑板撞飞了怪物，笑死
怪物完全没想到
视频最佳瞬间
摄影师永远不死
谁被吓得更惨？
你死定了
您好！有兴趣合作吗？我们是 Ride to Hell，一家卖优质滑板的滑板店。商务合作请发邮件：shtoto189@gmail.com
怪物要去告你了
怪物都被这内容震惊了
撞了怪物，喜提一赞

[smackplayer]
感受滑板的力量吧
友谊的小船说翻就翻
<playername> 用滑板解决了问题
他做了什么要挨这一下？
你临死前都会记得这个声音
您好！有兴趣合作吗？我们是 Ride to Hell，一家卖优质滑板的滑板店。商务合作请发邮件：shtoto189@gmail.com
这以后还怎么相信队友？
滑板拍脸，经典
这一下是替你做过的所有事还的
你们不是朋友了

[smackmonster]
怪物刚吃了一记滑板
怪物被滑板拍了一脸
<playername> 用滑板抽怪物，最佳内容
滑板是最强武器
现在怪物都怕滑手了
我们就需要更多这种内容
您好！有兴趣合作吗？我们是 Ride to Hell，一家卖优质滑板的滑板店。商务合作请发邮件：shtoto189@gmail.com
怪物现在知道什么叫疼了
滑板比看起来疼多了

[godsmack]
怪物被抽上轨道了
<playername> 把怪物抽到了另一个国家，然后跳起了舞
这不是滑手，这是气场之神
这哥们的气场把我全家冻住了两个小时
这之后，他配得上胜利之舞
气场太强了，怪物都扛不住
<playername> 把这游戏碾压了
怪物在全网面前被打脸
气场等级：神

[wallride]
他在墙上滑
重力辞职了
<playername> 找到了新的地面
原来墙也能滑滑板
蜘蛛侠，是你吗？？？
希望你啃一嘴地
他在墙上滑得比我爸开车还稳
物理定律是给弱者准备的

[wallplant]
纯纯的电影
<playername> 踹了墙一脚。墙会记住的
wallplant 很干净
墙都没反应过来发生了什么
这墙活该挨这一脚
我要是墙，可不会忍
他从墙上弹开，也弹开了所有烦恼

[dropin]
无所畏惧的 drop in
<playername> 从那么高下去？？打死我都不敢
drop in 水平：妈妈快看
他的膝盖是沙子做的
没有刹车，只有氛围
<playername> 不怕高。他妈怕。因为他没妈

[boardthrow]
扔出去再接住，这才叫有型
<playername> 像电影里一样跳上了滑行中的板子
滑板自己回来了，不像我前任
等等，这还能这么玩？
他把滑板当回旋镖扔
滑板回到了主人身边
原路退回

[boneless]
boneless？！2000 年代的味道
<playername> 一只脚一蹬就飞过了屋顶
这身连体服穿着舒服吗？
旧世界里的 boneless，经典中的经典

[revert]
转身转得真漂亮，跟电影一样
revert 很干净，爱了
<playername> 把滑板一转，像什么都没发生一样继续滑
这个掉头让我感觉我从没碰过滑板

[kickturn]
用后轮转了个身，帅
职业级 kickturn
<playername> 转弯比停车场里的车还溜

[nosemanual]
nose manual？！神级平衡
真希望你小时候有人拽着你的鼻子在地上拖
<playername> 的平衡感比他的神经还强

[boardslide]
栏杆上 boardslide，全是气场
板子横着，他还淡定得很
教我！！

[stalefish]
stalefish？！手绕到背后，风格拉满
<playername> 像抓鱼一样从脚跟那边抓住了板子
旧世界里的 stalefish，我震惊了
得多柔软才能这么弯
是鱼吗？不，是 <playername>

[crail]
干净的 crail，后手抓板头
<playername> 用后手抓住了板头，神级瑜伽
crail 直接从老滑板录像里出来的
手臂横过身体，他居然没摔

[airwalk]
他在空中走路
<playername> 在空中走路，我在地上都走不直
airwalk？！腿在一边，滑板在另一边
这才叫散步
这哥们只是出来散个步。在天上

[superman]
是鸟？是飞机？是 <playername>！
滑板超人，怪物都惊了
<playername> 去拯救世界了
他躺在空中跟躺沙发一样
你的披风丢哪了？

[backflip]
滑板后空翻？！
<playername> 后空翻了，居然没摔
旧世界里后空翻，我算是开眼了
光看着我就晕了
<playername>，你是体操运动员还是滑手？
妈妈，我也想这样
这种动作是怎么学会的？

[frontflip]
前空翻？！这合法吗？
<playername> 翻得像动画片里的角色
前空翻很干净，气场冲破天花板
他前空翻完像什么都没发生一样继续滑
怪物下巴都掉了
你翻得像你妈坐轮椅一样。respect

[boarddance]
他在滑板上跳舞
<playername> 在滑板上开了个迪厅
边滑边跳，神级平衡
他边滑边跳，我走路都走不直
怪物：我们要杀了他。<playername>：*跳舞*
氛围拉满
SpookTok 已经在等这个视频了
想象一下滑板迪厅

[escape]
快跑，<playername>，快跑！
怪物在追他，他在玩滑板。天才
谁更快：怪物还是滑板？
他不是在逃，他是在滑
轮子转快点！！！
太吓人了。滑快点！
旧世界最佳追逐戏
这不是恐怖片，这是滑板视频
怪物在狂奔，<playername> 在散步

[escaped]
他逃掉了！怪物啥也没捞着
<playername> 用滑板甩掉了怪物，传奇
怪物就是个 loser
这就是为什么在旧世界要带滑板
怪物跑输了一个滑手
速度救了他一命
再见了怪物。希望永远不见

[hippiejump]
hippie jump？！他跳起来的时候板子自己在滑
他一跳，板子：我等你
<playername> 和滑板分开了一秒又重逢了。真爱
滑板自己在跑。这是新怪物吗？！
这是信仰之跃
旧世界里的 hippie jump，老派

[hippiejumpover]
板子从他脚下穿过去，他跳过去了
<playername> 跳过了脚下滑过的板子。物理在哭泣
这才叫 hippie jump，大家都该这么做
他跳过去了，换我就后空翻了
天哪，他做得太烂了

[boardsnap]
滑板断成两半了
滑板 RIP。F
<playername> 把滑板弄断了，现在只能走路了
滑板：我累了，我走了
他用这玩意摔了多少次啊……
这是滑板的最后一个动作
弹幕扣 F 送滑板
滑板店已经在搓手了
真的，我们那时候可爱惜滑板了

[wornboard]
这滑板要散架了
听到那吱吱声了吗？是滑板在哭
轮子吱吱响得像恐怖片里的门
<playername>，买块新滑板吧
这滑板全靠意志力撑着
我敢说这滑板是胶带粘起来的
这吱吱声比怪物还吓人

[monstergrind]
他在怪物身上滑
<playername> 把怪物变成了栏杆
在怪物身上滑杆？！Skate 3 都没有
怪物：我真的在追你。<playername>：所以呢？
怪物变成出租车了
他在怪物身上滑得像坐扶梯
怪物震惊，摄影师震惊，我也震惊

[skitch]
扒车！他抓住就走了
<playername> 在搭顺风车
能扒着走为什么还要蹬？
免费出租车
抓稳了！

[monsterskitch]
他被怪物拖着走
怪物，你身上挂了个逃票的
怪物在当拖车
史上最勇的扒车
怪物：为什么跑得这么累？
快松手，它要吃了你！

[humbled]
被打脸了
<playername> 被打脸了
<playername> 在怪物头上跳舞，然后怪物在 <playername> 头上跳舞
一分钟从 AURA GOD 到零
气场：0。尊严：0。
怪物报仇了。<playername> 被狠狠打脸
因果报应是存在的
怪物：现在谁是 aura god？
那么多气场，全没了。被打脸了
他碾压了它，然后自己被做熟了
在全网面前被打脸
F。被打脸了
我就知道你是个 loser。

[train]
滑板小火车！！！
呜呜，下一站：地狱
<playername> 坐上了免费车
滑手小火车，我哭了
最前面那个在给所有人蹬
这才叫团队合作
旧世界的友谊小火车
抓稳了，不然要掉下去
司机别刹车！
怪物：*存在*。他们：呜呜

[rocketride]
他在滑板上装了火箭
<playername> 比我的网速还快
马斯克现在在冒汗
这不是滑板，这是导弹
这么快要去哪？？？去另一个世界？
没有刹车，没有脑子
<playername> 刚刚超过了光速
怪物都没看清发生了什么
千万别撞。啊
这才叫升级

[insured]
他本来应该死了
这保险开挂了
<playername> 靠一张纸骗过了死神
现实版不死图腾
Bail Insurance Co.，拿走我的钱
死神：来了。保险单：申请驳回
所以说要看清条款小字
他刚刚复活了？？
纸打败了怪物

[kaboom]
KABOOM
砰！！！
谁允许在滑板上用火箭燃料的
<playername> 被炸了一脸
迈克尔·贝认证
这比我的闹钟还响
安全守则：0
怪物：*起飞*
他现在不用火箭也能飞了
";
    }
}
