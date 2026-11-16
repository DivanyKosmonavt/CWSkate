using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace CWSkate
{
    internal static class SkateHUD
    {
        private class Pop
        {
            public string text;
            public Color color;
            public float born, life;
            public bool small;
        }

        private static readonly List<Pop> pops = new List<Pop>();
        private static GUIStyle style;

        public static void Popup(string text, Color color, float life, bool small = false)
        {
            pops.Add(new Pop { text = text, color = color, born = Time.time, life = life, small = small });
            if (pops.Count > 8) pops.RemoveAt(0);
        }

        internal static void Text(Rect r, string text, int size, Color color, TextAnchor anchor, float alpha = 1f)
        {
            if (style == null)
                style = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, richText = false, wordWrap = true, clipping = TextClipping.Overflow };
            style.fontSize = size;
            style.alignment = anchor;
            var shadow = new Color(0, 0, 0, 0.85f * alpha);
            float o = Mathf.Max(1f, size / 14f);
            style.normal.textColor = shadow;
            GUI.Label(new Rect(r.x + o, r.y + o, r.width, r.height), text, style);
            if (!Perf.Low) GUI.Label(new Rect(r.x - o * 0.5f, r.y + o, r.width, r.height), text, style); // (one shadow is enough on old PCs)
            color.a *= alpha;
            style.normal.textColor = color;
            GUI.Label(r, text, style);
        }

        public static void Draw(SkateController sc)
        {
            // nothing at all on screen until you start skating (popups only come from skating)
            if (!SkatePlugin.ShowHUD.Value) { pops.Clear(); return; }
            // OnGUI runs several times a frame (layout, input...): the HUD only needs drawing once
            if (Event.current.type != EventType.Repaint) return;
            float s = Screen.height / 1080f * SkatePlugin.HudScale.Value;
            float W = Screen.width, H = Screen.height;

            if (sc != null && sc.player != null && !sc.player.data.dead && sc.Riding)
                DrawRiding(sc, s, W, H);

            // developer tab: debug info in the top left corner
            if (sc != null && SkatePlugin.DevTools.Value && SkatePlugin.DevShowDebug.Value)
            {
                var r = new Rect(10 * s, 10 * s, 640 * s, 300 * s);
                GUI.color = new Color(0, 0, 0, 0.55f);
                GUI.DrawTexture(r, Texture2D.whiteTexture);
                GUI.color = Color.white;
                Text(new Rect(r.x + 8 * s, r.y + 6 * s, r.width - 16 * s, r.height - 12 * s), sc.DebugInfo(), (int)(15 * s), new Color(0.7f, 1f, 0.75f), TextAnchor.UpperLeft);
            }

            // the insurance policy just saved you: how long you stay untouchable
            if (SponsorItems.Invulnerable)
            {
                float left = SponsorItems.InvulnerableLeft;
                float pulse = left < 3f ? 0.55f + 0.45f * Mathf.Sin(Time.time * 10f) : 1f;
                Text(new Rect(0, H * 0.07f, W, 40 * s), Lang.T("НЕУЯЗВИМ  ", "INVULNERABLE  ") + Mathf.CeilToInt(left) + Lang.T(" с", "s"), (int)(30 * s), new Color(1f, 0.85f, 0.3f), TextAnchor.MiddleCenter, pulse);
            }

            // popups (rise and fade)
            for (int i = pops.Count - 1; i >= 0; i--)
                if (Time.time - pops[i].born > pops[i].life) pops.RemoveAt(i);
            float y = H * 0.30f;
            foreach (var p in pops)
            {
                float age = Time.time - p.born;
                float a = Mathf.Clamp01((p.life - age) / 0.5f);
                float scale = Mathf.Lerp(1.35f, 1f, Mathf.Clamp01(age / 0.15f));
                int size = (int)((p.small ? 26 : 44) * s * scale);
                Text(new Rect(0, y - age * 25 * s, W, size * 1.4f), p.text, size, p.color, TextAnchor.MiddleCenter, a);
                y += (p.small ? 34 : 56) * s;
            }
        }

        private static void DrawRiding(SkateController sc, float s, float W, float H)
        {
            long aura = Progress.Aura;
            Color rankColor = Aura.RankColor(aura);

            // aura counter (top right)
            float rw = 520 * s;
            Text(new Rect(W - rw - 24 * s, 18 * s, rw, 50 * s), Lang.T("АУРА ", "AURA ") + aura.ToString("N0"), (int)(40 * s), rankColor, TextAnchor.UpperRight);
            Text(new Rect(W - rw - 24 * s, 66 * s, rw, 34 * s), Aura.RankName(aura), (int)(26 * s), rankColor, TextAnchor.UpperRight);
            long next = Aura.NextThreshold(aura);
            string sub = next > 0 ? Lang.T("след. ранг с ", "next rank at ") + next.ToString("N0") : Lang.T("максимальный ранг", "max rank");
            sub += Lang.T("   |   лучшее комбо ", "   |   best combo ") + Progress.BestCombo.ToString("N0");
            Text(new Rect(W - rw - 24 * s, 100 * s, rw, 28 * s), sub, (int)(18 * s), new Color(1, 1, 1, 0.7f), TextAnchor.UpperRight);

            // speed + state (bottom left)
            string state = sc.MonsterGrinding ? "MONSTER GRIND" : sc.Skitching ? sc.SkitchName.ToUpperInvariant() : sc.Grinding ? sc.GrindName.ToUpperInvariant() : sc.Wallriding ? "WALLRIDE" : sc.Salto ? sc.SaltoLabel : sc.Hippie ? "HIPPIE JUMP" : sc.Airborne ? Lang.T("В ВОЗДУХЕ", "AIR") : sc.Dancing ? Lang.T("ТАНЕЦ НА ДОСКЕ", "BOARD DANCE") : sc.Escaping && SkatePlugin.ShowChaseHUD.Value ? Lang.T("ПОБЕГ!", "ESCAPE!") : sc.NoseManualing ? "NOSE MANUAL" : sc.Manualing ? "MANUAL" : sc.Kickturning ? "KICKTURN" : sc.Sliding ? "POWERSLIDE" : sc.Fakie ? "FAKIE" : Lang.T("ЕДЕШЬ", "ROLLING");
            if (SkatePlugin.ShowSpeed.Value)
                Text(new Rect(24 * s, H - 110 * s, 500 * s, 40 * s), Mathf.RoundToInt(sc.Speed * 3.6f) + Lang.T(" км/ч", " km/h"), (int)(32 * s), Color.white, TextAnchor.MiddleLeft);
            // board wear (once it's taken some hits)
            float health = BoardWear.Local;
            if (SkatePlugin.WearEnabled.Value && health < 0.95f)
            {
                Color hc = health > 0.5f ? Color.Lerp(new Color(1f, 0.85f, 0.3f), Color.white, (health - 0.5f) * 2f)
                                         : Color.Lerp(new Color(1f, 0.25f, 0.2f), new Color(1f, 0.85f, 0.3f), health * 2f);
                float blink = health < 0.2f ? 0.6f + 0.4f * Mathf.Sin(Time.time * 8f) : 1f;
                Text(new Rect(24 * s, H - 145 * s, 500 * s, 34 * s), Lang.T("ДОСКА ", "BOARD ") + Mathf.CeilToInt(health * 100f) + "%", (int)(22 * s), hc, TextAnchor.MiddleLeft, blink);
            }
            // the parts on the board
            string parts = BoardUpgrades.Describe(BoardUpgrades.Local);
            if (parts.Length > 0)
                Text(new Rect(24 * s, H - 172 * s, 700 * s, 28 * s), parts, (int)(17 * s), new Color(1f, 0.9f, 0.7f, 0.75f), TextAnchor.MiddleLeft);
            // the rocket's fuel (blinks when it's nearly empty)
            if (sc.HasRocket)
            {
                int fuel = sc.RocketFuel;
                float blink = fuel < 15 ? 0.55f + 0.45f * Mathf.Sin(Time.time * 9f) : 1f;
                Color fc = sc.RocketOn ? new Color(1f, 0.55f, 0.15f) : fuel <= 0 ? new Color(1f, 0.3f, 0.25f) : new Color(1f, 0.8f, 0.4f);
                Text(new Rect(24 * s, H - 199 * s, 700 * s, 28 * s), Lang.T("ТОПЛИВО РАКЕТЫ ", "ROCKET FUEL ") + fuel + "%   [" + SkatePlugin.RocketKey.Value + "]", (int)(19 * s), fc, TextAnchor.MiddleLeft, blink);
            }
            Text(new Rect(24 * s, H - 75 * s, 700 * s, 30 * s), state + "   [" + SkatePlugin.HelpKey.Value + Lang.T("] управление   [", "] controls   [") + SkatePlugin.MenuKey.Value + Lang.T("] настройки", "] settings"), (int)(18 * s), new Color(1, 1, 1, 0.65f), TextAnchor.MiddleLeft);

            // first-ride tips (top middle, on a dark plate)
            if (sc.TutorialTip != null)
            {
                float a = sc.TutorialAlpha;
                var plate = new Rect(W * 0.5f - 470 * s, H * 0.16f, 940 * s, 92 * s);
                GUI.color = new Color(0f, 0f, 0f, 0.6f * a);
                GUI.DrawTexture(plate, Texture2D.whiteTexture);
                GUI.color = Color.white;
                Text(new Rect(plate.x + 16 * s, plate.y + 6 * s, plate.width - 32 * s, 24 * s), Lang.T("СОВЕТ ", "TIP ") + sc.TutorialCounter, (int)(16 * s), new Color(1f, 0.85f, 0.3f), TextAnchor.UpperLeft, a);
                Text(new Rect(plate.x + 16 * s, plate.y + 28 * s, plate.width - 32 * s, 60 * s), sc.TutorialTip, (int)(22 * s), Color.white, TextAnchor.UpperLeft, a);
            }

            // chase (only with ShowChaseHUD on): how long you've been getting away
            if (SkatePlugin.ShowChaseHUD.Value && sc.Chased)
            {
                bool fast = sc.Speed > 5f || sc.Airborne;
                string line = fast ? Lang.T("ПОБЕГ ", "ESCAPE ") + sc.EscapeTime.ToString("0.0") + Lang.T(" с", "s") + (sc.EscapeTime >= 2f ? Lang.T("  - оторвись ради бонуса!", "  - lose it for the bonus!") : "")
                                   : Lang.T("СЛИШКОМ МЕДЛЕННО - ТОЛКАЙСЯ!", "TOO SLOW - PUSH!");
                Text(new Rect(0, H * 0.12f, W, 40 * s), line, (int)(28 * s), fast ? new Color(1f, 0.85f, 0.4f) : new Color(1f, 0.3f, 0.25f), TextAnchor.MiddleCenter);
            }

            // ollie charge bar
            if (sc.Charge > 0f)
            {
                var bg = new Rect(W * 0.5f - 100 * s, H * 0.62f, 200 * s, 10 * s);
                GUI.color = new Color(0, 0, 0, 0.5f);
                GUI.DrawTexture(bg, Texture2D.whiteTexture);
                GUI.color = Color.Lerp(new Color(0.6f, 0.9f, 1f), new Color(1f, 0.85f, 0.3f), sc.Charge);
                GUI.DrawTexture(new Rect(bg.x, bg.y, bg.width * sc.Charge, bg.height), Texture2D.whiteTexture);
                GUI.color = Color.white;
            }

            // current combo (bottom centre)
            var c = sc.combo;
            if (c.Active || sc.Manualing || sc.Grinding || sc.MonsterGrinding || sc.Skitching)
            {
                var sb = new StringBuilder();
                int start = Mathf.Max(0, c.Names.Count - 6);
                if (start > 0) sb.Append("... + ");
                for (int i = start; i < c.Names.Count; i++)
                {
                    if (i > start) sb.Append(" + ");
                    sb.Append(c.Names[i]);
                }
                void Live(string name, float t)
                {
                    if (sb.Length > 0) sb.Append(" + ");
                    sb.Append(name).Append(' ').Append(t.ToString("0.0")).Append('s');
                }
                if (sc.Manualing) Live(sc.NoseManualing ? "Nose Manual" : "Manual", sc.ManualTime);
                if (sc.Grinding) Live(sc.GrindName, sc.GrindTime);
                if (sc.MonsterGrinding) Live("Monster Grind", sc.MonsterGrindTime);
                if (sc.Skitching) Live(sc.SkitchName, sc.SkitchTime);
                Text(new Rect(W * 0.1f, H - 210 * s, W * 0.8f, 80 * s), sb.ToString(), (int)(28 * s), Color.white, TextAnchor.LowerCenter);
                string score = Mathf.RoundToInt(c.Points).ToString("N0") + "  x" + c.Multiplier;
                Color sc2 = Color.Lerp(new Color(1f, 0.9f, 0.5f), new Color(1f, 0.4f, 0.9f), Mathf.Clamp01(c.Count / 10f));
                Text(new Rect(0, H - 128 * s, W, 50 * s), score, (int)(42 * s), sc2, TextAnchor.MiddleCenter);
            }

            if (sc.ShowHelp) DrawHelp(s, H);
        }

        private static void DrawHelp(float s, float H)
        {
            string k(BepInEx.Configuration.ConfigEntry<KeyCode> e) => e.Value.ToString();
            bool kb = SkatePlugin.Steering.Value == SteeringMode.Keyboard;
            string help = Lang.Cur == Lng.Ru ?
                "УПРАВЛЕНИЕ СКЕЙТОМ\n" +
                "W  толчок (одно нажатие = один толчок, держи - толкаешься дальше)\n" +
                (kb ? "A / D  поворот, камера сама разворачивается за доской\n" : "Мышь  поворот\n") +
                "S  тормоз, на скорости = пауэрслайд\n" +
                "Держи ПРЫЖОК - присел, отпусти = олли (дольше держишь - выше)\n" +
                "ПРИСЕД (держать)  мэнуал,  ПРИСЕД + W  ноуз-мэнуал\n" +
                "ПРИСЕД + A / D на малой скорости  кикторн\n" +
                "ПРИСЕД + нажать ПРЫЖОК  бонлесс (выше олли)\n" +
                "S + нажать ПРЫЖОК  хиппи-джамп (прыгаешь, доска катится под тобой)\n" +
                "БЕГ сразу после приземления  ревёрт (разворот на 180)\n" +
                "Олли на перила или край = гринд, ПРЫЖОК - спрыгнуть\n" +
                "Олли в стену под углом = уоллрайд, ПРЫЖОК - спрыгнуть\n" +
                "Олли сверху на монстра = едешь на нём, ПРЫЖОК - спрыгнуть\n" +
                "Держи " + k(SkatePlugin.SkitchKey) + " рядом с монстром / игроком = скитчинг (он тебя тащит)\n" +
                "Держи " + k(SkatePlugin.TrainKey) + " прямо за игроком = паровозик (руки ему на плечи)\n" +
                "Летишь в стену: держи ПРИСЕД = уоллплант (оттолкнуться)\n" +
                "Съехать с уступа = дроп-ин (с ПРИСЕДОМ выдержишь высоту больше)\n" +
                "Без доски: " + k(SkatePlugin.ThrowKey) + " бросить её вперёд, догони - запрыгнешь\n" +
                "В воздухе:\n" +
                "  A / D  вращение (180, 360, 540...)\n" +
                "  W + ПРЫЖОК  сальто вперёд,  S + ПРЫЖОК  сальто назад  (ещё раз = двойное)\n" +
                "  " + k(SkatePlugin.KickflipKey) + " кикфлип   " + k(SkatePlugin.HeelflipKey) + " хилфлип   " + k(SkatePlugin.ShoveItKey) + " шов-ит\n" +
                "  " + k(SkatePlugin.TreFlipKey) + " тре флип   " + k(SkatePlugin.ImpossibleKey) + " импосибл  (ещё раз во время флипа = дабл)\n" +
                "  БЕГ  Indy grab  (+W нозграб, +S тейлграб, +ПРИСЕД мелон)\n" +
                "  БЕГ + ПРИСЕД + W  крейл,  БЕГ + ПРИСЕД + S  стейлфиш\n" +
                "  во время грэба: ПРЫЖОК = эйрволк,  W + ПРЫЖОК = суперман\n" +
                "  отпусти грэб до приземления!\n" +
                "Приземляйся ровно или задом наперёд (фейки).\n" +
                "Бордюры, ступеньки, стены сбивают - перепрыгивай их олли.\n" +
                (SkatePlugin.PickUpAfterBail.Value ? "После падения доска улетает: подойди и подбери на E.\n" : "") +
                "Доска - предмет: выбери её слот, чтобы держать, ЛКМ = ударить ей.\n" +
                "Детали из магазина (Misc): возьми в руки + ЛКМ = поставить на доску.\n" +
                "Ракета на доске: держи " + k(SkatePlugin.RocketKey) + " (канистры заливают топливо).\n" +
                "Врежься в монстра на скорости - получишь бонус ауры.\n" +
                "Монстр гонится? Уезжай быстро: аура и просмотры за побег.\n" +
                "Клавиша эмоций (T) на доске = танец прямо на ходу.\n" +
                (SkatePlugin.BailDamage.Value > 0 ? "Падения больно (на скорости - сильнее).\n" : "") +
                (SkatePlugin.WearEnabled.Value ? "Падения изнашивают доску: скрипит, тормозит, потом ломается.\n" : "") +
                "Батут: олли на нём - улетишь (держи ПРЫЖОК при приземлении - подбросит).\n" +
                "Трюки подряд = комбо с множителем, оно превращается в АУРУ.\n" +
                "Трюки на камеру дают просмотры и комменты.\n" +
                k(SkatePlugin.ToggleKey) + " слезть   " + k(SkatePlugin.ThirdPersonKey) + " камера   " +
                k(SkatePlugin.RecordsKey) + " рекорды   " + k(SkatePlugin.MenuKey) + " настройки"
                : Lang.Cur == Lng.Fr ?
                "COMMANDES DU SKATE\n" +
                "W  pousser (un appui = une poussée, maintiens pour continuer)\n" +
                (kb ? "A / D  tourner, la caméra suit la planche\n" : "Souris  diriger\n") +
                "S  freiner, en vitesse = powerslide\n" +
                "Maintiens SAUT pour t'accroupir, relâche = ollie (plus long = plus haut)\n" +
                "ACCROUPI (maintenir)  manual,  ACCROUPI + W  nose manual\n" +
                "ACCROUPI + A / D à petite vitesse  kickturn\n" +
                "ACCROUPI + appuie sur SAUT  boneless (plus haut qu'un ollie)\n" +
                "S + appuie sur SAUT  hippie jump (tu sautes, la planche roule sous toi)\n" +
                "SPRINT juste après l'atterrissage  revert (180 au sol)\n" +
                "Ollie sur une rampe ou un rebord = grind, SAUT pour sortir\n" +
                "Ollie dans un mur en biais = wallride, SAUT pour sauter\n" +
                "Ollie sur un monstre = tu rides sur son dos, SAUT pour descendre\n" +
                "Maintiens " + k(SkatePlugin.SkitchKey) + " près d'un monstre / joueur = skitch (il te tracte)\n" +
                "Maintiens " + k(SkatePlugin.TrainKey) + " juste derrière un joueur = petit train (mains sur ses épaules)\n" +
                "Tu fonces sur un mur : maintiens ACCROUPI = wallplant (repousse-toi)\n" +
                "Descends d'un rebord = drop in (avec ACCROUPI tu encaisses plus de hauteur)\n" +
                "Sans planche : " + k(SkatePlugin.ThrowKey) + " la lance devant toi, rattrape-la pour sauter dessus\n" +
                "En l'air :\n" +
                "  A / D  rotation (180, 360, 540...)\n" +
                "  W + SAUT  frontflip,  S + SAUT  backflip  (encore une fois = double)\n" +
                "  " + k(SkatePlugin.KickflipKey) + " kickflip   " + k(SkatePlugin.HeelflipKey) + " heelflip   " + k(SkatePlugin.ShoveItKey) + " shove-it\n" +
                "  " + k(SkatePlugin.TreFlipKey) + " tre flip   " + k(SkatePlugin.ImpossibleKey) + " impossible  (encore pendant le flip = double)\n" +
                "  SPRINT  Indy grab  (+W nosegrab, +S tailgrab, +ACCROUPI melon)\n" +
                "  SPRINT + ACCROUPI + W  crail,  SPRINT + ACCROUPI + S  stalefish\n" +
                "  pendant un grab : SAUT = airwalk,  W + SAUT = superman\n" +
                "  lâche le grab avant d'atterrir !\n" +
                "Atterris droit ou complètement à l'envers (fakie).\n" +
                "Trottoirs, marches et murs te font tomber - passe-les en ollie.\n" +
                (SkatePlugin.PickUpAfterBail.Value ? "Après une chute la planche s'envole : va la ramasser avec E.\n" : "") +
                "La planche est un objet : choisis son emplacement pour la tenir, clic gauche = frapper avec.\n" +
                "Pièces du magasin (Misc) : prends-en une en main + clic gauche = la monter sur ta planche.\n" +
                "Fusée sur la planche : maintiens " + k(SkatePlugin.RocketKey) + " pour l'allumer (les bidons la remplissent).\n" +
                "Fonce dans un monstre à pleine vitesse = bonus d'aura.\n" +
                "Un monstre te poursuit ? Fuis vite : aura et vues pour l'évasion.\n" +
                "Touche d'emote du jeu (T) sur la planche = danse en roulant.\n" +
                (SkatePlugin.BailDamage.Value > 0 ? "Les chutes font mal (encore plus en vitesse).\n" : "") +
                (SkatePlugin.WearEnabled.Value ? "Les chutes usent la planche : elle grince, ralentit, puis casse.\n" : "") +
                "Trampoline : un ollie dessus et tu t'envoles (maintiens SAUT à l'atterrissage pour rebondir).\n" +
                "Enchaîne les tricks pour un multiplicateur, le combo se transforme en AURA.\n" +
                "Les tricks filmés rapportent des vues et des commentaires.\n" +
                k(SkatePlugin.ToggleKey) + " descendre   " + k(SkatePlugin.ThirdPersonKey) + " caméra   " +
                k(SkatePlugin.RecordsKey) + " records   " + k(SkatePlugin.MenuKey) + " paramètres"
                : Lang.Cur == Lng.Zh ?
                "滑板操作\n" +
                "W  蹬地（按一下 = 蹬一下，按住 = 一直蹬）\n" +
                (kb ? "A / D  转向，镜头自动跟着滑板转\n" : "鼠标  转向\n") +
                "S  刹车，高速时 = powerslide（横刹）\n" +
                "按住 跳跃 - 下蹲，松开 = 豚跳 ollie（按越久跳越高）\n" +
                "蹲下（按住）  manual，  蹲下 + W  nose manual\n" +
                "低速时 蹲下 + A / D  kickturn\n" +
                "蹲下 + 点 跳跃  boneless（比豚跳更高）\n" +
                "S + 点 跳跃  hippie jump（你跳起来，滑板从你脚下继续滑）\n" +
                "落地后马上按 冲刺  revert（原地转 180）\n" +
                "豚跳上栏杆或边缘 = 滑杆 grind，跳跃 = 下来\n" +
                "斜着豚跳撞墙 = 飞墙 wallride，跳跃 = 下来\n" +
                "从上面豚跳到怪物身上 = 在它背上滑，跳跃 = 下来\n" +
                "在怪物 / 玩家旁边按住 " + k(SkatePlugin.SkitchKey) + " = skitch（被它拖着走）\n" +
                "紧跟在玩家身后按住 " + k(SkatePlugin.TrainKey) + " = 滑板小火车（双手搭在他肩上）\n" +
                "快撞墙时：按住 蹲下 = wallplant（蹬墙弹开）\n" +
                "从台子上滑下去 = drop in（蹲着能扛住更高的落差）\n" +
                "不在滑板上：" + k(SkatePlugin.ThrowKey) + " 把滑板往前扔，追上去就能跳上去\n" +
                "空中：\n" +
                "  A / D  旋转（180、360、540...）\n" +
                "  W + 跳跃  前空翻，  S + 跳跃  后空翻  （再按一次 = 两圈）\n" +
                "  " + k(SkatePlugin.KickflipKey) + " kickflip   " + k(SkatePlugin.HeelflipKey) + " heelflip   " + k(SkatePlugin.ShoveItKey) + " shove-it\n" +
                "  " + k(SkatePlugin.TreFlipKey) + " tre flip   " + k(SkatePlugin.ImpossibleKey) + " impossible  （翻板中再按 = double）\n" +
                "  冲刺  Indy grab  （+W nosegrab，+S tailgrab，+蹲下 melon）\n" +
                "  冲刺 + 蹲下 + W  crail，  冲刺 + 蹲下 + S  stalefish\n" +
                "  抓板时：跳跃 = airwalk，  W + 跳跃 = superman\n" +
                "  落地前要松开抓板！\n" +
                "要正着落地，或者完全倒着落地（fakie）。\n" +
                "路沿、台阶和墙会把你绊倒 - 用豚跳跳过去。\n" +
                (SkatePlugin.PickUpAfterBail.Value ? "摔车后滑板会飞出去：走过去按 E 捡起来。\n" : "") +
                "滑板是个物品：选中它的格子就能拿在手上，鼠标左键 = 用它抽。\n" +
                "商店（Misc）里的配件：拿在手上 + 鼠标左键 = 装到滑板上。\n" +
                "滑板上的火箭：按住 " + k(SkatePlugin.RocketKey) + " 点火（燃料罐可以加燃料）。\n" +
                "高速撞上怪物 = 气场加成。\n" +
                "被怪物追？快滑走：逃脱能拿气场和播放量。\n" +
                "游戏的表情键（T）在滑板上 = 边滑边跳舞。\n" +
                (SkatePlugin.BailDamage.Value > 0 ? "摔车会受伤（速度越快越疼）。\n" : "") +
                (SkatePlugin.WearEnabled.Value ? "摔车会磨损滑板：吱吱响、变慢，最后断掉。\n" : "") +
                "蹦床：在上面豚跳就能飞起来（落地时按住跳跃会弹得更高）。\n" +
                "连续做动作 = 带倍率的连招，连招会变成气场。\n" +
                "拍到镜头里的动作能涨播放量和评论。\n" +
                k(SkatePlugin.ToggleKey) + " 下滑板   " + k(SkatePlugin.ThirdPersonKey) + " 视角   " +
                k(SkatePlugin.RecordsKey) + " 纪录   " + k(SkatePlugin.MenuKey) + " 设置"
                :
                "SKATE CONTROLS\n" +
                "W  push (one kick per press, hold to keep kicking)\n" +
                (kb ? "A / D  carve, camera follows the board\n" : "Mouse  steer\n") +
                "S  brake, at speed = powerslide\n" +
                "Hold JUMP to crouch, release = ollie (longer = higher)\n" +
                "CROUCH (hold)  manual,  CROUCH + W  nose manual\n" +
                "CROUCH + A / D when slow  kickturn\n" +
                "CROUCH + tap JUMP  boneless (higher than an ollie)\n" +
                "S + tap JUMP  hippie jump (you jump, the board rolls on under you)\n" +
                "SPRINT right after landing  revert (180 on the ground)\n" +
                "Ollie onto a rail or ledge edge = grind, JUMP to pop off\n" +
                "Ollie into a wall at an angle = wallride, JUMP to jump off\n" +
                "Ollie down onto a monster = ride on its back, JUMP to pop off\n" +
                "Hold " + k(SkatePlugin.SkitchKey) + " next to a running monster / player = skitch (get towed)\n" +
                "Hold " + k(SkatePlugin.TrainKey) + " right behind a player = skate train (hands on their shoulders)\n" +
                "Flying at a wall: hold CROUCH = wallplant (kick off it)\n" +
                "Roll off a ledge = drop in (hold CROUCH for big drops)\n" +
                "Off the board: " + k(SkatePlugin.ThrowKey) + " throws it ahead, run after it to hop on\n" +
                "In the air:\n" +
                "  A / D  spin (180, 360, 540...)\n" +
                "  W + JUMP  frontflip,  S + JUMP  backflip  (again mid-flip = double)\n" +
                "  " + k(SkatePlugin.KickflipKey) + " kickflip   " + k(SkatePlugin.HeelflipKey) + " heelflip   " + k(SkatePlugin.ShoveItKey) + " shove-it\n" +
                "  " + k(SkatePlugin.TreFlipKey) + " tre flip   " + k(SkatePlugin.ImpossibleKey) + " impossible  (again mid-flip = double)\n" +
                "  SPRINT  Indy grab  (+W nosegrab, +S tailgrab, +CROUCH melon)\n" +
                "  SPRINT + CROUCH + W  crail,  SPRINT + CROUCH + S  stalefish\n" +
                "  while grabbing: JUMP = airwalk,  W + JUMP = superman\n" +
                "  let go of the grab before you land!\n" +
                "Land straight or fully backwards (fakie).\n" +
                "Curbs, steps, walls throw you off - ollie over them.\n" +
                (SkatePlugin.PickUpAfterBail.Value ? "After a bail the board flies off: walk over and press E to take it.\n" : "") +
                "The board is an item: select its slot to hold it, LMB = smack someone with it.\n" +
                "Board parts from the shop (Misc): hold one + LMB = put it on your board.\n" +
                "Rocket booster on the board: hold " + k(SkatePlugin.RocketKey) + " to fire it (fuel canisters refill it).\n" +
                "Crash into a monster at speed for bonus aura.\n" +
                "A monster chasing you? Ride away fast: ESCAPE aura and views.\n" +
                "Game emote key (T) while riding = dance on the board.\n" +
                (SkatePlugin.BailDamage.Value > 0 ? "Bails hurt (harder at speed).\n" : "") +
                (SkatePlugin.WearEnabled.Value ? "Crashes wear the board out: it squeaks, slows down, then snaps.\n" : "") +
                "Trampoline: ollie on it to fly (hold JUMP on landing to bounce).\n" +
                "Chain tricks for a multiplier, the combo cashes in as AURA.\n" +
                "Tricks caught on camera get views and comments.\n" +
                k(SkatePlugin.ToggleKey) + " off the board   " + k(SkatePlugin.ThirdPersonKey) + " camera   " +
                k(SkatePlugin.RecordsKey) + " records   " + k(SkatePlugin.MenuKey) + " settings";
            var r = new Rect(20 * s, H * 0.03f, 720 * s, 1130 * s);
            GUI.color = new Color(0, 0, 0, 0.6f);
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = Color.white;
            Text(new Rect(r.x + 16 * s, r.y + 12 * s, r.width - 32 * s, r.height - 24 * s), help, (int)(19 * s), Color.white, TextAnchor.UpperLeft);
        }
    }
}
