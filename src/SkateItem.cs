using HarmonyLib;
using Photon.Pun;
using UnityEngine;
using Zorro.Core;

namespace CWSkate
{
    // The skateboard is a real item of the game: it's sold in the shop (Misc), comes with the delivery drone,
    // lies on the ground as a normal pickup, takes a slot in your inventory and can be dropped / thrown / given away.
    //   - board in a slot, slot not selected  -> it hangs on your back
    //   - board slot selected                 -> you hold it in your hands (LMB = hit with it, B = ride)
    //   - riding                              -> it's under your feet (hands are free, you can hold the camera)
    // Only one board per player.
    internal static class SkateItem
    {
        public const string ItemName = "Skateboard";
        public const string PersistentId = "5c8a7e2b-3d41-4f6a-9b2e-6c0d1a7f8e93"; // never change: saves use it
        public const int Price = 400; // the board's price in the shop

        public static Item Item { get; private set; }
        private static GameObject templateHolder;

        private static float recheck;
        public static bool Ready => Item != null;

        // In case the game rebuilds its item list: put the board back in.
        public static void EnsureRegistered()
        {
            if (Item == null) { TryRegister(); return; }
            recheck -= Time.unscaledDeltaTime;
            if (recheck > 0f) return;
            recheck = 2f;
            try
            {
                var db = SingletonAsset<ItemDatabase>.Instance;
                if (db != null && !db.Objects.Contains(Item))
                {
                    db.AddRuntimeEntry(Item);
                    SkatePlugin.Log.LogInfo("Skateboard item re-added to the item list, id " + Item.id);
                }
            }
            catch { }
        }

        // Adds the item to the game's item database. Every player with the mod gets the same item id.
        public static void TryRegister()
        {
            if (Item != null) return;
            ItemDatabase db;
            try { db = SingletonAsset<ItemDatabase>.Instance; }
            catch { return; }
            if (db == null || db.Objects == null) return;

            foreach (var it in db.Objects)
                if (it != null && it.persistentID == PersistentId) { Item = it; return; }

            var item = CreateBoardItem(false);
            db.AddRuntimeEntry(item);
            Item = item;
            SkatePlugin.Log.LogInfo("Skateboard item registered, id " + item.id);
        }

        // ------------------------------------------------------------ the Ride to Hell pro board (a sponsor's gift)

        // A second kind of board: the same in every way, only it always wears the Ride to Hell skin.
        // Not sold in the shop: the Ride to Hell sponsor deal sends it.
        public const string ProPersistentId = "8d2f5a1c-7b3e-4c9d-a6f1-2e4b8c0d9f17"; // never change
        public const string ProName = "Ride to Hell Board";
        public static Item ProItem { get; private set; }

        public static void EnsureProRegistered(ItemDatabase db)
        {
            if (Item == null || db == null) return;
            if (ProItem == null)
                foreach (var it in db.Objects)
                    if (it != null && it.persistentID == ProPersistentId) { ProItem = it; break; }
            if (ProItem == null)
            {
                ProItem = CreateBoardItem(true);
                db.AddRuntimeEntry(ProItem);
                SkatePlugin.Log.LogInfo("Ride to Hell board registered, id " + ProItem.id);
            }
            else if (!db.Objects.Contains(ProItem)) db.AddRuntimeEntry(ProItem);
        }

        public static bool IsPro(Item it) => it != null && (it == ProItem || it.persistentID == ProPersistentId);

        // The shop reads the item's own displayName (not GetLocalizedDisplayName), so our items keep it
        // in the mod's language. Called about once a second; cheap when nothing changed.
        public static void ApplyNames()
        {
            SetName(Item, Lang.T("Скейтборд", ItemName));
            SetName(ProItem, Lang.T("Доска Ride to Hell", ProName));
            SetName(SponsorItems.Policy, Lang.T("Страховой полис", SponsorItems.PolicyName));
            foreach (var d in SkateParts.Defs) SetName(d.Item, d.Name);
        }

        private static void SetName(Item it, string name)
        {
            if (it != null && it.displayName != name) it.displayName = name;
        }

        // is the board in our own inventory the Ride to Hell one?
        public static bool LocalIsPro
        {
            get
            {
                var p = Player.localPlayer;
                return p != null && FindSlot(p, out int slot) && p.TryGetInventory(out var inv) && IsPro(inv.slots[slot].ItemInSlot.item);
            }
        }

        private static Item CreateBoardItem(bool pro)
        {
            var item = ScriptableObject.CreateInstance<Item>();
            item.name = pro ? ProName : ItemName;
            item.displayName = pro ? ProName : ItemName;
            // Icons\<name>.png, or drawn in code
            item.icon = (pro ? SkateArt.EmbeddedIcon("ride_to_hell_board") : null) ?? SkateArt.EmbeddedIcon("skateboard") ?? SkateArt.Icon();
            item.itemObject = BuildItemObject(pro);
            item.itemType = Item.ItemType.Tool;
            item.toolBudgetCost = 1;
            item.spawnable = false;        // not found as loot in the old world
            item.mass = 2f;
            item.groundSizeMultiplier = 1f;
            item.groundMassMultiplier = 1f;
            // the hand is held like with any tool (the board itself is placed from the hand, see SkateboardItemBehaviour)
            item.holdPos = new Vector3(0.3f, -0.32f, 0.5f);
            item.holdRotation = Vector3.zero;
            item.persistentID = pro ? ProPersistentId : PersistentId;
            item.purchasable = !pro;       // the pro board only comes from the sponsor
            item.Category = ShopItemCategory.Misc;
            item.price = Price;
            item.quantity = 0;
            item.hideFlags = HideFlags.DontUnloadUnusedAsset;
            return item;
        }

        // The object the game spawns for the item (in your hand, or lying on the ground).
        // It stays switched off itself; the game makes active copies of it.
        private static GameObject BuildItemObject(bool pro)
        {
            if (templateHolder == null)
            {
                templateHolder = new GameObject("CWSkate_ItemTemplates");
                templateHolder.SetActive(false);
                templateHolder.hideFlags = HideFlags.HideAndDontSave;
                Object.DontDestroyOnLoad(templateHolder);
            }

            var root = new GameObject(pro ? "RideToHellBoardItem" : "SkateboardItem");
            root.transform.SetParent(templateHolder.transform, false);

            var visual = BoardVisual.Create("Board");
            visual.transform.SetParent(root.transform, false);
            visual.isItem = true;
            visual.pro = pro;

            var col = root.AddComponent<BoxCollider>();
            col.size = new Vector3(0.22f, 0.12f, 0.8f);
            col.center = new Vector3(0f, -0.02f, 0f);
            col.enabled = false; // the game switches item colliders on itself

            // where the right hand is: the item's origin, not rotated (the board is placed around the hand,
            // see SkateboardItemBehaviour)
            var gizmo = new GameObject("HandGizmo");
            gizmo.transform.SetParent(root.transform, false);
            gizmo.transform.localPosition = Vector3.zero;
            gizmo.transform.localRotation = Quaternion.identity;
            gizmo.AddComponent<HandGizmo>();
            new GameObject("Marker").transform.SetParent(gizmo.transform, false); // HandGizmo hides its first child

            root.AddComponent<ItemInstance>();
            root.AddComponent<SkateboardItemBehaviour>();
            return root;
        }

        // any skateboard: the normal one or the Ride to Hell pro board
        public static bool Is(Item it) => it != null && Item != null && (it == Item || it.persistentID == PersistentId || IsPro(it));

        // ------------------------------------------------------------ inventory helpers

        public static bool HasBoard(Player p) => FindSlot(p, out _);

        public static bool FindSlot(Player p, out int slotID)
        {
            slotID = -1;
            if (p == null || Item == null || !p.TryGetInventory(out var inv)) return false;
            for (int i = 0; i < inv.slots.Length; i++)
                if (Is(inv.slots[i].ItemInSlot.item)) { slotID = i; return true; }
            return false;
        }

        // true while the board is in your hands (its slot is selected)
        public static bool HoldingBoard(Player p) =>
            p != null && FindSlot(p, out int slot) && p.data.selectedItemSlot == slot;

        // After a bail: the board leaves your inventory and flies off as a normal pickup.
        public static bool DropBoard(Player p, Vector3 pos, Quaternion rot, Vector3 vel, Vector3 angVel)
        {
            if (!FindSlot(p, out int slot) || !p.TryGetInventory(out var inv)) return false;
            if (p.data.selectedItemSlot == slot) p.data.selectedItemSlot = -1;
            if (!inv.TryRemoveItemFromSlot(slot, out var desc)) return false;
            if (p.refs.view != null) BoardVisual.SetSkinOwner(desc.data, p.refs.view.OwnerActorNr); // the board on the floor keeps our skin
            p.RequestCreatePickup(desc.item.id, desc.data, pos, rot, vel, angVel);
            return true;
        }

        // ------------------------------------------------------------ patches

        // no localization entry for the board: use its plain name instead of an error
        [HarmonyPatch(typeof(Item), nameof(Item.GetLocalizedDisplayName))]
        private static class Patch_DisplayName
        {
            private static bool Prefix(Item __instance, ref string __result)
            {
                // in the mod's language (Auto = the game's)
                if (IsPro(__instance)) { __result = Lang.T("Доска Ride to Hell", ProName); return false; }
                if (Is(__instance)) { __result = Lang.T("Скейтборд", ItemName); return false; }
                if (SponsorItems.IsPolicy(__instance)) { __result = Lang.T("Страховой полис", SponsorItems.PolicyName); return false; }
                var part = SkateParts.DefOf(__instance);
                if (part != null) { __result = part.Name; return false; }
                return true;
            }
        }

        // (the shop falls back to displayName by itself, it just logs one harmless error about it)

        // Only one board: a player who already has one can't pick up another.
        [HarmonyPatch(typeof(PlayerInventory), nameof(PlayerInventory.TryAddItem), new[] { typeof(ItemDescriptor), typeof(InventorySlot) }, new[] { ArgumentType.Normal, ArgumentType.Out })]
        private static class Patch_OnlyOne
        {
            private static bool Prefix(PlayerInventory __instance, ItemDescriptor item, ref InventorySlot slot, ref bool __result)
            {
                if (!Is(item.item)) return true;
                foreach (var s in __instance.slots)
                {
                    if (!Is(s.ItemInSlot.item)) continue;
                    slot = null;
                    __result = false;
                    return false;
                }
                return true;
            }
        }

        // ...and doesn't even see "Pick up" on it
        [HarmonyPatch(typeof(Interactable), nameof(Interactable.IsValid))]
        private static class Patch_PickupValid
        {
            private static void Postfix(Interactable __instance, Player player, ref bool __result)
            {
                if (!__result || !(__instance is Pickup p) || p.itemInstance == null || !Is(p.itemInstance.item)) return;
                if (HasBoard(player)) __result = false;
            }
        }
    }

    // While swinging, the game's "where to hold the item" goes along the swing arc, so the whole arm swings.
    [HarmonyPatch(typeof(ItemInstance), nameof(ItemInstance.ConfigItemPosition))]
    internal static class Patch_SwingArm
    {
        private static void Postfix(ItemInstance __instance, Player player)
        {
            var sb = __instance.GetComponent<SkateboardItemBehaviour>();
            if (sb == null || !sb.Swinging || __instance.itemBody == null || player == null) return;
            try
            {
                var head = player.refs.ragdoll.GetBodypart(BodypartType.Head);
                var camPoint = head != null ? head.animationTarget.transform.Find("CameraPoint") : null;
                if (camPoint == null) return;
                sb.SwingPose(out Vector3 pos, out Vector3 e);
                // same order the game uses for item rotations
                Quaternion rot = Quaternion.LookRotation(player.data.lookDirection, player.GetUpDirection());
                rot *= Quaternion.Euler(Vector3.up * e.y);
                rot *= Quaternion.Euler(Vector3.right * e.x);
                rot *= Quaternion.Euler(Vector3.forward * e.z);
                var target = __instance.itemBody.animationTarget.transform;
                target.SetPositionAndRotation(Vector3.Lerp(target.position, camPoint.position + player.LookDirection(pos), 0.6f),
                                              Quaternion.Slerp(target.rotation, rot, 0.6f));
            }
            catch { }
        }
    }

    // Script on the item itself: placing the board in the hand, and hitting with it (LMB).
    internal class SkateboardItemBehaviour : ItemInstanceBehaviour
    {
        private Transform visual;

        // The game turns the hand exactly like the item (its hand point has no rotation), so relative to the item
        // the hand is always in the same pose: fingers up, palm facing forward. The board stands upright
        // in front of the palm with its bottom (trucks) against the palm and the grip facing out,
        // the palm between the two trucks - the way skaters carry a board.
        public static readonly Vector3 HeldOffset = new Vector3(-0.02f, 0.10f, 0.085f);
        private static readonly Quaternion HeldRotation = Quaternion.LookRotation(Vector3.up, Vector3.forward);

        public override void ConfigItem(ItemInstanceData data, PhotonView playerView) { }

        private void LateUpdate()
        {
            if (visual == null)
            {
                var bv = GetComponentInChildren<BoardVisual>();
                if (bv == null) return;
                visual = bv.transform;
            }
            // who holds it only changes when the item moves to another parent: don't look it up every frame
            if (transform.parent != lastParent || heldCache < 0)
            {
                lastParent = transform.parent;
                heldCache = GetComponentInParent<Player>() != null ? 1 : 0;
            }
            bool held = heldCache == 1;
            // lying on the ground: the board is just the item itself
            visual.localPosition = held ? HeldOffset : Vector3.zero;
            visual.localRotation = held ? HeldRotation : Quaternion.identity;

            // the collider follows the board (upright in the hand, flat on the ground)
            if (held != colliderHeld)
            {
                colliderHeld = held;
                var box = GetComponent<BoxCollider>();
                if (box != null)
                {
                    box.size = held ? new Vector3(0.22f, 0.8f, 0.12f) : new Vector3(0.22f, 0.12f, 0.8f);
                    box.center = held ? HeldOffset + new Vector3(0f, 0f, -0.02f) : new Vector3(0f, -0.02f, 0f);
                }
            }
        }

        private bool colliderHeld;
        private Transform lastParent;
        private int heldCache = -1;

        // ------------------------------------------------------------ hitting with the board (LMB while holding it)

        private const float SwingDuration = 0.5f;
        private const float StrikeAt = 0.5f; // part of the swing where it connects
        private float swingTime = -1f;
        private bool struck;
        private static float nextSwing;

        public bool Swinging => swingTime >= 0f;

        // The arm swing: where the hand (with the board) is, in front of the camera (x right, y up, z forward),
        // and how the board is tilted. Wind up over the shoulder, chop forward and down, come back.
        private static Vector3 RestPos => SkateItem.Item != null ? SkateItem.Item.holdPos : new Vector3(0.3f, -0.32f, 0.5f);
        private static readonly Vector3 WindUpPos = new Vector3(0.42f, 0.12f, 0.22f), WindUpRot = new Vector3(-55f, 25f, 10f);
        private static readonly Vector3 StrikePos = new Vector3(0.02f, -0.22f, 0.85f), StrikeRot = new Vector3(75f, -20f, -10f);

        public void SwingPose(out Vector3 pos, out Vector3 euler)
        {
            float t = Mathf.Clamp01(swingTime / SwingDuration);
            if (t < 0.32f)
            {
                float k = Smooth(t / 0.32f);
                pos = Vector3.Lerp(RestPos, WindUpPos, k);
                euler = Vector3.Lerp(Vector3.zero, WindUpRot, k);
            }
            else if (t < 0.55f)
            {
                float k = (t - 0.32f) / 0.23f;
                k = k * k; // accelerates into the hit
                pos = Vector3.Lerp(WindUpPos, StrikePos, k);
                euler = Vector3.Lerp(WindUpRot, StrikeRot, k);
            }
            else
            {
                float k = Smooth((t - 0.55f) / 0.45f);
                pos = Vector3.Lerp(StrikePos, RestPos, k);
                euler = Vector3.Lerp(StrikeRot, Vector3.zero, k);
            }
        }

        private static float Smooth(float x) => x * x * (3f - 2f * x);

        // also called for other players' boards (they told us they swung)
        public void PlaySwing()
        {
            swingTime = 0f;
            struck = false;
            AudioSource.PlayClipAtPoint(SkateSounds.Whoosh, transform.position, 0.7f * SkatePlugin.Volume.Value);
        }

        private void Update()
        {
            if (swingTime >= 0f)
            {
                swingTime += Time.deltaTime;
                if (isHeldByMe && !struck && swingTime >= SwingDuration * StrikeAt)
                {
                    struck = true;
                    Melee.Strike(Player.localPlayer);
                }
                if (swingTime >= SwingDuration) swingTime = -1f;
            }

            if (!isHeldByMe || !SkatePlugin.SwingEnabled.Value) return;
            var lp = Player.localPlayer;
            if (lp == null || lp.HasLockedInput() || !GlobalInputHandler.CanTakeInput()) return;
            if (lp.input.clickWasPressed && Time.time >= nextSwing && swingTime < 0f)
            {
                nextSwing = Time.time + SkatePlugin.SwingCooldown.Value;
                PlaySwing();
                NetSync.SendSwing(lp);
            }
        }
    }
}
