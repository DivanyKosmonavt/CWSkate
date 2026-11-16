using UnityEngine;

namespace CWSkate
{
    // All sounds are generated in code, so the mod needs no asset files.
    internal static class SkateSounds
    {
        private const int Rate = 44100;
        private static AudioClip roll, pop, land, chime, bail, whoosh;

        public static AudioClip Roll => roll ? roll : roll = MakeRoll();
        public static AudioClip Pop => pop ? pop : pop = MakePop();
        public static AudioClip Land => land ? land : land = MakeLand();
        public static AudioClip Chime => chime ? chime : chime = MakeChime();
        public static AudioClip Bail => bail ? bail : bail = MakeBail();
        public static AudioClip Whoosh => whoosh ? whoosh : whoosh = MakeWhoosh();
        private static AudioClip push, grind;
        public static AudioClip Push => push ? push : push = MakePush();
        public static AudioClip Grind => grind ? grind : grind = MakeGrind();

        // foot brushing the ground: soft and low, so it doesn't get annoying
        private static AudioClip MakePush()
        {
            int n = (int)(Rate * 0.2f);
            var d = new float[n];
            var rng = new System.Random(21);
            float lp = 0, lp2 = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                lp += ((float)(rng.NextDouble() * 2 - 1) - lp) * 0.08f;
                lp2 += (lp - lp2) * 0.15f;
                float env = Mathf.Sin(Mathf.PI * Mathf.Pow(t, 0.7f));
                d[i] = lp2 * env * env * 1.2f;
            }
            return Clip("cwskate_push", d);
        }

        private static AudioClip hit;
        public static AudioClip Hit => hit ? hit : hit = MakeHit();

        private static AudioClip rocket, boom;
        public static AudioClip Rocket => rocket ? rocket : rocket = MakeRocket();
        public static AudioClip Boom => boom ? boom : boom = MakeBoom();

        // an explosion: a sharp crack, a deep boom that rumbles out, and some crackling debris
        private static AudioClip MakeBoom()
        {
            int n = (int)(Rate * 2.6f);
            var d = new float[n];
            var rng = new System.Random(1234);
            float lp = 0, lp2 = 0, lp3 = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float white = (float)(rng.NextDouble() * 2 - 1);
                lp += (white - lp) * 0.04f;
                lp2 += (lp - lp2) * 0.06f;
                lp3 += (white - lp3) * 0.5f;
                float crack = lp3 * Mathf.Exp(-t * 25f) * 1.6f;
                float body = lp2 * 9f * Mathf.Exp(-t * 1.6f) * Mathf.Min(1f, t / 0.01f);
                float thump = Mathf.Sin(2 * Mathf.PI * (55f - 20f * t) * t) * Mathf.Exp(-t * 3f) * 0.9f;
                float debris = (rng.NextDouble() < 0.003 * Mathf.Exp(-t * 1.5f) * 10 ? white : 0f) * Mathf.Exp(-t * 2f) * 0.8f;
                d[i] = Mathf.Clamp(crack + body + thump + debris, -1f, 1f);
            }
            return Clip("cwskate_boom", d);
        }

        // rocket burning: a deep roar (low noise) with a hiss on top and crackles, loops
        private static AudioClip MakeRocket()
        {
            int n = Rate * 2, fade = 4000;
            var raw = new float[n + fade];
            var rng = new System.Random(77);
            float lp = 0, lp2 = 0, hp = 0;
            for (int i = 0; i < raw.Length; i++)
            {
                float white = (float)(rng.NextDouble() * 2 - 1);
                lp += (white - lp) * 0.05f;
                lp2 += (lp - lp2) * 0.08f;
                hp += (white - hp) * 0.3f;
                float t = i / (float)Rate;
                float wobble = 1f + 0.25f * Mathf.Sin(t * 31f) * Mathf.Sin(t * 7f); // the flame flutters
                float crackle = rng.NextDouble() < 0.002 ? white * 2.5f : 0f;
                raw[i] = lp2 * 5f * wobble + (white - hp) * 0.18f + crackle;
            }
            var d = new float[n];
            for (int i = 0; i < n; i++) d[i] = raw[i];
            for (int i = 0; i < fade; i++)
            {
                float k = i / (float)fade;
                d[i] = raw[i] * k + raw[n + i] * (1 - k);
            }
            for (int i = 0; i < n; i++) d[i] = Mathf.Clamp(d[i], -1f, 1f) * 0.85f;
            return Clip("cwskate_rocket", d);
        }

        // ---- a worn-out board
        private static AudioClip squeak, creak, snap;
        public static AudioClip Squeak => squeak ? squeak : squeak = MakeSqueak();
        public static AudioClip Creak => creak ? creak : creak = MakeCreak();
        public static AudioClip Snap => snap ? snap : snap = MakeSnap();

        // dry wheel bearing: a short high squeal that wobbles in pitch
        private static AudioClip MakeSqueak()
        {
            int n = (int)(Rate * 0.22f);
            var d = new float[n];
            float ph = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float f = 2600f + 700f * Mathf.Sin(t * 70f) + 300f * Mathf.Sin(t * 23f);
                ph += 2 * Mathf.PI * f / Rate;
                float env = Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / 0.22f));
                d[i] = (Mathf.Sin(ph) * 0.6f + Mathf.Sin(ph * 2.01f) * 0.2f) * env * env * 0.5f;
            }
            return Clip("cwskate_squeak", d);
        }

        // old wood bending: a low rough "eeeerk" made of fast little stick-slip clicks
        private static AudioClip MakeCreak()
        {
            int n = (int)(Rate * 0.55f);
            var d = new float[n];
            var rng = new System.Random(41);
            float phase = 0, lp = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float rate = Mathf.Lerp(90f, 160f, Mathf.Sin(t / 0.55f * Mathf.PI)); // clicks per second, speeds up then slows
                phase += rate / Rate;
                float click = 0f;
                if (phase >= 1f) { phase -= 1f; click = 1f; }
                lp += (click * (float)(0.6 + rng.NextDouble() * 0.8) - lp) * 0.25f;
                float body = Mathf.Sin(2 * Mathf.PI * 420f * t) * lp * 3f + lp * 2f;
                float env = Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / 0.55f));
                d[i] = Mathf.Clamp(body * env * 0.55f, -1f, 1f);
            }
            return Clip("cwskate_creak", d);
        }

        // the deck snapping in half: a loud dry crack and some splinters
        private static AudioClip MakeSnap()
        {
            int n = (int)(Rate * 0.7f);
            var d = new float[n];
            var rng = new System.Random(13);
            float lp = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float white = (float)(rng.NextDouble() * 2 - 1);
                lp += (white - lp) * 0.7f;
                float crack = lp * Mathf.Exp(-t * 30f) * 1.4f;
                float thud = Mathf.Sin(2 * Mathf.PI * (110f - 40f * t) * t) * Mathf.Exp(-t * 9f);
                float splinters = (rng.NextDouble() < 0.004 * Mathf.Exp(-t * 4f) * 60 ? white : 0f) * Mathf.Exp(-t * 5f);
                d[i] = Mathf.Clamp(crack + thud * 0.8f + splinters * 0.6f, -1f, 1f);
            }
            return Clip("cwskate_snap", d);
        }

        // wooden "BONK": short low knock with a crack on top
        private static AudioClip MakeHit()
        {
            int n = (int)(Rate * 0.32f);
            var d = new float[n];
            var rng = new System.Random(7);
            float lp = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float body = Mathf.Sin(2 * Mathf.PI * (140f - 60f * t) * t) * Mathf.Exp(-t * 14f);
                lp += ((float)(rng.NextDouble() * 2 - 1) - lp) * 0.5f;
                float crack = lp * Mathf.Exp(-t * 55f);
                d[i] = Mathf.Clamp(body * 0.9f + crack * 0.7f, -1f, 1f);
            }
            return Clip("cwskate_hit", d);
        }

        private static AudioSource ui;

        // non-positional one-shot (used outside of riding, e.g. picking up the board)
        public static void PlayUi(AudioClip clip, float vol)
        {
            if (clip == null) return;
            if (ui == null)
            {
                var go = new GameObject("CWSkate_UiAudio");
                go.hideFlags = HideFlags.HideAndDontSave;
                Object.DontDestroyOnLoad(go);
                ui = go.AddComponent<AudioSource>();
                ui.spatialBlend = 0f;
                ui.playOnAwake = false;
            }
            ui.PlayOneShot(clip, vol * SkatePlugin.Volume.Value);
        }

        // metal-on-metal scrape, loops
        private static AudioClip MakeGrind()
        {
            int n = Rate * 1, fade = 3000;
            var raw = new float[n + fade];
            var rng = new System.Random(33);
            float hp = 0, ph1 = 0, ph2 = 0;
            for (int i = 0; i < raw.Length; i++)
            {
                float white = (float)(rng.NextDouble() * 2 - 1);
                hp += (white - hp) * 0.6f;
                float t = i / (float)Rate;
                ph1 += 2 * Mathf.PI * (2300f + 180f * Mathf.Sin(t * 13f)) / Rate;
                ph2 += 2 * Mathf.PI * (3450f + 260f * Mathf.Sin(t * 7f)) / Rate;
                raw[i] = (white - hp) * 0.35f + Mathf.Sin(ph1) * 0.12f + Mathf.Sin(ph2) * 0.07f;
            }
            var d = new float[n];
            for (int i = 0; i < n; i++) d[i] = raw[i];
            for (int i = 0; i < fade; i++)
            {
                float k = i / (float)fade;
                d[i] = raw[i] * k + raw[n + i] * (1 - k);
            }
            return Clip("cwskate_grind", d);
        }

        private static AudioClip Clip(string name, float[] data)
        {
            var c = AudioClip.Create(name, data.Length, 1, Rate, false);
            c.SetData(data, 0);
            c.hideFlags = HideFlags.DontUnloadUnusedAsset;
            return c;
        }

        // Rumbly wheel noise with little clacks from pavement cracks. Loops seamlessly.
        private static AudioClip MakeRoll()
        {
            int n = Rate * 2, fade = 4000;
            var raw = new float[n + fade];
            var rng = new System.Random(7);
            float lp = 0, lp2 = 0, hp = 0;
            for (int i = 0; i < raw.Length; i++)
            {
                float white = (float)(rng.NextDouble() * 2 - 1);
                lp += (white - lp) * 0.09f;
                lp2 += (lp - lp2) * 0.09f;
                hp += (white - hp) * 0.5f;
                float t = i / (float)Rate;
                float ph = t % 0.5f;
                float clack = ph < 0.012f ? (float)(rng.NextDouble() * 2 - 1) * (1 - ph / 0.012f) * 0.35f : 0f;
                raw[i] = lp2 * 2.6f + (white - hp) * 0.04f + clack;
            }
            var d = new float[n];
            for (int i = 0; i < n; i++) d[i] = raw[i];
            for (int i = 0; i < fade; i++)
            {
                float k = i / (float)fade;
                d[i] = raw[i] * k + raw[n + i] * (1 - k);
            }
            for (int i = 0; i < n; i++) d[i] = Mathf.Clamp(d[i], -1, 1) * 0.9f;
            return Clip("cwskate_roll", d);
        }

        private static AudioClip MakePop()
        {
            int n = (int)(Rate * 0.16f);
            var d = new float[n];
            var rng = new System.Random(3);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float noise = (float)(rng.NextDouble() * 2 - 1) * Mathf.Exp(-t / 0.012f);
                float body = Mathf.Sin(2 * Mathf.PI * 210 * t) * Mathf.Exp(-t / 0.035f);
                d[i] = Mathf.Clamp(noise * 0.8f + body * 0.6f, -1, 1);
            }
            return Clip("cwskate_pop", d);
        }

        private static AudioClip MakeLand()
        {
            int n = (int)(Rate * 0.3f);
            var d = new float[n];
            var rng = new System.Random(5);
            float lp = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                lp += ((float)(rng.NextDouble() * 2 - 1) - lp) * 0.2f;
                float thump = Mathf.Sin(2 * Mathf.PI * 75 * t) * Mathf.Exp(-t / 0.07f);
                float clack = lp * Mathf.Exp(-t / 0.03f) * 2f;
                d[i] = Mathf.Clamp(thump * 0.9f + clack * 0.6f, -1, 1);
            }
            return Clip("cwskate_land", d);
        }

        private static AudioClip MakeChime()
        {
            float[] notes = { 659.25f, 830.6f, 987.8f, 1318.5f };
            int n = (int)(Rate * 0.9f);
            var d = new float[n];
            for (int k = 0; k < notes.Length; k++)
            {
                int start = (int)(Rate * 0.07f * k);
                for (int i = start; i < n; i++)
                {
                    float t = (i - start) / (float)Rate;
                    float env = Mathf.Exp(-t / 0.25f) * Mathf.Min(1, t / 0.005f);
                    d[i] += (Mathf.Sin(2 * Mathf.PI * notes[k] * t) + 0.3f * Mathf.Sin(4 * Mathf.PI * notes[k] * t)) * env * 0.22f;
                }
            }
            return Clip("cwskate_chime", d);
        }

        // Sad "aura lost" trombone-ish slide
        private static AudioClip MakeBail()
        {
            int n = (int)(Rate * 0.8f);
            var d = new float[n];
            float phase = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float f = Mathf.Lerp(330, 110, t / 0.8f) * (1 + 0.02f * Mathf.Sin(t * 40));
                phase += 2 * Mathf.PI * f / Rate;
                float env = Mathf.Min(1, t / 0.02f) * Mathf.Exp(-t / 0.5f);
                float s = Mathf.Sin(phase) + 0.5f * Mathf.Sin(2 * phase) + 0.25f * Mathf.Sin(3 * phase);
                d[i] = s * env * 0.3f;
            }
            return Clip("cwskate_bail", d);
        }

        private static AudioClip MakeWhoosh()
        {
            int n = (int)(Rate * 0.28f);
            var d = new float[n];
            var rng = new System.Random(9);
            float lp = 0, lp2 = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                float white = (float)(rng.NextDouble() * 2 - 1);
                lp += (white - lp) * 0.35f;
                lp2 += (lp - lp2) * 0.35f;
                d[i] = (lp - lp2) * Mathf.Sin(Mathf.PI * t) * 1.4f;
            }
            return Clip("cwskate_whoosh", d);
        }
    }
}
