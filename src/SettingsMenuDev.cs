using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using Zorro.Core;

namespace CWSkate
{
    // The developer tab (F8, only with DevTools switched on in "HUD & sound"): cheats and shortcuts for testing the mod.
    internal static partial class SettingsMenu
    {
        private static int devDifficulty = 1; // index into DevDifficulties
        private static string[] devDealNames;
        private static readonly DIFFICULTY[] DevDifficulties = { DIFFICULTY.easy, DIFFICULTY.medium, DIFFICULTY.hard, DIFFICULTY.veryHard };
        private static readonly string[] DevDifficultyNamesRu = { "Лёгкая", "Средняя", "Сложная", "Очень сложная" };
        private static readonly string[] DevDifficultyNamesEn = { "Easy", "Medium", "Hard", "Very hard" };
        private static readonly string[] DevDifficultyNamesFr = { "Facile", "Moyen", "Difficile", "Très difficile" };
        private static readonly string[] DevDifficultyNamesZh = { "简单", "中等", "困难", "非常困难" };

        // the tab appears / disappears right away when DevTools is switched
        internal static void Recollect()
        {
            if (Open) Collect();
        }

        private static bool DevButton(string ru, string en, float s, float width = 230f)
        {
            return GUILayout.Button(Lang.T(ru, en), buttonStyle, GUILayout.Width(width * s), GUILayout.Height(28 * s));
        }

        private static void DevHeader(string ru, string en, float s)
        {
            GUILayout.Space(12 * s);
            GUILayout.Label(Lang.T(ru, en), headerStyle);
        }

        private static void DrawDevTools(float s)
        {
            var p = Player.localPlayer;
            if (p == null) { GUILayout.Label(Lang.T("Сначала зайди в игру.", "Get into the game first."), descStyle); return; }

            // ---- items
            DevHeader("Выдать предмет (появится перед тобой)", "Give an item (appears in front of you)", s);
            GUILayout.BeginHorizontal();
            if (DevButton("Скейтборд", "Skateboard", s)) DevTools.Give(SkateItem.Item);
            if (DevButton("Доска Ride to Hell", "Ride to Hell board", s)) DevTools.Give(SkateItem.ProItem);
            if (DevButton("Страховой полис", "Insurance policy", s)) DevTools.Give(SponsorItems.Policy);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            int col = 0;
            foreach (var d in SkateParts.Defs)
            {
                if (DevButton(d.NameRu, d.NameEn, s)) DevTools.Give(d.Item);
                if (++col % 3 == 0) { GUILayout.FlexibleSpace(); GUILayout.EndHorizontal(); GUILayout.BeginHorizontal(); }
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            // ---- sponsor deals
            DevHeader("Скейт-спонсоры: запустить контракт", "Skate sponsors: start a deal", s);
            GUILayout.BeginHorizontal();
            GUILayout.Label(Lang.T("Сложность:", "Difficulty:"), labelStyle, GUILayout.Width(120 * s));
            if (GUILayout.Button(Lang.Pick(DevDifficultyNamesRu, DevDifficultyNamesEn, DevDifficultyNamesFr, DevDifficultyNamesZh)[devDifficulty], buttonStyle, GUILayout.Width(200 * s), GUILayout.Height(28 * s)))
                devDifficulty = (devDifficulty + 1) % DevDifficulties.Length;
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            col = 0;
            if (devDealNames == null)
            {
                devDealNames = new string[DevTools.Deals.Length];
                for (int i = 0; i < devDealNames.Length; i++) devDealNames[i] = ((NetworkDealBase)Activator.CreateInstance(DevTools.Deals[i])).DealName;
            }
            for (int i = 0; i < DevTools.Deals.Length; i++)
            {
                if (GUILayout.Button(devDealNames[i], buttonStyle, GUILayout.Width(230 * s), GUILayout.Height(28 * s)))
                    DevTools.StartDeal(DevTools.Deals[i], DevDifficulties[devDifficulty]);
                if (++col % 3 == 0) { GUILayout.FlexibleSpace(); GUILayout.EndHorizontal(); GUILayout.BeginHorizontal(); }
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.Label(DevTools.ActiveDealText(), descStyle);
            GUILayout.BeginHorizontal();
            if (DevButton("Выполнить текущий контракт", "Complete the current deal", s, 300f)) DevTools.CompleteDeal();
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            // ---- aura
            DevHeader("Аура", "Aura", s);
            GUILayout.BeginHorizontal();
            if (DevButton("+10 000 ауры", "+10,000 aura", s)) Progress.Aura += 10000;
            if (DevButton("Сразу AURA GOD", "AURA GOD now", s)) Progress.Aura = Math.Max(Progress.Aura, 1500000);
            if (DevButton("Аура = 0", "Aura = 0", s)) Progress.Aura = 0;
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            // ---- the board
            DevHeader("Доска в инвентаре", "The board in your inventory", s);
            GUILayout.BeginHorizontal();
            if (DevButton("Починить (100%)", "Repair (100%)", s)) DevTools.SetWear(1f);
            if (DevButton("Почти сломать (10%)", "Nearly broken (10%)", s)) DevTools.SetWear(0.1f);
            if (DevButton("Полный бак ракеты", "Fill the rocket", s)) DevTools.FillFuel();
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (DevButton("Снять все детали", "Remove all parts", s)) DevTools.ClearParts();
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            // ---- the mod itself
            DevHeader("Мод", "The mod", s);
            GUILayout.BeginHorizontal();
            if (DevButton("Подсказки ещё раз", "Show the tips again", s))
            {
                SkatePlugin.TutorialDone.Value = false;
                SkateHUD.Popup(Lang.T("Подсказки появятся, когда залезешь на доску", "The tips show up when you get on the board"), new Color(0.6f, 0.9f, 1f), 2f, small: true);
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }
    }

    internal static class DevTools
    {
        public static readonly Type[] Deals = { typeof(RideToHellDeal), typeof(GrindKingsDeal), typeof(MonsterWheelsDeal), typeof(BailInsuranceDeal), typeof(BoomRocketsDeal) };

        // ---- the developer switches (only work while DevTools is on)
        private static bool On(BepInEx.Configuration.ConfigEntry<bool> e) => SkatePlugin.DevTools.Value && e.Value;
        public static bool NoWear => On(SkatePlugin.DevNoWear);
        public static bool PerfectLandings => On(SkatePlugin.DevPerfectLandings);
        public static float JumpMul => On(SkatePlugin.DevSuperJump) ? 1.8f : 1f;
        public static float SpeedMul => On(SkatePlugin.DevDoubleSpeed) ? 2f : 1f;
        public static float AirGravity => SkatePlugin.AirGravity.Value * (On(SkatePlugin.DevLowGravity) ? 0.3f : 1f);

        // (extra = added after the translated text: a name, an error message)
        private static void Say(string ru, string en, bool bad = false, string extra = "") =>
            SkateHUD.Popup(Lang.T(ru, en) + extra, bad ? new Color(1f, 0.6f, 0.5f) : new Color(0.6f, 0.9f, 1f), 2f, small: true);

        // drops the item in front of you (the host's game makes it, like any dropped item)
        public static void Give(Item it)
        {
            var p = Player.localPlayer;
            if (p == null) return;
            if (it == null) { Say("Предмет ещё не зарегистрирован", "That item isn't registered yet", true); return; }
            Vector3 look = p.data.lookDirection;
            Vector3 flat = new Vector3(look.x, 0f, look.z);
            if (flat.sqrMagnitude < 1e-4f) flat = Vector3.forward;
            Vector3 at = p.Center() + flat.normalized * 1f + Vector3.up * 0.3f;
            p.RequestCreatePickup(it, new ItemInstanceData(Guid.NewGuid()), at, Quaternion.LookRotation(flat.normalized));
        }

        // starts a skate deal the same way the "accept" button on the monitor does (the sponsor's gift comes by drone)
        public static void StartDeal(Type t, DIFFICULTY dif)
        {
            try
            {
                if (NetworkDealBoss.me == null) { Say("Контракты работают только в доме", "Deals only work in the house", true); return; }
                var deal = (NetworkDealBase)Activator.CreateInstance(t);
                var allowed = deal.AllowedDifficulties;
                if (allowed != null && allowed.Count > 0 && !allowed.Contains(dif)) dif = allowed[allowed.Count - 1];
                deal.Init(SingletonAsset<DealRewardDatabase>.Instance.GetRandom(), dif);
                NetworkDealBoss.me.AddDeal(deal);
                Say("Контракт запущен: ", "Deal started: ", false, deal.DealName);
            }
            catch (Exception e) { SkatePlugin.Log.LogWarning("Dev deal: " + e); Say("Не вышло: ", "Failed: ", true, e.Message); }
        }

        public static void CompleteDeal()
        {
            try
            {
                if (NetworkDealBoss.activeDeal == null) { Say("Нет активного контракта", "No active deal", true); return; }
                NetworkDealBoss.CompleteDealAndClaimReward();
                Say("Контракт выполнен", "Deal completed");
            }
            catch (Exception e) { SkatePlugin.Log.LogWarning("Dev complete deal: " + e); Say("Не вышло: ", "Failed: ", true, e.Message); }
        }

        public static string ActiveDealText()
        {
            try
            {
                var d = NetworkDealBoss.activeDeal;
                if (d == null) return Lang.T("Активного контракта нет.", "No active deal.");
                return Lang.T("Сейчас: ", "Now: ") + d.DealName + "  " + d.ProgressInt + " / " + d.RequiredAmount() + "  (" + d.State + ")";
            }
            catch { return ""; }
        }

        public static void SetWear(float left)
        {
            var e = BoardWear.Entry(BoardWear.LocalData(), true);
            if (e == null) { Say("Нет доски в инвентаре", "No board in your inventory", true); return; }
            e.m_lifeTimeLeft = e.m_maxLifeTime * left;
        }

        public static void FillFuel()
        {
            var data = BoardWear.LocalData();
            if (data == null) { Say("Нет доски в инвентаре", "No board in your inventory", true); return; }
            BoardUpgrades.SetFuel(data, BoardUpgrades.FullTank);
        }

        public static void ClearParts()
        {
            var data = BoardWear.LocalData();
            if (data == null) { Say("Нет доски в инвентаре", "No board in your inventory", true); return; }
            BoardUpgrades.Set(data, 0);
        }
    }
}
