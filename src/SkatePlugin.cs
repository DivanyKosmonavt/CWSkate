using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace CWSkate
{
    internal enum SteeringMode { Keyboard, Mouse }

    // The skateboard is a real item now, so everyone in the lobby needs the mod
    // (the game uses this to only match players who have it).
    [ContentWarningPlugin(Guid, Version, false)]
    [BepInPlugin(Guid, "CW Skate", Version)]
    public class SkatePlugin : BaseUnityPlugin
    {
        public const string Guid = "com.ddk.cwskate";
        public const string Version = "1.0.0";

        internal static SkatePlugin Instance;
        internal static ManualLogSource Log;
        internal static ConfigFile Cfg;

        // Keys
        internal static ConfigEntry<KeyCode> ToggleKey, KickflipKey, HeelflipKey, ShoveItKey, TreFlipKey, ImpossibleKey;
        internal static ConfigEntry<KeyCode> HelpKey, ThirdPersonKey, MenuKey, RecordsKey, ThrowKey, SkitchKey, TrainKey, RocketKey;

        // Riding
        internal static ConfigEntry<SteeringMode> Steering;
        internal static ConfigEntry<SkateDifficulty> Difficulty;
        internal static ConfigEntry<bool> ExplosiveFuel, RocketCanExplode;
        internal static ConfigEntry<bool> CameraFollow;
        internal static ConfigEntry<float> CameraFollowSpeed;
        internal static ConfigEntry<bool> GoofyStance;
        internal static ConfigEntry<float> PushSpeed, PushImpulse, PushInterval, MaxSpeed, RollFriction, TurnSpeed, RideHeight;

        // Air
        internal static ConfigEntry<float> OllieMin, OllieMax, AirGravity, SpinSpeed, FlipSpeed, SaltoSpeed;

        // Grinds
        internal static ConfigEntry<bool> GrindsEnabled, WallridesEnabled, MonsterGrinds, ShowGrindSparks, ShowWheelTrails;
        internal static ConfigEntry<float> GrindMagnet, GrindFriction;

        // Bails
        internal static ConfigEntry<float> TripSpeed, WallBailSpeed, MaxLandingSpeed, BailTime, GetUpCooldown;
        internal static ConfigEntry<bool> Bails, ExternalForcesOn, PickUpAfterBail;
        internal static ConfigEntry<float> KnockOffStrength, PullOffStrength;
        internal static ConfigEntry<int> HitDamageBail;

        // Trampoline
        internal static ConfigEntry<bool> TrampolineAutoBounce;
        internal static ConfigEntry<float> TrampolineBoost;

        // Camera
        internal static ConfigEntry<float> ThirdPersonDistance, ThirdPersonHeight;

        // Hitting with the board
        internal static ConfigEntry<bool> SwingEnabled, HitPlayers, AuraGodSmack, HumbleOnDeath;
        internal static ConfigEntry<float> SwingCooldown, SwingRange, KnockForce, KnockdownTime;
        internal static ConfigEntry<int> PlayerHitDamage, MonsterHitAura;

        // Video content
        internal static ConfigEntry<bool> SkateContent, ContentBonuses, WearEnabled;
        internal static ConfigEntry<float> WearRate;
        internal static ConfigEntry<float> ContentMultiplier, RideBoardScale;

        // Aura / HUD / sound
        internal static ConfigEntry<bool> AuraPerSaveFile, AuraGlow, ShowHUD, ShowSpeed, ShowRanks, ShareRank, ShowChaseHUD, PerformanceMode;

        // Skin
        internal static ConfigEntry<string> Skin, BlockedEmotes;
        internal static ConfigEntry<MenuLanguage> Language, CommentsLanguage;

        // Developer tab
        internal static ConfigEntry<bool> DevTools, DevShowDebug, DevInfiniteFuel,
            DevNoWear, DevSuperJump, DevDoubleSpeed, DevLowGravity, DevPerfectLandings;
        internal static ConfigEntry<float> BodySlopeLean;
        internal static ConfigEntry<bool> TutorialDone; // the first-ride tips were shown
        internal static ConfigEntry<int> BailPenalty, MonsterBonkAura, EscapeAura, BailDamage;
        internal static ConfigEntry<float> AuraMultiplier, HudScale, Volume, PushVolume, TalkDuck;

        private static ConfigEntry<int> ConfigVersion;

        // Old (1.0-1.2) global progress, moved to the progress file on first start
        internal static ConfigEntry<long> OldTotalAura, OldBestCombo;
        internal static ConfigEntry<bool> OldOwnsSkateboard;

        private static ConfigDescription D(string text) => new ConfigDescription(text);
        private static ConfigDescription D(string text, float min, float max) => new ConfigDescription(text, new AcceptableValueRange<float>(min, max));
        private static ConfigDescription D(string text, int min, int max) => new ConfigDescription(text, new AcceptableValueRange<int>(min, max));

        private void Awake()
        {
            Instance = this;
            Log = Logger;
            Cfg = Config;

            ToggleKey = Config.Bind("1. Keys", "ToggleSkateboard", KeyCode.B, D("Встать на доску / слезть"));
            KickflipKey = Config.Bind("1. Keys", "Kickflip", KeyCode.F, D("Кикфлип (в воздухе). Ещё раз во время флипа = дабл/трипл"));
            HeelflipKey = Config.Bind("1. Keys", "Heelflip", KeyCode.G, D("Хилфлип (в воздухе)"));
            ShoveItKey = Config.Bind("1. Keys", "PopShoveIt", KeyCode.X, D("Поп шов-ит (в воздухе)"));
            TreFlipKey = Config.Bind("1. Keys", "TreFlip", KeyCode.C, D("Тре флип (в воздухе)"));
            ImpossibleKey = Config.Bind("1. Keys", "Impossible", KeyCode.Z, D("Импосибл (в воздухе)"));
            HelpKey = Config.Bind("1. Keys", "ToggleHelp", KeyCode.H, D("Показать / скрыть управление"));
            ThirdPersonKey = Config.Bind("1. Keys", "ThirdPerson", KeyCode.F5, D("Вид от первого / третьего лица"));
            MenuKey = Config.Bind("1. Keys", "SettingsMenu", KeyCode.F8, D("Открыть меню настроек мода"));
            RecordsKey = Config.Bind("1. Keys", "Records", KeyCode.Tab, D("Держи, чтобы посмотреть рекорды (текущее сохранение)"));
            SkitchKey = Config.Bind("1. Keys", "Skitch", KeyCode.F, D("Держи на доске рядом с монстром или игроком (до ~3 м) — цепляешься рукой, и он тебя тащит. Работает и в воздухе. Нажатие F в воздухе без никого рядом — кикфлип"));
            TrainKey = Config.Bind("1. Keys", "SkateTrain", KeyCode.Y, D("Паровозик: держи на доске, когда игрок прямо перед тобой (до ~2 м) — обе руки ему на плечи, и едешь за ним. За тебя так же может зацепиться следующий"));
            RocketKey = Config.Bind("1. Keys", "Rocket", KeyCode.LeftAlt, D("Держи на доске с ракетным ускорителем — ракета жжёт топливо и разгоняет далеко за обычную скорость (но поворачивать почти нельзя)"));
            ThrowKey = Config.Bind("1. Keys", "ThrowBoard", KeyCode.G, D("Не на доске: бросить доску вперёд, догнать и запрыгнуть на ходу (не ставь на T — эмоция в игре, и на V — рация)"));

            Steering = Config.Bind("2. Riding", "Steering", SteeringMode.Keyboard, D("Keyboard = поворот на A/D, как в Skate 3. Mouse = едешь туда, куда смотришь"));
            CameraFollow = Config.Bind("2. Riding", "CameraFollowsBoard", true, D("Камера сама разворачивается по ходу доски, когда не двигаешь мышью"));
            CameraFollowSpeed = Config.Bind("2. Riding", "CameraFollowSpeed", 3.5f, D("Как быстро камера догоняет доску", 0.5f, 10f));
            GoofyStance = Config.Bind("2. Riding", "GoofyStance", false, D("Стойка: выкл = regular (левая нога впереди), вкл = goofy"));
            PushSpeed = Config.Bind("2. Riding", "PushSpeed", 8.5f, D("Скорость, до которой можно разогнаться толчками (м/с)", 3f, 20f));
            PushImpulse = Config.Bind("2. Riding", "PushStrength", 1.7f, D("Сколько скорости даёт один толчок ногой (м/с)", 0.3f, 6f));
            PushInterval = Config.Bind("2. Riding", "PushInterval", 0.55f, D("Пауза между толчками (сек)", 0.15f, 1.5f));
            MaxSpeed = Config.Bind("2. Riding", "MaxSpeed", 22f, D("Максимальная скорость, например с горки (м/с)", 5f, 50f));
            BodySlopeLean = Config.Bind("2. Riding", "BodySlopeLean", 1.2f, D("Насколько тело наклоняется вместе с доской на склонах (0 = всегда прямо, 1 = ровно как доска, больше 1 = сильнее доски)", 0f, 2f));
            RollFriction = Config.Bind("2. Riding", "RollFriction", 0.18f, D("Как быстро доска сама замедляется", 0f, 2f));
            TurnSpeed = Config.Bind("2. Riding", "TurnSpeed", 150f, D("Скорость поворота (градусов/сек)", 30f, 400f));
            RideHeight = Config.Bind("2. Riding", "RideHeight", 0.11f, D("На сколько доска поднимает игрока", 0f, 0.3f));
            WearEnabled = Config.Bind("2. Riding", "BoardWear", true, D("Износ доски: падения, жёсткие приземления, гринды и удары доской её изнашивают. Изношенная скрипит, медленнее и слабее, а в конце ломается пополам"));
            WearRate = Config.Bind("2. Riding", "WearRate", 1f, D("Как быстро изнашивается доска (1 = примерно 10–12 сильных падений до поломки)", 0.1f, 5f));
            BlockedEmotes = Config.Bind("2. Riding", "BlockedEmotes", "Emote_PushUp, Emote_HalfBackflip", D("Эмоции, которые нельзя делать на доске (имена анимаций через запятую). По умолчанию: Воркаут 2 и Сальто назад 1"));

            OllieMin = Config.Bind("3. Air", "OllieMin", 6.0f, D("Сила короткого олли", 2f, 15f));
            OllieMax = Config.Bind("3. Air", "OllieMax", 8.5f, D("Сила полностью заряженного олли (держать прыжок)", 2f, 20f));
            AirGravity = Config.Bind("3. Air", "AirGravity", 20f, D("Гравитация в прыжке", 5f, 60f));
            SpinSpeed = Config.Bind("3. Air", "SpinSpeed", 540f, D("Скорость вращения в воздухе (градусов/сек)", 90f, 1500f));
            FlipSpeed = Config.Bind("3. Air", "FlipSpeed", 1f, D("Скорость флипов (1 = обычная)", 0.4f, 3f));
            SaltoSpeed = Config.Bind("3. Air", "SaltoSpeed", 600f, D("Скорость сальто вперёд/назад (градусов/сек). Больше = легче успеть до приземления", 300f, 1200f));

            GrindsEnabled = Config.Bind("4. Grinds", "Grinds", true, D("Гринды по перилам и краям (запрыгни олли на край)"));
            WallridesEnabled = Config.Bind("4. Grinds", "Wallrides", true, D("Уоллрайд: запрыгни олли на стену под углом и едешь по ней (пробел — спрыгнуть)"));
            ShowGrindSparks = Config.Bind("4. Grinds", "GrindSparks", true, D("Искры из-под доски, когда едешь по перилам"));
            MonsterGrinds = Config.Bind("4. Grinds", "MonsterGrinds", true, D("Гринд по монстру: запрыгни олли сверху на монстра и едешь у него на спине (пробел — спрыгнуть)"));
            GrindMagnet = Config.Bind("4. Grinds", "GrindMagnet", 0.35f, D("Насколько далеко доска «притягивается» к перилам (м)", 0.05f, 1f));
            GrindFriction = Config.Bind("4. Grinds", "GrindFriction", 0.6f, D("Как быстро гаснет скорость на гринде", 0f, 5f));

            Difficulty = Config.Bind("5. Bails", "Difficulty", SkateDifficulty.Normal, D("Сложность. Лёгкая: доска сама перепрыгивает бугорки, бордюры и ступеньки, стены сбивают только чуть выше скорости толчков, приземления прощают ошибки (боком - только если совсем поперёк, недокрученный флип, высота). Средняя: бугорки перепрыгивает только на обычной скорости (быстрее - бордюр сбивает), стены сбивают быстрее 5 м/с, флипы и высота чуть мягче, чем на сложной. Сложная: как в Skate 3 - всё это сбивает с доски"));
            RocketCanExplode = Config.Bind("5. Bails", "RocketCanExplode", true, D("Ракета на доске может взорваться, если врезаться, пока она горит"));
            Bails = Config.Bind("5. Bails", "Bails", true, D("Падения. Выкл = можно врезаться без последствий"));
            TripSpeed = Config.Bind("5. Bails", "TripSpeed", 2.6f, D("Если врезаться в препятствие у колёс быстрее этого (м/с) — перелетишь через доску", 0.5f, 15f));
            WallBailSpeed = Config.Bind("5. Bails", "WallBailSpeed", 3.2f, D("Если врезаться телом в стену быстрее этого (м/с) — падение", 0.5f, 15f));
            MaxLandingSpeed = Config.Bind("5. Bails", "MaxLandingSpeed", 13f, D("Приземление быстрее этого (м/с вниз) — ноги не выдержат", 5f, 40f));
            BailTime = Config.Bind("5. Bails", "BailTime", 2.4f, D("Сколько секунд лежишь после падения", 0.5f, 6f));
            GetUpCooldown = Config.Bind("5. Bails", "GetUpCooldown", 1.2f, D("Сколько секунд после того, как встал, нельзя делать трюки (чтобы случайно не упасть снова)", 0f, 4f));
            PickUpAfterBail = Config.Bind("5. Bails", "PickUpAfterBail", true, D("После каждого падения доска улетает, и за ней надо сходить и подобрать (E). Выкл = доска сама возвращается под ноги"));
            ExternalForcesOn = Config.Bind("5. Bails", "ExternalForces", true, D("Чёрные дыры, выстрелы, удары и взрывы толкают тебя на доске и могут сбить с неё"));
            KnockOffStrength = Config.Bind("5. Bails", "KnockOffStrength", 3f, D("Удар сильнее этого (м/с) сбивает с доски. Меньше = падаешь от любого толчка", 0.5f, 20f));
            PullOffStrength = Config.Bind("5. Bails", "PullOffStrength", 10f, D("Притяжение сильнее этого (например чёрная дыра) стаскивает с доски", 1f, 60f));
            HitDamageBail = Config.Bind("5. Bails", "HitDamageBail", 10, D("Получил столько урона за раз — падаешь с доски. 0 = урон не сбивает", 0, 100));
            BailDamage = Config.Bind("5. Bails", "BailDamage", 10, D("Урон, когда падаешь со скейта на обычной скорости (~8 м/с). Быстрее = больнее (до x2.5), медленно = меньше. 0 = без урона", 0, 50));

            TrampolineAutoBounce = Config.Bind("6. Trampoline", "AutoBounce", false, D("Батут сам подбрасывает при приземлении. Выкл = только если прыгнуть"));
            TrampolineBoost = Config.Bind("6. Trampoline", "Boost", 1.8f, D("Во сколько раз батут усиливает олли", 1f, 4f));

            ThirdPersonDistance = Config.Bind("7. Camera", "ThirdPersonDistance", 2.8f, D("Дальность камеры от третьего лица", 1f, 8f));
            ThirdPersonHeight = Config.Bind("7. Camera", "ThirdPersonHeight", 0.35f, D("Высота камеры от третьего лица", -0.5f, 2f));

            AuraPerSaveFile = Config.Bind("8. Aura", "AuraPerSaveFile", true, D("Аура хранится в файле сохранения. Выкл = одна аура на всё (для клиента)"));
            AuraMultiplier = Config.Bind("8. Aura", "AuraMultiplier", 1f, D("Множитель получаемой ауры", 0.1f, 10f));
            BailPenalty = Config.Bind("8. Aura", "BailPenalty", 100, D("Сколько ауры теряешь при падении", 0, 5000));
            MonsterBonkAura = Config.Bind("8. Aura", "MonsterBonkAura", 500, D("Сколько ауры даёт врезаться в монстра на скорости", 0, 5000));
            EscapeAura = Config.Bind("8. Aura", "EscapeAura", 300, D("Аура за побег: монстр гнался за тобой, а ты укатил от него", 0, 5000));
            ShowWheelTrails = Config.Bind("8. Aura", "WheelTrails", true, D("Светящийся след за колёсами цвета твоего ранга ауры (у AURA GOD — радуга)"));
            AuraGlow = Config.Bind("8. Aura", "AuraGlow", true, D("Свечение вокруг тебя цвета твоего ранга, растёт с комбо (видно от третьего лица, F5)"));
            ShowRanks = Config.Bind("8. Aura", "ShowRanksAboveHeads", true, D("Показывать ранг ауры над головами других игроков с модом"));
            ShareRank = Config.Bind("8. Aura", "ShareMyRank", true, D("Другие игроки с модом видят твой ранг над твоей головой"));

            ShowHUD = Config.Bind("9. HUD & Sound", "ShowHUD", true, D("Показывать интерфейс мода"));
            ShowSpeed = Config.Bind("9. HUD & Sound", "ShowSpeed", true, D("Показывать скорость"));
            ShowChaseHUD = Config.Bind("9. HUD & Sound", "ShowChaseHUD", false, D("Подсказки во время погони монстра: «IT'S CHASING YOU», таймер побега, «TOO SLOW - PUSH!»"));
            Language = Config.Bind("9. HUD & Sound", "Language", MenuLanguage.Auto, D("Язык всего мода: это меню, надписи на экране, подсказки, рекорды, файл настроек, названия предметов, надписи над побитым монстром. Auto = как меню игры. (Названия трюков и рангов остаются английскими)"));
            CommentsLanguage = Config.Bind("9. HUD & Sound", "CommentsLanguage", MenuLanguage.Auto, D("Язык комментариев зрителей под твоими видео. Auto = как меню игры. Каждый игрок видит комменты на своём выбранном языке"));
            HudScale = Config.Bind("9. HUD & Sound", "HudScale", 0.77f, D("Размер интерфейса", 0.5f, 2f));
            Volume = Config.Bind("9. HUD & Sound", "Volume", 0.8f, D("Громкость звуков доски", 0f, 1f));
            PushVolume = Config.Bind("9. HUD & Sound", "PushVolume", 0.3f, D("Громкость толчка ногой (отдельно от остальных звуков)", 0f, 1f));
            PerformanceMode = Config.Bind("9. HUD & Sound", "PerformanceMode", false, D("Режим для слабых ПК: без светящегося следа за колёсами и свечения ауры, без теней от доски, меньше искр и огня, без дыма ракеты, взрывы попроще. Сам скейт и трюки работают так же"));
            TalkDuck = Config.Bind("9. HUD & Sound", "TalkDuck", 0.2f, D("Пока ты говоришь, звуки доски притихают до этой громкости — чтобы голос было слышно на записи камеры. 1 = не притихать", 0f, 1f));

            SwingEnabled = Config.Bind("12. Combat", "SwingEnabled", true, D("ЛКМ с доской в руках — удар доской"));
            SwingCooldown = Config.Bind("12. Combat", "SwingCooldown", 2f, D("Пауза между ударами (сек)", 0.3f, 4f));
            SwingRange = Config.Bind("12. Combat", "SwingRange", 2.42f, D("Дальность удара (м)", 1f, 4f));
            KnockForce = Config.Bind("12. Combat", "KnockForce", 3.42f, D("Насколько сильно отбрасывает того, кого ударил", 1f, 30f));
            KnockdownTime = Config.Bind("12. Combat", "KnockdownTime", 1.5f, D("Сколько секунд лежит тот, кого ударил", 0f, 5f));
            HitPlayers = Config.Bind("12. Combat", "HitPlayers", true, D("Можно бить других игроков. Выкл = только монстров"));
            PlayerHitDamage = Config.Bind("12. Combat", "PlayerHitDamage", 1, D("Урон по игроку за удар (0 = без урона, просто падает)", 0, 50));
            MonsterHitAura = Config.Bind("12. Combat", "MonsterHitAura", 1000, D("Аура за удар по монстру (одного и того же — не чаще раза в 5 сек)", 0, 2000));
            AuraGodSmack = Config.Bind("12. Combat", "AuraGodSmack", true, D("На максимальном ранге ауры (AURA GOD) удар по монстру отправляет его в полёт, а ты танцуешь"));
            ExplosiveFuel = Config.Bind("12. Combat", "ExplosiveFuel", true, D("Ракетное топливо и ракетный ускоритель, лежащие на полу, взрываются, если ударить их доской или если они сильно обо что-то ударятся (а рядом лежащие взрываются цепочкой)"));
            HumbleOnDeath = Config.Bind("12. Combat", "HumbleOnDeath", true, D("Если монстр, которого ты ударил на AURA GOD, потом тебя убил (в течение 3 минут) — теряешь всю ауру, а в комментах пишут, что тебя опустили"));

            SkateContent = Config.Bind("13. Video", "SkateContent", true, D("Трюки, снятые на камеру, дают просмотры и комментарии зрителей про скейт"));
            ContentMultiplier = Config.Bind("13. Video", "ContentMultiplier", 1f, D("Множитель просмотров за скейт на видео", 0.1f, 5f));
            ContentBonuses = Config.Bind("13. Video", "ContentBonuses", true, D("Бонусы к просмотрам: сложные трюки, высота и длительность, монстр рядом, селфи, катание компанией, мало здоровья, ранг ауры"));
            RideBoardScale = Config.Bind("2. Riding", "RideBoardScale", 1.35f, D("Размер доски под ногами во время езды", 0.8f, 2f));

            DevTools = Config.Bind("9. HUD & Sound", "DevTools", false, D("Вкладка «Разработчик» в этом меню: выдать предметы, запустить контракт спонсора, ауру, починить доску и т.п. Для проверки мода"));
            DevShowDebug = Config.Bind("14. Dev", "ShowDebugInfo", false, D("Отладочная информация на экране: скорость, состояние, комбо, износ, топливо, контракт"));
            DevInfiniteFuel = Config.Bind("14. Dev", "InfiniteRocketFuel", false, D("Ракета не тратит топливо"));
            DevNoWear = Config.Bind("14. Dev", "UnbreakableBoard", false, D("Доска не ломается: износ не тратится"));
            DevPerfectLandings = Config.Bind("14. Dev", "PerfectLandings", false, D("Любое приземление удачное (недокрученный флип, боком, высота - не важно)"));
            DevSuperJump = Config.Bind("14. Dev", "SuperJump", false, D("Олли в 1,8 раза сильнее"));
            DevDoubleSpeed = Config.Bind("14. Dev", "DoubleSpeed", false, D("Скорость толчков и максимальная скорость x2"));
            DevLowGravity = Config.Bind("14. Dev", "LowGravity", false, D("Низкая гравитация в прыжке - долгие полёты для трюков"));
            DevTools.SettingChanged += (s, a) => SettingsMenu.Recollect();

            Skin = Config.Bind("11. Skin", "Skin", "default", D("Рисунок доски. Картинки лежат в папке SkateSkins рядом с модом — можно добавлять свои"));

            // new defaults for settings the player never touched
            ConfigVersion = Config.Bind("Internal", "ConfigVersion", 0, D("(служебное)"));
            TutorialDone = Config.Bind("Internal", "TutorialDone", false, D("Подсказки при первом залезании на доску уже были (false = показать ещё раз)"));
            if (ConfigVersion.Value < 14)
            {
                if (Mathf.Approximately(HudScale.Value, 1f)) HudScale.Value = 0.77f;
                ConfigVersion.Value = 14;
            }
            if (ConfigVersion.Value < 15)
            {
                // T is the game's emote key (dancing on the board uses it): the board throw moves to V
                if (ThrowKey.Value == KeyCode.T) ThrowKey.Value = KeyCode.V;
                ConfigVersion.Value = 15;
            }
            if (ConfigVersion.Value < 16)
            {
                // V is the game's push-to-talk: talking threw the board. G is free off the board (it's a flip only in the air)
                if (ThrowKey.Value == KeyCode.V) ThrowKey.Value = KeyCode.G;
                ConfigVersion.Value = 16;
            }
            if (ConfigVersion.Value < 17)
            {
                // Easy was the default for a day and turned out too easy: Normal is the new default
                if (Difficulty.Value == SkateDifficulty.Easy) Difficulty.Value = SkateDifficulty.Normal;
                ConfigVersion.Value = 17;
            }
            if (ConfigVersion.Value < 18)
            {
                // the slope lean was too weak
                if (Mathf.Abs(BodySlopeLean.Value - 0.85f) < 0.01f) BodySlopeLean.Value = 1.2f;
                ConfigVersion.Value = 18;
            }

            OldTotalAura = Config.Bind("Progress", "TotalAura", 0L, D("(устарело, перенесено в cwskate_progress.txt)"));
            OldBestCombo = Config.Bind("Progress", "BestCombo", 0L, D("(устарело, перенесено в cwskate_progress.txt)"));
            OldOwnsSkateboard = Config.Bind("Progress", "OwnsSkateboard", false, D("(устарело, перенесено в cwskate_progress.txt)"));

            Skins.Init();
            SkateComments.Init();
            try { SkateItem.TryRegister(); SkateParts.EnsureRegistered(); } catch (System.Exception e) { Log.LogWarning("Item not registered yet: " + e.Message); }

            // patch class by class, so one broken patch (e.g. after a game update) doesn't take the whole mod down
            var harmony = new Harmony(Guid);
            foreach (var t in AccessTools.GetTypesFromAssembly(typeof(SkatePlugin).Assembly))
            {
                try { harmony.CreateClassProcessor(t).Patch(); }
                catch (System.Exception e) { Log.LogError("Patch " + t.Name + " failed: " + e.Message); }
            }

            var go = new GameObject("CWSkate_Manager");
            go.hideFlags = HideFlags.HideAndDontSave;
            DontDestroyOnLoad(go);
            go.AddComponent<SkateManager>();

            Log.LogInfo("CW Skate " + Version + " loaded. Press " + ToggleKey.Value + " to ride, " + MenuKey.Value + " for settings.");
        }
    }
}
