using UnityEngine;

namespace CWSkate
{
    // Skateboard model. The root is placed at the board centre,
    // the "model" child carries the flip / manual rotation.
    internal class BoardVisual : MonoBehaviour
    {
        // Distance from board centre down to the bottom of the wheels.
        public const float WheelBase = 0.069f;

        public Transform model;
        [SerializeField] private MeshRenderer deckRenderer; // serialized so copies of the item keep it
        [SerializeField] private Renderer[] wheelRenderers;
        private int skinVersion = -1;
        private int upgrades = -1;
        private Material edge; // the deck's edge: plain wood, or maple
        private Rigidbody flyRig;
        private BoxCollider flyCol;
        private float flyTime;

        public bool Flying => flyRig != null;

        public static BoardVisual Create(string name)
        {
            var root = new GameObject(name);
            var bv = root.AddComponent<BoardVisual>();
            var model = new GameObject("Model").transform;
            model.SetParent(root.transform, false);
            bv.model = model;

            // deck
            var deck = new GameObject("Deck");
            deck.transform.SetParent(model, false);
            deck.AddComponent<MeshFilter>().sharedMesh = SkateArt.DeckMesh();
            bv.deckRenderer = deck.AddComponent<MeshRenderer>();
            bv.deckRenderer.sharedMaterials = new[] { SkateArt.Grip, SkateArt.Graphic, SkateArt.Wood };

            var wheels = new System.Collections.Generic.List<Renderer>();
            foreach (float z in new[] { 0.215f, -0.215f })
            {
                // base plate, hanger, axle
                Part(model, PrimitiveType.Cube, new Vector3(0, -0.0125f, z), new Vector3(0.07f, 0.012f, 0.085f), Quaternion.identity, SkateArt.Metal);
                Part(model, PrimitiveType.Cube, new Vector3(0, -0.03f, z), new Vector3(0.15f, 0.022f, 0.035f), Quaternion.identity, SkateArt.Metal);
                Part(model, PrimitiveType.Cylinder, new Vector3(0, -0.042f, z), new Vector3(0.008f, 0.1f, 0.008f), Quaternion.Euler(0, 0, 90), SkateArt.Metal);
                foreach (float x in new[] { 0.087f, -0.087f })
                    wheels.Add(Part(model, PrimitiveType.Cylinder, new Vector3(x, -0.042f, z), new Vector3(0.054f, 0.016f, 0.054f), Quaternion.Euler(0, 0, 90), SkateArt.Wheel));
            }
            bv.wheelRenderers = wheels.ToArray();

            // the rocket booster under the tail (shown only with the part on the board)
            var rocket = new GameObject("Rocket");
            rocket.transform.SetParent(model, false);
            rocket.transform.localPosition = new Vector3(0f, -0.034f, -0.39f);
            bv.rocketNozzle = BuildRocket(rocket.transform, 1f);
            rocket.SetActive(false);
            bv.rocketModel = rocket;
            return bv;
        }

        [SerializeField] private GameObject rocketModel;
        [SerializeField] private Transform rocketNozzle;
        private ParticleSystem flame, smoke;
        private bool flameOn = true;
        private Transform flameCore;

        // A little red rocket pointing forward (+z): body, nose, metal nozzle, three fins. Returns the back end of the nozzle.
        public static Transform BuildRocket(Transform parent, float k)
        {
            var body = Quaternion.Euler(90f, 0f, 0f); // cylinders stand along y: lay them along z
            Part(parent, PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.062f, 0.1f, 0.062f) * k, body, SkateArt.RocketRed);
            Part(parent, PrimitiveType.Sphere, new Vector3(0f, 0f, 0.1f) * k, new Vector3(0.062f, 0.062f, 0.09f) * k, Quaternion.identity, SkateArt.RocketRed);
            Part(parent, PrimitiveType.Cylinder, new Vector3(0f, 0f, 0.035f) * k, new Vector3(0.064f, 0.008f, 0.064f) * k, body, SkateArt.Wheel); // white band
            Part(parent, PrimitiveType.Cylinder, new Vector3(0f, 0f, -0.115f) * k, new Vector3(0.05f, 0.022f, 0.05f) * k, body, SkateArt.Metal); // nozzle
            for (int i = 0; i < 3; i++)
            {
                var r = Quaternion.Euler(0f, 0f, 90f + i * 120f);
                Part(parent, PrimitiveType.Cube, r * new Vector3(0.042f, 0f, 0f) * k + new Vector3(0f, 0f, -0.075f) * k, new Vector3(0.035f, 0.004f, 0.05f) * k, r, SkateArt.RocketRed);
            }
            var end = new GameObject("NozzleEnd").transform;
            end.SetParent(parent, false);
            end.localPosition = new Vector3(0f, 0f, -0.14f) * k;
            end.localRotation = Quaternion.Euler(0f, 180f, 0f); // fire goes out of the back
            return end;
        }

        // the rocket's fire and smoke (made the first time it fires)
        public void SetRocketFlame(bool on)
        {
            if (rocketNozzle == null) return;
            if (on && flame == null)
            {
                flame = SkateFx.Flame(rocketNozzle);
                smoke = SkateFx.Smoke(rocketNozzle);
                flameCore = SkateFx.FlameCore(rocketNozzle);
            }
            if (flame == null) return;
            if (!on && !flameOn) return; // already out
            flameOn = on;
            var e1 = flame.emission;
            e1.rateOverTime = on ? (Perf.Low ? 90f : 220f) : 0f;
            var e2 = smoke.emission;
            e2.rateOverTime = on && !Perf.Low ? 45f : 0f; // (no smoke in the low-end PC mode)
            if (flameCore != null)
            {
                if (flameCore.gameObject.activeSelf != on) flameCore.gameObject.SetActive(on);
                if (on)
                {
                    // the tongue of fire flickers
                    float len = Random.Range(0.8f, 1.3f), w = Random.Range(0.85f, 1.1f);
                    flameCore.localScale = new Vector3(0.05f * w, 0.05f * w, 0.18f * len);
                    flameCore.localPosition = new Vector3(0f, 0f, 0.09f * len);
                }
            }
        }

        // Shows the parts on the board: orange speed wheels / green soft wheels, a light maple edge.
        public void ApplyUpgrades(int flags)
        {
            if (flags == upgrades) return;
            upgrades = flags;
            Material wheel = (flags & BoardUpgrades.SpeedWheels) != 0 ? SkateArt.WheelSpeed
                           : (flags & BoardUpgrades.SoftWheels) != 0 ? SkateArt.WheelSoft : SkateArt.Wheel;
            if (wheelRenderers != null)
                foreach (var r in wheelRenderers) if (r != null) r.sharedMaterial = wheel;
            if (rocketModel != null) rocketModel.SetActive((flags & BoardUpgrades.Rocket) != 0);
            edge = (flags & BoardUpgrades.MapleDeck) != 0 ? SkateArt.Maple : SkateArt.Wood;
            if (deckRenderer != null)
            {
                var mats = deckRenderer.sharedMaterials;
                if (mats.Length > 2) { mats[2] = edge; deckRenderer.sharedMaterials = mats; }
            }
        }

        private static Renderer Part(Transform parent, PrimitiveType type, Vector3 pos, Vector3 scale, Quaternion rot, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            var col = go.GetComponent<Collider>();
            if (col) DestroyImmediate(col);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = rot;
            go.transform.localScale = scale;
            var rend = go.GetComponent<Renderer>();
            rend.sharedMaterial = mat;
            return rend;
        }

        // Puts a skin on the deck (only does work when the skin changed).
        public void ApplySkin(SkinSet skin)
        {
            if (skin == null || deckRenderer == null || skin.Version == skinVersion) return;
            skinVersion = skin.Version;
            deckRenderer.sharedMaterials = new[] { skin.Grip ? skin.Grip : SkateArt.Grip, skin.Graphic ? skin.Graphic : SkateArt.Graphic, edge ? edge : SkateArt.Wood };
        }

        public void Place(Vector3 pos, Quaternion rot, Quaternion localRot, Vector3 localOffset)
        {
            if (Flying) return;
            transform.SetPositionAndRotation(pos, rot);
            model.localRotation = localRot;
            model.localPosition = localOffset;
        }

        // Board strapped to the player's back, wheels facing out.
        public void PlaceOnBack(Player p)
        {
            if (Flying || p == null) return;
            var rag = p.refs.ragdoll;
            var hip = rag.GetBodypart(BodypartType.Hip);
            var torso = rag.GetBodypart(BodypartType.Torso);
            if (!hip || !torso) return;
            var neck = rag.GetBodypart(BodypartType.Neck);
            Vector3 top = neck ? neck.transform.position : torso.transform.position + Vector3.up * 0.3f;
            Vector3 up = (top - hip.transform.position).normalized;
            Vector3 fwd = Vector3.ProjectOnPlane(torso.transform.forward, up);
            if (fwd.sqrMagnitude < 1e-4f) fwd = Vector3.ProjectOnPlane(p.data.lookDirection, up);
            fwd.Normalize();

            Vector3 centre = Vector3.Lerp(hip.transform.position, torso.transform.position, 0.7f) - fwd * 0.2f;
            // nose points up along the spine (tilted a bit), grip side against the back
            Quaternion rot = Quaternion.AngleAxis(22f, fwd) * Quaternion.LookRotation(up, fwd);
            transform.SetPositionAndRotation(centre, rot);
            transform.localScale = Vector3.one; // normal size on the back
            model.localRotation = Quaternion.identity;
            model.localPosition = Vector3.zero;
        }

        // Board shoots away when you bail.
        public Vector3 FlyVelocity => flyRig != null ? flyRig.linearVelocity : Vector3.zero;
        public Rigidbody FlyRig => flyRig;
        private static PhysicsMaterial slipperyMat;

        // slippery = rolls on along the floor (thrown on purpose) instead of tumbling to a stop
        public void Fling(Vector3 velocity, float spin = 14f, bool slippery = false)
        {
            if (Flying) return;
            Vector3 p = model.position;
            Quaternion r = model.rotation;
            model.localRotation = Quaternion.identity;
            model.localPosition = Vector3.zero;
            transform.SetPositionAndRotation(p + Vector3.up * 0.05f, r);

            flyCol = gameObject.AddComponent<BoxCollider>();
            flyCol.size = new Vector3(0.21f, 0.08f, 0.8f);
            flyCol.center = new Vector3(0, -0.025f, 0);
            if (slippery)
            {
                if (slipperyMat == null)
                    slipperyMat = new PhysicsMaterial("CWSkate_Wheels") { dynamicFriction = 0.03f, staticFriction = 0.05f, frictionCombine = PhysicsMaterialCombine.Minimum, bounciness = 0.05f };
                flyCol.sharedMaterial = slipperyMat;
            }
            flyRig = gameObject.AddComponent<Rigidbody>();
            flyRig.mass = 1.5f;
            flyRig.interpolation = RigidbodyInterpolation.Interpolate;
            flyRig.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            flyRig.linearVelocity = velocity;
            flyRig.angularVelocity = Random.insideUnitSphere * spin;
            flyTime = 0;
        }

        public void StopFling()
        {
            if (flyRig) DestroyImmediate(flyRig);
            if (flyCol) DestroyImmediate(flyCol);
            flyRig = null;
            flyCol = null;
        }

        public bool OutOfWorld => transform.position.y < -150f;

        // true for the copies the game makes of the skateboard item (in a hand or lying on the ground)
        public bool isItem;
        // the Ride to Hell pro board: always its own skin
        public bool pro;
        private float skinCheck;
        private ItemInstance itemInstance;

        // Who last had the board: kept in the item's own data, which travels with it when it's dropped and picked up
        // (and over the network), so a board on the floor keeps the skin of the last player who had it.
        public static void SetSkinOwner(ItemInstanceData data, int actor)
        {
            if (data == null || actor <= 0) return;
            if (!data.TryGetEntry<IntEntry>(out var e)) { e = new IntEntry(); data.AddDataEntry(e); }
            e.i = actor;
        }

        private static int SkinOwner(ItemInstanceData data) => data != null && data.TryGetEntry<IntEntry>(out var e) ? e.i : 0;

        private static SkinSet SkinOfActor(int actor)
        {
            var me = Photon.Pun.PhotonNetwork.LocalPlayer;
            if (me == null || actor == me.ActorNumber) return Skins.Local;
            return Skins.ForActor(actor);
        }

        private bool shadowsOn = true;

        // the low-end PC mode: the board casts no shadow (one draw less per part for every light with shadows)
        private void UpdateShadows()
        {
            bool want = !Perf.Low;
            if (want == shadowsOn) return;
            shadowsOn = want;
            foreach (var r in GetComponentsInChildren<MeshRenderer>(true))
            {
                if (r.name.StartsWith("CWSkate_")) continue; // the glowing bits never cast shadows
                r.shadowCastingMode = want ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }

        private void Update()
        {
            UpdateShadows();
            if (isItem)
            {
                // wear the skin of whoever holds it; on the floor, the skin of whoever had it last
                skinCheck -= Time.deltaTime;
                if (skinCheck <= 0f)
                {
                    skinCheck = 0.5f;
                    if (itemInstance == null) itemInstance = GetComponentInParent<ItemInstance>();
                    var data = itemInstance != null ? itemInstance.instanceData : null;
                    ApplyUpgrades(BoardUpgrades.Of(data)); // the parts travel in the item's own data
                    var holder = GetComponentInParent<Player>();
                    if (pro) ApplySkin(Skins.RideToHell); // the sponsor's board looks the same whoever has it
                    else if (holder != null && holder.refs.view != null)
                    {
                        int actor = holder.refs.view.OwnerActorNr;
                        SetSkinOwner(data, actor);
                        ApplySkin(holder.IsLocal ? Skins.Local : Skins.ForActor(actor));
                    }
                    else
                    {
                        int owner = SkinOwner(data);
                        if (owner > 0) ApplySkin(SkinOfActor(owner));
                        else if (skinVersion < 0) ApplySkin(Skins.Default);
                    }
                }
            }
            if (!Flying) return;
            flyTime += Time.deltaTime;
            // fell out of the world: park it
            if (OutOfWorld && !flyRig.isKinematic)
            {
                flyRig.linearVelocity = Vector3.zero;
                flyRig.isKinematic = true;
            }
        }
    }
}
