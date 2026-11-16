using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using Zorro.Core;

namespace CWSkate
{
    // Skate sponsors: deals that show up on the three monitors in the house next to the game's own.
    // The game finds every class that derives from NetworkDealBase in every loaded DLL, so ours join in by themselves.
    // Like the game's deals they are checked on the uploaded video: every matching skate moment on it counts.
    // (The game counts the same kind of moment from the same player only a few times per video,
    //  so the deals ask for different tricks rather than "10 kickflips".)
    //
    // Not abstract on purpose: the game makes one of every class it finds, an abstract one would crash it.
    // This base one says UseInGame = false, so it is never offered.
    internal class SkateDeal : NetworkDealBase
    {
        // which skate moments this deal counts
        protected virtual bool Counts(SkateAct k) => false;

        // the deal's own number (the game's deals are 0..10; ours start at 200, never change them)
        protected virtual byte Index => 199;
        protected virtual string Brand => "Skate";
        protected virtual string Email => "skate@cwskate.mod";
        protected virtual string TitleRu => "";
        protected virtual string TitleEn => "";
        protected virtual string DescRu => "";   // {0} = how many
        protected virtual string DescEn => "";
        protected virtual string SuccessRu => "";
        protected virtual string SuccessEn => "";
        protected virtual string FailRu => "";
        protected virtual string FailEn => "";
        protected virtual string IconName => "skateboard";
        protected virtual RARITY DealRarity => RARITY.common;
        protected virtual List<string> Gifts => new List<string>();

        // how many, per difficulty
        protected virtual int Amount(DIFFICULTY d)
        {
            switch (d)
            {
                case DIFFICULTY.veryEasy: case DIFFICULTY.easy: return 1;
                case DIFFICULTY.medium: return 2;
                case DIFFICULTY.hard: return 3;
                default: return 4;
            }
        }

        protected virtual List<DIFFICULTY> Difficulties => new List<DIFFICULTY> { DIFFICULTY.easy, DIFFICULTY.medium, DIFFICULTY.hard, DIFFICULTY.veryHard };

        public override bool UseInGame => false;
        public override string DealName => Brand;
        public override string EmailTitle => Lang.T(TitleRu, TitleEn);
        public override string EmailAddress => Email;
        public override List<DIFFICULTY> AllowedDifficulties => Difficulties;
        public override List<string> PersistentIdOfItem => Gifts;
        public override RARITY Rarity => DealRarity;
        // a picture that exists in the game, so its loader is happy; ours replaces it in Init
        public override string IconPath => "Party-Music-Dance-Woman--Streamline-Ultimate";
        public override byte GetIndex() => Index;
        public override int RequiredAmount() => Amount(difficulty);
        public override NetworkDealBase CreateNew() => (NetworkDealBase)System.Activator.CreateInstance(GetType());
        public override string DealDescription() => string.Format(Lang.T(DescRu, DescEn), RequiredAmount());
        public override string GetSuccessEmailBody() => Lang.T(SuccessRu, SuccessEn);
        public override string GetFailedEmailBody() => Lang.T(FailRu, FailEn);
        public override void Update() { }
        public override void OnDestroy() { }

        public override void Init(DealRewardBase reward, DIFFICULTY dif)
        {
            base.Init(reward, dif);
            var icon = SkateArt.EmbeddedIcon(IconName) ?? SkateArt.EmbeddedIcon("skateboard");
            if (icon != null) iconSprite = icon;
        }

        // the uploaded video: count our moments on it
        public override void ReviewUploadContent(ContentBuffer contentBuffer)
        {
            base.ReviewUploadContent(contentBuffer);
            if (contentBuffer == null || contentBuffer.buffer == null || State != DEAL_STATE.progressing) return;
            int n = 0;
            foreach (var b in contentBuffer.buffer)
                if (b.frame.contentEvent is SkateContentEvent sk && Counts(sk.kind)) n++;
            SkatePlugin.Log.LogInfo("Skate deal " + Brand + ": " + n + " matching moments on the video");
            if (n > 0) ProgressInt = Mathf.Min(RequiredAmount(), ProgressInt + n); // (the game marks it done by itself)
        }

        // ------------------------------------------------------------ joining the game's list

        private static readonly System.Type[] All =
        {
            typeof(RideToHellDeal), typeof(GrindKingsDeal), typeof(MonsterWheelsDeal), typeof(BailInsuranceDeal), typeof(BoomRocketsDeal),
        };
        private static bool added;
        private static float nextCheck;

        // If the game built its list before our DLL was loaded, put our deals in it. (Called by the manager.)
        public static void EnsureRegistered()
        {
            if (added || Time.unscaledTime < nextCheck) return;
            nextCheck = Time.unscaledTime + 2f;
            NetworkDealDataBase db;
            try { db = SingletonAsset<NetworkDealDataBase>.Instance; }
            catch { return; }
            if (db == null || db.deals == null) return; // not loaded yet: it will find ours by itself
            foreach (var t in All)
            {
                if (db.deals.Exists(d => d != null && d.GetType() == t)) continue;
                db.deals.Add((NetworkDealBase)System.Activator.CreateInstance(t));
                SkatePlugin.Log.LogInfo("Skate deal added: " + t.Name);
            }
            added = true;
        }

        // Our texts aren't in the game's translation tables: let it use ours instead (and not complain).
        [HarmonyPatch]
        private static class Patch_Localized
        {
            private static IEnumerable<System.Reflection.MethodBase> TargetMethods()
            {
                foreach (var n in new[] { "LocalizedEmailTitle", "LocalizedDealName", "LocalizedDescription", "LocalizedSuccessEmailBody", "LocalizedFailedEmailBody" })
                {
                    var m = AccessTools.Method(typeof(NetworkDealBase), n);
                    if (m != null) yield return m;
                }
            }

            private static bool Prefix(NetworkDealBase __instance, ref string __result)
            {
                if (!(__instance is SkateDeal)) return true;
                __result = "";
                return false;
            }
        }
    }

    // Ride to Hell, the skate shop from the comments: any skate tricks on video. Sends its pro board.
    internal class RideToHellDeal : SkateDeal
    {
        public override bool UseInGame => true;
        protected override byte Index => 200;
        protected override string Brand => "Ride to Hell";
        protected override string Email => "deals@ridetohell.sk";
        protected override string IconName => "ride_to_hell_board";
        protected override List<string> Gifts => new List<string> { SkateItem.ProPersistentId };
        protected override string TitleRu => "ХОЧЕШЬ КАТАТЬ ЗА НАС?";
        protected override string TitleEn => "WANNA RIDE FOR US?";
        protected override string DescRu => "Сними {0} скейт-трюков на нашей про-доске Ride to Hell. Покажи Старому миру, как катаются настоящие райдеры!";
        protected override string DescEn => "Film {0} skate tricks on our Ride to Hell pro board. Show the Old World how real riders roll!";
        protected override string SuccessRu => "Теперь ты официальный райдер Ride to Hell. Доску оставь себе, она твоя.";
        protected override string SuccessEn => "You're an official Ride to Hell rider now. Keep the board, it's yours.";
        protected override string FailRu => "Мы ждали от тебя большего. Доску можешь оставить... как напоминание.";
        protected override string FailEn => "We expected more from you. Keep the board... as a reminder.";

        protected override bool Counts(SkateAct k)
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

        protected override int Amount(DIFFICULTY d)
        {
            switch (d)
            {
                case DIFFICULTY.veryEasy: case DIFFICULTY.easy: return 3;
                case DIFFICULTY.medium: return 5;
                case DIFFICULTY.hard: return 8;
                default: return 12;
            }
        }
    }

    // Grind Kings: grinds and wallrides (rails, walls, monsters' backs).
    internal class GrindKingsDeal : SkateDeal
    {
        public override bool UseInGame => true;
        protected override byte Index => 201;
        protected override string Brand => "Grind Kings";
        protected override string Email => "kings@grindkings.rail";
        protected override string TitleRu => "ПЕРИЛА ЗОВУТ";
        protected override string TitleEn => "THE RAILS ARE CALLING";
        protected override string DescRu => "Сними {0} гриндов или уоллрайдов: по перилам, по стенам, хоть по спине монстра.";
        protected override string DescEn => "Film {0} grinds or wallrides: on rails, on walls, even on a monster's back.";
        protected override string SuccessRu => "Металл плачет, а мы счастливы. Ты настоящий король гриндов.";
        protected override string SuccessEn => "The metal is crying and we're happy. You're a true grind king.";
        protected override string FailRu => "Перила остались целы. Нам это не нравится.";
        protected override string FailEn => "The rails are still in one piece. We don't like that.";
        protected override bool Counts(SkateAct k) => k == SkateAct.Grind || k == SkateAct.Wallride || k == SkateAct.MonsterGrind;
    }

    // Monster Wheels: skating with monsters - crash into them, smack them, ride away, ride on them.
    internal class MonsterWheelsDeal : SkateDeal
    {
        public override bool UseInGame => true;
        protected override byte Index => 202;
        protected override string Brand => "Monster Wheels";
        protected override string Email => "boss@monsterwheels.gg";
        protected override string TitleRu => "ПОКАЖИ МОНСТРАМ НАШИ КОЛЁСА";
        protected override string TitleEn => "SHOW THE MONSTERS OUR WHEELS";
        protected override string DescRu => "Сними {0} моментов скейта с монстрами: врежься в монстра, ударь его доской, укати от погони или прокатись у него на спине.";
        protected override string DescEn => "Film {0} skate moments with monsters: crash into one, smack one with the board, ride away from a chase or ride on its back.";
        protected override string SuccessRu => "Монстры теперь боятся наших колёс. Спасибо за рекламу!";
        protected override string SuccessEn => "The monsters fear our wheels now. Thanks for the ad!";
        protected override string FailRu => "Монстры даже не заметили наши колёса. Обидно.";
        protected override string FailEn => "The monsters didn't even notice our wheels. Sad.";
        protected override bool Counts(SkateAct k) =>
            k == SkateAct.MonsterBonk || k == SkateAct.SmackMonster || k == SkateAct.GodSmack || k == SkateAct.Escape || k == SkateAct.MonsterGrind;
    }

    // Bail Insurance Co.: they want footage of falls for their ads. Send a policy that saves you from dying once.
    internal class BailInsuranceDeal : SkateDeal
    {
        public override bool UseInGame => true;
        protected override byte Index => 203;
        protected override string Brand => "Bail Insurance Co.";
        protected override string Email => "claims@bailinsurance.co";
        protected override string IconName => "insurance_policy";
        protected override List<string> Gifts => new List<string> { SponsorItems.PolicyId };
        protected override string TitleRu => "МЫ СТРАХУЕМ НЕСТРАХУЕМОЕ";
        protected override string TitleEn => "WE INSURE THE UNINSURABLE";
        protected override string DescRu => "Нам нужны падения для рекламы. Сними {0} падений со скейта. Высылаем полис: если ты умрёшь, он один раз тебя спасёт (держи его в инвентаре).";
        protected override string DescEn => "We need bails for our ads. Film {0} falls off your board. We're sending you a policy: if you die, it saves you once (keep it in your inventory).";
        protected override string SuccessRu => "Отличные падения! Наша реклама будет хитом. Выплата прилагается.";
        protected override string SuccessEn => "Great bails! Our ad is going to be a hit. Your payout is attached.";
        protected override string FailRu => "Ты подозрительно осторожен. Мы пересматриваем твою страховку.";
        protected override string FailEn => "You're suspiciously careful. We're reviewing your policy.";
        protected override bool Counts(SkateAct k) => k == SkateAct.Bail || k == SkateAct.MonsterBonk || k == SkateAct.BoardSnap;
    }

    // Boom Rockets: a rocket company run by a certain billionaire. Sends a rocket and fuel, wants rocket rides on video.
    internal class BoomRocketsDeal : SkateDeal
    {
        public override bool UseInGame => true;
        protected override byte Index => 204;
        protected override string Brand => "Boom Rockets";
        protected override string Email => "elon@boomrockets.x";
        protected override string IconName => "rocket";
        protected override RARITY DealRarity => RARITY.lessCommon;
        protected override List<string> Gifts
        {
            get
            {
                var l = new List<string>();
                foreach (var d in SkateParts.Defs)
                    if (d.Kind == PartKind.Rocket || d.Kind == PartKind.Fuel) l.Add(d.PersistentId);
                return l;
            }
        }
        protected override List<DIFFICULTY> Difficulties => new List<DIFFICULTY> { DIFFICULTY.easy, DIFFICULTY.medium, DIFFICULTY.hard };
        protected override string TitleRu => "НА МАРС НА СКЕЙТБОРДЕ";
        protected override string TitleEn => "TO MARS ON A SKATEBOARD";
        protected override string DescRu => "Мы в Boom Rockets делаем ракеты для всего, даже для скейтов. Высылаем ускоритель и топливо. Сними {0} полётов на ракете. Следующая остановка: Марс.";
        protected override string DescEn => "At Boom Rockets we put rockets on everything, even skateboards. We're sending you a booster and fuel. Film {0} rocket rides. Next stop: Mars.";
        protected override string SuccessRu => "Ракетная наука работает! Акции Boom Rockets выросли на 4000%. Подпись: Илон.";
        protected override string SuccessEn => "Rocket science works! Boom Rockets stock is up 4000%. Signed: Elon.";
        protected override string FailRu => "Произошла быстрая незапланированная разборка. Всё равно считаем это успехом.";
        protected override string FailEn => "We had a rapid unscheduled disassembly. We're calling it a success anyway.";
        protected override bool Counts(SkateAct k) => k == SkateAct.RocketRide;

        protected override int Amount(DIFFICULTY d)
        {
            switch (d)
            {
                case DIFFICULTY.veryEasy: case DIFFICULTY.easy: return 1;
                case DIFFICULTY.medium: return 2;
                default: return 3;
            }
        }
    }
}
