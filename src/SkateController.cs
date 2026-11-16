using System.Collections.Generic;
using UnityEngine;

namespace CWSkate
{
    // Lives on the local player. Handles riding, tricks, grinds, combos and aura.
    internal partial class SkateController : MonoBehaviour
    {
        public static SkateController Local;

        public Player player;
        public bool Riding { get; private set; }
        public readonly Combo combo = new Combo();
        public bool ShowHelp;
        public float SpawnTime;

        // you have a board when the skateboard item is in your inventory
        public static bool Owns => SkateItem.HasBoard(Player.localPlayer);

        // live info for the HUD
        public float Speed => speed;
        public bool Airborne => airborne;
        public bool Grinding => grinding;
        public string GrindName => grinding ? (grindSlide ? "Boardslide" : "50-50 Grind") : "";
        public float GrindTime => grinding ? grindTime : 0f;
        public bool Manualing => manual;
        public bool NoseManualing => manual && noseManual;
        public bool Kickturning => kickturning;
        public float ManualTime => manual ? manualTime : 0f;
        public bool Grabbing => grab != Grab.None;
        public bool Sliding => sliding;
        public bool Fakie => fakie;
        public float Charge => charging ? charge / MaxCharge : 0f;

        private const float MaxCharge = 0.5f;

        private BoardVisual board;
        private AuraHalo halo;
        private AudioSource rollSrc, grindSrc, sfxSrc;
        private float rollVol, grindVol;

        // raw input (captured before the game zeroes it)
        private Vector2 rawMove;
        private bool rawJump, rawSprint, rawCrouch;
        private int inputFrame = -100;
        private bool prevJump;
        private float lastMouseMove = -10f;

        // movement
        private float heading, bodyYaw, speed; // heading = travel direction, bodyYaw = where the board's nose points
        private bool fakie;
        private float sinceLanded = 10f;
        private float pushTimer, pushDip;
        private float kickT = -1f, kickDur = 0.45f; // the back foot on the floor during a push (0..1, below 0 = not pushing)
        private float lean;
        private float blockedTime;

        // air
        private bool airborne;
        private float airTime;
        private Vector3 airVel;
        private float lastAirVy;
        private bool ollieThisAir;
        private float spinAccum;
        private bool pendingOllie;
        private float charge;
        private bool charging;
        private Flip flip;
        private int flipCount;
        private float flipProgress;
        private readonly List<KeyValuePair<Flip, int>> flipsThisAir = new List<KeyValuePair<Flip, int>>();
        private Grab grab, grabThisAir;
        private float grabTime, grabHold, supermanBlend, hipToBoard = 0.9f;

        // grind
        private bool grinding;
        private bool grindSlide;
        private float grindYawOffset;
        private Vector3 railPoint, railDir;
        private float grindTime;
        private float sinceGrind = 10f;

        // ground tricks
        private bool manual, noseManual;
        private float manualTime;
        private bool kickturning;          // pivoting on the back wheels
        private float kickAccum;
        private bool pendingBoneless, bonelessThisAir;
        private bool prevSprint;
        private float revertCooldown;
        private const float KickturnMaxSpeed = 2.5f, KickturnSpeed = 260f;
        private bool sliding;
        private float slideTime;
        private float slideDir = 1f;
        private float slideBlend, manualBlend, noseBlend, grabBlend, airPitch, leanVis;

        // bail
        private bool bailed;
        private float recoverTimer;
        private float getUpTimer;   // after getting back up: no tricks for a moment
        private bool GettingUp => getUpTimer > 0f;

        // pushed around by the world (black holes, shots, punches)
        private Vector3 extVel;
        private float pullTime, wobble;

        // grab pose
        private Grab sentGrab;
        private BodypartType grabHand;
        private Vector3 grabLocal;
        private static readonly Collider[] overlapHits = new Collider[24];

        // what's under us (our own ray: the game ignores physics objects like trampolines and monsters)
        private static readonly RaycastHit[] probeHits = new RaycastHit[12];
        private static readonly RaycastHit[] castHits = new RaycastHit[16];
        private float standHipHeight = 1.05f;
        private Trampoline trampUnder;
        private float slideOffCooldown;
        private float stuckTimer;

        // visuals
        private Vector3 boardUp = Vector3.up;
        private Vector3 bodySlopeUp = Vector3.up; // the body's lean on slopes (follows boardUp)
        private float feetOffset = 0.12f;

        private float oldFallThreshold = 1.5f;
        private static bool loggedValues;
        private static int crashMask = -1, grindMask = -1;

        private static readonly string[] TripReasonsEn = { "ATE CONCRETE", "FACEPLANT", "TRIPPED!", "THE GROUND WON", "WHO PUT THAT THERE?!" };
        private static readonly string[] TripReasonsRu = { "ПОЦЕЛОВАЛ АСФАЛЬТ", "ЛИЦОМ В ПОЛ", "СПОТКНУЛСЯ!", "ЗЕМЛЯ ПОБЕДИЛА", "КТО ЭТО СЮДА ПОСТАВИЛ?!" };
        private static readonly string[] TripReasonsFr = { "BISOU AU BITUME", "FACEPLANT", "TRÉBUCHÉ !", "LE SOL A GAGNÉ", "QUI A MIS ÇA LÀ ?!" };
        private static readonly string[] TripReasonsZh = { "脸先着地", "亲吻大地", "绊倒了！", "地面赢了", "谁把这玩意放这的？！" };
        private static string[] TripReasons => Lang.Pick(TripReasonsRu, TripReasonsEn, TripReasonsFr, TripReasonsZh);

        private float StanceOffset => SkatePlugin.GoofyStance.Value ? -90f : 90f;
        private bool Bails => SkatePlugin.Bails.Value;

        private void Awake()
        {
            player = GetComponent<Player>();
            Local = this;
            SpawnTime = Time.time;

            var audioGo = new GameObject("CWSkate_Audio");
            audioGo.transform.SetParent(transform, false);
            rollSrc = MakeLoop(audioGo, SkateSounds.Roll);
            grindSrc = MakeLoop(audioGo, SkateSounds.Grind);
            rocketSrc = MakeLoop(audioGo, SkateSounds.Rocket);
            sfxSrc = audioGo.AddComponent<AudioSource>();
            sfxSrc.spatialBlend = 0f;
            sfxSrc.playOnAwake = false;
        }

        private static AudioSource MakeLoop(GameObject go, AudioClip clip)
        {
            var s = go.AddComponent<AudioSource>();
            s.clip = clip;
            s.loop = true;
            s.spatialBlend = 0f;
            s.volume = 0f;
            s.playOnAwake = false;
            return s;
        }

        private void Start()
        {
            NetSync.SetState(CurrentState, force: true);
        }

        private void OnDestroy()
        {
            if (Riding)
                PlayerRagdoll.RagdollIfFellForLongerThan = oldFallThreshold;
            if (board) Destroy(board.gameObject);
            if (halo) Destroy(halo.gameObject);
            if (Local == this) Local = null;
        }

        // 0 = no board, 1 = on the back, 2 = riding. While it's in your hands the game shows it itself.
        private int CurrentState => !Owns || (!Riding && SkateItem.HoldingBoard(player)) ? NetSync.StateNone : Riding ? NetSync.StateRiding : NetSync.StateBack;

        public void OnOwnershipChanged()
        {
            EnsureBoard();
            NetSync.SetState(CurrentState);
        }

        private void EnsureBoard()
        {
            if (board == null)
                board = BoardVisual.Create("CWSkate_Board");
        }

        // ---------------------------------------------------------------- sparks on rails, glowing wheel tracks
        private GrindSparks sparks;
        private WheelTrails trails;
        private bool sentGrindFx;

        private void UpdateFx(float dt)
        {
            if (trails == null) trails = WheelTrails.Attach(board);
            if (sparks == null) sparks = GrindSparks.Create();
            bool rolling = Riding && !bailed && !board.Flying;
            board.SetRocketFlame(rocketOn && rolling);
            bool trailOn = SkatePlugin.ShowWheelTrails.Value && rolling && !airborne && !grinding && !wallriding && !monsterGrinding && !hippie && speed > 2f;
            trails.Tick(trailOn, Aura.RankColor(Progress.Aura), dt, fire: BoardUpgrades.Has(BoardUpgrades.ProBoard)); // the Ride to Hell board leaves fire
            // metal on metal sparks a lot; the wood of a boardslide only a little
            bool sparkOn = SkatePlugin.ShowGrindSparks.Value && rolling && grinding;
            sparks.Set(sparkOn, railPoint, -YawDir(heading), (grindSlide ? 25f : 70f) * Mathf.Clamp(speed / 6f, 0.4f, 1.6f));
            // others with the mod see our sparks too
            if (grinding != sentGrindFx)
            {
                sentGrindFx = grinding;
                NetSync.SendGrindFx(player, grinding, grindSlide);
            }
        }

        public void ReceiveInput(Player.PlayerInput inp)
        {
            bool can = GlobalInputHandler.CanTakeInput();
            rawMove = inp.movementInput;
            rawJump = can && (IsPressed(inp.jumpAction) || GlobalInputHandler.JumpKey.GetKey());
            rawSprint = can && (IsPressed(inp.sprintAction) || GlobalInputHandler.SprintKey.GetKey());
            rawCrouch = can && (IsPressed(inp.crouchAction) || GlobalInputHandler.CrouchKey.GetKey());
            if (inp.lookInput.sqrMagnitude > 0.0004f) lastMouseMove = Time.time;
            inputFrame = Time.frameCount;
        }

        private static bool IsPressed(UnityEngine.InputSystem.InputActionReference r)
        {
            try { return r != null && r.action != null && r.action.IsPressed(); }
            catch { return false; }
        }

        // ---------------------------------------------------------------- riding on / off

        // Places where you can't ride
        private string BlockReason()
        {
            if (player.data.inWaterAmount > 0.2f) return Lang.T("ДОСКА НЕ ПЛАВАЕТ", "THE BOARD DOESN'T FLOAT");
            if (player.data.currentBed != null) return Lang.T("НЕ В КРОВАТИ", "NOT IN BED");
            if (player.data.currentSeat != null) return Lang.T("НЕ СИДЯ", "NOT WHILE SITTING");
            return null;
        }

        public void StartRiding()
        {
            if (Riding || player.data.dead || player.Ragdoll()) return;
            if (!Owns) return; // no board in the inventory: the key does nothing
            string block = BlockReason();
            if (block != null)
            {
                SkateHUD.Popup(block, new Color(1f, 0.7f, 0.5f), 2f, small: true);
                return;
            }
            thrown = false;
            // the board was in your hands: put it down under your feet
            if (SkateItem.HoldingBoard(player)) player.data.selectedItemSlot = -1;
            Riding = true;
            heading = CamYaw();
            bodyYaw = heading;
            Vector3 v = Hip().linearVelocity;
            speed = Mathf.Min(new Vector3(v.x, 0, v.z).magnitude, SkatePlugin.PushSpeed.Value);
            fakie = false;
            airborne = grinding = false;
            bailed = false;
            pendingOllie = false;
            prevJump = rawJump;
            manual = sliding = charging = wallriding = false;
            grab = Grab.None;
            flip = Flip.None;
            flipsThisAir.Clear();
            ResetTrickState();
            sinceLanded = 0f;
            blockedTime = 0f;
            getUpTimer = 0f;
            extVel = Vector3.zero;
            pullTime = wobble = 0f;
            ExternalForces.Clear();

            EnsureBoard();
            board.StopFling();
            board.gameObject.SetActive(true);

            oldFallThreshold = PlayerRagdoll.RagdollIfFellForLongerThan;
            PlayerRagdoll.RagdollIfFellForLongerThan = 3.2f;
            NetSync.SetState(NetSync.StateRiding);
            StartTutorialIfFirst(); // the tips, the very first time only
            rollSrc.Play();
            grindSrc.Play();
            rocketSrc.Play();

            if (!loggedValues)
            {
                loggedValues = true;
                var pc = player.refs.controller;
                SkatePlugin.Log.LogInfo($"Game values: movementForce={pc.movementForce} jumpImpulse={pc.jumpImpulse} gravity={pc.gravity} drag={player.refs.ragdoll.drag} fixedDt={Time.fixedDeltaTime}");
            }
        }

        // Getting off (B, dying, water...) in the middle of a flip, a hippie jump, a monster grind or a skitch:
        // none of it may carry over to the next ride (you rode on upside down, or the board rolled off by itself).
        private void ResetTrickState()
        {
            SetRocket(false);
            ClearSalto();
            saltoWeld = tuckBlend = 0f;
            hippie = pendingHippie = false;
            monsterGrinding = false;
            grindMonster = null;
            mgHip = null;
            StopSkitch();
            dropIn = false;
        }

        public void StopRiding()
        {
            if (!Riding) return;
            if (grinding) EndGrind();
            ResetDanceAndEscape();
            if (combo.Active && !bailed) Commit();
            combo.Clear();
            FlushDistance();
            Progress.Save();
            Riding = false;
            PlayerRagdoll.RagdollIfFellForLongerThan = oldFallThreshold;
            NetSync.SetState(CurrentState);
            if (board) board.StopFling();
            airborne = manual = sliding = charging = pendingOllie = grinding = wallriding = false;
            kickturning = noseManual = pendingBoneless = bonelessThisAir = false;
            grab = Grab.None;
            SyncGrab();
            ResetTrickState();
            StopTutorial();
            bailed = false;
            ExternalForces.Clear();
            player.data.isCrouching = false;
            player.data.rotationOvverideStr = 0f;
            rollSrc.Stop();
            grindSrc.Stop();
            rocketSrc.Stop();
            if (halo) halo.Hide();
        }

        // ---------------------------------------------------------------- per frame

        private void Update()
        {
            if (player == null || !player.data.playerSetUpAndReady) return;
            if (Local != this) Local = this;

            bool canInput = GlobalInputHandler.CanTakeInput() && !player.HasLockedInput();
            if (canInput && Input.GetKeyDown(SkatePlugin.ToggleKey.Value))
            {
                if (Riding) StopRiding(); else StartRiding();
            }
            if (canInput && !Riding && Input.GetKeyDown(SkatePlugin.ThrowKey.Value))
                ThrowBoard();
            if (canInput && Input.GetKeyDown(SkatePlugin.HelpKey.Value))
                ShowHelp = !ShowHelp;

            NetSync.SetState(CurrentState);
            NetSync.SetUpgrades(Owns ? BoardUpgrades.Local : 0); // others see our wheels and deck
            // others with the mod see our aura glow too
            NetSync.SetGlow(Riding && !bailed && SkatePlugin.AuraGlow.Value ? combo.Count : -1);

            if (!Riding) return;
            // no board any more (it flew off after a bail, or you dropped it) / dead / took it in your hands
            if (!Owns || player.data.dead || SkateItem.HoldingBoard(player))
            {
                if (player.data.dead) combo.Clear(); // otherwise the combo still counts
                StopRiding();
                return;
            }
            string block = BlockReason();
            if (block != null)
            {
                // fell into water / lay down in bed / sat down: get off the board
                if (player.data.inWaterAmount > 0.2f) SkateHUD.Popup(block, new Color(0.5f, 0.8f, 1f), 2f, small: true);
                combo.Clear();
                StopRiding();
                return;
            }

            if (Time.frameCount - inputFrame > 2)
            {
                rawMove = Vector2.zero;
                rawJump = rawSprint = rawCrouch = false;
            }
            float dt = Time.deltaTime;
            TickTutorial(dt);

            // dancing (the game's emote key): you roll on and can carve, but no pushing or tricks until it's over
            bool dancing = player.data.emoteTime > 0f && !bailed && !player.Ragdoll();
            if (dancing)
            {
                rawMove.y = 0f;
                rawJump = rawSprint = rawCrouch = false;
            }
            TickDance(dt, dancing);
            TickEscape(dt);
            WearSounds(dt);
            if ((grinding || wallriding || monsterGrinding) && !bailed) Wear(0.5f * dt); // metal, walls and monster backs eat the board slowly
            TickSkitch(dt, canInput);
            if (!Riding) return; // the board just snapped
            TickRocket(dt, canInput);

            // knocked over by anything (monster, our own bail, a huge drop)
            if (player.Ragdoll())
            {
                if (!bailed) Bail(Lang.T("СБИЛИ С НОГ", "WIPEOUT"), external: true);
                recoverTimer = 0.5f;
                return;
            }
            if (bailed)
            {
                recoverTimer -= dt;
                if (recoverTimer > 0) return;
                bailed = false;
                board.StopFling();
                speed = 0;
                heading = CamYaw();
                bodyYaw = heading;
                fakie = false;
                airborne = grinding = false;
                sinceLanded = 0f;
                blockedTime = 0f;
                prevJump = rawJump;
                extVel = Vector3.zero;
                pullTime = wobble = 0f;
                ExternalForces.Clear();
                getUpTimer = SkatePlugin.GetUpCooldown.Value;
            }
            getUpTimer -= dt;

            // just got up: keys you're still holding (or mash by accident) don't do tricks yet
            bool tricksOk = !GettingUp;
            bool jumpDown = rawJump && !prevJump;
            bool jumpUp = !rawJump && prevJump;
            prevJump = rawJump;
            if (!tricksOk) { jumpDown = jumpUp = false; }
            bool sprintWas = prevSprint;
            prevSprint = rawSprint;

            if (monsterGrinding)
            {
                // on a monster's back: JUMP to pop off, A/D to turn around on it
                if (jumpDown) pendingOllie = true;
                if (Mathf.Abs(rawMove.x) > 0.2f) { bodyYaw += rawMove.x * 180f * dt; heading = bodyYaw - (fakie ? 180f : 0f); }
                grab = Grab.None;
            }
            else if (grinding || wallriding)
            {
                // pop off the rail / the wall
                if (jumpDown) pendingOllie = true;
                float along = grinding ? Yaw(railDir) + grindYawOffset : Yaw(wallAlong) + (fakie ? 180f : 0f);
                bodyYaw = Mathf.MoveTowardsAngle(bodyYaw, along, 720f * dt);
                grab = Grab.None;
            }
            else if (!airborne)
            {
                // Hippie jump: S + tap JUMP while rolling - you jump, the board rolls on under you
                if (jumpDown && rawMove.y < -0.5f && !rawCrouch && tricksOk && speed > 2f && player.data.sinceGrounded < 0.15f && !pendingOllie)
                {
                    pendingOllie = pendingHippie = true;
                    charging = false;
                    charge = 0f;
                }
                // Boneless: hold CROUCH and tap JUMP - plant a foot and pop higher than an ollie
                else if (jumpDown && rawCrouch && tricksOk && player.data.sinceGrounded < 0.15f && !pendingBoneless)
                {
                    pendingOllie = pendingBoneless = true;
                    charging = false;
                    charge = 0f;
                    pushDip = 0.25f; // the body dips while the foot goes down
                }
                else if (rawJump && !rawCrouch && tricksOk && player.data.sinceGrounded < 0.15f)
                {
                    charging = true;
                    charge = Mathf.Min(charge + dt, MaxCharge);
                }
                if (jumpUp && charging)
                    pendingOllie = true;

                // Kickturn: CROUCH + A/D when slow - nose up, pivot on the back wheels
                bool wantKick = rawCrouch && tricksOk && speed < KickturnMaxSpeed && Mathf.Abs(rawMove.x) > 0.2f && !charging && !pendingBoneless;
                if (wantKick)
                {
                    if (!kickturning) { kickturning = true; kickAccum = 0f; if (manual) EndManual(); }
                    float turn = rawMove.x * KickturnSpeed * dt;
                    heading += turn;
                    kickAccum += Mathf.Abs(turn);
                    combo.KeepAlive();
                }
                else if (kickturning) EndKickturn();

                // Manual (CROUCH) / nose manual (CROUCH + W)
                bool wantManual = rawCrouch && tricksOk && speed > 1.5f && !charging && !kickturning && !pendingBoneless;
                bool wantNose = rawMove.y > 0.5f;
                if (wantManual && !manual)
                {
                    manual = true;
                    noseManual = wantNose;
                    manualTime = 0;
                    if (sliding) EndSlide();
                    SkateActivity.Local(SkateAct.Manual, combo.Count, noseManual ? "Nose Manual" : "Manual");
                }
                else if (wantManual && manual && wantNose != noseManual && manualTime < 0.3f) noseManual = wantNose; // pick the end at the start
                else if (!wantManual && manual) EndManual();
                if (manual) { manualTime += dt; combo.KeepAlive(); }

                // Revert: SPRINT right after landing - spin the board 180 and keep rolling
                bool sprintDown = rawSprint && !sprintWas;
                revertCooldown -= dt;
                // only right after a landing (like in Skate): otherwise SPRINT pressed by habit spun you around mid-ride
                if (sprintDown && tricksOk && speed > 1f && !manual && !kickturning && revertCooldown <= 0f && sinceLanded < 0.7f) DoRevert();

                bool wantSlide = rawMove.y < -0.5f && tricksOk && speed > 4.5f && !manual;
                if (wantSlide && !sliding) { sliding = true; slideTime = 0; slideDir = rawMove.x < -0.2f ? -1f : 1f; }
                else if (!wantSlide && sliding) EndSlide();
                if (sliding) slideTime += dt;

                grab = Grab.None;
                bodyYaw = Mathf.MoveTowardsAngle(bodyYaw, heading + (fakie ? 180f : 0f), 900f * dt);
            }
            else
            {
                charging = false;
                charge = 0;
                if (manual) EndManual();
                if (kickturning) EndKickturn();
                if (sliding) EndSlide();

                // spin (not in the first moment of a bump you rolled off: A/D is still you steering)
                if (tricksOk && Mathf.Abs(rawMove.x) > 0.2f && (ollieThisAir || airTime > 0.25f))
                {
                    float d = rawMove.x * SkatePlugin.SpinSpeed.Value * dt;
                    bodyYaw += d;
                    spinAccum += d;
                }

                // grabs: SPRINT = Indy, +W = Nosegrab, +S = Tailgrab, +CROUCH = Melon,
                // +CROUCH+W = Crail, +CROUCH+S = Stalefish; JUMP while grabbing = Airwalk (+W = Superman)
                if (rawSprint && tricksOk && flip == Flip.None && salto == 0 && !hippie)
                {
                    Grab want = rawCrouch && rawMove.y > 0.5f ? Grab.Crail
                              : rawCrouch && rawMove.y < -0.5f ? Grab.Stalefish
                              : rawMove.y > 0.5f ? Grab.Nosegrab
                              : rawMove.y < -0.5f ? Grab.Tailgrab
                              : rawCrouch ? Grab.Melon : Grab.Indy;
                    if (grab != Grab.None && jumpDown) want = rawMove.y > 0.5f ? Grab.Superman : Grab.Airwalk;
                    if (grab == Grab.None) SetGrab(want, true);
                    else if (want != grab && (GrabPose.IsBody(want) || (grabHold < 0.15f && !GrabPose.IsBody(grab))))
                        SetGrab(want, false); // keys pressed a moment after SPRINT still count, a jump turns it into a body grab
                    grabTime += dt;
                    grabHold += dt;
                }
                else grab = Grab.None;

                // frontflip / backflip: W or S + JUMP (not while grabbing, that's airwalk / superman)
                if (jumpDown && tricksOk && grab == Grab.None && !rawSprint && flip == Flip.None && !hippie && Mathf.Abs(rawMove.y) > 0.5f)
                    StartSalto(rawMove.y > 0f ? 1 : -1);
                TickSalto(dt);

                // flips
                // (no board under you in a hippie jump; while holding on to someone F grabbed them instead of a kickflip)
                if (canInput && tricksOk && grab == Grab.None && salto == 0 && !hippie && !skitching)
                {
                    if (Input.GetKeyDown(SkatePlugin.KickflipKey.Value)) StartFlip(Flip.Kickflip);
                    else if (Input.GetKeyDown(SkatePlugin.HeelflipKey.Value)) StartFlip(Flip.Heelflip);
                    else if (Input.GetKeyDown(SkatePlugin.ShoveItKey.Value)) StartFlip(Flip.ShoveIt);
                    else if (Input.GetKeyDown(SkatePlugin.TreFlipKey.Value)) StartFlip(Flip.TreFlip);
                    else if (Input.GetKeyDown(SkatePlugin.ImpossibleKey.Value)) StartFlip(Flip.Impossible);
                }
                if (flip != Flip.None)
                {
                    flipProgress += dt * SkatePlugin.FlipSpeed.Value / Tricks.FlipDuration(flip);
                    if (flipProgress >= flipCount)
                    {
                        flipsThisAir.Add(new KeyValuePair<Flip, int>(flip, flipCount));
                        flip = Flip.None;
                        flipProgress = 0;
                    }
                }
            }

            // push: the body dips for a moment on every kick
            pushDip -= dt;
            if (kickT >= 0f)
            {
                kickT += dt / kickDur;
                if (kickT >= 1f || !FeetOnDeck || manual || sliding) kickT = -1f; // (an ollie, a bail... the foot goes back to its pose)
            }
            // tucked in a frontflip (the game's crouch also rounds the back, which a backflip doesn't want: the tuck does the knees)
            player.data.isCrouching = charging || grab != Grab.None || pushDip > 0f || salto > 0;
            SyncGrab();

            // stand sideways to the nose of the board, lean into turns, wobble when something hits you
            lean = Mathf.MoveTowards(lean, airborne || grinding ? 0f : rawMove.x * Mathf.Clamp01(speed / 4f), dt * 4f);
            wobble = Mathf.MoveTowards(wobble, 0f, dt * 2.5f);
            Vector3 travel = YawDir(heading);
            Vector3 right = Vector3.Cross(Vector3.up, travel);
            Vector3 push = new Vector3(extVel.x, 0, extVel.z);
            Vector3 nose = YawDir(bodyYaw);
            Vector3 up = (Vector3.up + right * lean * 0.22f + Vector3.ClampMagnitude(push * 0.12f, 0.35f)
                          - nose * (0.2f * manualBlend) + nose * (0.2f * noseBlend)
                          + (wallriding ? wallNormal * 0.9f : Vector3.zero) // lean out from the wall
                          + right * Mathf.Sin(Time.time * 17f) * wobble * 0.15f).normalized;
            // superman: the body lies flat in the air, head towards the nose
            supermanBlend = Mathf.MoveTowards(supermanBlend, airborne && grab == Grab.Superman ? 1f : 0f, dt * 6f);
            // (in a skate train you face forward, the hands on the shoulders in front)
            // on a slope the body leans with the board (perpendicular to the floor, like a real skater), not straight up
            Vector3 slopeTarget = Vector3.up;
            if (!airborne && !grinding && !wallriding && !monsterGrinding)
                slopeTarget = Vector3.SlerpUnclamped(Vector3.up, boardUp, SkatePlugin.BodySlopeLean.Value); // (above 1 = leans more than the board)
            if (Vector3.Angle(Vector3.up, slopeTarget) > 45f) slopeTarget = Vector3.RotateTowards(Vector3.up, slopeTarget, 45f * Mathf.Deg2Rad, 0f); // (not up a wall)
            bodySlopeUp = Vector3.Slerp(bodySlopeUp, slopeTarget, dt * 8f);
            Quaternion slopeRot = Quaternion.FromToRotation(Vector3.up, bodySlopeUp);
            player.data.rotationOvveride = slopeRot * SaltoRotation() * GrabPose.SupermanRotation(Quaternion.LookRotation(YawDir(bodyYaw + StanceOffset * (1f - trainBlend)), up), nose, supermanBlend);
            player.data.rotationOvverideStr = 2f;

            CameraFollow(dt);

            if (combo.Tick(dt, airborne || manual || sliding || charging || grinding || wallriding || monsterGrinding || skitching))
                Commit();
        }

        // called right before the body copies the animation skeleton (see GrabPose)
        internal void ApplyPoseIK()
        {
            if (!Riding || bailed || board == null || board.Flying) return;
            if (!airborne)
            {
                // the feet stay on the deck (also while dancing)
                if (FeetOnDeck) GrabPose.ApplyFeetOnDeck(player.refs.ragdoll, SkatePlugin.GoofyStance.Value, board.model, 1f, kickT);
                SkitchPose(); // holding on to whoever tows you
                return;
            }
            // in the air, still holding on (a flip or a grab needs the arms more)
            if (skitchBlend > 0.01f && tuckBlend <= 0.01f && grabBlend <= 0.01f) { SkitchPose(); return; }
            if (tuckBlend > 0.01f) { GrabPose.ApplyTuck(player.refs.ragdoll, tuckBlend, lastSaltoDir < 0); return; } // flip: tucked in a ball
            if (grabBlend <= 0.01f) return;
            Grab g = grab != Grab.None ? grab : grabThisAir;
            GrabPose.ApplyIK(player.refs.ragdoll, g, SkatePlugin.GoofyStance.Value, board.model, grabBlend);
        }

        private void SetGrab(Grab g, bool fresh)
        {
            grab = g;
            // the first grab of this air is the one that scores, unless you turned it into something better
            if (grabThisAir == Grab.None || !fresh) grabThisAir = g;
            if (fresh) grabHold = 0f;
            if (GrabPose.IsBody(g)) Play(SkateSounds.Whoosh, 0.55f);
            SkateActivity.Local(SkateAct.Grab, combo.Count, Tricks.GrabName(g));
            GrabPose.Target(g, SkatePlugin.GoofyStance.Value, out grabHand, out grabLocal);
        }

        // tell other modded players which grab we're doing, so they see the hand on the board too
        private void SyncGrab()
        {
            if (grab == sentGrab) return;
            sentGrab = grab;
            NetSync.SendGrab(player, grab);
        }

        // Skate 3 style: the camera swings around behind the board when you're not moving the mouse.
        private void CameraFollow(float dt)
        {
            if (SkatePlugin.Steering.Value != SteeringMode.Keyboard || !SkatePlugin.CameraFollow.Value) return;
            if (Time.time - lastMouseMove < 0.6f) return;
            if (!airborne && !grinding && speed < 0.6f) return;
            float travelYaw = grinding ? Yaw(railDir) : wallriding ? Yaw(wallAlong) : airborne && airVel.sqrMagnitude > 1f ? Yaw(airVel) : heading;
            try
            {
                float target = HelperFunctions.DirectionToLook(YawDir(travelYaw)).x;
                float cur = player.data.playerLookValues.x;
                player.data.playerLookValues.x = cur + Mathf.DeltaAngle(cur, target) * Mathf.Clamp01(SkatePlugin.CameraFollowSpeed.Value * dt);
            }
            catch { }
        }

        private void LateUpdate()
        {
            if (player == null || !player.data.playerSetUpAndReady) return;
            UpdateLegCollisions();
            // no board, or it's in your hands (the game draws the item there): nothing on the back
            if (!Owns || (!Riding && SkateItem.HoldingBoard(player)))
            {
                if (board) { board.StopFling(); board.gameObject.SetActive(false); }
                if (halo) halo.Hide();
                if (sparks) sparks.Set(false, Vector3.zero, Vector3.zero, 0f);
                thrown = false;
                return;
            }
            EnsureBoard();
            board.gameObject.SetActive(true);
            board.ApplySkin(BoardUpgrades.Has(BoardUpgrades.ProBoard) ? Skins.RideToHell : Skins.Local); // the sponsor's board keeps its own look
            board.ApplyUpgrades(BoardUpgrades.Local);
            float dt = Time.deltaTime;
            UpdateFx(dt);
            if (!Riding)
            {
                if (halo) halo.Hide();
                if (thrown)
                {
                    UpdateThrownBoard(); // flying / rolling ahead of you
                    if (thrown || Riding) return;
                }
                if (board.Flying) board.StopFling();
                board.PlaceOnBack(player);
                return;
            }
            UpdateBoard(dt);
            UpdateAudio(dt);
            UpdateGlow(dt);
        }

        // ---------------------------------------------------------------- physics (called instead of PlayerController.Movement)

        public void SkateFixedUpdate()
        {
            var d = player.data;
            var rag = player.refs.ragdoll;
            float dt = Time.fixedDeltaTime;
            Rigidbody hip = Hip();
            Vector3 v = hip.linearVelocity;
            Vector3 flat = new Vector3(v.x, 0, v.z);
            float comp = (1f - rag.drag) / dt; // cancels the game's per-step velocity drag
            float gravity = DevTools.AirGravity;
            sinceLanded += dt;
            sinceGrind += dt;
            slideOffCooldown -= dt;
            pushTimer -= dt;
            wallTrickCooldown -= dt;

            if (bailed) { ExternalForces.Clear(); return; }
            if (HandleExternal(v, dt)) return;
            if (CheckMonster(hip.position, flat)) return;
            PinFeet(rag, v);   // feet stay on the deck
            LeanBody(rag);     // lean with the board on slopes

            if (grinding)
            {
                GrindFixed(hip, v, comp, dt);
                return;
            }
            if (wallriding)
            {
                WallFixed(hip, v, comp, dt);
                return;
            }
            if (monsterGrinding)
            {
                MonsterGrindFixed(hip, v, comp, dt);
                return;
            }

            // grab: the hand goes to the board, knees come up
            if (airborne && grab != Grab.None && board != null && !board.Flying)
                GrabPose.ApplyForces(rag, grab, SkatePlugin.GoofyStance.Value, board.model, grabBlend);

            // manual / nose manual / kickturn: the foot on the raised end goes up with the board
            if (!airborne && board != null && !board.Flying && (manualBlend > 0.05f || noseBlend > 0.05f))
                FeetOnTiltedDeck(rag, hip.linearVelocity);

            // The board lifts us off the floor, so feet don't touch the ground any more.
            // Always probe the ground with a ray (the game only does that while walking),
            // otherwise standing still on the board counts as falling forever.
            // (not while upside down in a flip: the game measures from the head, which is near the floor then)
            if ((!airborne || (airTime > 0.15f && v.y < 0.5f)) && salto == 0)
                rag.GroundRaycast();
            bool grounded = d.isGrounded;

            // our own look at what's under the board
            bool below = ProbeBelow(hip.position, out RaycastHit under);
            if (grounded && !airborne && below && under.distance < 2f && under.rigidbody == null)
                standHipHeight = Mathf.Clamp(Mathf.Lerp(standHipHeight, under.distance, 0.1f), 0.6f, 1.6f);
            bool onSupport = below && under.distance <= standHipHeight + 0.08f;
            trampUnder = onSupport ? Owner<Trampoline>.Of(under.collider) : null;

            // physics objects we CAN stand on (trampolines, the diving bell, heavy/fixed things)
            // and ones we slide off (monsters, small loose stuff)
            bool rbUnder = onSupport && under.rigidbody != null;
            bool creatureUnder = rbUnder && Owner<Player>.Of(under.collider) != null;
            bool standable = rbUnder && !creatureUnder && (trampUnder != null || under.rigidbody.isKinematic || under.rigidbody.mass > 40f
                                                          || Owner<StandableRig>.Of(under.collider) != null);
            bool descending = v.y <= 0.5f;
            bool virtualGround = standable && (!airborne || (descending && airTime > 0.12f));
            // the game sometimes loses the floor for a moment (its ray starts at the head, which drops when you crouch to push);
            // if the floor is right there under the board, you're still on it
            bool floorUnder = !airborne && below && under.rigidbody == null && under.distance <= standHipHeight + 0.3f;
            bool groundedEff = grounded || virtualGround || floorUnder;
            if (virtualGround)
            {
                float targetY = under.point.y + standHipHeight;
                float vyWant = (targetY - hip.position.y) * 10f;
                AddToAllRigs(Vector3.up * ((vyWant - v.y) * 20f + v.y * comp));
                d.sinceGrounded = 0f;
            }

            if (pendingOllie)
            {
                pendingOllie = false;
                if (onSupport && Owner<Bed>.Of(under.collider) != null)
                    SkateHUD.Popup(Lang.T("НА КРОВАТИ НЕ ПРЫГАЕМ", "NO JUMPING ON THE BED"), new Color(1f, 0.8f, 0.5f), 1.5f, small: true);
                else if (!airborne && d.sinceGrounded < 0.2f)
                {
                    DoOllie(trampUnder, pendingBoneless && !pendingHippie);
                    if (pendingHippie) StartHippie();
                }
                pendingBoneless = pendingHippie = false;
                charging = false;
                charge = 0;
            }

            if (airborne)
            {
                airTime += dt;
                if (salto == 0 && !hippie)
                {
                    if (TryStartGrind(hip, v))
                        return;
                    if (TryStartWallride(hip, v))
                        return;
                    TryWallplant(hip);
                }
                if ((grounded && airTime > 0.12f) || (virtualGround && descending))
                {
                    Land();
                    if (bailed) return;
                }
                else
                {
                    lastAirVy = v.y;
                    // coming down onto a monster: ride it
                    if (descending && creatureUnder && airTime > 0.12f && TryStartMonsterGrind(under)) return;
                    // Physics objects (monsters, loose props) never count as ground in this game,
                    // so standing on one would keep you "in the air" forever. Slide off instead.
                    if (descending && rbUnder && !standable && airTime > 0.12f) SlideOff(hip.position, under);

                    // not moving at all mid-air = stuck on something
                    if (airTime > 0.5f && v.magnitude < 0.8f) stuckTimer += dt; else stuckTimer = 0f;
                    if (stuckTimer > 0.3f)
                    {
                        airVel = flat;
                        if (onSupport) SlideOff(hip.position, under);
                        stuckTimer = 0f;
                    }
                }
            }
            else if (!groundedEff && d.sinceGrounded > 0.15f)
            {
                TakeOff(false);
                StartDrop(); // rolled off an edge
            }

            RocketFixed(dt);
            if (skitching) SkitchFixed(dt); // towed: speed and direction come from the arm
            if (!airborne)
            {
                GroundFixed(hip, v, flat, comp, dt, groundedEff, onSupport ? under.point : hip.position - Vector3.up * standHipHeight);
                // the game lost the floor for a moment but it's right there: keep falling onto it (no floating, no fake take-off)
                if (floorUnder && !grounded && !virtualGround) AddToAllRigs(Vector3.down * DevTools.AirGravity);
            }
            else
            {
                AirFixed(v, flat, comp, gravity);
                if (airborne && !bailed) SaltoFixed();
            }
        }

        // Something pushes or pulls the rider. Small pushes move you (and make you wobble),
        // a hard hit or a strong pull throws you off the board. Returns true if you bailed.
        private bool HandleExternal(Vector3 v, float dt)
        {
            Vector3 imp = ExternalForces.Impulse, acc = ExternalForces.Accel;
            float dmg = ExternalForces.Damage;
            ExternalForces.Clear();
            if (!SkatePlugin.ExternalForcesOn.Value)
            {
                extVel = Vector3.zero;
                return false;
            }
            float knock = SkatePlugin.KnockOffStrength.Value;
            int dmgLimit = SkatePlugin.HitDamageBail.Value;
            if (Bails && imp.magnitude > knock) { Bail(Lang.T("СБИЛИ С ДОСКИ", "KNOCKED OFF THE BOARD"), knocked: true); return true; }
            if (Bails && dmgLimit > 0 && dmg >= dmgLimit) { Bail(Lang.T("ПОДСТРЕЛИЛИ С ДОСКИ", "SHOT OFF THE BOARD"), knocked: true); return true; }

            float pullLimit = SkatePlugin.PullOffStrength.Value;
            if (acc.magnitude > pullLimit) pullTime += dt; else pullTime = Mathf.Max(0f, pullTime - dt * 2f);
            if (Bails && pullTime > 0.35f) { Bail(Lang.T("СТАЩИЛО С ДОСКИ", "PULLED OFF THE BOARD"), knocked: true); return true; }

            if (imp.sqrMagnitude > 0.01f) wobble = Mathf.Min(1.5f, wobble + imp.magnitude * 0.5f);
            Vector3 add = imp + acc * dt;
            if (airborne || grinding)
            {
                airVel += new Vector3(add.x, 0, add.z);
                if (grinding && add.magnitude > 1.5f)
                {
                    EndGrind();
                    TakeOff(false);
                }
                return false;
            }
            // on the ground the wheels grip, so sideways pushes fade out quickly
            extVel += new Vector3(add.x, 0, add.z);
            extVel = Vector3.MoveTowards(extVel, Vector3.zero, (2f + extVel.magnitude) * dt);
            if (Bails && extVel.magnitude > knock * 1.5f) { Bail(Lang.T("СТОЛКНУЛИ С ДОСКИ", "PUSHED OFF THE BOARD"), knocked: true); return true; }
            // thrown up: leave the ground
            if (imp.y > 2.5f)
            {
                TakeOff(false);
                airVel += extVel;
                extVel = Vector3.zero;
            }
            return false;
        }

        // Running into a monster: you go flying, but it's worth a lot of aura.
        private bool CheckMonster(Vector3 hipPos, Vector3 flat)
        {
            if (monsterGrinding) return false; // riding it, not crashing into it
            if (!Bails) return false;          // bails off: crashing into monsters doesn't throw you off either
            Vector3 vel = airborne ? airVel : flat;
            float spd = new Vector3(vel.x, 0, vel.z).magnitude;
            if (spd < SkatePlugin.TripSpeed.Value || GettingUp) return false;
            Vector3 dir = new Vector3(vel.x, 0, vel.z) / spd;
            Vector3 p1 = hipPos + dir * 0.35f - Vector3.up * 0.6f, p2 = hipPos + dir * 0.35f + Vector3.up * 0.5f;
            int n = Physics.OverlapCapsuleNonAlloc(p1, p2, 0.45f, overlapHits, ~(1 << 29), QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var c = overlapHits[i];
                if (c == null) continue;
                var other = Owner<Player>.Of(c);
                if (other == null || other == player || !other.ai || other.data.dead) continue;
                if (skitching && other == skitchTarget) continue; // the one towing you isn't something you crash into
                // coming down on top of it from the air: that's a monster grind, not a crash
                if (airborne && SkatePlugin.MonsterGrinds.Value && c.bounds.max.y < hipPos.y - 0.2f) continue;
                MonsterBonk(other);
                return true;
            }
            return false;
        }

        private void MonsterBonk(Player monster)
        {
            int bonus = Mathf.RoundToInt(SkatePlugin.MonsterBonkAura.Value * SkatePlugin.AuraMultiplier.Value);
            Bail(Lang.T("БУМ В МОНСТРА!", "MONSTER BONK!"), trip: true, monster: true);
            if (bonus > 0)
            {
                Progress.Aura += bonus;
                SkateHUD.Popup("+" + bonus.ToString("N0") + Lang.T(" АУРЫ", " AURA"), new Color(1f, 0.85f, 0.3f), 2.4f);
                Play(SkateSounds.Chime, 0.8f);
            }
        }

        private void GroundFixed(Rigidbody hip, Vector3 v, Vector3 flat, float comp, float dt, bool grounded, Vector3 groundPoint)
        {
            var d = player.data;
            float maxSpeed = SkatePlugin.MaxSpeed.Value * BoardUpgrades.MaxSpeedMul * DevTools.SpeedMul; // speed wheels go faster

            // steering
            float turnFactor = speed < 0.3f ? 0.7f : Mathf.Lerp(1f, 0.5f, speed / maxSpeed);
            if (manual) turnFactor *= 0.7f;
            if (sliding) turnFactor *= 0.3f;
            // (speed wheels turn worse; at rocket speed you can barely turn at all)
            float turn = SkatePlugin.TurnSpeed.Value * turnFactor * BoardUpgrades.TurnMul * RocketTurnMul(maxSpeed) * dt;
            if (SkatePlugin.Steering.Value == SteeringMode.Keyboard)
                heading += kickturning ? 0f : rawMove.x * turn;
            else
                heading = Mathf.MoveTowardsAngle(heading, CamYaw() + rawMove.x * 30f, turn * 1.3f);

            Vector3 n = d.sinceGrounded < 0.2f && d.groundNormal.sqrMagnitude > 0.5f ? d.groundNormal : Vector3.up;
            Vector3 dir = Vector3.ProjectOnPlane(YawDir(heading), n).normalized;
            Vector3 dirFlat = new Vector3(dir.x, 0, dir.z).normalized;

            // pushing: separate kicks, like a real skater
            float pushTop = SkatePlugin.PushSpeed.Value * (1f - 0.2f * WearWeakness) * BoardUpgrades.PushTopMul * DevTools.SpeedMul; // a worn board is slower, the wheels matter too
            if (rawMove.y > 0.5f && !manual && !sliding && grounded && pushTimer <= 0f && speed < pushTop)
            {
                float kick = SkatePlugin.PushImpulse.Value * (speed < 1f ? 1.4f : 1f) * Mathf.Clamp01(1.25f - speed / pushTop);
                speed = Mathf.Min(pushTop, speed + Mathf.Max(kick, 0.3f));
                pushTimer = SkatePlugin.PushInterval.Value;
                // the back foot goes down and kicks (it has to be back on the deck before the next push)
                kickDur = Mathf.Clamp(SkatePlugin.PushInterval.Value * 0.85f, 0.2f, 0.5f);
                kickT = 0f;
                pushDip = kickDur * 0.7f; // the front knee bends so the back foot reaches the floor
                NetSync.SendPush(player, kickDur);
                Play(SkateSounds.Push, 0.35f * SkatePlugin.PushVolume.Value);
            }
            if (grounded)
                speed += Vector3.Dot(Vector3.down * 9.81f, dir) * 0.8f * dt; // slopes
            speed = Mathf.MoveTowards(speed, 0f, (SkatePlugin.RollFriction.Value + speed * 0.008f) * dt);
            if (sliding) speed = Mathf.MoveTowards(speed, 0f, 6f * dt);
            else if (rawMove.y < -0.5f) speed = Mathf.MoveTowards(speed, 0f, 4f * dt); // foot brake

            // what's in front of us
            if (grounded && speed > 0.6f && sinceLanded > 0.08f)
            {
                int crash = CheckCrash(groundPoint, n, dirFlat, speed, dt, out Player hitMonster);
                if (crash == 3)
                {
                    if (Bails && speed >= SkatePlugin.TripSpeed.Value) { MonsterBonk(hitMonster); return; }
                    speed = 0f;
                }
                else if (crash == 1)
                {
                    // Easy / Normal: the board hops over bumps, curbs and steps by itself instead of throwing you off
                    // (Normal: only up to about pushing speed)
                    if (!Hard && speed < StepOverMaxSpeed) StepOver();
                    else
                    {
                        // (soft wheels roll over small stuff you'd trip on otherwise)
                        if (Bails && speed >= SkatePlugin.TripSpeed.Value * BoardUpgrades.TripMul) { Bail(TripReasons[Random.Range(0, TripReasons.Length)], trip: true); return; }
                        speed = 0f; // slow: the board just stops
                    }
                }
                else if (crash == 2)
                {
                    // (Easy: a wall only throws you off a bit above pushing speed, Normal: above 5 m/s)
                    // (an invisible collider box just stops the board: crashing into nothing you can see feels like a bug)
                    if (Bails && speed >= WallGroundBailSpeed && !(lastBlock.collider != null && Invisible(lastBlock)))
                    {
                        LogCrash("ground", lastBlock);
                        Bail(Lang.T("ВРЕЗАЛСЯ В СТЕНУ", "SLAMMED INTO A WALL"), wall: true);
                        return;
                    }
                    speed = 0f;
                }
            }

            // body got stopped by something the casts didn't see (Easy: only if it lasts longer)
            float along = Vector3.Dot(flat, dirFlat);
            if (Bails && !skitching && sinceLanded > 0.3f && speed > SkatePlugin.TripSpeed.Value + 0.5f && along < speed * 0.45f) // (being yanked by a tow isn't a crash)
            {
                blockedTime += dt;
                if (blockedTime > (Easy ? 0.25f : 0.08f)) { Bail(Lang.T("РАЗБИЛСЯ", "CRASHED"), trip: true); return; }
            }
            else blockedTime = 0f;
            if (speed > along + 2.5f)
                speed = Mathf.MoveTowards(speed, Mathf.Max(0f, along), 20f * dt);
            speed = CapSpeed(speed, maxSpeed, dt); // (the rocket goes past the top speed, then it eases back down)

            if (grounded && speed > 0.3f) distanceTravelled += speed * dt;
            Vector3 desired = dirFlat * speed + extVel;
            Vector3 acc = (desired - flat) * 9f + desired * comp;
            ExternalForces.Self = true;
            try { player.refs.ragdoll.AddForce(Vector3.ClampMagnitude(acc, 150f), ForceMode.Acceleration); }
            finally { ExternalForces.Self = false; }
            // never zero: keeps the game's ground ray running even when standing still
            d.movementVector = dirFlat;

            if (!grounded)
                AddToAllRigs(Vector3.down * DevTools.AirGravity);
        }

        private void AirFixed(Vector3 v, Vector3 flat, float comp, float gravity)
        {
            // hit a wall mid-air? (not while something is towing you: that changes direction on its own)
            if (airVel.magnitude > 2f && !skitching)
            {
                float alongAir = Vector3.Dot(flat, airVel.normalized);
                if (airVel.magnitude - alongAir > 3.5f)
                {
                    // (only if there really is a wall ahead: scraping a roof or an invisible box isn't a crash)
                    if (Bails && airVel.magnitude > SkatePlugin.WallBailSpeed.Value + ByDifficulty(5f, 2.5f, 1f) && sinceGrind > 0.3f
                        && AirWallAhead(Hip(), out RaycastHit wallHit) == 2)
                    {
                        // a glancing hit is a wallride, not a crash
                        if (TryStartWallride(Hip(), v, 2f)) return;
                        LogCrash("mid-air", wallHit);
                        Bail(Lang.T("ВЛЕТЕЛ В СТЕНУ", "HIT A WALL MID-AIR"), wall: true);
                        return;
                    }
                    airVel = flat;
                }
            }
            Vector3 acc = (airVel - flat) * 3f + flat * comp;
            acc.y = v.y * comp - gravity;
            AddToAllRigs(acc);
            player.data.movementVector = Vector3.zero;
        }

        // 0 = clear, 1 = something at wheel height (trip), 2 = something at body height (wall), 3 = a monster
        private int CheckCrash(Vector3 groundPoint, Vector3 n, Vector3 dir, float spd, float dt, out Player monster)
        {
            if (crashMask == -1)
                crashMask = LayerMask.GetMask("Terrain", "Prop", "Default", "Player", "Interactable");
            Quaternion rot = Quaternion.LookRotation(dir, n);

            // low: curbs, steps, boxes, bushes (5..35 cm)
            Vector3 lowOrigin = groundPoint + n * 0.2f - dir * 0.15f;
            int c = Physics.BoxCastNonAlloc(lowOrigin, new Vector3(0.12f, 0.14f, 0.05f), dir, castHits, rot, 0.6f + spd * dt * 3f, crashMask, QueryTriggerInteraction.Ignore);
            if (AnyBlocking(c, n, dir, out monster)) return monster != null ? 3 : 1;

            // high: walls, doors, people
            Vector3 p1 = groundPoint + n * 0.6f - dir * 0.1f, p2 = groundPoint + n * 1.45f - dir * 0.1f;
            c = Physics.CapsuleCastNonAlloc(p1, p2, 0.22f, dir, castHits, 0.3f + spd * dt * 3f, crashMask, QueryTriggerInteraction.Ignore);
            if (AnyBlocking(c, n, dir, out monster)) return monster != null ? 3 : 2;
            return 0;
        }

        private RaycastHit lastBlock; // what CheckCrash ran into last

        private bool AnyBlocking(int count, Vector3 n, Vector3 dir, out Player monster)
        {
            monster = null;
            lastBlock = default;
            for (int i = 0; i < count; i++)
            {
                var h = castHits[i];
                if (h.collider == null || h.distance <= 0f) continue;
                if (board != null && h.collider.transform.IsChildOf(board.transform)) continue;
                if (Owner<SkateboardItemBehaviour>.Of(h.collider) != null) continue;
                var hp = Owner<Player>.Of(h.collider);
                if (hp == player) continue;
                if (skitching && hp != null && hp == skitchTarget) continue; // the one towing you (right in front in a train)
                var rb = h.rigidbody;
                if (rb != null && !rb.isKinematic && rb.mass < 10f && hp == null) continue; // small loose stuff gets pushed
                if (hp != null && hp.ai && !hp.data.dead) { monster = hp; return true; }
                if (Vector3.Dot(h.normal, n) > 0.55f) continue;   // a ramp, roll up it
                if (Vector3.Dot(h.normal, -dir) < 0.3f) continue; // just scraping along a side
                lastBlock = h;
                return true;
            }
            return false;
        }

        // Nearest thing straight below the hip that isn't us (all layers, physics objects included).
        private bool ProbeBelow(Vector3 hipPos, out RaycastHit best)
        {
            best = default;
            int count = Physics.RaycastNonAlloc(hipPos, Vector3.down, probeHits, 2.5f, ~(1 << 29), QueryTriggerInteraction.Ignore);
            float bestD = float.MaxValue;
            bool found = false;
            for (int i = 0; i < count; i++)
            {
                var h = probeHits[i];
                if (h.collider == null || h.distance >= bestD) continue;
                if (board != null && h.collider.transform.IsChildOf(board.transform)) continue;
                if (Owner<Player>.Of(h.collider) == player) continue;
                best = h;
                bestD = h.distance;
                found = true;
            }
            return found;
        }

        // Push the rider off a monster / loose object they landed on.
        private void SlideOff(Vector3 hipPos, RaycastHit under)
        {
            if (slideOffCooldown > 0f) return;
            slideOffCooldown = 0.35f;
            Vector3 away = hipPos - under.collider.bounds.center;
            away.y = 0f;
            if (away.sqrMagnitude < 0.01f) away = airVel.sqrMagnitude > 0.1f ? airVel : YawDir(heading);
            away.Normalize();
            SetAllVelocities(rv => new Vector3(away.x * 4f, Mathf.Max(rv.y, 2.5f), away.z * 4f));
            airVel = away * 4f;
        }

        private void SetAllVelocities(System.Func<Vector3, Vector3> f)
        {
            var rigs = player.refs.ragdoll.rigList;
            for (int i = 0; i < rigs.Count; i++)
                if (rigs[i] && !rigs[i].isKinematic) rigs[i].linearVelocity = f(rigs[i].linearVelocity);
        }

        private void AddToAllRigs(Vector3 acc)
        {
            var rigs = player.refs.ragdoll.rigList;
            for (int i = 0; i < rigs.Count; i++)
                if (rigs[i] && !rigs[i].isKinematic) rigs[i].AddForce(acc, ForceMode.Acceleration);
        }

        private void DoOllie(Trampoline tramp, bool boneless = false)
        {
            float t = charge / MaxCharge;
            float pop = Mathf.Lerp(SkatePlugin.OllieMin.Value, SkatePlugin.OllieMax.Value, t);
            if (boneless)
            {
                // the planted foot launches you higher than any ollie, but costs some speed
                pop = SkatePlugin.OllieMax.Value * 1.2f;
                speed *= 0.8f;
            }
            pop *= DevTools.JumpMul;
            if (tramp != null)
            {
                pop *= SkatePlugin.TrampolineBoost.Value;
                TrampolineFx(tramp, pop);
            }
            SetAllVelocities(rv => new Vector3(rv.x, pop, rv.z));
            player.data.sinceJump = 0f;
            player.data.isGrounded = false;
            TakeOff(true);
            bonelessThisAir = boneless;
            SkateActivity.Local(SkateAct.Ollie, combo.Count, (fakie ? "Fakie " : "") + (boneless ? "Boneless" : "Ollie"));
            Play(SkateSounds.Pop, 0.9f);
        }

        private void TakeOff(bool ollie)
        {
            airborne = true;
            dropIn = false;
            airTime = 0;
            lastAirVy = 0;
            ollieThisAir = ollie;
            bonelessThisAir = false;
            spinAccum = 0;
            grabTime = 0;
            grab = grabThisAir = Grab.None;
            flip = Flip.None;
            flipCount = 0;
            flipProgress = 0;
            flipsThisAir.Clear();
            ClearSalto();
            hippie = false;
            if (manual) EndManual();
            if (sliding) EndSlide();
            if (kickturning) EndKickturn();
            airVel = YawDir(heading) * speed;
            combo.KeepAlive();
        }

        private void StartFlip(Flip f)
        {
            if (flip != Flip.None)
            {
                // same key again mid-flip = one more rotation
                if (f == flip && flipCount < 3 && flipProgress < flipCount)
                {
                    flipCount++;
                    SkateActivity.Local(SkateAct.Flip, combo.Count, (fakie ? "Fakie " : "") + Tricks.FlipName(flip, flipCount));
                    Play(SkateSounds.Whoosh, 0.5f);
                    NetSync.SendFlip(player, flip, flipCount);
                }
                return;
            }
            flip = f;
            SkateActivity.Local(SkateAct.Flip, combo.Count, (fakie ? "Fakie " : "") + Tricks.FlipName(f, 1));
            flipCount = 1;
            flipProgress = 0;
            Play(SkateSounds.Whoosh, 0.6f);
            NetSync.SendFlip(player, flip, flipCount);
        }

        // Turns everything done in this air into combo entries.
        private void ScoreAir()
        {
            if (flip != Flip.None) flipsThisAir.Add(new KeyValuePair<Flip, int>(flip, flipCount));
            flip = Flip.None;
            flipProgress = 0;

            int spin = Mathf.RoundToInt(Mathf.Abs(spinAccum) / 180f) * 180;
            string spinName = spin >= 180 ? (spinAccum > 0 ? "FS " : "BS ") + spin : null;
            float spinPts = Tricks.SpinPoints(spin);
            int before = combo.Count;

            for (int i = 0; i < flipsThisAir.Count; i++)
            {
                var fl = flipsThisAir[i];
                string name = Tricks.FlipName(fl.Key, fl.Value);
                float pts = Tricks.FlipPoints(fl.Key, fl.Value);
                if (i == 0 && spinName != null) { name = spinName + " " + name; pts += spinPts; spinName = null; }
                combo.Add(name, pts);
            }
            if (spinName != null) combo.Add(spinName, spinPts);
            ScoreSaltos();
            if (grabTime > 0.15f) combo.Add(Tricks.GrabName(grabThisAir), Tricks.GrabPoints(grabThisAir, grabTime));
            if (bonelessThisAir && airTime > 0.2f) combo.Add(fakie ? "Fakie Boneless" : "Boneless", 90f);
            bonelessThisAir = false;
            if (combo.Count == before && airTime > 0.25f)
            {
                if (ollieThisAir) combo.Add(fakie ? "Fakie Ollie" : "Ollie", 50f);
                else if (airTime > 0.5f) combo.Add("Drop", 40f);
            }
            combo.AddBonus(airTime * 60f);
            if (airTime > 1.3f)
            {
                combo.Add("BIG AIR", 300f + (airTime - 1.3f) * 400f);
                SkateActivity.Local(SkateAct.BigAir, combo.Count, "", Mathf.Clamp(1f + (airTime - 1.3f) * 0.5f, 1f, 2.5f)); // longer flight, more views
            }
            if (spin >= 360) SkateActivity.Local(SkateAct.Spin, combo.Count, spin + "");

            // records
            int flips = 0;
            foreach (var fl in flipsThisAir) flips += fl.Value;
            if (flips > 0) Progress.AddStat("flips", flips, save: false);
            if (spin >= 180) Progress.MaxStat("spin", spin);
            if (airTime > 0.25f) Progress.MaxStat("air_ms", (long)(airTime * 1000f));
            if (combo.Active) combo.KeepAlive();
            flipsThisAir.Clear();
            grab = grabThisAir = Grab.None;
            grabTime = 0f;
        }

        // The board is tipped on one pair of wheels: keep both feet on the deck (the raised end lifts its foot and knee).
        private void FeetOnTiltedDeck(PlayerRagdoll rag, Vector3 hipVel)
        {
            var fl = rag.GetBodypart(BodypartType.Foot_L);
            var fr = rag.GetBodypart(BodypartType.Foot_R);
            if (fl == null || fr == null || fl.rig == null || fr.rig == null) return;
            // how much higher the tipped deck is than the flat one, under each foot
            float dl = DeckRise(fl.rig.position), dr = DeckRise(fr.rig.position);
            bool leftHigh = dl > dr;
            var high = leftHigh ? fl : fr;
            var low = leftHigh ? fr : fl;
            float rise = Mathf.Abs(dl - dr);
            if (rise < 0.01f) return;
            float blend = Mathf.Max(manualBlend, noseBlend);
            GrabPose.Lift(high, low.rig.position.y + rise, hipVel, blend * 1.6f);
            var highKnee = rag.GetBodypart(leftHigh ? BodypartType.Knee_L : BodypartType.Knee_R);
            var lowKnee = rag.GetBodypart(leftHigh ? BodypartType.Knee_R : BodypartType.Knee_L);
            if (highKnee != null && lowKnee != null && lowKnee.rig != null)
                GrabPose.Lift(highKnee, lowKnee.rig.position.y + rise * 0.8f, hipVel, blend);
        }

        // height of the tipped deck above the flat deck at this point (from above)
        private float DeckRise(Vector3 at)
        {
            float Y(Transform tr)
            {
                Vector3 n = tr.up, p = tr.position;
                return p.y - (n.x * (at.x - p.x) + n.z * (at.z - p.z)) / Mathf.Max(0.2f, n.y);
            }
            return Y(board.model) - Y(board.transform);
        }

        private void Land()
        {
            airborne = false;
            sinceLanded = 0f;
            blockedTime = 0f;
            float travelYaw = airVel.sqrMagnitude > 1f ? Yaw(airVel) : heading;
            heading = travelYaw;
            speed = new Vector3(airVel.x, 0, airVel.z).magnitude;
            float rel = Mathf.DeltaAngle(travelYaw, bodyYaw);

            if (Bails && !GettingUp && !DevTools.PerfectLandings)
            {
                string fail = null;
                if (hippie && HippieMissed()) fail = Lang.T("ПРОМАХНУЛСЯ МИМО ДОСКИ", "MISSED THE BOARD");
                else if (SaltoUnfinished) fail = salto > 0 ? Lang.T("НЕ ДОКРУТИЛ САЛЬТО ВПЕРЁД", "DIDN'T FINISH THE FRONTFLIP") : Lang.T("НЕ ДОКРУТИЛ САЛЬТО НАЗАД", "DIDN'T FINISH THE BACKFLIP");
                else if (flip != Flip.None && flipProgress < flipCount - ByDifficulty(0.5f, 0.3f, 0.15f)) fail = Lang.T("НЕ ДОКРУТИЛ ФЛИП", "DIDN'T FINISH THE FLIP");
                // sideways only counts after a real jump at speed (slow, or a tiny hop off a bump: you just straighten up)
                // (Easy: only when really sideways, otherwise you straighten up by yourself)
                else if (Mathf.Abs(rel) > (Easy ? 65f : 50f) && Mathf.Abs(rel) < (Easy ? 115f : 130f) && speed > 3f && (ollieThisAir || airTime > 0.35f)) fail = Lang.T("ПРИЗЕМЛИЛСЯ БОКОМ", "LANDED SIDEWAYS");
                else if (lastAirVy < -LandingLimit && trampUnder == null) fail = Lang.T("НОГИ НЕ ВЫДЕРЖАЛИ", "LEGS GAVE UP");
                else if (!Easy && grab != Grab.None && airTime > 0.3f) fail = Lang.T("НЕ ОТПУСТИЛ ДОСКУ", "STILL HOLDING THE BOARD");
                if (fail != null)
                {
                    Bail(fail, trip: true);
                    return;
                }
            }
            fakie = Mathf.Abs(rel) >= 130f;
            if (!fakie) bodyYaw = travelYaw + rel; // keep it smooth, Update snaps it straight
            if (hippie) LandHippie();
            WearLanding();
            if (bailed) return; // the board snapped under you
            ScoreDropIn();
            ScoreAir();

            if (trampUnder != null && (SkatePlugin.TrampolineAutoBounce.Value || rawJump))
            {
                TrampolineBounce(trampUnder);
                return;
            }
            Play(SkateSounds.Land, 0.85f);
        }

        // Landing on a trampoline while holding jump (or with AutoBounce) throws you back up.
        private void TrampolineBounce(Trampoline t)
        {
            float vel = Mathf.Clamp(-lastAirVy * 0.9f, 6f, 22f) + (rawJump ? 4f : 0f);
            SetAllVelocities(rv => new Vector3(rv.x, vel, rv.z));
            player.data.sinceJump = 0f;
            player.data.isGrounded = false;
            TrampolineFx(t, vel);
            TakeOff(true);
            combo.AddBonus(40f);
        }

        private void TrampolineFx(Trampoline t, float vel)
        {
            player.data.sinceTrampoline = 0f;
            try
            {
                var sfx = vel > 14f ? t.jumpSFX_Big : vel > 10f ? t.jumpSFX_Medium : t.jumpSFX;
                if (sfx != null) foreach (var s in sfx) s?.Play(t.transform.position);
                if (t.rig != null) t.rig.AddForce(Vector3.down * Mathf.Min(vel, 15f) * t.bounceForce, ForceMode.VelocityChange);
            }
            catch { }
        }

        // ---------------------------------------------------------------- grinds

        private bool SurfaceAt(Vector3 p, float y, out float hitY)
        {
            hitY = y;
            if (Physics.Raycast(new Vector3(p.x, y + 0.25f, p.z), Vector3.down, out RaycastHit h, 0.37f, grindMask, QueryTriggerInteraction.Ignore)
                && Mathf.Abs(h.point.y - y) < 0.1f && h.normal.y > 0.7f)
            {
                hitY = h.point.y;
                return true;
            }
            return false;
        }

        // Falling onto a rail or the edge of a ledge with the board = grind.
        private bool TryStartGrind(Rigidbody hip, Vector3 v)
        {
            if (!SkatePlugin.GrindsEnabled.Value || airTime < 0.15f || v.y > 0.5f || flip != Flip.None) return false;
            // only after a jump (ollie, boneless, a pop off a rail / wall / monster): rolling off a curb or a step
            // used to "grind" the edge you just left
            if (!ollieThisAir || skitching) return false;
            if (airVel.magnitude < 1.5f || sinceGrind < 0.4f) return false;
            if (grindMask == -1) grindMask = LayerMask.GetMask("Terrain", "Prop", "Default");

            var rag = player.refs.ragdoll;
            Vector3 boardPos = FeetPos(rag, hip.position) - Vector3.up * feetOffset;
            float magnet = SkatePlugin.GrindMagnet.Value;
            if (!Physics.Raycast(boardPos + Vector3.up * 0.3f, Vector3.down, out RaycastHit top, 0.3f + BoardVisual.WheelBase + magnet, grindMask, QueryTriggerInteraction.Ignore))
                return false;
            if (top.normal.y < 0.8f || Owner<Player>.Of(top.collider) != null) return false;
            if (top.rigidbody != null && !top.rigidbody.isKinematic) return false;

            float travelYaw = Yaw(airVel);
            float y = top.point.y;
            for (int k = 0; k < 9; k++)
            {
                float a = k == 0 ? 0f : ((k + 1) / 2) * 15f * (k % 2 == 0 ? 1f : -1f);
                Vector3 d = YawDir(travelYaw + a);
                Vector3 side = Vector3.Cross(Vector3.up, d).normalized;
                bool l = SurfaceAt(top.point + side * 0.2f, y, out _);
                bool r = SurfaceAt(top.point - side * 0.2f, y, out _);
                if (l && r) continue;                                   // wide floor, not a rail
                if (!SurfaceAt(top.point + d * 0.3f, y, out _) || !SurfaceAt(top.point - d * 0.3f, y, out _)) continue; // must go on along d
                StartGrind(top.point, d);
                return true;
            }
            return false;
        }

        private void StartGrind(Vector3 point, Vector3 dir)
        {
            ScoreAir();
            grinding = true;
            airborne = false;
            grindTime = 0f;
            railPoint = point;
            railDir = new Vector3(dir.x, 0, dir.z).normalized;
            speed = Mathf.Max(Vector3.Dot(new Vector3(airVel.x, 0, airVel.z), railDir), 2.5f);
            heading = Yaw(railDir);

            float rel = Mathf.DeltaAngle(Yaw(railDir), bodyYaw);
            float arel = Mathf.Abs(rel);
            grindSlide = arel > 50f && arel < 130f;
            SkateActivity.Local(SkateAct.Grind, combo.Count, grindSlide ? "Boardslide" : "50-50");
            grindYawOffset = grindSlide ? Mathf.Sign(rel) * 90f : (arel >= 130f ? 180f : 0f);
            fakie = !grindSlide && arel >= 130f;
            Play(SkateSounds.Land, 0.6f);
            combo.KeepAlive();
        }

        private void GrindFixed(Rigidbody hip, Vector3 v, float comp, float dt)
        {
            var d = player.data;
            grindTime += dt;
            combo.KeepAlive();
            d.sinceGrounded = 0f;
            d.movementVector = Vector3.zero;
            speed = Mathf.MoveTowards(speed, 0f, SkatePlugin.GrindFriction.Value * dt);

            if (pendingOllie)
            {
                pendingOllie = false;
                EndGrind();
                float pop = SkatePlugin.OllieMin.Value;
                SetAllVelocities(rv => new Vector3(railDir.x * speed, pop, railDir.z * speed));
                player.data.sinceJump = 0f;
                TakeOff(true);
                Play(SkateSounds.Pop, 0.9f);
                return;
            }

            Vector3 next = railPoint + railDir * speed * dt;
            bool more = SurfaceAt(next + railDir * 0.15f, railPoint.y, out float yAhead);
            if (speed < 0.6f || !more)
            {
                // ran out of rail (or speed): fly off the end
                EndGrind();
                SetAllVelocities(rv => new Vector3(railDir.x * speed, Mathf.Max(rv.y, 0.5f), railDir.z * speed));
                TakeOff(false);
                return;
            }
            float yHere = SurfaceAt(next, railPoint.y, out float yy) ? yy : railPoint.y;
            float drop = railPoint.y - yHere;
            speed += drop * 9.81f * 0.6f / Mathf.Max(speed, 1f); // sloped rails speed you up
            railPoint = new Vector3(next.x, yHere, next.z);

            // hold the rider right above the rail
            Vector3 target = railPoint + Vector3.up * standHipHeight;
            Vector3 desired = railDir * speed + (target - hip.position) * 10f;
            Vector3 acc = (desired - v) * 25f + desired * comp;
            AddToAllRigs(Vector3.ClampMagnitude(acc, 300f));
        }

        private void EndGrind()
        {
            if (!grinding) return;
            grinding = false;
            sinceGrind = 0f;
            if (grindTime > 0.15f)
            {
                combo.Add(grindSlide ? "Boardslide" : "50-50 Grind", (grindSlide ? 120f : 100f) + grindTime * (grindSlide ? 220f : 180f));
                Progress.MaxStat("grind_ms", (long)(grindTime * 1000f));
                if (grindTime > 0.6f) // a long grind is worth more on video
                    SkateActivity.Local(SkateAct.Grind, combo.Count, grindSlide ? "Boardslide" : "50-50", Mathf.Clamp(1f + grindTime * 0.3f, 1f, 2.2f));
            }
            combo.KeepAlive();
        }

        // Kickturn ends: worth something once you've turned at least ~90 degrees
        private void EndKickturn()
        {
            if (!kickturning) return;
            kickturning = false;
            if (kickAccum >= 80f)
            {
                int deg = Mathf.Max(90, Mathf.RoundToInt(kickAccum / 90f) * 90);
                combo.Add("Kickturn " + deg, 30f + deg * 0.35f);
                SkateActivity.Local(SkateAct.Spin, combo.Count, "Kickturn " + deg);
            }
            kickAccum = 0f;
        }

        // Revert: the board (and you) spin 180 on the ground, you keep rolling the same way, now fakie.
        // Right after a landing it continues the combo.
        private void DoRevert()
        {
            fakie = !fakie; // Update turns the body to match, ~0.2 s
            revertCooldown = 0.5f;
            speed *= 0.92f;
            Play(SkateSounds.Whoosh, 0.45f);
            if (combo.Active && sinceLanded < 0.7f)
            {
                combo.Add("Revert", 70f);
                SkateActivity.Local(SkateAct.Spin, combo.Count, "Revert");
            }
        }

        private void EndManual()
        {
            if (manualTime > 0.4f)
            {
                if (noseManual) combo.Add("Nose Manual", 80f + manualTime * 150f);
                else combo.Add("Manual", 60f + manualTime * 120f);
                Progress.MaxStat("manual_ms", (long)(manualTime * 1000f));
                if (manualTime > 1.2f) // a long manual is worth more on video
                    SkateActivity.Local(SkateAct.Manual, combo.Count, noseManual ? "Nose Manual" : "Manual", Mathf.Clamp(1f + (manualTime - 1.2f) * 0.3f, 1f, 2f));
            }
            manual = false;
            manualTime = 0;
        }

        private void EndSlide()
        {
            if (slideTime > 0.35f) combo.Add("Powerslide", 40f + slideTime * 80f);
            sliding = false;
            slideTime = 0;
        }

        // ---------------------------------------------------------------- aura

        private void Commit()
        {
            long value = combo.Value;
            if (value <= 0) { combo.Clear(); return; }
            long before = Progress.Aura;
            long after = before + value;
            Progress.Aura = after;

            var gold = new Color(1f, 0.85f, 0.3f);
            SkateHUD.Popup("+" + value.ToString("N0") + Lang.T(" АУРЫ", " AURA"), gold, 2.2f);
            if (value > Progress.BestCombo)
            {
                Progress.BestCombo = value;
                if (value > 1000) SkateHUD.Popup(Lang.T("НОВОЕ ЛУЧШЕЕ КОМБО!", "NEW BEST COMBO!"), new Color(0.4f, 1f, 0.6f), 2.2f, small: true);
            }
            if (Aura.RankIndex(after) > Aura.RankIndex(before))
            {
                SkateHUD.Popup(Lang.T("НОВЫЙ РАНГ: ", "RANK UP: ") + Aura.RankName(after).ToUpperInvariant(), Aura.RankColor(after), 3.5f);
                Play(SkateSounds.Chime, 1f);
            }
            Play(SkateSounds.Chime, 0.6f);

            // records (Tab)
            Progress.AddStat("combos", 1, save: false);
            // bigger combos (in aura) get more views on top of the trick count
            if (combo.Count >= 3) SkateActivity.Local(SkateAct.Combo, combo.Count, "", Mathf.Clamp(1f + Mathf.Log10(Mathf.Max(1f, value / 1000f)) * 0.5f, 1f, 2.5f));
            Progress.AddStat("tricks", combo.Count, save: false);
            FlushDistance();
            int place = Progress.AddCombo(value, string.Join(" + ", combo.Names));
            if (place > 1 && combo.Count >= 2) SkateHUD.Popup("#" + place + Lang.T(" В ТВОИХ РЕКОРДАХ", " IN YOUR RECORDS"), new Color(0.6f, 0.9f, 1f), 2f, small: true);
            combo.Clear();
        }

        private float distanceTravelled;

        private void FlushDistance()
        {
            if (distanceTravelled < 1f) return;
            long m = (long)distanceTravelled;
            distanceTravelled -= m;
            Progress.AddStat("dist", m, save: false);
        }

        private static bool IsLeg(BodypartType t)
        {
            switch (t)
            {
                case BodypartType.Leg_L: case BodypartType.Knee_L: case BodypartType.Foot_L: case BodypartType.Toe_L: case BodypartType.Hip_L:
                case BodypartType.Leg_R: case BodypartType.Knee_R: case BodypartType.Foot_R: case BodypartType.Toe_R: case BodypartType.Hip_R:
                    return true;
            }
            return false;
        }

        // trip = the board stopped dead and you fly forward over it
        // wall = you hit something with your body and bounce back
        // knocked = something hit / pulled you: your body keeps the push it got
        // monster = you crashed into a monster (no aura lost, you get a bonus instead)
        private void Bail(string reason, bool external = false, bool trip = false, bool wall = false, bool knocked = false, bool monster = false)
        {
            if (bailed) return;
            bailed = true;
            SkatePlugin.Log.LogInfo("Bail: " + reason + " (speed " + speed.ToString("0.0") + ", air " + airborne + ")");
            bool hadCombo = combo.Active || airborne || grinding;
            if (grinding) grinding = false;
            wallriding = false;
            if (grab != Grab.None) { grab = Grab.None; SyncGrab(); }
            // The monster you were riding (or just jumped off) threw you: that's its fault, not yours.
            // The combo you built up on it counts, and you lose no aura.
            bool offMonster = external && (monsterGrinding || Time.time - monsterGrindEndedAt < 1.5f);
            if (offMonster && (combo.Active || monsterGrinding))
            {
                if (monsterGrinding && monsterGrindTime > 0.2f) combo.Add("Monster Grind", 220f + monsterGrindTime * 320f);
                Commit();
                SkateHUD.Popup(Lang.T("МОНСТР СБРОСИЛ - КОМБО СОХРАНЕНО", "THROWN OFF THE MONSTER - COMBO SAVED"), new Color(1f, 0.6f, 0.9f), 2.2f, small: true);
            }
            int lostTricks = combo.Count; // the bigger the combo you lose, the funnier the fall
            combo.Clear();
            ClearSalto();
            hippie = false;
            monsterGrinding = false;
            grindMonster = null;
            StopSkitch();

            Progress.AddStat(monster ? "bonks" : "bails", 1, save: false);
            float crashSpeed = Mathf.Max(speed, airborne ? new Vector3(airVel.x, 0, airVel.z).magnitude : 0f);
            SkateActivity.Local(monster ? SkateAct.MonsterBonk : SkateAct.Bail, lostTricks, "", Mathf.Clamp(1f + crashSpeed * 0.04f, 1f, 2f)); // faster crash, more views

            // falling off hurts: the faster you hit (or the harder you land), the more.
            // Not when something knocked you off: that hit already did its own damage.
            int baseDamage = SkatePlugin.BailDamage.Value;
            // at rocket speed everything is worse: up to x6 the damage (instead of x2.5) and x1.5 the wear
            bool rocketCrash = RocketCrash(crashSpeed);
            bool rocketWasBurning = rocketOn;
            SetRocket(false);
            if (rocketWasBurning) MaybeBlowUpRocket(); // crashing with the rocket on: it can go off
            if (baseDamage > 0 && !external && !knocked)
            {
                float impact = Mathf.Max(crashSpeed, sinceLanded < 0.1f ? -lastAirVy * 0.7f : 0f);
                float dmg = baseDamage * (rocketCrash ? Mathf.Clamp(impact / 8f, 0.4f, 4f) * 1.5f : Mathf.Clamp(impact / 8f, 0.4f, 2.5f));
                try { player.CallTakeDamage(dmg); }
                catch (System.Exception e) { SkatePlugin.Log.LogWarning("Bail damage: " + e.Message); }
            }
            // Bails off and something else knocked you down (a monster attack, the game's own ragdoll):
            // you go down, but nothing else happens - no aura lost, no wear, the board stays yours
            bool soft = external && !Bails;
            // the board takes the hit too (faster crash, into a wall or a monster = more)
            if (!soft) BoardWear.Damage((external && !hadCombo ? 2f : (6f + crashSpeed * 0.6f) * (monster ? 1.3f : 1f) * (wall ? 1.2f : 1f)) * (rocketCrash ? 1.5f : 1f));
            bool snapped = BoardWear.LocalBroken;
            FlushDistance();
            Progress.Save();

            if (monster)
            {
                SkateHUD.Popup(reason, new Color(1f, 0.55f, 0.9f), 2.2f);
                Play(SkateSounds.Bail, 0.7f);
            }
            else if (!soft && !offMonster && (!external || hadCombo))
            {
                int penalty = knocked ? 0 : SkatePlugin.BailPenalty.Value;
                if (penalty > 0) Progress.Aura -= penalty;
                SkateHUD.Popup(penalty > 0 ? Lang.T("ПАДЕНИЕ! -", "BAIL! -") + penalty + Lang.T(" АУРЫ", " AURA") : Lang.T("ПАДЕНИЕ!", "BAIL!"), new Color(1f, 0.3f, 0.3f), 2.2f);
                SkateHUD.Popup(reason, new Color(1f, 0.6f, 0.6f), 2.2f, small: true);
                Play(SkateSounds.Bail, 0.9f);
            }

            var rag = player.refs.ragdoll;
            Vector3 v = Hip().linearVelocity;
            Vector3 flat = new Vector3(v.x, 0, v.z);
            Vector3 dir = flat.magnitude > 0.5f ? flat.normalized : YawDir(heading);
            float spd = Mathf.Max(speed, flat.magnitude);

            if (knocked)
            {
                // the hit already moved the body, just let it go limp
                rag.CallFall(SkatePlugin.BailTime.Value);
            }
            else if (!external)
            {
                foreach (var bp in rag.bodypartList)
                {
                    var rig = bp != null ? bp.rig : null;
                    if (rig == null || rig.isKinematic) continue;
                    Vector3 rv = rig.linearVelocity;
                    if (wall)
                    {
                        rv = -dir * (spd * 0.3f) + Vector3.up * 1.5f;
                    }
                    else if (trip)
                    {
                        // legs get stopped by the board, the rest of you keeps going
                        if (IsLeg(bp.bodypartType)) rv = new Vector3(rv.x * 0.2f, rv.y, rv.z * 0.2f);
                        else rv += dir * (spd * 0.15f) + Vector3.up * (1.2f + spd * 0.12f);
                    }
                    else rv += Vector3.up * 1f;
                    rig.linearVelocity = rv;
                }
                rag.CallFall(SkatePlugin.BailTime.Value);
            }

            Vector3 flingVel = trip || wall ? -dir * 1.2f + Vector3.up * 2.2f // board hits the obstacle and pops back
                                            : flat * 1.1f + dir * 2f + Vector3.up * 2.5f;
            bool dropped = false;
            if (snapped)
            {
                // worn out: it breaks in half, gone from the inventory
                BoardWear.Snap(player, board.model.position, board.model.rotation, flingVel, board.transform.localScale.x);
                board.gameObject.SetActive(false);
                dropped = true; // (others hide our board the same way as a dropped one)
            }
            else if (SkatePlugin.PickUpAfterBail.Value && !soft)
            {
                // the board leaves your inventory and flies off as a normal item: walk over and press E to take it back
                Transform m = board.model;
                dropped = SkateItem.DropBoard(player, m.position + Vector3.up * 0.05f, m.rotation, flingVel, Random.insideUnitSphere * 8f);
                if (dropped) board.gameObject.SetActive(false);
            }
            if (!dropped) board.Fling(flingVel, trip || wall ? 8f : 14f);
            NetSync.SendBail(player, dropped);
            NetSync.SetState(CurrentState);

            airborne = false;
            manual = sliding = charging = pendingOllie = false;
            kickturning = noseManual = pendingBoneless = bonelessThisAir = false;
            grab = grabThisAir = Grab.None;
            flip = Flip.None;
            flipProgress = 0;
            flipsThisAir.Clear();
            speed = 0;
            extVel = Vector3.zero;
            pullTime = 0f;
            recoverTimer = 0.5f;
            player.data.isCrouching = false;
            player.data.rotationOvverideStr = 0f;
        }

        // ---------------------------------------------------------------- visuals & audio

        private void UpdateBoard(float dt)
        {
            if (board == null || board.Flying) return;
            float bs = SkatePlugin.RideBoardScale.Value; // bigger board under your feet
            board.transform.localScale = Vector3.one * bs;
            var rag = player.refs.ragdoll;
            Vector3 hipPos = rag.GetBodypart(BodypartType.Hip).transform.position;
            Vector3 feet = FeetPos(rag, hipPos);

            slideBlend = Mathf.MoveTowards(slideBlend, sliding ? 1f : 0f, dt * 6f);
            manualBlend = Mathf.MoveTowards(manualBlend, (manual && !noseManual) || kickturning ? 1f : 0f, dt * 7f);
            noseBlend = Mathf.MoveTowards(noseBlend, manual && noseManual ? 1f : 0f, dt * 7f);
            grabBlend = Mathf.MoveTowards(grabBlend, grab != Grab.None ? 1f : 0f, dt * 9f);
            airPitch = Mathf.MoveTowards(airPitch, airborne && flip == Flip.None && grab == Grab.None ? -8f : 0f, dt * 60f);
            leanVis = Mathf.MoveTowards(leanVis, lean, dt * 4f);

            if (grinding)
            {
                // trucks on the rail (50-50) or the middle of the deck on it (boardslide)
                Vector3 p = railPoint + Vector3.up * ((grindSlide ? 0.012f : 0.047f) * bs);
                boardUp = Vector3.Slerp(boardUp, Vector3.up, dt * 12f);
                float gy = bodyYaw;
                board.Place(p, Quaternion.LookRotation(YawDir(gy), Vector3.up), Quaternion.Euler(0, 0, Mathf.Sin(Time.time * 23f) * 1.5f), Vector3.zero);
                return;
            }

            if (hippie)
            {
                UpdateHippieBoard(dt, bs); // rolling on by itself under you
                return;
            }

            if (wallriding)
            {
                // wheels on the wall, under the feet
                Vector3 onWall = feet - wallNormal * Vector3.Dot(feet - wallPoint, wallNormal);
                board.Place(onWall + wallNormal * (BoardVisual.WheelBase * bs), Quaternion.LookRotation(wallAlong, wallNormal), Quaternion.identity, Vector3.zero);
                return;
            }

            Vector3 pos, up;
            bool hitGround = Physics.Raycast(hipPos, Vector3.down, out RaycastHit hit, 2.2f,
                HelperFunctions.GetMask(HelperFunctions.LayerType.TerrainProp), QueryTriggerInteraction.Ignore);
            if (!airborne && hitGround && hit.distance < standHipHeight + 0.4f)
            {
                pos = hit.point + hit.normal * (BoardVisual.WheelBase * bs);
                up = hit.normal;
                feetOffset = Mathf.Clamp(Mathf.Lerp(feetOffset, feet.y - pos.y, dt * 5f), 0.02f, 0.4f);
            }
            else
            {
                pos = feet - Vector3.up * feetOffset;
                up = Vector3.up;
            }
            boardUp = Vector3.Slerp(boardUp, up, dt * 12f);

            float yaw = bodyYaw + slideDir * 80f * slideBlend;
            Vector3 fwd = Vector3.ProjectOnPlane(YawDir(yaw), boardUp);
            if (fwd.sqrMagnitude < 1e-4f) fwd = YawDir(yaw);
            Quaternion baseRot = Quaternion.LookRotation(fwd.normalized, boardUp);

            Quaternion local = WearWobble() * Quaternion.Euler(0, 0, -leanVis * 8f);
            Vector3 offset = Vector3.zero;
            if (flip != Flip.None)
            {
                local = Tricks.FlipRotation(flip, flipProgress);
                offset.y += Mathf.Sin(Mathf.PI * Mathf.Clamp01(flipProgress / flipCount)) * 0.16f;
            }
            // manual: tip up on the back wheels
            if (manualBlend > 0f)
            {
                Quaternion m = Quaternion.Euler(-14f * manualBlend, 0, 0);
                Vector3 pivot = new Vector3(0, -BoardVisual.WheelBase, -0.215f);
                offset += pivot - m * pivot;
                local = m * local;
            }
            // nose manual: tip down onto the front wheels
            if (noseBlend > 0f)
            {
                Quaternion m = Quaternion.Euler(14f * noseBlend, 0, 0);
                Vector3 pivot = new Vector3(0, -BoardVisual.WheelBase, 0.215f);
                offset += pivot - m * pivot;
                local = m * local;
            }
            // grab: the board comes up with the knees, tilts, and meets the hand halfway
            if (grabBlend > 0f)
            {
                Grab g = grab != Grab.None ? grab : grabThisAir;
                local = Quaternion.Slerp(Quaternion.identity, Tricks.GrabRotation(g), grabBlend) * local;
            }
            if (airPitch != 0f) local = Quaternion.Euler(airPitch, 0, 0) * local;
            if (grabBlend <= 0.01f && salto == 0) hipToBoard = Mathf.Clamp(hipPos.y - pos.y, 0.3f, 1.6f);
            if (grabBlend > 0f)
            {
                // In a grab the feet follow the board (IK), so the board can't follow the feet:
                // it hangs off the hips and comes up a third of the way (the knees bend).
                // Airwalk / superman: the feet are off the board, it hangs from the hands.
                Grab gb = grab != Grab.None ? grab : grabThisAir;
                Vector3 anchor = GrabPose.IsBody(gb) ? GrabPose.BodyAnchor(rag, gb, hipPos, YawDir(bodyYaw))
                                                     : hipPos - Vector3.up * (hipToBoard * 0.66f);
                pos = Vector3.Lerp(pos, anchor, grabBlend);
                Vector3 grabWorld = pos + baseRot * (offset + local * grabLocal);
                pos += GrabPose.MeetOffset(rag, grabHand, grabWorld, grabBlend);
            }
            // frontflip / backflip: the board is welded to the real feet (and lets go smoothly after)
            saltoWeld = Mathf.MoveTowards(saltoWeld, salto != 0 ? 1f : 0f, dt * 6f);
            tuckBlend = Mathf.MoveTowards(tuckBlend, salto != 0 && airborne ? 1f : 0f, dt * 8f); // ball up fast, open up as it ends
            if (saltoWeld > 0f && SaltoAssist.WeldBoard(rag, SkatePlugin.GoofyStance.Value, SaltoRotation() * YawDir(bodyYaw), feetOffset, out Vector3 wp, out Quaternion wr))
            {
                pos = Vector3.Lerp(pos, wp, saltoWeld);
                baseRot = Quaternion.Slerp(baseRot, wr, saltoWeld);
            }

            board.Place(pos, baseRot, local, offset);
        }

        internal static Vector3 FeetPos(PlayerRagdoll rag, Vector3 hipPos)
        {
            var fl = rag.GetBodypart(BodypartType.Foot_L);
            var fr = rag.GetBodypart(BodypartType.Foot_R);
            if (fl && fr) return (fl.transform.position + fr.transform.position) * 0.5f;
            return hipPos - Vector3.up * 0.9f;
        }

        // The camera records everything it hears right where it is - and the board is right there under you.
        // So the board goes quiet while you talk (and a bit quieter while you hold a camera), or your voice gets lost.
        private float voiceDuck;
        private float SoundMul
        {
            get
            {
                float duck = Mathf.Lerp(1f, SkatePlugin.TalkDuck.Value, voiceDuck);
                bool camera = player != null && player.data.currentItem != null && player.data.currentItem.GetComponent<VideoCamera>() != null;
                return duck * (camera ? 0.6f : 1f);
            }
        }

        private void UpdateAudio(float dt)
        {
            float mic = player.data.microphoneValue;
            voiceDuck = mic > 0.04f ? Mathf.MoveTowards(voiceDuck, 1f, dt * 8f)   // talking: down fast
                                    : Mathf.MoveTowards(voiceDuck, 0f, dt * 1.2f); // quiet again: back up slowly (between words it stays down)
            float vol = SkatePlugin.Volume.Value * SoundMul;
            float target = 0f;
            bool down = !bailed && !player.Ragdoll();
            if (!airborne && !grinding && down)
                target = Mathf.Clamp01(speed / 10f) * 0.45f * (sliding ? 1.4f : 1f);
            rollVol = Mathf.MoveTowards(rollVol, target, dt * 3f);
            rollSrc.volume = rollVol * vol * BoardUpgrades.RollVolumeMul; // soft wheels are quieter
            rollSrc.pitch = 0.75f + speed / 18f + (sliding ? 0.3f : 0f);

            grindVol = Mathf.MoveTowards(grindVol, grinding && down ? 0.35f : 0f, dt * 6f);
            grindSrc.volume = grindVol * vol;
            grindSrc.pitch = 0.85f + speed / 25f;

            // the rocket roars up fast and dies down a bit slower
            rocketVol = Mathf.MoveTowards(rocketVol, rocketOn && down ? 1f : 0f, dt * (rocketOn ? 8f : 3f));
            rocketSrc.volume = rocketVol * 0.55f * vol;
            rocketSrc.pitch = 0.85f + Mathf.Min(speed, 40f) / 80f;
        }

        private void UpdateGlow(float dt)
        {
            // A soft halo around the body instead of a light: a light lit up a big coloured circle
            // on the floor around you, which looked like a huge shadow in the dark.
            if (!SkatePlugin.AuraGlow.Value || Perf.Low) // (the low-end PC mode has no glow)
            {
                if (halo) halo.Hide();
                return;
            }
            if (halo == null) halo = AuraHalo.Create(localPlayer: true);
            long aura = Progress.Aura;
            float target = Aura.RankGlow(aura) * 0.35f;
            if (combo.Active) target += 0.2f + Mathf.Min(combo.Count, 10) * 0.05f;
            if (bailed) target = 0f;
            Color c = combo.Active && combo.Count >= 5 ? Color.HSVToRGB(Time.time * 0.6f % 1f, 0.5f, 1f) : Aura.RankColor(aura);
            halo.Set(player.refs.ragdoll.GetBodypart(BodypartType.Torso).transform.position, c, target, dt);
        }

        private void Play(AudioClip clip, float vol)
        {
            if (clip && sfxSrc) sfxSrc.PlayOneShot(clip, vol * SkatePlugin.Volume.Value * SoundMul);
        }

        // ---------------------------------------------------------------- helpers

        private Rigidbody Hip() => player.refs.ragdoll.GetBodypart(BodypartType.Hip).rig;

        private float CamYaw()
        {
            Vector3 l = player.data.lookDirection;
            l.y = 0;
            if (l.sqrMagnitude < 1e-4f) return heading;
            return Mathf.Atan2(l.x, l.z) * Mathf.Rad2Deg;
        }

        private static float Yaw(Vector3 v) => Mathf.Atan2(v.x, v.z) * Mathf.Rad2Deg;

        internal static Vector3 YawDir(float yaw) => Quaternion.Euler(0, yaw, 0) * Vector3.forward;
    }
}