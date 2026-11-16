using System.Collections.Generic;
using System.Reflection;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Localization.Settings;

namespace CWSkate
{
    // (new languages go at the end, so the saved choice in the config file keeps its meaning)
    internal enum MenuLanguage { Auto, Russian, English, French, Chinese }

    // the language actually in use (Zh = Simplified Chinese, also used when the game is in Traditional)
    internal enum Lng { En, Ru, Fr, Zh }

    // Every language choice in the mod goes through here.
    // Language = the mod itself (menu, config file, item name, monster mockery).
    // CommentsLanguage = viewer comments only. Auto in both = the game's interface language.
    // How it works: the code holds Russian and English side by side (T(ru, en)); any other language is looked up
    // by the English text in its own table (LangFr.cs). A new language = a new table + a line in Table().
    // (Trick and rank names are English in any language.)
    internal static partial class Lang
    {
        public static Lng Cur => Pick(SkatePlugin.Language);
        public static Lng Comments => Pick(SkatePlugin.CommentsLanguage);
        public static bool Ru => Cur == Lng.Ru;

        private static Lng Pick(ConfigEntry<MenuLanguage> e)
        {
            var l = e != null ? e.Value : MenuLanguage.Auto;
            switch (l)
            {
                case MenuLanguage.Russian: return Lng.Ru;
                case MenuLanguage.English: return Lng.En;
                case MenuLanguage.French: return Lng.Fr;
                case MenuLanguage.Chinese: return Lng.Zh;
                default: return Game();
            }
        }

        public static string T(string ru, string en) => In(Cur, ru, en);

        // the same, for a chosen language (the viewer comments have their own)
        public static string In(Lng l, string ru, string en)
        {
            if (l == Lng.Ru) return ru;
            if (l == Lng.En) return en;
            var table = Table(l);
            if (table != null && en != null && table.TryGetValue(en, out var s)) return s;
            Missing(l, en);
            return en;
        }

        // with {0}, {1}... filled in after the translation (for texts with numbers or keys inside)
        public static string F(string ru, string en, params object[] args)
        {
            string t = T(ru, en);
            try { return string.Format(t, args); }
            catch { return string.Format(en, args); } // a broken translation must not break the game
        }

        // a pick for things that aren't single texts (lists of phrases and so on)
        public static X Pick<X>(X ru, X en, X fr, X zh)
        {
            switch (Cur) { case Lng.Ru: return ru; case Lng.Fr: return fr; case Lng.Zh: return zh; default: return en; }
        }

        private static Dictionary<string, string> Table(Lng l)
        {
            switch (l) { case Lng.Fr: return Fr; case Lng.Zh: return Zh; default: return null; }
        }

        // a text nobody has translated yet: say so in the log once (it shows in English meanwhile)
        private static readonly HashSet<string> reported = new HashSet<string>();
        private static void Missing(Lng l, string en)
        {
            if (string.IsNullOrEmpty(en) || !reported.Add(l + "|" + en)) return;
            SkatePlugin.Log?.LogWarning("No " + l + " text for: \"" + en + "\"");
        }

        // the language the game's own menus are in right now
        public static Lng Game()
        {
            string code = GameCode();
            if (string.IsNullOrEmpty(code))
            {
                switch (Application.systemLanguage)
                {
                    case SystemLanguage.Russian: return Lng.Ru;
                    case SystemLanguage.French: return Lng.Fr;
                    case SystemLanguage.Chinese:
                    case SystemLanguage.ChineseSimplified:
                    case SystemLanguage.ChineseTraditional: return Lng.Zh;
                    default: return Lng.En;
                }
            }
            code = code.ToLowerInvariant();
            if (code.StartsWith("ru")) return Lng.Ru;
            if (code.StartsWith("fr")) return Lng.Fr;
            if (code.StartsWith("zh")) return Lng.Zh; // zh-Hans and zh-Hant
            return Lng.En;
        }

        private static string GameCode()
        {
            string code = null;
            try
            {
                var loc = LocalizationSettings.HasSettings ? LocalizationSettings.SelectedLocale : null;
                if (loc != null) code = loc.Identifier.Code;
            }
            catch { }
            if (string.IsNullOrEmpty(code))
            {
                // too early for the localization system: read the game's language setting itself
                try
                {
                    var s = GameHandler.Instance != null ? GameHandler.Instance.SettingsHandler?.GetSetting<LanguageSetting>() : null;
                    if (s != null && s.m_locales != null && s.Value >= 0 && s.Value < s.m_locales.Count && s.m_locales[s.Value] != null)
                        code = s.m_locales[s.Value].Identifier.Code;
                }
                catch { }
            }
            return code;
        }

        public static string Section(string key, string ru)
        {
            switch (Cur)
            {
                case Lng.Ru: return ru;
                case Lng.Fr: if (SectionsFr.TryGetValue(key, out var fr)) return fr; break;
                case Lng.Zh: if (SectionsZh.TryGetValue(key, out var zh)) return zh; break;
            }
            return SectionsEn.TryGetValue(key, out var en) ? en : key;
        }

        // a setting's description in the mod's language
        public static string Desc(ConfigEntryBase e) => DescIn(e, Cur);

        private static string DescIn(ConfigEntryBase e, Lng l)
        {
            string ru = RuDesc.TryGetValue(e, out var r) ? r : e.Description.Description;
            if (l == Lng.Ru) return ru;
            string key = e.Definition.Key;
            if (l == Lng.Fr && DescFr.TryGetValue(key, out var fr)) return fr;
            if (l == Lng.Zh && DescZh.TryGetValue(key, out var zh)) return zh;
            return DescEn.TryGetValue(key, out var en) ? en : ru;
        }

        // how a choice like Auto / Russian / English is written on its button
        public static string Value(object v)
        {
            if (v is MenuLanguage l)
            {
                switch (l)
                {
                    case MenuLanguage.Russian: return "Русский";
                    case MenuLanguage.English: return "English";
                    case MenuLanguage.French: return "Français";
                    case MenuLanguage.Chinese: return "简体中文";
                    default: return T("Авто (как в игре)", "Auto (same as the game)");
                }
            }
            if (v is SteeringMode m)
                return m == SteeringMode.Keyboard ? T("Клавиатура", "Keyboard") : T("Мышь", "Mouse");
            if (v is SkateDifficulty sd)
                return sd == SkateDifficulty.Easy ? T("Лёгкая", "Easy") : sd == SkateDifficulty.Normal ? T("Средняя", "Normal") : T("Сложная", "Hard");
            return v.ToString();
        }

        // ------------------------------------------------------------ the config file follows the language too

        private static readonly Dictionary<ConfigEntryBase, string> RuDesc = new Dictionary<ConfigEntryBase, string>();
        private static readonly FieldInfo DescField = AccessTools.Field(typeof(ConfigEntryBase), "<Description>k__BackingField");
        private static int written = -1; // -1 = not yet, otherwise the Lng the file is written in
        private static float nextCheck;

        // called every frame by the manager, does the work about once a second
        public static void Tick()
        {
            if (Time.unscaledTime < nextCheck || SkatePlugin.Cfg == null || DescField == null) return;
            nextCheck = Time.unscaledTime + 1f;
            try { SkateItem.ApplyNames(); } catch { } // item names in the shop
            Lng lang = Cur;
            int want = (int)lang;
            if (want == written) return;
            try
            {
                foreach (var kv in SkatePlugin.Cfg)
                {
                    var e = kv.Value;
                    var d = e.Description;
                    if (!RuDesc.ContainsKey(e)) RuDesc[e] = d.Description;
                    string text = DescIn(e, lang);
                    if (text != d.Description) DescField.SetValue(e, new ConfigDescription(text, d.AcceptableValues, d.Tags));
                }
                // the first check at start-up doesn't rewrite a Russian file that is already Russian
                if (written != -1 || lang != Lng.Ru) SkatePlugin.Cfg.Save();
                written = want;
            }
            catch (System.Exception ex)
            {
                written = want; // don't retry every second
                SkatePlugin.Log.LogWarning("Config language: " + ex.Message);
            }
        }

        private static readonly Dictionary<string, string> SectionsEn = new Dictionary<string, string>
        {
            { "1. Keys", "Keys" }, { "2. Riding", "Riding" }, { "3. Air", "Air" }, { "4. Grinds", "Grinds" },
            { "5. Bails", "Bails" }, { "6. Trampoline", "Trampoline" }, { "7. Camera", "Camera" }, { "8. Aura", "Aura" },
            { "9. HUD & Sound", "HUD & sound" }, { "10. Shop", "Shop" }, { "11. Skin", "Skin" }, { "12. Combat", "Board smack" },
            { "13. Video", "Video & comments" },
            { "14. Dev", "Developer" },
        };

        private static readonly Dictionary<string, string> DescEn = new Dictionary<string, string>
        {
            // keys
            { "ToggleSkateboard", "Get on / off the board" },
            { "Kickflip", "Kickflip (in the air). Again during the flip = double / triple" },
            { "Heelflip", "Heelflip (in the air)" },
            { "PopShoveIt", "Pop shove-it (in the air)" },
            { "TreFlip", "Tre flip (in the air)" },
            { "Impossible", "Impossible (in the air)" },
            { "ToggleHelp", "Show / hide the controls" },
            { "ThirdPerson", "First / third person view" },
            { "SettingsMenu", "Open the mod settings" },
            { "Records", "Hold to see your records (current save)" },
            { "Skitch", "Hold it on the board next to a monster or player (up to ~3 m): you grab on and get towed. Works in the air too. F in the air with nobody near = kickflip" },
            { "ThrowBoard", "Off the board: throw it ahead, run after it and hop on (don't put it on T, the game's emote key, or V, push-to-talk)" },
            { "Rocket", "Hold it on a board with the rocket booster: it burns fuel and pushes you way past the normal top speed (but you can barely turn)" },
            { "SkateTrain", "Skate train: hold it on the board with a player right in front of you (up to ~2 m) - both hands on their shoulders and you ride behind them. The next skater can hold on to you the same way" },
            // riding
            { "Steering", "Keyboard = turn with A/D, like in Skate 3. Mouse = you ride where you look" },
            { "CameraFollowsBoard", "The camera turns with the board when you don't move the mouse" },
            { "CameraFollowSpeed", "How fast the camera catches up with the board" },
            { "GoofyStance", "Stance: off = regular (left foot forward), on = goofy" },
            { "PushSpeed", "Top speed you can push up to (m/s)" },
            { "PushStrength", "How much speed one push gives (m/s)" },
            { "PushInterval", "Pause between pushes (s)" },
            { "MaxSpeed", "Top speed, e.g. down a hill (m/s)" },
            { "RollFriction", "How fast the board slows down by itself" },
            { "TurnSpeed", "Turning speed (degrees/s)" },
            { "RideHeight", "How much the board lifts you up" },
            { "BoardWear", "Board wear: bails, hard landings, grinds and smacks wear the board out. A worn board squeaks, gets slower and weaker, and in the end snaps in half" },
            { "WearRate", "How fast the board wears out (1 = about 10-12 hard bails until it snaps)" },
            { "BlockedEmotes", "Emotes you can't do on the board (animation names, comma separated). Default: Workout 2 and Backflip p1" },
            { "RideBoardScale", "Size of the board under your feet while riding" },
            // air
            { "OllieMin", "Power of a short ollie" },
            { "OllieMax", "Power of a fully charged ollie (hold jump)" },
            { "AirGravity", "Gravity in the air" },
            { "SpinSpeed", "Spin speed in the air (degrees/s)" },
            { "FlipSpeed", "Flip speed (1 = normal)" },
            { "SaltoSpeed", "Frontflip / backflip speed (degrees/s). Faster = easier to come round before landing" },
            // grinds
            { "Grinds", "Grinds on rails and edges (ollie onto the edge)" },
            { "Wallrides", "Wallride: ollie into a wall at an angle and ride along it (jump to get off)" },
            { "MonsterGrinds", "Monster grind: ollie down onto a monster and ride on its back (jump to get off)" },
            { "GrindSparks", "Sparks from under the board when you ride along a rail" },
            { "WheelTrails", "A glowing track behind the wheels in your aura rank's colour (a rainbow for AURA GOD)" },
            { "GrindMagnet", "How far the board gets pulled onto a rail (m)" },
            { "GrindFriction", "How fast you lose speed on a grind" },
            // bails
            { "Bails", "Bails. Off = crash into anything with no consequences" },
            { "TripSpeed", "Hit an obstacle at the wheels faster than this (m/s) and you fly over the board" },
            { "WallBailSpeed", "Hit a wall with your body faster than this (m/s) and you bail" },
            { "MaxLandingSpeed", "Land faster than this (m/s down) and your legs give up" },
            { "BailTime", "How many seconds you lie on the floor after a bail" },
            { "GetUpCooldown", "How many seconds after getting up you can't do tricks (so you don't bail again by accident)" },
            { "PickUpAfterBail", "After every bail the board flies off and you have to go and pick it up (E). Off = it comes back under your feet" },
            { "ExternalForces", "Black holes, shots, hits and explosions push you on the board and can knock you off it" },
            { "KnockOffStrength", "A hit harder than this (m/s) knocks you off the board. Lower = any push knocks you off" },
            { "PullOffStrength", "A pull stronger than this (e.g. a black hole) drags you off the board" },
            { "HitDamageBail", "Take this much damage at once and you fall off the board. 0 = damage doesn't knock you off" },
            { "BailDamage", "Damage when you bail at a normal speed (~8 m/s). Faster = more (up to x2.5), slow = less. 0 = no damage" },
            // trampoline / camera
            { "AutoBounce", "The trampoline bounces you up by itself when you land. Off = only if you jump" },
            { "Boost", "How much the trampoline boosts an ollie" },
            { "ThirdPersonDistance", "Third person camera distance" },
            { "ThirdPersonHeight", "Third person camera height" },
            // aura
            { "AuraPerSaveFile", "Aura is kept in the save file. Off = one aura for everything (for clients)" },
            { "AuraMultiplier", "Aura multiplier" },
            { "BailPenalty", "How much aura you lose on a bail" },
            { "MonsterBonkAura", "Aura for crashing into a monster at speed" },
            { "EscapeAura", "Aura for a getaway: a monster chased you and you rode away from it" },
            { "AuraGlow", "A glow in your rank's colour around you, grows with the combo (seen in third person, F5)" },
            { "ShowRanksAboveHeads", "Show the aura rank above the heads of other players with the mod" },
            { "ShareMyRank", "Other players with the mod see your rank above your head" },
            // HUD & sound
            { "ShowHUD", "Show the mod's HUD" },
            { "ShowSpeed", "Show the speed" },
            { "ShowChaseHUD", "Hints while a monster chases you: \"IT'S CHASING YOU\", the escape timer, \"TOO SLOW - PUSH!\"" },
            { "HudScale", "HUD size" },
            { "Volume", "Board sound volume" },
            { "PushVolume", "Push sound volume (separate from the other sounds)" },
            { "PerformanceMode", "Low-end PC mode: no glowing wheel tracks or aura glow, no board shadows, fewer sparks and less fire, no rocket smoke, simpler explosions. Riding and tricks work the same" },
            { "TalkDuck", "While you talk the board sounds go down to this volume, so your voice can be heard on the camera recording. 1 = don't go down" },
            { "Language", "Language of the whole mod: this menu, the on-screen texts, tips, records, the settings file, item names, the words above a smacked monster. Auto = same as the game's menus. (Trick and rank names stay English)" },
            { "CommentsLanguage", "Language of the viewer comments under your videos. Auto = same as the game's menus. Each player sees comments in their own choice" },
            { "ConfigVersion", "(internal)" },
            { "TutorialDone", "The first-ride tips were already shown (false = show them again)" },
            { "TotalAura", "(old, moved to cwskate_progress.txt)" },
            { "BestCombo", "(old, moved to cwskate_progress.txt)" },
            { "OwnsSkateboard", "(old, moved to cwskate_progress.txt)" },
            // combat
            { "SwingEnabled", "LMB with the board in your hands = smack with it" },
            { "SwingCooldown", "Pause between smacks (s)" },
            { "SwingRange", "Smack range (m)" },
            { "KnockForce", "How hard whoever you hit gets knocked back" },
            { "KnockdownTime", "How many seconds whoever you hit stays down" },
            { "HitPlayers", "You can hit other players. Off = monsters only" },
            { "PlayerHitDamage", "Damage to a player per hit (0 = no damage, they just fall)" },
            { "MonsterHitAura", "Aura for smacking a monster (the same one at most once every 5 s)" },
            { "AuraGodSmack", "At the top aura rank (AURA GOD) a smack sends the monster flying and you dance" },
            { "DevTools", "The Developer tab in this menu: give items, start a sponsor deal, aura, repair the board and so on. For testing the mod" },
            { "ShowDebugInfo", "Debug info on screen: speed, state, combo, wear, fuel, the deal" },
            { "InfiniteRocketFuel", "The rocket never runs out of fuel" },
            { "UnbreakableBoard", "The board never breaks: no wear" },
            { "PerfectLandings", "Every landing works (unfinished flip, sideways, height - doesn't matter)" },
            { "SuperJump", "Ollies are 1.8x stronger" },
            { "DoubleSpeed", "Pushing speed and top speed x2" },
            { "LowGravity", "Low gravity in the air - long flights for tricks" },
            { "BodySlopeLean", "How much the body leans with the board on slopes (0 = always upright, 1 = exactly like the board, above 1 = more than the board)" },
            { "Difficulty", "Difficulty. Easy: the board hops over bumps, curbs and steps by itself, walls only knock you off a bit above pushing speed, landings forgive mistakes (sideways - only fully sideways fails, an unfinished flip, height). Normal: hops over bumps only up to pushing speed (faster - a curb throws you), walls knock you off above 5 m/s, flips and drops a bit more forgiving than Hard. Hard: like Skate 3 - all of that throws you off" },
            { "RocketCanExplode", "The rocket on your board can blow up if you crash while it's burning" },
            { "ExplosiveFuel", "Rocket fuel and rocket boosters lying on the floor blow up when you smack them with the board or when they hit something hard (and the ones next to them go off too)" },
            { "HumbleOnDeath", "If the monster you smacked at AURA GOD kills you later (within 3 minutes), you lose all your aura and the comments call you humbled" },
            // video
            { "SkateContent", "Tricks caught on camera give views and viewer comments about skating" },
            { "ContentMultiplier", "View multiplier for skating on video" },
            { "ContentBonuses", "View bonuses: hard tricks, height and length, monsters nearby, selfies, skating together, low health, aura rank" },
            // skin
            { "Skin", "The board's graphic. The pictures are in the SkateSkins folder next to the mod, you can add your own" },
        };
    }
}
