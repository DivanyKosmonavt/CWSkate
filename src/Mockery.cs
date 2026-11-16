using System.Collections.Generic;
using UnityEngine;

namespace CWSkate
{
    // After an AURA GOD smack: a huge red "L" floats over the beaten monster with rotating insults under it,
    // and a sad trombone plays over it. Everyone with the mod sees it.
    internal static class Mockery
    {
        private class Mock
        {
            public Player monster;
            public float start;
            public bool trombone;
        }

        private static readonly List<Mock> mocks = new List<Mock>();

        // the first two always come first, right after the smack
        private static readonly string[] Ru =
        {
            "Моггнут", "Приготовлен",
            "лох", "-1 000 000 ауры", "Cringe Lord", "скилл ишью", "иди поплачь", "ратио", "минус монстр",
            "уволен из ада", "даже не больно было", "NPC", "позор семьи", "ну ты и пельмень"
        };

        private static readonly string[] En =
        {
            "Mogged", "Cooked",
            "-1,000,000 aura", "Cringe Lord", "skill issue", "go cry", "ratio", "fired from hell",
            "didn't even hurt", "NPC", "family disappointment", "get rekt"
        };

        private static readonly string[] Fr =
        {
            "Moggé", "Cuit",
            "-1 000 000 d'aura", "Cringe Lord", "skill issue", "va pleurer", "ratio", "viré de l'enfer",
            "même pas mal", "PNJ", "la honte de la famille", "t'es rekt", "le seum"
        };

        private static readonly string[] Zh =
        {
            "被碾压了", "熟透了",
            "气场 -1,000,000", "Cringe Lord", "菜就多练", "回家哭去吧", "ratio", "被地狱开除了",
            "一点都不疼", "NPC", "家族之耻", "寄了", "就这？"
        };

        public static void Start(Player monster)
        {
            if (monster == null) return;
            mocks.RemoveAll(m => m.monster == monster);
            mocks.Add(new Mock { monster = monster, start = Time.time });
        }

        // other players' smacks
        public static void StartFromNetwork(int viewID)
        {
            var view = Photon.Pun.PhotonView.Find(viewID);
            var p = view != null ? view.GetComponent<Player>() : null;
            if (p != null && p.ai) Start(p); // only ever over a monster
        }

        public static void Draw()
        {
            if (mocks.Count == 0) return;
            for (int i = mocks.Count - 1; i >= 0; i--)
                if (mocks[i].monster == null || Time.time - mocks[i].start > Melee.MockTime) mocks.RemoveAt(i);
            if (mocks.Count == 0) return;

            var mc = MainCamera.instance;
            if (mc == null || mc.Cam == null) return;
            var cam = mc.Cam;
            Vector3 camPos = cam.transform.position;
            int mask = HelperFunctions.GetMask(HelperFunctions.LayerType.TerrainProp);
            float s = Screen.height / 1080f;
            var phrases = Lang.Pick(Ru, En, Fr, Zh);

            foreach (var m in mocks)
            {
                float age = Time.time - m.start;
                Vector3 pos;
                try { pos = m.monster.HeadPosition() + Vector3.up * 0.8f; }
                catch { continue; }

                // sad trombone once it has landed
                if (!m.trombone && age > 1.2f && Event.current.type == EventType.Repaint)
                {
                    m.trombone = true;
                    AudioSource.PlayClipAtPoint(SkateSounds.Bail, pos, SkatePlugin.Volume.Value);
                }

                if (Event.current.type != EventType.Repaint) continue;
                float dist = Vector3.Distance(camPos, pos);
                if (dist > 90f) continue;
                Vector3 sp = cam.WorldToScreenPoint(pos);
                if (sp.z <= 0f) continue;
                if (Physics.Linecast(camPos, pos, out RaycastHit hit, mask, QueryTriggerInteraction.Ignore)
                    && hit.collider.GetComponentInParent<Player>() != m.monster) continue;

                float k = Mathf.Lerp(1.2f, 0.45f, dist / 90f);
                float pop = Mathf.Lerp(1.8f, 1f, Mathf.Clamp01(age / 0.25f)); // slams in big
                float wobble = Mathf.Sin(age * 9f) * 4f;
                float alpha = Mathf.Clamp01((Melee.MockTime - age) / 0.6f);
                float y = Screen.height - sp.y;
                int big = Mathf.Max(18, (int)(110 * s * k * pop));
                int small = Mathf.Max(11, (int)(30 * s * k));
                SkateHUD.Text(new Rect(sp.x - 300 + wobble, y - big * 1.25f, 600, big * 1.3f), "L", big, new Color(1f, 0.15f, 0.15f), TextAnchor.LowerCenter, alpha);
                // "Mogged", then "Cooked" (bigger, gold), then the rest of the insults in a random order
                int step = (int)(age / 0.8f);
                bool headline = step < 2;
                string phrase = headline ? phrases[step] : phrases[2 + (int)(step + m.start * 7f) % (phrases.Length - 2)];
                int size = headline ? (int)(small * 1.5f) : small;
                SkateHUD.Text(new Rect(sp.x - 300, y + 4, 600, size * 1.5f), phrase, size, headline ? new Color(1f, 0.85f, 0.3f) : Color.white, TextAnchor.UpperCenter, alpha);
            }
        }
    }
}
