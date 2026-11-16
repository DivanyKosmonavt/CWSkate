using HarmonyLib;
using Photon.Pun;
using UnityEngine;

namespace CWSkate
{
    // Things the skate sponsors send with their deals (not sold in the shop):
    //  - the Ride to Hell pro board (see SkateItem: a second kind of board with its own skin);
    //  - the Bail Insurance Co. policy: a sheet of paper that works like Minecraft's totem of undying.
    //    Keep it anywhere in your inventory: the moment you would die, you don't - the policy is used up instead
    //    and you're back on your feet with some health.
    internal static class SponsorItems
    {
        public const string PolicyId = "4f7c1e9a-2d6b-4a8e-b3f5-9c0e1d2a7b63"; // never change
        public const string PolicyName = "Insurance Policy";
        public const float HealthAfterSave = 30f; // like the game's own revive
        public const float InvulnerableTime = 15f; // after a save nothing can hurt you for a while

        private static float invulnerableUntil = -1f;
        public static float InvulnerableLeft => Mathf.Max(0f, invulnerableUntil - Time.time);
        public static bool Invulnerable => Time.time < invulnerableUntil;

        public static Item Policy { get; private set; }
        private static GameObject templateHolder;

        public static bool IsPolicy(Item it) => it != null && (it == Policy || it.persistentID == PolicyId);

        // after the board parts (everyone registers in the same order, so the item ids match)
        public static void EnsureRegistered(ItemDatabase db)
        {
            SkateItem.EnsureProRegistered(db);
            if (db == null || SkateItem.ProItem == null) return;
            if (Policy == null)
                foreach (var it in db.Objects)
                    if (it != null && it.persistentID == PolicyId) { Policy = it; break; }
            if (Policy == null)
            {
                Policy = CreatePolicy();
                db.AddRuntimeEntry(Policy);
                SkatePlugin.Log.LogInfo("Insurance policy registered, id " + Policy.id);
            }
            else if (!db.Objects.Contains(Policy)) db.AddRuntimeEntry(Policy);
        }

        private static Item CreatePolicy()
        {
            var item = ScriptableObject.CreateInstance<Item>();
            item.name = PolicyName;
            item.displayName = PolicyName;
            item.icon = SkateArt.EmbeddedIcon("insurance_policy") ?? SkateArt.PolicyIcon();
            item.itemObject = BuildPolicyObject();
            item.itemType = Item.ItemType.Tool;
            item.toolBudgetCost = 1;
            item.spawnable = false;
            item.mass = 1f; // (a very light item shook in the hand)
            item.groundSizeMultiplier = 1f;
            item.groundMassMultiplier = 1f;
            item.holdPos = new Vector3(0.3f, -0.3f, 0.5f);
            item.holdRotation = Vector3.zero;
            item.persistentID = PolicyId;
            item.purchasable = false; // only from the Bail Insurance Co. deal
            item.Category = ShopItemCategory.Misc;
            item.price = 0;
            item.quantity = 0;
            item.hideFlags = HideFlags.DontUnloadUnusedAsset;
            return item;
        }

        // a sheet of paper: held up in front of the palm / lying flat on the floor
        private static GameObject BuildPolicyObject()
        {
            if (templateHolder == null)
            {
                templateHolder = new GameObject("CWSkate_SponsorTemplates");
                templateHolder.SetActive(false);
                templateHolder.hideFlags = HideFlags.HideAndDontSave;
                Object.DontDestroyOnLoad(templateHolder);
            }
            var root = new GameObject("InsurancePolicy");
            root.transform.SetParent(templateHolder.transform, false);
            var model = new GameObject("Model").transform;
            model.SetParent(root.transform, false);
            var sheet = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var c = sheet.GetComponent<Collider>();
            if (c) Object.DestroyImmediate(c);
            sheet.transform.SetParent(model, false);
            // An A4 sheet next to the game's characters: they're about twice the size of a real person,
            // so a real-size 21 x 29.7 cm sheet looked tiny in their hands. This one is A4 to them.
            sheet.transform.localScale = new Vector3(0.42f, 0.003f, 0.594f);
            sheet.GetComponent<Renderer>().sharedMaterial = SkateArt.Paper;

            var col = root.AddComponent<BoxCollider>();
            col.size = new Vector3(0.44f, 0.012f, 0.62f);
            col.center = new Vector3(0f, 0.005f, 0f);
            col.enabled = false; // the game switches item colliders on itself

            // (the part behaviour gives it the two poses; flag 0 = nothing happens on LMB)
            var b = root.AddComponent<SkatePartBehaviour>();
            b.flag = 0;
            // held upright by its bottom edge in front of the palm / flat on the floor
            b.SetPoses(new Vector3(0f, 0.24f, 0.07f), Quaternion.Euler(-90f, 0f, 0f), new Vector3(0f, 0.003f, 0f), Quaternion.identity);
            model.localPosition = b.groundPos;
            model.localRotation = b.groundRot;

            var gizmo = new GameObject("HandGizmo");
            gizmo.transform.SetParent(root.transform, false);
            gizmo.AddComponent<HandGizmo>();
            new GameObject("Marker").transform.SetParent(gizmo.transform, false);

            root.AddComponent<ItemInstance>();
            return root;
        }

        // ------------------------------------------------------------ the totem of undying

        // A policy in the inventory: used up, and you live. True if it saved you.
        private static bool TryUseInsurance(Player p)
        {
            if (p == null || p.ai || p.data == null || p.data.dead || !p.TryGetInventory(out var inv)) return false;
            int slot = -1;
            for (int i = 0; i < inv.slots.Length; i++)
                if (IsPolicy(inv.slots[i].ItemInSlot.item)) { slot = i; break; }
            if (slot < 0) return false;
            if (p.data.selectedItemSlot == slot) p.data.selectedItemSlot = -1;
            if (!inv.TryRemoveItemFromSlot(slot, out _)) return false;

            p.data.health = Mathf.Max(p.data.health, HealthAfterSave);
            invulnerableUntil = Time.time + InvulnerableTime;
            SkatePlugin.Log.LogInfo("Insurance policy used: death cancelled, invulnerable for " + InvulnerableTime + " s");
            SkateHUD.Popup(Lang.T("СТРАХОВКА СРАБОТАЛА!", "INSURANCE PAID OUT!"), new Color(1f, 0.85f, 0.3f), 3f);
            SkateHUD.Popup(Lang.F("Bail Insurance Co. оплатила твою смерть - ты неуязвим {0} сек", "Bail Insurance Co. covered your death - untouchable for {0} s", (int)InvulnerableTime), new Color(0.7f, 1f, 0.7f), 3f, small: true);
            SkateSounds.PlayUi(SkateSounds.Chime, 1f);
            PlayTotemFx(p);
            if (p.refs.view != null) NetSync.SendTotem(p);
            Progress.AddStat("insured", 1);
            SkateActivity.Local(SkateAct.Insured, 0, "", 2f); // cheating death on camera is great content
            return true;
        }

        // the golden burst around whoever was saved (everyone sees it)
        public static void PlayTotemFx(Player p)
        {
            if (p == null) return;
            var torso = p.refs.ragdoll != null ? p.refs.ragdoll.GetBodypart(BodypartType.Torso) : null;
            Vector3 at = torso != null ? torso.transform.position : p.Center();
            SkateFx.TotemBurst(at);
            if (!p.IsLocal) AudioSource.PlayClipAtPoint(SkateSounds.Chime, at, SkatePlugin.Volume.Value);
        }

        // Untouchable after a save: no damage at all
        [HarmonyPatch(typeof(Player), nameof(Player.TakeDamage))]
        private static class Patch_Invulnerable
        {
            private static bool Prefix(Player __instance)
            {
                return !(Invulnerable && __instance.refs.view != null && __instance.refs.view.IsMine);
            }
        }

        // You'd die from damage (your own game decides that): cancel it before anyone is told.
        [HarmonyPatch(typeof(Player), "Die")]
        private static class Patch_Die
        {
            private static bool Prefix(Player __instance)
            {
                try
                {
                    if (__instance.refs.view == null || !__instance.refs.view.IsMine) return true;
                    if (Invulnerable) { __instance.data.health = Mathf.Max(__instance.data.health, 1f); return false; } // still untouchable
                    return !TryUseInsurance(__instance);
                }
                catch (System.Exception e) { SkatePlugin.Log.LogWarning("Insurance: " + e.Message); return true; }
            }
        }

        // Someone else killed you outright (e.g. a monster on the host's game): you don't die,
        // and everyone else, who has just seen you die, sees you get back up.
        [HarmonyPatch(typeof(Player), "RPCA_PlayerDie")]
        private static class Patch_RpcDie
        {
            private static bool Prefix(Player __instance)
            {
                try
                {
                    if (__instance.refs.view == null || !__instance.refs.view.IsMine || __instance.data.dead) return true;
                    if (!Invulnerable && !TryUseInsurance(__instance)) return true;
                    __instance.refs.view.RPC("RPCA_PlayerRevive", RpcTarget.Others);
                    return false;
                }
                catch (System.Exception e) { SkatePlugin.Log.LogWarning("Insurance: " + e.Message); return true; }
            }
        }
    }
}
