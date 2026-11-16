using System.Collections.Generic;
using UnityEngine;

namespace CWSkate
{
    internal enum Flip : byte { None, Kickflip, Heelflip, ShoveIt, TreFlip, Impossible }

    // new grabs always go at the end (the number goes over the network)
    internal enum Grab : byte { None, Indy, Melon, Nosegrab, Tailgrab, Stalefish, Crail, Airwalk, Superman }

    internal static class Tricks
    {
        public static string FlipName(Flip f, int count)
        {
            string prefix = count == 2 ? "Double " : count >= 3 ? "Triple " : "";
            switch (f)
            {
                case Flip.Kickflip: return prefix + "Kickflip";
                case Flip.Heelflip: return prefix + "Heelflip";
                case Flip.ShoveIt: return prefix + (count >= 2 ? "Shove-it" : "Pop Shove-it");
                case Flip.TreFlip: return prefix + "Tre Flip";
                case Flip.Impossible: return prefix + "Impossible";
            }
            return "";
        }

        public static float FlipPoints(Flip f, int count)
        {
            float basePts;
            switch (f)
            {
                case Flip.Kickflip: basePts = 150; break;
                case Flip.Heelflip: basePts = 150; break;
                case Flip.ShoveIt: basePts = 100; break;
                case Flip.TreFlip: basePts = 350; break;
                case Flip.Impossible: basePts = 300; break;
                default: return 0;
            }
            float mult = count == 2 ? 2.4f : count >= 3 ? 4f : 1f;
            return basePts * mult;
        }

        // Seconds per full rotation (before the FlipSpeed setting)
        public static float FlipDuration(Flip f)
        {
            switch (f)
            {
                case Flip.ShoveIt: return 0.36f;
                case Flip.TreFlip: return 0.5f;
                case Flip.Impossible: return 0.5f;
                default: return 0.42f;
            }
        }

        public static float SpinPoints(int degrees)
        {
            if (degrees < 180) return 0;
            if (degrees == 180) return 150;
            if (degrees == 360) return 400;
            if (degrees == 540) return 800;
            if (degrees == 720) return 1300;
            if (degrees == 900) return 2000;
            return 2000 + (degrees - 900) / 180f * 900f;
        }

        // Board rotation for a flip in progress. progress goes 0..count.
        public static Quaternion FlipRotation(Flip f, float progress)
        {
            switch (f)
            {
                case Flip.Kickflip: return Quaternion.Euler(0, 0, -360f * progress);
                case Flip.Heelflip: return Quaternion.Euler(0, 0, 360f * progress);
                case Flip.ShoveIt: return Quaternion.Euler(0, 180f * progress, 0);
                case Flip.TreFlip: return Quaternion.Euler(0, 360f * progress, 0) * Quaternion.Euler(0, 0, -360f * progress);
                case Flip.Impossible: return Quaternion.Euler(360f * progress, 0, 0);
            }
            return Quaternion.identity;
        }

        public static string GrabName(Grab g)
        {
            switch (g)
            {
                case Grab.Melon: return "Melon Grab";
                case Grab.Nosegrab: return "Nosegrab";
                case Grab.Tailgrab: return "Tailgrab";
                case Grab.Stalefish: return "Stalefish";
                case Grab.Crail: return "Crail";
                case Grab.Airwalk: return "Airwalk";
                case Grab.Superman: return "Superman";
                default: return "Indy Grab";
            }
        }

        public static float GrabPoints(Grab g, float seconds)
        {
            switch (g)
            {
                case Grab.Melon: return 120f + seconds * 260f;
                case Grab.Nosegrab:
                case Grab.Tailgrab: return 130f + seconds * 280f;
                case Grab.Stalefish:
                case Grab.Crail: return 150f + seconds * 300f;
                case Grab.Airwalk: return 220f + seconds * 330f;
                case Grab.Superman: return 280f + seconds * 380f;
                default: return 100f + seconds * 250f;
            }
        }

        // How the board sits while grabbed (pulled up to the hand and tilted)
        public static Quaternion GrabRotation(Grab g)
        {
            switch (g)
            {
                case Grab.Melon: return Quaternion.Euler(0, 0, -38f);
                case Grab.Nosegrab: return Quaternion.Euler(-42f, 0, 0);
                case Grab.Tailgrab: return Quaternion.Euler(42f, 0, 0);
                case Grab.Stalefish: return Quaternion.Euler(14f, 0, -36f);  // heel edge up, tail a bit down
                case Grab.Crail: return Quaternion.Euler(-48f, 0, 14f);      // nose pulled up by the back hand
                case Grab.Airwalk: return Quaternion.Euler(-28f, 0, 18f);    // nose up, kicked out from under the feet
                case Grab.Superman: return Quaternion.Euler(-8f, 0, 0);      // held out flat under you
                default: return Quaternion.Euler(0, 0, 38f);
            }
        }
    }

    // A running combo. Tricks add up, the multiplier is the number of tricks.
    internal class Combo
    {
        public const float Grace = 1.1f;

        public readonly List<string> Names = new List<string>();
        public float Points;
        public int Count;
        private float grace;

        public bool Active => Count > 0;
        public int Multiplier => Mathf.Max(1, Count);
        public long Value => (long)(Points * Multiplier * SkatePlugin.AuraMultiplier.Value);

        public void Add(string name, float pts)
        {
            Names.Add(name);
            Points += pts;
            Count++;
            grace = Grace;
        }

        public void AddBonus(float pts)
        {
            if (Active) Points += pts;
        }

        public void KeepAlive() => grace = Grace;

        // Returns true when the combo should be cashed in.
        public bool Tick(float dt, bool busy)
        {
            if (!Active) return false;
            if (busy)
            {
                grace = Mathf.Max(grace, 0.35f);
                return false;
            }
            grace -= dt;
            return grace <= 0f;
        }

        public void Clear()
        {
            Names.Clear();
            Points = 0;
            Count = 0;
            grace = 0;
        }
    }

    internal static class Aura
    {
        private static readonly long[] Thresholds = { long.MinValue, 0, 1000, 5000, 15000, 40000, 100000, 250000, 600000, 1500000 };
        private static readonly string[] Names = { "Cringe Lord", "NPC", "Rookie", "Skater", "Sigma", "Gigachad", "Aura Farmer", "Skate Demon", "Tony Hawk's Ghost", "AURA GOD" };
        private static readonly Color[] Colors =
        {
            new Color(0.55f, 0.55f, 0.55f),
            new Color(0.85f, 0.85f, 0.85f),
            new Color(0.55f, 0.85f, 1f),
            new Color(0.35f, 1f, 0.55f),
            new Color(0.3f, 0.6f, 1f),
            new Color(1f, 0.85f, 0.25f),
            new Color(1f, 0.55f, 0.15f),
            new Color(1f, 0.25f, 0.3f),
            new Color(0.8f, 0.35f, 1f),
            new Color(1f, 1f, 1f),
        };

        public static int RankIndex(long aura)
        {
            int idx = 0;
            for (int i = 0; i < Thresholds.Length; i++)
                if (aura >= Thresholds[i]) idx = i;
            return idx;
        }

        public static string RankName(long aura) => Names[RankIndex(aura)];

        // the top rank (AURA GOD)
        public static bool IsMaxRank(long aura) => RankIndex(aura) == Names.Length - 1;

        public static Color RankColor(long aura)
        {
            int i = RankIndex(aura);
            if (i == Names.Length - 1)
                return Color.HSVToRGB(Time.time * 0.25f % 1f, 0.6f, 1f); // rainbow for the gods
            return Colors[i];
        }

        public static long NextThreshold(long aura)
        {
            for (int i = 0; i < Thresholds.Length; i++)
                if (Thresholds[i] > aura) return Thresholds[i];
            return -1;
        }

        // 0..1 how strong the idle glow is for this rank
        public static float RankGlow(long aura) => Mathf.Clamp01((RankIndex(aura) - 1) / 8f);
    }
}
