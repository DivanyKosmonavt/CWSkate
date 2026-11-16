using HarmonyLib;
using Photon.Pun;
using UnityEngine;
using Zorro.Core;

namespace CWSkate
{
    // Board upgrades: parts sold in the shop (Misc) like any item. Hold one and press LMB: it goes onto your board.
    // What's on the board is stored in the board item itself (like the wear), so the parts go wherever the board goes:
    // given to a friend, dropped, saved. A board that snaps takes its parts with it.
    internal static class BoardUpgrades
    {
        // one bit per part (stored in the board's IntRangeEntry.selectedValue; maxValue = the rocket's fuel, 0..100)
        public const int SpeedWheels = 1, SoftWheels = 2, MapleDeck = 4, Rocket = 8;
        public const int FuelCan = 16; // a fuel canister: not a part on the board, it fills the rocket's tank
        public const int ProBoard = 32; // not stored: set in Local (and sent to others) when our board is the Ride to Hell one
        public const int WheelsMask = SpeedWheels | SoftWheels; // one set of wheels at a time
        public const int FullTank = 100;

        public static int Of(ItemInstanceData data) =>
            data != null && data.TryGetEntry<IntRangeEntry>(out var e) ? e.selectedValue : 0;

        private static IntRangeEntry Entry(ItemInstanceData data)
        {
            if (!data.TryGetEntry<IntRangeEntry>(out var e)) { e = new IntRangeEntry(); data.AddDataEntry(e); }
            return e;
        }

        public static void Set(ItemInstanceData data, int flags)
        {
            if (data != null) Entry(data).selectedValue = flags;
        }

        // the rocket's fuel, 0..100
        public static int FuelOf(ItemInstanceData data) =>
            data != null && data.TryGetEntry<IntRangeEntry>(out var e) ? Mathf.Clamp(e.maxValue, 0, FullTank) : 0;

        public static void SetFuel(ItemInstanceData data, int fuel)
        {
            if (data != null) Entry(data).maxValue = Mathf.Clamp(fuel, 0, FullTank);
        }

        // burning fuel bit by bit (the board keeps whole units)
        private static float burnt;
        public static void BurnLocal(float amount)
        {
            var data = BoardWear.LocalData();
            if (data == null) return;
            burnt += amount;
            if (burnt < 1f) return;
            int whole = (int)burnt;
            burnt -= whole;
            SetFuel(data, FuelOf(data) - whole);
            cacheFrame = -1;
        }

        public static int LocalFuel => FuelOf(BoardWear.LocalData());

        // the parts on the board in our own inventory (worked out once a frame)
        private static int cacheFrame = -1, cacheFlags;
        public static int Local
        {
            get
            {
                if (cacheFrame != Time.frameCount)
                {
                    cacheFrame = Time.frameCount;
                    cacheFlags = Of(BoardWear.LocalData()) & ~ProBoard;
                    if (SkateItem.LocalIsPro) cacheFlags |= ProBoard;
                }
                return cacheFlags;
            }
        }

        public static bool Has(int flag) => (Local & flag) != 0;

        // what each part does
        // The Ride to Hell pro board is as fast as speed wheels without their worse turning, and wears out 2.5x slower.
        private static bool Fast => Has(SpeedWheels) || Has(ProBoard);
        public static float MaxSpeedMul => Fast ? 1.25f : 1f;                                       // speed wheels / pro board: faster top speed...
        public static float TurnMul => Has(SpeedWheels) ? 0.8f : 1f;                                // ...but speed wheels hold turns worse
        public static float PushTopMul => (Fast ? 1.2f : 1f) * (Has(SoftWheels) ? 0.92f : 1f);      // soft wheels are a bit slower
        public static float TripMul => Has(SoftWheels) ? 1.6f : 1f;                                 // soft wheels roll over small stuff
        public static float RollVolumeMul => Has(SoftWheels) ? 0.6f : 1f;                           // and they're quieter
        public static float WearMul => (Has(MapleDeck) ? 0.5f : 1f) * (Has(ProBoard) ? 0.4f : 1f);  // maple deck x2 slower, pro board x2.5 slower

        // "SPEED WHEELS + MAPLE DECK" for the HUD ("" = nothing on it)
        public static string Describe(int flags)
        {
            string s = "";
            foreach (var d in SkateParts.Defs)
                if ((flags & d.Flag) != 0) s += (s.Length > 0 ? " + " : "") + d.Name.ToUpperInvariant();
            return s;
        }
    }

    internal enum PartKind { Wheels, Deck, Rocket, Fuel }

    internal class PartDef
    {
        public int Flag;
        public string PersistentId, NameEn, NameRu;
        public string Name => Lang.T(NameRu, NameEn); // in the mod's language
        public int Price;
        public Color Color;
        public PartKind Kind;
        public bool Wheels => Kind == PartKind.Wheels;
        // the shop picture: Icons\<IconName>.png in the project (built into the DLL)
        public string IconName => NameEn.ToLowerInvariant().Replace(" booster", "").Replace(' ', '_');
        public Item Item;
    }

    internal static class SkateParts
    {
        // never change the ids (saves use them) or the order (every player registers them in the same order)
        public static readonly PartDef[] Defs =
        {
            new PartDef { Flag = BoardUpgrades.SpeedWheels, PersistentId = "7e1c2a90-5b3d-4c8e-a1f2-0d9e8b7c6a51", NameEn = "Speed Wheels", NameRu = "Скоростные колёса", Price = 35, Kind = PartKind.Wheels, Color = new Color(1f, 0.38f, 0.12f) },
            new PartDef { Flag = BoardUpgrades.SoftWheels, PersistentId = "2b9d4f61-8a7e-4d3c-b5a0-6e1f2c3d4b72", NameEn = "Soft Wheels", NameRu = "Мягкие колёса", Price = 25, Kind = PartKind.Wheels, Color = new Color(0.4f, 0.9f, 0.45f) },
            new PartDef { Flag = BoardUpgrades.MapleDeck, PersistentId = "c4e8a1b3-6f2d-4e9a-8c7b-1a5d3f9e2c84", NameEn = "Maple Deck", NameRu = "Кленовая дека", Price = 45, Kind = PartKind.Deck, Color = new Color(0.93f, 0.76f, 0.5f) },
            new PartDef { Flag = BoardUpgrades.Rocket, PersistentId = "9a3f6c2e-1d4b-4b7a-9e8f-5c2a7d1e3b95", NameEn = "Rocket Booster", NameRu = "Ракетный ускоритель", Price = 120, Kind = PartKind.Rocket, Color = new Color(0.9f, 0.15f, 0.12f) },
            new PartDef { Flag = BoardUpgrades.FuelCan, PersistentId = "e5b2d8f4-3c1a-4f6e-a9d7-8b4c2e6f1a06", NameEn = "Rocket Fuel", NameRu = "Ракетное топливо", Price = 10, Kind = PartKind.Fuel, Color = new Color(1f, 0.75f, 0.1f) },
        };

        private static GameObject templateHolder;

        public static PartDef DefOf(Item it)
        {
            if (it == null) return null;
            foreach (var d in Defs) if (d.Item == it || it.persistentID == d.PersistentId) return d;
            return null;
        }

        public static PartDef DefOf(int flag)
        {
            foreach (var d in Defs) if (d.Flag == flag) return d;
            return null;
        }

        private static float recheck;

        // after the board (same order for everyone, so every player gets the same item ids)
        public static void EnsureRegistered()
        {
            if (SkateItem.Item == null) return;
            // once everything is in, only look again every couple of seconds (the game may rebuild its list)
            if (SponsorItems.Policy != null)
            {
                recheck -= Time.unscaledDeltaTime;
                if (recheck > 0f) return;
                recheck = 2f;
            }
            ItemDatabase db;
            try { db = SingletonAsset<ItemDatabase>.Instance; }
            catch { return; }
            if (db == null || db.Objects == null) return;
            foreach (var d in Defs)
            {
                if (d.Item == null)
                {
                    foreach (var it in db.Objects)
                        if (it != null && it.persistentID == d.PersistentId) { d.Item = it; break; }
                }
                if (d.Item == null)
                {
                    d.Item = Create(d);
                    db.AddRuntimeEntry(d.Item);
                    SkatePlugin.Log.LogInfo("Board part registered: " + d.NameEn + ", id " + d.Item.id);
                }
                else if (!db.Objects.Contains(d.Item)) db.AddRuntimeEntry(d.Item); // the game rebuilt its list
            }
            SponsorItems.EnsureRegistered(db); // then the sponsors' gifts: the pro board and the insurance policy
        }

        private static Item Create(PartDef d)
        {
            var item = ScriptableObject.CreateInstance<Item>();
            item.name = d.NameEn;
            item.displayName = d.NameEn;
            item.icon = SkateArt.EmbeddedIcon(d.IconName) ?? SkateArt.PartIcon(d.Color, d.Kind); // Icons\<name>.png, or drawn in code
            item.itemObject = BuildObject(d);
            item.itemType = Item.ItemType.Tool;
            item.toolBudgetCost = 1;
            item.spawnable = false;
            item.mass = 1f;
            item.groundSizeMultiplier = 1f;
            item.groundMassMultiplier = 1f;
            item.holdPos = new Vector3(0.3f, -0.3f, 0.5f);
            item.holdRotation = Vector3.zero;
            item.persistentID = d.PersistentId;
            item.purchasable = true;
            item.Category = ShopItemCategory.Misc;
            item.price = d.Price;
            item.quantity = 0;
            item.hideFlags = HideFlags.DontUnloadUnusedAsset;
            return item;
        }

        // what you hold / what lies on the ground: a pair of wheels on an axle, or a bare deck
        private static GameObject BuildObject(PartDef d)
        {
            if (templateHolder == null)
            {
                templateHolder = new GameObject("CWSkate_PartTemplates");
                templateHolder.SetActive(false);
                templateHolder.hideFlags = HideFlags.HideAndDontSave;
                Object.DontDestroyOnLoad(templateHolder);
            }
            var root = new GameObject("SkatePart_" + d.Flag);
            root.transform.SetParent(templateHolder.transform, false);
            var model = new GameObject("Model").transform;
            model.SetParent(root.transform, false);
            var col = root.AddComponent<BoxCollider>();
            var b = root.AddComponent<SkatePartBehaviour>();
            b.flag = d.Flag;
            // Two poses for the model: in the hand and on the ground.
            // In the hand the item's origin is the hand: fingers point up (+y), the palm faces forward (+z).
            if (d.Wheels)
            {
                // an axle with two wheels: held across the palm / lying on the floor
                var wheel = d.Flag == BoardUpgrades.SpeedWheels ? SkateArt.WheelSpeed : SkateArt.WheelSoft;
                Part(model, PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.012f, 0.11f, 0.012f), Quaternion.Euler(0, 0, 90), SkateArt.Metal);
                foreach (float x in new[] { 0.1f, -0.1f })
                    Part(model, PrimitiveType.Cylinder, new Vector3(x, 0f, 0f), new Vector3(0.075f, 0.022f, 0.075f), Quaternion.Euler(0, 0, 90), wheel);
                b.SetPoses(new Vector3(0f, 0.07f, 0.075f), Quaternion.identity, new Vector3(0f, 0.038f, 0f), Quaternion.identity);
                col.size = new Vector3(0.28f, 0.08f, 0.08f);
                col.center = new Vector3(0f, 0.038f, 0f);
            }
            else if (d.Kind == PartKind.Rocket)
            {
                // held upright in front of the palm, nose up / lying on its side on the floor
                BoardVisual.BuildRocket(model, 1.4f);
                b.SetPoses(new Vector3(0f, 0.09f, 0.075f), Quaternion.Euler(-90f, 0f, 0f), new Vector3(0f, 0.06f, 0f), Quaternion.identity);
                col.size = new Vector3(0.13f, 0.13f, 0.46f);
                col.center = new Vector3(0f, 0.06f, 0.02f);
            }
            else if (d.Kind == PartKind.Fuel)
            {
                // a red jerry can: carried by the handle (it hangs under the hand) / standing on the floor
                Part(model, PrimitiveType.Cube, Vector3.zero, new Vector3(0.17f, 0.24f, 0.08f), Quaternion.identity, SkateArt.RocketRed);
                Part(model, PrimitiveType.Cube, new Vector3(-0.02f, 0.135f, 0f), new Vector3(0.09f, 0.025f, 0.03f), Quaternion.identity, SkateArt.Metal);
                Part(model, PrimitiveType.Cylinder, new Vector3(0.06f, 0.14f, 0f), new Vector3(0.025f, 0.03f, 0.025f), Quaternion.Euler(0, 0, -25f), SkateArt.Metal);
                b.SetPoses(new Vector3(0.02f, -0.11f, 0.06f), Quaternion.identity, new Vector3(0f, 0.12f, 0f), Quaternion.identity);
                col.size = new Vector3(0.18f, 0.28f, 0.1f);
                col.center = new Vector3(0f, 0.14f, 0f);
            }
            else
            {
                // a bare deck: carried like the board (upright in front of the palm) / flat on the floor
                Part(model, PrimitiveType.Cube, Vector3.zero, new Vector3(0.21f, 0.014f, 0.78f), Quaternion.identity, SkateArt.Maple);
                b.SetPoses(SkateboardItemBehaviour.HeldOffset, Quaternion.LookRotation(Vector3.up, Vector3.forward), new Vector3(0f, 0.01f, 0f), Quaternion.identity);
                col.size = new Vector3(0.22f, 0.03f, 0.8f);
                col.center = new Vector3(0f, 0.01f, 0f);
            }
            model.localPosition = b.groundPos;
            model.localRotation = b.groundRot;
            col.enabled = false; // the game switches item colliders on itself

            var gizmo = new GameObject("HandGizmo");
            gizmo.transform.SetParent(root.transform, false);
            gizmo.AddComponent<HandGizmo>();
            new GameObject("Marker").transform.SetParent(gizmo.transform, false); // HandGizmo hides its first child

            root.AddComponent<ItemInstance>();
            return root;
        }

        private static void Part(Transform parent, PrimitiveType type, Vector3 pos, Vector3 scale, Quaternion rot, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            var c = go.GetComponent<Collider>();
            if (c) Object.DestroyImmediate(c);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = rot;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
        }

        // Put the part in your hand onto the board in your inventory. Swapping wheels: the old pair drops at your feet.
        public static void Install(Player p, int flag)
        {
            var d = DefOf(flag);
            if (p == null || d == null) return;
            if (!SkateItem.FindSlot(p, out int boardSlot) || !p.TryGetInventory(out var inv))
            {
                SkateHUD.Popup(Lang.T("НУЖНА ДОСКА В ИНВЕНТАРЕ", "YOU NEED A BOARD IN YOUR INVENTORY"), new Color(1f, 0.7f, 0.5f), 2f, small: true);
                return;
            }
            var data = inv.slots[boardSlot].ItemInSlot.data;
            int cur = BoardUpgrades.Of(data);
            int hand = p.data.selectedItemSlot;
            if (hand < 0 || hand >= inv.slots.Length || DefOf(inv.slots[hand].ItemInSlot.item) != d) return;

            // a fuel canister: fills the rocket's tank
            if (d.Kind == PartKind.Fuel)
            {
                if ((cur & BoardUpgrades.Rocket) == 0) { SkateHUD.Popup(Lang.T("СНАЧАЛА ПОСТАВЬ РАКЕТУ НА ДОСКУ", "PUT A ROCKET ON YOUR BOARD FIRST"), new Color(1f, 0.7f, 0.5f), 2f, small: true); return; }
                if (BoardUpgrades.FuelOf(data) >= BoardUpgrades.FullTank) { SkateHUD.Popup(Lang.T("БАК И ТАК ПОЛНЫЙ", "THE TANK IS ALREADY FULL"), new Color(1f, 0.8f, 0.5f), 1.6f, small: true); return; }
                if (!inv.TryRemoveItemFromSlot(hand, out _)) return;
                BoardUpgrades.SetFuel(data, BoardUpgrades.FullTank);
                SkateSounds.PlayUi(SkateSounds.Whoosh, 0.6f);
                SkateHUD.Popup(Lang.T("РАКЕТА ЗАПРАВЛЕНА", "ROCKET REFUELED"), d.Color, 2f);
                return;
            }

            if ((cur & flag) != 0)
            {
                SkateHUD.Popup(Lang.T("УЖЕ СТОИТ НА ДОСКЕ", "ALREADY ON YOUR BOARD"), new Color(1f, 0.8f, 0.5f), 1.6f, small: true);
                return;
            }
            if (!inv.TryRemoveItemFromSlot(hand, out _)) return;

            int old = d.Wheels ? cur & BoardUpgrades.WheelsMask : 0;
            if (d.Wheels) cur &= ~BoardUpgrades.WheelsMask;
            BoardUpgrades.Set(data, cur | flag);
            if (d.Kind == PartKind.Rocket) BoardUpgrades.SetFuel(data, BoardUpgrades.FullTank); // comes with a full tank

            var oldDef = old != 0 ? DefOf(old) : null;
            if (oldDef != null && oldDef.Item != null)
            {
                // the old wheels aren't lost: they drop in front of you (sell them, give them away, put them back on)
                Vector3 at = p.Center() + p.data.lookDirection.normalized * 0.6f;
                p.RequestCreatePickup(oldDef.Item, new ItemInstanceData(System.Guid.NewGuid()), at, Quaternion.identity);
            }
            SkateSounds.PlayUi(SkateSounds.Land, 0.8f);
            SkateSounds.PlayUi(SkateSounds.Chime, 0.5f);
            SkateHUD.Popup(Lang.F("НА ДОСКЕ: {0}", "{0} INSTALLED", d.Name.ToUpperInvariant()), d.Color, 2.2f);
            if (oldDef != null) SkateHUD.Popup(Lang.F("старые {0} выпали тебе под ноги", "the old {0} dropped at your feet", oldDef.Name.ToLowerInvariant()), new Color(1f, 1f, 1f, 0.8f), 2.2f, small: true);
            SkatePlugin.Log.LogInfo("Board part installed: " + d.NameEn + " (board now " + BoardUpgrades.Describe(cur | flag) + ")");
        }
    }

    // On the part item itself: LMB while holding it = put it on your board.
    internal class SkatePartBehaviour : ItemInstanceBehaviour
    {
        [SerializeField] public int flag;
        private float nextHint;
        // how the model sits in the hand and on the ground (serialized: the game makes copies of the item)
        [SerializeField] public Vector3 heldPos, groundPos;
        [SerializeField] public Quaternion heldRot = Quaternion.identity, groundRot = Quaternion.identity;
        private Transform model;

        public void SetPoses(Vector3 hp, Quaternion hr, Vector3 gp, Quaternion gr)
        {
            heldPos = hp; heldRot = hr; groundPos = gp; groundRot = gr;
        }

        public override void ConfigItem(ItemInstanceData data, PhotonView playerView) { }

        // rocket fuel / a rocket lying around: a hard hit and it blows up (the host decides, it moves the things lying around)
        private float enabledAt;
        private void OnEnable() => enabledAt = Time.time;

        private void OnCollisionEnter(Collision c)
        {
            if (flag != BoardUpgrades.Rocket && flag != BoardUpgrades.FuelCan) return;
            if (!PhotonNetwork.IsMasterClient || Time.time - enabledAt < 3f) return; // (not the drop out of the delivery drone)
            if (c.relativeVelocity.magnitude < 7.5f || GetComponentInParent<Player>() != null) return;
            var p = GetComponentInParent<Pickup>();
            if (p != null) Boom.TryExplodePickup(p);
        }

        private int lastHeld = -1;
        private Transform lastParent;
        private int heldCache = -1;
        private bool colStored;
        private Vector3 groundColSize, groundColCenter;

        private void LateUpdate()
        {
            if (model == null) model = transform.Find("Model");
            if (model == null) return;
            // who holds it only changes when the item moves to another parent: don't look it up every frame
            if (transform.parent != lastParent || heldCache < 0)
            {
                lastParent = transform.parent;
                heldCache = GetComponentInParent<Player>() != null ? 1 : 0;
            }
            bool held = heldCache == 1;
            model.localPosition = held ? heldPos : groundPos;
            model.localRotation = held ? heldRot : groundRot;

            // the collider follows the model's pose (a flat collider poking into the hand made held items shake)
            if ((held ? 1 : 0) == lastHeld) return;
            lastHeld = held ? 1 : 0;
            var box = GetComponent<BoxCollider>();
            if (box == null) return;
            if (!colStored) { colStored = true; groundColSize = box.size; groundColCenter = box.center; }
            if (held)
            {
                Quaternion rel = heldRot * Quaternion.Inverse(groundRot);
                Vector3 sz = rel * groundColSize;
                box.size = new Vector3(Mathf.Abs(sz.x), Mathf.Abs(sz.y), Mathf.Abs(sz.z));
                box.center = heldPos + rel * (groundColCenter - groundPos);
            }
            else
            {
                box.size = groundColSize;
                box.center = groundColCenter;
            }
        }

        private void Update()
        {
            if (!isHeldByMe) return;
            var lp = Player.localPlayer;
            if (lp == null || lp.HasLockedInput() || !GlobalInputHandler.CanTakeInput()) return;
            if (!lp.input.clickWasPressed) return;
            if (flag != 0) SkateParts.Install(lp, flag);
            else if (Time.time > nextHint)
            {
                // the insurance policy: nothing to install, just say how it works
                nextHint = Time.time + 2f;
                SkateHUD.Popup(Lang.T("Просто держи полис в инвентаре: он один раз спасёт от смерти", "Just keep the policy in your inventory: it saves you from death once"),
                    new Color(0.8f, 1f, 0.8f), 2.5f, small: true);
            }
        }
    }
}
