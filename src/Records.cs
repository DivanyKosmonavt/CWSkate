using UnityEngine;

namespace CWSkate
{
    // Hold Tab: your records for this save (best combos and stats).
    internal static class Records
    {
        private static Texture2D bg;

        public static void Draw()
        {
            if (Event.current.type != EventType.Repaint) return;
            if (SettingsMenu.Open || !Input.GetKey(SkatePlugin.RecordsKey.Value)) return;
            // nothing before you ever had a board
            if (!SkateController.Owns && Progress.Aura == 0 && Progress.TopCombos.Count == 0) return;
            var lp = Player.localPlayer;
            if (lp == null || lp.HasLockedInput()) return; // typing on a terminal etc.

            float s = Screen.height / 1080f * Mathf.Max(0.7f, SkatePlugin.HudScale.Value);
            float W = 820 * s, H = 720 * s;
            var r = new Rect((Screen.width - W) / 2f, (Screen.height - H) / 2f, W, H);
            if (bg == null)
            {
                bg = new Texture2D(1, 1) { hideFlags = HideFlags.DontUnloadUnusedAsset };
                bg.SetPixel(0, 0, new Color(0.05f, 0.04f, 0.08f, 0.88f));
                bg.Apply();
            }
            GUI.DrawTexture(r, bg);

            float x = r.x + 30 * s, y = r.y + 22 * s, w = W - 60 * s;
            var gold = new Color(1f, 0.85f, 0.3f);
            var dim = new Color(1f, 1f, 1f, 0.6f);
            SkateHUD.Text(new Rect(x, y, w, 44 * s), Lang.T("РЕКОРДЫ", "RECORDS"), (int)(34 * s), gold, TextAnchor.UpperLeft);
            SkateHUD.Text(new Rect(x, y + 8 * s, w, 30 * s), SkatePlugin.AuraPerSaveFile.Value ? Lang.T("это сохранение", "this save file") : Lang.T("все сохранения", "all saves"), (int)(17 * s), dim, TextAnchor.UpperRight);
            y += 52 * s;

            long aura = Progress.Aura;
            SkateHUD.Text(new Rect(x, y, w, 36 * s), Lang.T("АУРА ", "AURA ") + aura.ToString("N0") + "   " + Aura.RankName(aura), (int)(26 * s), Aura.RankColor(aura), TextAnchor.UpperLeft);
            y += 46 * s;

            SkateHUD.Text(new Rect(x, y, w, 28 * s), Lang.T("ЛУЧШИЕ КОМБО", "BEST COMBOS"), (int)(20 * s), gold, TextAnchor.UpperLeft);
            y += 30 * s;
            var top = Progress.TopCombos;
            if (top.Count == 0)
            {
                SkateHUD.Text(new Rect(x, y, w, 26 * s), Lang.T("Пока пусто. Сделай пару трюков!", "Nothing yet. Land some tricks!"), (int)(18 * s), dim, TextAnchor.UpperLeft);
                y += 30 * s;
            }
            for (int i = 0; i < top.Count; i++)
            {
                SkateHUD.Text(new Rect(x, y, 170 * s, 26 * s), (i + 1) + ".  " + top[i].Key.ToString("N0"), (int)(19 * s), Color.white, TextAnchor.UpperLeft);
                SkateHUD.Text(new Rect(x + 175 * s, y + 1 * s, w - 175 * s, 44 * s), top[i].Value, (int)(15 * s), dim, TextAnchor.UpperLeft);
                y += 44 * s;
            }
            y += 10 * s;

            SkateHUD.Text(new Rect(x, y, w, 28 * s), Lang.T("СТАТИСТИКА", "STATS"), (int)(20 * s), gold, TextAnchor.UpperLeft);
            y += 32 * s;
            string[] left =
            {
                Lang.T("Комбо   ", "Combos landed   ") + Progress.Stat("combos").ToString("N0"),
                Lang.T("Трюков   ", "Tricks done   ") + Progress.Stat("tricks").ToString("N0"),
                Lang.T("Флипов   ", "Flips   ") + Progress.Stat("flips").ToString("N0"),
                Lang.T("Сальто   ", "Front / backflips   ") + Progress.Stat("saltos").ToString("N0"),
                Lang.T("Падений   ", "Bails   ") + Progress.Stat("bails").ToString("N0"),
                Lang.T("Врезаний в монстров   ", "Monster bonks   ") + Progress.Stat("bonks").ToString("N0"),
                Lang.T("Ударов по монстрам   ", "Monsters smacked   ") + Progress.Stat("smacks").ToString("N0"),
                Lang.T("Побегов от монстров   ", "Monster escapes   ") + Progress.Stat("escapes").ToString("N0"),
                Lang.T("Раз опустили   ", "Times humbled   ") + Progress.Stat("humbled").ToString("N0"),
                Lang.T("Танцев на доске   ", "Board dances   ") + Progress.Stat("dances").ToString("N0"),
            };
            string[] right =
            {
                Lang.T("Хиппи-джампов   ", "Hippie jumps   ") + Progress.Stat("hippies").ToString("N0"),
                Lang.T("Гриндов по монстрам   ", "Monster grinds   ") + Progress.Stat("monster_grinds").ToString("N0"),
                Lang.T("Паровозиков   ", "Skate trains   ") + Progress.Stat("trains").ToString("N0"),
                Lang.T("Сломано досок   ", "Boards broken   ") + Progress.Stat("boards_broken").ToString("N0"),
                Lang.T("Дольше всего в воздухе   ", "Longest air   ") + Seconds(Progress.Stat("air_ms")),
                Lang.T("Самый долгий гринд   ", "Longest grind   ") + Seconds(Progress.Stat("grind_ms")),
                Lang.T("Самый долгий мэнуал   ", "Longest manual   ") + Seconds(Progress.Stat("manual_ms")),
                Lang.T("Самое большое вращение   ", "Biggest spin   ") + Progress.Stat("spin") + "°",
                Lang.T("Самый долгий полёт на ракете   ", "Longest rocket ride   ") + Seconds(Progress.Stat("rocket_ms")),
                Lang.T("Проехано   ", "Distance skated   ") + Distance(Progress.Stat("dist")),
            };
            for (int i = 0; i < Mathf.Max(left.Length, right.Length); i++)
            {
                if (i < left.Length) SkateHUD.Text(new Rect(x, y, w / 2f, 26 * s), left[i], (int)(18 * s), Color.white, TextAnchor.UpperLeft);
                if (i < right.Length) SkateHUD.Text(new Rect(x + w / 2f, y, w / 2f, 26 * s), right[i], (int)(18 * s), Color.white, TextAnchor.UpperLeft);
                y += 28 * s;
            }
        }

        private static string Seconds(long ms) => (ms / 1000f).ToString("0.0") + Lang.T(" с", " s");

        private static string Distance(long m) => m >= 1000 ? (m / 1000f).ToString("0.0") + Lang.T(" км", " km") : m + Lang.T(" м", " m");
    }

    // Rank above the heads of other players who have the mod (and a board).
    internal static class RankTags
    {
        private const float MaxDistance = 35f;

        public static void Draw()
        {
            if (Event.current.type != EventType.Repaint || !SkatePlugin.ShowRanks.Value) return;
            var ph = PlayerHandler.instance;
            var mc = MainCamera.instance;
            if (ph == null || ph.players == null || mc == null || mc.Cam == null) return;
            var cam = mc.Cam;
            Vector3 camPos = cam.transform.position;
            int mask = HelperFunctions.GetMask(HelperFunctions.LayerType.TerrainProp);
            float s = Screen.height / 1080f * Mathf.Max(0.7f, SkatePlugin.HudScale.Value);

            foreach (var p in ph.players)
            {
                if (p == null || p.ai || p.IsLocal || p.data == null || p.data.dead || !p.data.playerSetUpAndReady) continue;
                long aura = NetSync.GetAura(p);
                if (aura < 0) continue;
                var head = p.refs.ragdoll.GetBodypart(BodypartType.Head);
                if (head == null) continue;
                Vector3 pos = head.transform.position + Vector3.up * 0.45f;
                float dist = Vector3.Distance(camPos, pos);
                if (dist > MaxDistance) continue;
                Vector3 sp = cam.WorldToScreenPoint(pos);
                if (sp.z <= 0f) continue;
                // hidden behind walls
                if (Physics.Linecast(camPos, pos, out RaycastHit hit, mask, QueryTriggerInteraction.Ignore)
                    && hit.collider.GetComponentInParent<Player>() != p) continue;

                float k = Mathf.Lerp(1f, 0.55f, dist / MaxDistance);
                float alpha = Mathf.Clamp01((MaxDistance - dist) / 6f);
                float y = Screen.height - sp.y;
                int big = Mathf.Max(10, (int)(24 * s * k)), small = Mathf.Max(9, (int)(15 * s * k));
                SkateHUD.Text(new Rect(sp.x - 200, y - big * 2.1f, 400, big * 1.4f), Aura.RankName(aura), big, Aura.RankColor(aura), TextAnchor.LowerCenter, alpha);
                SkateHUD.Text(new Rect(sp.x - 200, y - big * 0.75f, 400, small * 1.4f), aura.ToString("N0") + " aura", small, new Color(1f, 1f, 1f, 0.75f), TextAnchor.UpperCenter, alpha);
            }
        }
    }
}
