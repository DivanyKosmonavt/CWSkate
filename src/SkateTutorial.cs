using UnityEngine;

namespace CWSkate
{
    // Tips for a new player, the first time you get on a board. A fall or getting off only pauses them:
    // the next ride carries on from the same tip. Only once you've seen them all are they marked as done
    // (in the settings file, so they never come back, whatever the save).
    internal partial class SkateController
    {
        private int tutStep = -1;
        private float tutTime;
        private const float TipTime = 6.5f;
        private const int TipCount = 7;

        public string TutorialTip { get; private set; }
        public string TutorialCounter => (tutStep + 1) + " / " + TipCount;
        public float TutorialAlpha { get; private set; }

        // called when you get on the board
        private void StartTutorialIfFirst()
        {
            if (SkatePlugin.TutorialDone.Value) { tutStep = -1; return; }
            if (tutStep < 0 || tutStep >= TipCount) tutStep = 0;
            tutTime = 0f; // the interrupted tip starts over
        }

        // off the board: hide the tip, but remember where we were
        private void StopTutorial()
        {
            TutorialTip = null;
        }

        private void TickTutorial(float dt)
        {
            if (tutStep < 0) { TutorialTip = null; return; }
            // lying on the floor after a fall: pause (the tip comes back once you're up)
            if (bailed) { TutorialTip = null; return; }
            if (TutorialTip == null && tutTime > 0f) tutTime = 0f; // was hidden: show it again from the start
            tutTime += dt;
            if (tutTime > TipTime) { tutStep++; tutTime = 0f; }
            if (tutStep >= TipCount)
            {
                SkatePlugin.TutorialDone.Value = true; // seen them all: never again
                tutStep = -1;
                TutorialTip = null;
                return;
            }
            TutorialTip = Tip(tutStep);
            TutorialAlpha = Mathf.Clamp01(Mathf.Min(tutTime / 0.4f, (TipTime - tutTime) / 0.4f)); // fades in and out
        }

        private static string Tip(int i)
        {
            string K(BepInEx.Configuration.ConfigEntry<KeyCode> e) => e.Value.ToString();
            switch (i)
            {
                case 0: return Lang.T("Ты на доске! W - толкаться, A / D - поворачивать, S - тормозить",
                                      "You're on the board! W - push, A / D - turn, S - brake");
                case 1: return Lang.T("Зажми ПРОБЕЛ и отпусти - олли. Чем дольше держишь, тем выше прыжок",
                                      "Hold SPACE and let go - an ollie. The longer you hold, the higher you jump");
                case 2: return Lang.F("В воздухе: {0} - флипы, SHIFT - грэб, A / D - вращение. Приземляйся ровно!",
                                      "In the air: {0} - flips, SHIFT - grab, A / D - spin. Land straight!",
                                      K(SkatePlugin.KickflipKey) + " " + K(SkatePlugin.HeelflipKey) + " " + K(SkatePlugin.ShoveItKey) + " " + K(SkatePlugin.TreFlipKey) + " " + K(SkatePlugin.ImpossibleKey));
                case 3: return Lang.T("Запрыгни олли на перила - это гринд. CTRL на земле - мэнуал (езда на задних колёсах)",
                                      "Ollie onto a rail - that's a grind. CTRL on the ground - a manual (riding on the back wheels)");
                case 4: return Lang.T("Трюки подряд - это комбо. Комбо превращается в АУРУ: копи её до ранга AURA GOD",
                                      "Tricks in a row make a combo. The combo turns into AURA: farm it up to AURA GOD");
                case 5: return Lang.T("Снимай трюки на камеру - за них будут просмотры и комменты зрителей",
                                      "Film your tricks - they get views and viewer comments");
                default: return Lang.F("{0} - всё управление, {1} - настройки, {2} - слезть с доски. Удачи!",
                                       "{0} - all the controls, {1} - settings, {2} - get off. Have fun!",
                                       K(SkatePlugin.HelpKey), K(SkatePlugin.MenuKey), K(SkatePlugin.ToggleKey));
            }
        }
    }
}
