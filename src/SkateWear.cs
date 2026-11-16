using UnityEngine;

namespace CWSkate
{
    // Board wear: every crash, hard landing and smack takes a bit off. A worn board squeaks and creaks,
    // gets slower and weaker, and in the end snaps in half (gone from your inventory, buy a new one).
    // The wear lives in the item's own data, so it stays with the board when it's dropped, picked up or saved,
    // and the game shows it as a percentage on the item.
    internal static class BoardWear
    {
        public const float Max = 100f;

        public static LifeTimeEntry Entry(ItemInstanceData data, bool create)
        {
            if (data == null) return null;
            if (data.TryGetEntry<LifeTimeEntry>(out var e)) return e;
            if (!create) return null;
            e = new LifeTimeEntry { m_lifeTimeLeft = Max, m_maxLifeTime = Max };
            data.AddDataEntry(e);
            return e;
        }

        // 0..1 health of a board (1 = new)
        public static float Of(ItemInstanceData data)
        {
            var e = Entry(data, false);
            return e == null || e.m_maxLifeTime <= 0f ? 1f : Mathf.Clamp01(e.m_lifeTimeLeft / e.m_maxLifeTime);
        }

        // the board in our own inventory
        public static ItemInstanceData LocalData()
        {
            var p = Player.localPlayer;
            if (p == null || !SkateItem.FindSlot(p, out int slot) || !p.TryGetInventory(out var inv)) return null;
            return inv.slots[slot].ItemInSlot.data;
        }

        public static float Local => SkatePlugin.WearEnabled.Value ? Of(LocalData()) : 1f;
        public static bool LocalBroken => SkatePlugin.WearEnabled.Value && LocalData() != null && Of(LocalData()) <= 0f;

        // Takes wear off our board. Says so when it crosses half and a quarter.
        public static void Damage(float amount)
        {
            if (!SkatePlugin.WearEnabled.Value || amount <= 0f || DevTools.NoWear) return;
            var e = Entry(LocalData(), true);
            if (e == null) return;
            if (e.m_maxLifeTime <= 0f) e.m_maxLifeTime = Max;
            float before = e.m_lifeTimeLeft / e.m_maxLifeTime;
            // (a maple deck takes half the wear)
            e.m_lifeTimeLeft = Mathf.Max(0f, e.m_lifeTimeLeft - amount * SkatePlugin.WearRate.Value * BoardUpgrades.WearMul);
            float after = e.m_lifeTimeLeft / e.m_maxLifeTime;
            if (before > 0.5f && after <= 0.5f && after > 0f)
                SkateHUD.Popup(Lang.T("ДОСКА ТРЕСКАЕТСЯ", "YOUR BOARD IS CRACKING"), new Color(1f, 0.75f, 0.4f), 2f, small: true);
            if (before > 0.2f && after <= 0.2f && after > 0f)
            {
                SkateHUD.Popup(Lang.T("ДОСКА ВОТ-ВОТ СЛОМАЕТСЯ!", "THE BOARD IS ABOUT TO SNAP!"), new Color(1f, 0.45f, 0.3f), 2.4f, small: true);
                SkateSounds.PlayUi(SkateSounds.Creak, 0.8f);
            }
        }

        // Snaps our board: gone from the inventory, two halves fly off.
        public static void Snap(Player p, Vector3 pos, Quaternion rot, Vector3 vel, float scale)
        {
            if (p == null || !SkateItem.FindSlot(p, out int slot) || !p.TryGetInventory(out var inv)) return;
            if (p.data.selectedItemSlot == slot) p.data.selectedItemSlot = -1;
            bool pro = SkateItem.IsPro(inv.slots[slot].ItemInSlot.item);
            inv.TryRemoveItemFromSlot(slot, out _);
            BrokenBoard.Spawn(pos, rot, vel, scale, pro ? Skins.RideToHell : Skins.Local);
            if (p.refs.view != null) NetSync.SendSnap(p, pos, rot, vel, scale);
            SkateHUD.Popup(Lang.T("ДОСКА СЛОМАЛАСЬ ПОПОЛАМ!", "YOUR BOARD SNAPPED IN HALF!"), new Color(1f, 0.35f, 0.3f), 3f);
            SkateHUD.Popup(Lang.T("купи новую в магазине", "buy a new one in the shop"), new Color(1f, 0.7f, 0.6f), 3f, small: true);
            Progress.AddStat("boards_broken", 1);
            SkateActivity.Local(SkateAct.BoardSnap, 0, "", Mathf.Clamp(1f + vel.magnitude * 0.05f, 1f, 2f));
        }
    }

    // The two halves of a snapped board: real little physics objects that tumble and disappear after a while.
    internal static class BrokenBoard
    {
        public static void Spawn(Vector3 pos, Quaternion rot, Vector3 vel, float scale, SkinSet skin)
        {
            AudioSource.PlayClipAtPoint(SkateSounds.Snap, pos, SkatePlugin.Volume.Value);
            Vector3 side = rot * Vector3.right;
            for (int h = 0; h < 2; h++)
            {
                float dir = h == 0 ? 1f : -1f; // nose half, tail half
                var root = new GameObject("CWSkate_BrokenHalf");
                root.transform.SetPositionAndRotation(pos + rot * new Vector3(0f, 0.02f, dir * 0.2f * scale), rot);
                root.transform.localScale = Vector3.one * scale;
                Material grip = skin != null && skin.Grip ? skin.Grip : SkateArt.Grip;
                Material graphic = skin != null && skin.Graphic ? skin.Graphic : SkateArt.Graphic;
                Part(root.transform, new Vector3(0f, 0.008f, 0f), new Vector3(0.21f, 0.004f, 0.39f), Quaternion.identity, grip);         // grip on top
                Part(root.transform, new Vector3(0f, -0.001f, 0f), new Vector3(0.21f, 0.014f, 0.39f), Quaternion.identity, SkateArt.Wood); // deck
                Part(root.transform, new Vector3(0f, -0.009f, 0f), new Vector3(0.205f, 0.003f, 0.385f), Quaternion.identity, graphic);   // graphic underneath
                // splinters at the broken end
                for (int s = 0; s < 3; s++)
                    Part(root.transform, new Vector3(-0.07f + s * 0.07f, 0f, -dir * (0.2f + 0.02f * s)), new Vector3(0.035f, 0.01f, 0.05f),
                        Quaternion.Euler(Random.Range(-25f, 25f), Random.Range(-30f, 30f), 0f), SkateArt.Wood);
                // its truck and wheels
                Part(root.transform, new Vector3(0f, -0.03f, dir * 0.015f), new Vector3(0.15f, 0.022f, 0.035f), Quaternion.identity, SkateArt.Metal);
                foreach (float x in new[] { 0.087f, -0.087f })
                    Part(root.transform, new Vector3(x, -0.042f, dir * 0.015f), new Vector3(0.054f, 0.016f, 0.054f), Quaternion.Euler(0, 0, 90), SkateArt.Wheel, PrimitiveType.Cylinder);

                var col = root.AddComponent<BoxCollider>();
                col.size = new Vector3(0.21f, 0.06f, 0.4f);
                col.center = new Vector3(0f, -0.02f, 0f);
                var rb = root.AddComponent<Rigidbody>();
                rb.mass = 0.8f;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                rb.linearVelocity = vel + side * (dir * 1.5f) + rot * new Vector3(0f, 0f, dir * 2f) + Vector3.up * 3f;
                rb.angularVelocity = Random.insideUnitSphere * 12f;
                Object.Destroy(root, 20f);
            }
        }

        private static void Part(Transform parent, Vector3 pos, Vector3 size, Quaternion r, Material mat, PrimitiveType type = PrimitiveType.Cube)
        {
            var go = GameObject.CreatePrimitive(type);
            var c = go.GetComponent<Collider>();
            if (c) Object.DestroyImmediate(c);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = r;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = mat;
        }
    }

    internal partial class SkateController
    {
        private float squeakTimer = 1f, creakTimer = 4f;

        // 0..1 how worn out our board is (0 = new)
        private float Worn => 1f - BoardWear.Local;

        // a weak board: slower and the legs (and deck) take less of a landing
        private float WearWeakness => Mathf.InverseLerp(0.5f, 0f, BoardWear.Local); // 0 above half, 1 when nearly gone

        // takes wear off; if that was the last of it while riding, the board snaps under you
        private void Wear(float amount)
        {
            BoardWear.Damage(amount);
            if (BoardWear.LocalBroken && Riding && !bailed) Bail(Lang.T("ДОСКА СЛОМАЛАСЬ!", "YOUR BOARD SNAPPED!"), trip: true);
        }

        // a worn board squeaks (wheels) and creaks (deck) while you ride
        private void WearSounds(float dt)
        {
            float worn = Worn;
            if (!SkatePlugin.WearEnabled.Value || worn < 0.25f || bailed) return;
            float k = Mathf.InverseLerp(0.25f, 1f, worn); // 0 at 75% left, 1 at the end
            if (!airborne && !grinding && speed > 1.5f)
            {
                squeakTimer -= dt;
                if (squeakTimer <= 0f)
                {
                    squeakTimer = Mathf.Lerp(2.6f, 0.55f, k) * Random.Range(0.7f, 1.3f) / Mathf.Clamp(speed / 6f, 0.6f, 2f);
                    Play(SkateSounds.Squeak, Mathf.Lerp(0.15f, 0.45f, k));
                }
            }
            creakTimer -= dt;
            if (creakTimer <= 0f)
            {
                creakTimer = Mathf.Lerp(9f, 2.5f, k) * Random.Range(0.6f, 1.4f);
                if (!airborne)
                {
                    Play(SkateSounds.Creak, Mathf.Lerp(0.25f, 0.7f, k));
                    if (worn > 0.6f) SkateActivity.Local(SkateAct.WornBoard, combo.Count, "", 1f + k * 0.5f); // "that board is about to go"
                }
            }
        }

        // landing: a creak if the board is worn, and some wear for a hard one
        private void WearLanding()
        {
            float hard = -lastAirVy - LandingLimit * 0.6f;
            if (hard > 0f) Wear(1.5f + hard * 0.5f);
            if (Worn > 0.4f && Random.value < Worn) Play(SkateSounds.Creak, 0.5f);
        }

        // the board model shakes a bit when it's nearly gone
        private Quaternion WearWobble()
        {
            float k = Mathf.InverseLerp(0.3f, 0f, BoardWear.Local);
            if (k <= 0f || !SkatePlugin.WearEnabled.Value) return Quaternion.identity;
            return Quaternion.Euler(Mathf.Sin(Time.time * 37f) * 1.2f * k, 0f, Mathf.Sin(Time.time * 29f) * 2.5f * k);
        }
    }
}
