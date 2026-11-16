using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace CWSkate
{
    // In-game settings window (F8). Every option of the config file, grouped by section.
    internal static partial class SettingsMenu
    {
        public static bool Open { get; private set; }

        private static Rect window;
        private static Vector2 scroll;
        private static int tab;
        private static List<string> sections;
        private static Dictionary<string, List<ConfigEntryBase>> entries;
        private static ConfigEntryBase waitingKey;
        private static float resetAuraArmed, resetAllArmed;
        private static GUIStyle winStyle, labelStyle, descStyle, headerStyle, buttonStyle, tabStyle, tabOnStyle;
        private static Texture2D bgTex, tabTex, tabOnTex, btnTex;

        private static readonly Dictionary<string, string> SectionNames = new Dictionary<string, string>
        {
            { "1. Keys", "Клавиши" }, { "2. Riding", "Езда" }, { "3. Air", "Прыжки" }, { "4. Grinds", "Гринды" },
            { "5. Bails", "Падения" }, { "6. Trampoline", "Батут" }, { "7. Camera", "Камера" }, { "8. Aura", "Аура" },
            { "9. HUD & Sound", "Интерфейс и звук" }, { "10. Shop", "Магазин" }, { "11. Skin", "Скин" }, { "12. Combat", "Удар доской" }, { "13. Video", "Видео и комменты" },
            { "14. Dev", "Разработчик" },
        };

        public static void Tick()
        {
            if (Input.GetKeyDown(SkatePlugin.MenuKey.Value) || (Open && waitingKey == null && Input.GetKeyDown(KeyCode.Escape)))
                SetOpen(!Open);
        }

        private static void SetOpen(bool open)
        {
            if (open == Open) return;
            Open = open;
            waitingKey = null;
            var cfg = SkatePlugin.Cfg;
            if (open)
            {
                cfg.SaveOnConfigSet = false;
                Collect();
            }
            else
            {
                cfg.SaveOnConfigSet = true;
                cfg.Save();
            }
        }

        private static void Collect()
        {
            entries = new Dictionary<string, List<ConfigEntryBase>>();
            foreach (var kv in SkatePlugin.Cfg)
            {
                string sec = kv.Key.Section;
                if (!SectionNames.ContainsKey(sec)) continue; // hides the old "Progress" section
                if (sec == "14. Dev" && !SkatePlugin.DevTools.Value) continue; // the developer tab only when it's switched on
                if (!entries.TryGetValue(sec, out var list)) entries[sec] = list = new List<ConfigEntryBase>();
                list.Add(kv.Value);
            }
            sections = entries.Keys.OrderBy(k => int.TryParse(k.Split('.')[0], out int n) ? n : 99).ToList();
            tab = Mathf.Clamp(tab, 0, Mathf.Max(0, sections.Count - 1));
        }

        private static Texture2D Tex(Color c)
        {
            var t = new Texture2D(1, 1);
            t.SetPixel(0, 0, c);
            t.Apply();
            t.hideFlags = HideFlags.DontUnloadUnusedAsset;
            return t;
        }

        private static void Styles(float s)
        {
            if (winStyle == null)
            {
                bgTex = Tex(new Color(0.07f, 0.06f, 0.09f, 0.96f));
                tabTex = Tex(new Color(0.18f, 0.16f, 0.22f, 1f));
                tabOnTex = Tex(new Color(0.95f, 0.3f, 0.6f, 1f));
                btnTex = Tex(new Color(0.25f, 0.22f, 0.3f, 1f));
                winStyle = new GUIStyle(GUI.skin.window);
                winStyle.normal.background = bgTex;
                winStyle.onNormal.background = bgTex;
                winStyle.normal.textColor = new Color(1f, 0.85f, 0.3f);
                winStyle.onNormal.textColor = new Color(1f, 0.85f, 0.3f);
                winStyle.fontStyle = FontStyle.Bold;
                labelStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
                labelStyle.normal.textColor = Color.white;
                descStyle = new GUIStyle(GUI.skin.label) { wordWrap = true };
                descStyle.normal.textColor = new Color(0.75f, 0.75f, 0.8f);
                headerStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
                headerStyle.normal.textColor = new Color(1f, 0.85f, 0.3f);
                buttonStyle = new GUIStyle(GUI.skin.button);
                buttonStyle.normal.background = btnTex;
                buttonStyle.hover.background = tabTex;
                buttonStyle.normal.textColor = Color.white;
                buttonStyle.hover.textColor = Color.white;
                tabStyle = new GUIStyle(buttonStyle);
                tabStyle.normal.background = tabTex;
                tabOnStyle = new GUIStyle(buttonStyle);
                tabOnStyle.normal.background = tabOnTex;
                tabOnStyle.hover.background = tabOnTex;
                tabOnStyle.fontStyle = FontStyle.Bold;
            }
            int fs = Mathf.RoundToInt(16 * s);
            winStyle.fontSize = Mathf.RoundToInt(20 * s);
            labelStyle.fontSize = fs;
            descStyle.fontSize = Mathf.RoundToInt(13 * s);
            headerStyle.fontSize = Mathf.RoundToInt(18 * s);
            buttonStyle.fontSize = fs;
            tabStyle.fontSize = Mathf.RoundToInt(14 * s);
            tabOnStyle.fontSize = Mathf.RoundToInt(14 * s);
        }

        public static void Draw()
        {
            if (!Open) return;
            if (entries == null) Collect();
            float s = Mathf.Clamp(Screen.height / 1080f, 0.6f, 2f);
            Styles(s);

            // waiting for a key to rebind
            if (waitingKey != null && Event.current.type == EventType.KeyDown && Event.current.keyCode != KeyCode.None)
            {
                if (Event.current.keyCode != KeyCode.Escape) waitingKey.BoxedValue = Event.current.keyCode;
                waitingKey = null;
                Event.current.Use();
            }

            float w = Mathf.Min(900 * s, Screen.width - 40), h = Mathf.Min(760 * s, Screen.height - 40);
            if (window.width < 10f || window.width != w) window = new Rect((Screen.width - w) / 2f, (Screen.height - h) / 2f, w, h);
            ShowCursor();
            window = GUI.Window(0x5ca7e, window, DrawWindow, Lang.T("CW SKATE — НАСТРОЙКИ", "CW SKATE — SETTINGS"), winStyle);
        }

        public static void ShowCursor()
        {
            if (!Open) return;
            if (Cursor.lockState != CursorLockMode.None) Cursor.lockState = CursorLockMode.None;
            if (!Cursor.visible) Cursor.visible = true;
        }

        private static void DrawWindow(int id)
        {
            float s = Mathf.Clamp(Screen.height / 1080f, 0.6f, 2f);
            GUILayout.Space(8 * s);

            // tabs
            int perRow = 5;
            for (int row = 0; row * perRow < sections.Count; row++)
            {
                GUILayout.BeginHorizontal();
                for (int i = row * perRow; i < Mathf.Min(sections.Count, row * perRow + perRow); i++)
                {
                    string name = Lang.Section(sections[i], SectionNames.TryGetValue(sections[i], out var n) ? n : sections[i]);
                    if (GUILayout.Button(name, i == tab ? tabOnStyle : tabStyle, GUILayout.Height(32 * s)))
                    {
                        tab = i;
                        scroll = Vector2.zero;
                        waitingKey = null;
                    }
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.Space(8 * s);

            scroll = GUILayout.BeginScrollView(scroll);
            if (sections.Count > 0)
            {
                string sec = sections[tab];
                foreach (var e in entries[sec])
                    DrawEntry(e, s);
                if (sec == "8. Aura") DrawAuraTools(s);
                if (sec == "14. Dev") DrawDevTools(s);
            }
            GUILayout.EndScrollView();

            GUILayout.Space(6 * s);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(Lang.T("Сбросить вкладку", "Reset tab"), buttonStyle, GUILayout.Height(34 * s)))
                foreach (var e in entries[sections[tab]]) e.BoxedValue = e.DefaultValue;
            bool allArmed = Time.unscaledTime < resetAllArmed;
            if (GUILayout.Button(allArmed ? Lang.T("Точно сбросить всё?", "Really reset everything?") : Lang.T("Сбросить всё", "Reset all"), allArmed ? tabOnStyle : buttonStyle, GUILayout.Height(34 * s)))
            {
                if (allArmed)
                {
                    foreach (var list in entries.Values) foreach (var e in list) e.BoxedValue = e.DefaultValue;
                    resetAllArmed = 0f;
                }
                else resetAllArmed = Time.unscaledTime + 3f;
            }
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(Lang.T("Закрыть", "Close"), buttonStyle, GUILayout.Width(160 * s), GUILayout.Height(34 * s)))
                SetOpen(false);
            GUILayout.EndHorizontal();

            GUI.DragWindow(new Rect(0, 0, 10000, 30 * s));
        }

        private static void DrawSkinPicker(float s)
        {
            GUILayout.BeginVertical();
            GUILayout.Label(Lang.T("Скин доски", "Board skin"), labelStyle);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("◀", buttonStyle, GUILayout.Width(44 * s), GUILayout.Height(30 * s))) Skins.Cycle(-1);
            GUILayout.Label(SkatePlugin.Skin.Value, headerStyle, GUILayout.Width(300 * s), GUILayout.Height(30 * s));
            if (GUILayout.Button("▶", buttonStyle, GUILayout.Width(44 * s), GUILayout.Height(30 * s))) Skins.Cycle(1);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.Space(4 * s);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(Lang.T("Открыть папку со скинами", "Open the skins folder"), buttonStyle, GUILayout.Width(280 * s), GUILayout.Height(30 * s))) Skins.OpenFolder();
            if (GUILayout.Button(Lang.T("Обновить", "Refresh"), buttonStyle, GUILayout.Width(160 * s), GUILayout.Height(30 * s))) Skins.Refresh();
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.Label(Lang.F(
                "Найдено скинов: {0}. Свой скин: скопируй default.png в папке SkateSkins, " +
                "переименуй и нарисуй что хочешь (верх картинки = нос доски), потом нажми «Обновить». " +
                "Верх доски — файл имя_grip.png. Подробнее — в README.txt в той же папке. " +
                "Игроки с модом видят твой скин.",
                "Skins found: {0}. Your own skin: copy default.png in the SkateSkins folder, " +
                "rename it and draw whatever you like (top of the picture = nose of the board), then press Refresh. " +
                "The top of the board is name_grip.png. More in README_EN.txt in the same folder. " +
                "Players with the mod see your skin.", Skins.Available.Count), descStyle);
            GUILayout.EndVertical();
        }

        private static void DrawEntry(ConfigEntryBase e, float s)
        {
            if (e == SkatePlugin.Skin) { DrawSkinPicker(s); return; }
            GUILayout.BeginVertical();
            GUILayout.BeginHorizontal();
            GUILayout.Label(e.Definition.Key, labelStyle, GUILayout.Width(250 * s));

            var type = e.SettingType;
            var range = e.Description.AcceptableValues;
            if (type == typeof(bool))
            {
                bool v = (bool)e.BoxedValue;
                if (GUILayout.Button(v ? Lang.T("ВКЛ", "ON") : Lang.T("ВЫКЛ", "OFF"), v ? tabOnStyle : buttonStyle, GUILayout.Width(120 * s), GUILayout.Height(26 * s)))
                    e.BoxedValue = !v;
                GUILayout.FlexibleSpace();
            }
            else if (type == typeof(float))
            {
                float v = (float)e.BoxedValue;
                float min = 0f, max = 10f;
                if (range is AcceptableValueRange<float> r) { min = r.MinValue; max = r.MaxValue; }
                float nv = GUILayout.HorizontalSlider(v, min, max, GUILayout.MinWidth(200 * s));
                GUILayout.Label(v.ToString(max - min > 50f ? "0" : "0.00"), labelStyle, GUILayout.Width(70 * s));
                if (Mathf.Abs(nv - v) > 1e-5f) e.BoxedValue = (float)Math.Round(nv, max - min > 50f ? 0 : 2);
            }
            else if (type == typeof(int))
            {
                int v = (int)e.BoxedValue;
                int min = 0, max = 1000;
                if (range is AcceptableValueRange<int> r) { min = r.MinValue; max = r.MaxValue; }
                int nv = Mathf.RoundToInt(GUILayout.HorizontalSlider(v, min, max, GUILayout.MinWidth(200 * s)));
                GUILayout.Label(v.ToString(), labelStyle, GUILayout.Width(70 * s));
                if (nv != v) e.BoxedValue = nv;
            }
            else if (type == typeof(KeyCode))
            {
                string label = waitingKey == e ? Lang.T("нажми клавишу...", "press a key...") : e.BoxedValue.ToString();
                if (GUILayout.Button(label, waitingKey == e ? tabOnStyle : buttonStyle, GUILayout.Width(200 * s), GUILayout.Height(26 * s)))
                    waitingKey = waitingKey == e ? null : e;
                GUILayout.FlexibleSpace();
            }
            else if (type.IsEnum)
            {
                if (GUILayout.Button(Lang.Value(e.BoxedValue), buttonStyle, GUILayout.Width(240 * s), GUILayout.Height(26 * s)))
                {
                    var vals = Enum.GetValues(type);
                    int idx = Array.IndexOf(vals, e.BoxedValue);
                    e.BoxedValue = vals.GetValue((idx + 1) % vals.Length);
                }
                GUILayout.FlexibleSpace();
            }
            else
            {
                GUILayout.Label(e.BoxedValue.ToString(), labelStyle);
                GUILayout.FlexibleSpace();
            }

            if (!Equals(e.BoxedValue, e.DefaultValue))
            {
                if (GUILayout.Button("↺", buttonStyle, GUILayout.Width(34 * s), GUILayout.Height(26 * s)))
                    e.BoxedValue = e.DefaultValue;
            }
            else GUILayout.Space(38 * s);
            GUILayout.EndHorizontal();
            GUILayout.Label(Lang.Desc(e), descStyle);
            GUILayout.Space(8 * s);
            GUILayout.EndVertical();
        }

        private static void DrawAuraTools(float s)
        {
            GUILayout.Space(10 * s);
            GUILayout.Label(Lang.T("Текущее сохранение: ", "Current save: ") + Progress.SaveKey, descStyle);
            GUILayout.Label(Lang.T("Аура: ", "Aura: ") + Progress.Aura.ToString("N0") + Lang.T("   Лучшее комбо: ", "   Best combo: ") + Progress.BestCombo.ToString("N0"), labelStyle);
            bool armed = Time.unscaledTime < resetAuraArmed;
            if (GUILayout.Button(armed ? Lang.T("Точно? Нажми ещё раз", "Sure? Press again") : Lang.T("Обнулить ауру (текущее сохранение)", "Reset aura (current save)"), armed ? tabOnStyle : buttonStyle, GUILayout.Width(360 * s), GUILayout.Height(30 * s)))
            {
                if (armed) { Progress.ResetCurrent(); resetAuraArmed = 0f; }
                else resetAuraArmed = Time.unscaledTime + 3f;
            }
        }

        // ------------------------------------------------------------ while open: free cursor, no game input

        [HarmonyPatch(typeof(GlobalInputHandler), nameof(GlobalInputHandler.CanTakeInput))]
        private static class Patch_CanTakeInput
        {
            private static void Postfix(ref bool __result)
            {
                if (Open) __result = false;
            }
        }

        // the game locks and hides the cursor every frame; while the menu is open it doesn't get to
        [HarmonyPatch(typeof(CursorHandler), "Update")]
        private static class Patch_Cursor
        {
            private static bool Prefix()
            {
                if (!Open) return true;
                ShowCursor();
                return false;
            }
        }
    }
}
