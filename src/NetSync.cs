using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using PhotonHashtable = ExitGames.Client.Photon.Hashtable;

namespace CWSkate
{
    // Lets players who also have the mod see your board and your flips.
    // Vanilla players simply ignore all of this.
    internal static class NetSync
    {
        public const string RidingKey = "cwskate";
        private const byte EvFlip = 177;
        private const byte EvBail = 178;
        private const byte EvGrab = 181;
        private const byte EvSkin = 182;
        private const byte EvSwing = 183;
        private const byte EvActivity = 184;
        private const byte EvMock = 185;
        private const byte EvThrow = 186;
        private const byte EvSalto = 187;
        private const byte EvSnap = 188;   // our board broke in half
        private const byte EvHippie = 189;
        private const byte EvSkitch = 190;
        private const byte EvGrindFx = 191;
        private const byte EvRocket = 192;
        private const byte EvTotem = 193;
        private const byte EvBoom = 194;
        private const byte EvPush = 195; // keep the last one last: OnEvent checks the range
        public const string AuraKey = "cwskate_aura";
        private static long lastAuraSent = long.MinValue;
        private static float auraTimer;

        private static LoadBalancingClient subscribed;

        private static bool Online => PhotonNetwork.InRoom && !PhotonNetwork.OfflineMode;

        public const int StateNone = 0, StateBack = 1, StateRiding = 2;
        private static int lastSent = -1;

        // 0 = no board, 1 = board on the back, 2 = riding
        public static void SetState(int state, bool force = false)
        {
            if (state == lastSent && !force) return;
            try
            {
                if (PhotonNetwork.InRoom && PhotonNetwork.LocalPlayer != null)
                {
                    PhotonNetwork.LocalPlayer.SetCustomProperties(new PhotonHashtable { { RidingKey, state } });
                    lastSent = state;
                }
            }
            catch (System.Exception e) { SkatePlugin.Log.LogWarning("SetState failed: " + e.Message); }
        }

        public static int GetState(Player p)
        {
            try
            {
                var owner = p.refs.view != null ? p.refs.view.Owner : null;
                if (owner == null || owner.CustomProperties == null) return StateNone;
                if (!owner.CustomProperties.TryGetValue(RidingKey, out object v)) return StateNone;
                if (v is int i) return i;
                if (v is byte b) return b;
                return StateNone;
            }
            catch { return StateNone; }
        }

        public static void SendFlip(Player p, Flip flip, int count)
        {
            Send(EvFlip, new object[] { p.refs.view.ViewID, (byte)flip, (byte)count });
        }

        public static void SendSnap(Player p, Vector3 pos, Quaternion rot, Vector3 vel, float scale)
        {
            Send(EvSnap, new object[] { p.refs.view.ViewID, pos, rot, vel, scale });
        }

        // skitching: who we hold on to (-1 = let go)
        // we started / stopped grinding (for the sparks on their screen)
        public static void SendGrindFx(Player p, bool on, bool slide)
        {
            if (p == null || p.refs.view == null) return;
            Send(EvGrindFx, new object[] { p.refs.view.ViewID, on, slide });
        }

        // train = both hands on the shoulders of the one in front
        // something blew up (fuel, a rocket): everyone shows it and takes their own hit
        public static void SendBoom(Vector3 at, float power)
        {
            Send(EvBoom, new object[] { at, power });
        }

        // we pushed off the floor (their screen shows the back foot kicking)
        public static void SendPush(Player p, float duration)
        {
            if (p == null || p.refs.view == null) return;
            Send(EvPush, new object[] { p.refs.view.ViewID, duration });
        }

        // our insurance policy just saved us from dying
        public static void SendTotem(Player p)
        {
            if (p == null || p.refs.view == null) return;
            Send(EvTotem, new object[] { p.refs.view.ViewID });
        }

        // our rocket fired up / went out
        public static void SendRocket(Player p, bool on)
        {
            if (p == null || p.refs.view == null) return;
            Send(EvRocket, new object[] { p.refs.view.ViewID, on });
        }

        public static void SendSkitch(Player p, int targetViewID, bool rightHand, bool train)
        {
            Send(EvSkitch, new object[] { p.refs.view.ViewID, targetViewID, rightHand, train });
        }

        // hippie jump: the board rolls on from here by itself
        public static void SendHippie(Player p, Vector3 boardPos, float yaw, float speed)
        {
            Send(EvHippie, new object[] { p.refs.view.ViewID, boardPos, yaw, speed });
        }

        // frontflip (+1) / backflip (-1), how many turns, how fast
        public static void SendSalto(Player p, int dir, int count, float speed)
        {
            Send(EvSalto, new object[] { p.refs.view.ViewID, dir, count, speed });
        }

        // dropped = the board left their inventory and flies as a normal item (don't draw our own copy)
        public static void SendBail(Player p, bool dropped)
        {
            Send(EvBail, new object[] { p.refs.view.ViewID, dropped });
        }

        // Our aura glow for other players: combo size while riding, -1 = no glow. Sent when it changes.
        public const string GlowKey = "cwskate_glow";
        private static int lastGlowSent = int.MinValue;
        private static float glowTimer;

        public static void SetGlow(int value)
        {
            glowTimer -= Time.unscaledDeltaTime;
            if (value == lastGlowSent || glowTimer > 0f) return;
            glowTimer = 0.25f;
            try
            {
                if (!PhotonNetwork.InRoom || PhotonNetwork.LocalPlayer == null) { lastGlowSent = int.MinValue; return; }
                PhotonNetwork.LocalPlayer.SetCustomProperties(new PhotonHashtable { { GlowKey, value } });
                lastGlowSent = value;
            }
            catch { }
        }

        public static int GetGlow(Player p)
        {
            try
            {
                var owner = p.refs.view != null ? p.refs.view.Owner : null;
                if (owner == null || owner.CustomProperties == null) return -1;
                if (owner.CustomProperties.TryGetValue(GlowKey, out object v) && v is int i) return i;
                return -1;
            }
            catch { return -1; }
        }

        // The parts on our board (wheels, deck) for other players. Sent when they change.
        public const string UpgradesKey = "cwskate_upg";
        private static int lastUpgradesSent = int.MinValue;

        public static void SetUpgrades(int flags)
        {
            if (flags == lastUpgradesSent) return;
            try
            {
                if (!PhotonNetwork.InRoom || PhotonNetwork.LocalPlayer == null) { lastUpgradesSent = int.MinValue; return; } // (sent again in the next room)
                PhotonNetwork.LocalPlayer.SetCustomProperties(new PhotonHashtable { { UpgradesKey, flags } });
                lastUpgradesSent = flags;
            }
            catch { }
        }

        public static int GetUpgrades(Player p)
        {
            try
            {
                var owner = p.refs.view != null ? p.refs.view.Owner : null;
                if (owner == null || owner.CustomProperties == null) return 0;
                return owner.CustomProperties.TryGetValue(UpgradesKey, out object v) && v is int i ? i : 0;
            }
            catch { return 0; }
        }

        // we threw the board ahead of us
        public static void SendThrow(Player p, Vector3 velocity)
        {
            Send(EvThrow, new object[] { p.refs.view.ViewID, velocity });
        }

        // AURA GOD smack: everyone sees the mockery over that monster
        public static void SendMock(int monsterViewID)
        {
            Send(EvMock, new object[] { monsterViewID });
        }

        // a skate moment (for other players' cameras)
        public static void SendActivity(SkateAct kind, int combo, string trick, float power)
        {
            Send(EvActivity, new object[] { (byte)kind, combo, trick ?? "", power });
        }

        // we swung the board: others play the swing on the board in our hand
        public static void SendSwing(Player p)
        {
            Send(EvSwing, new object[] { p.refs.view.ViewID });
        }

        public static void SendGrab(Player p, Grab g)
        {
            Send(EvGrab, new object[] { p.refs.view.ViewID, (byte)g });
        }


        public static void SendSkin(byte[] graphic, byte[] grip, int[] targets)
        {
            try
            {
                if (!Online || PhotonNetwork.CurrentRoom.PlayerCount < 2) return;
                var opt = new RaiseEventOptions { Receivers = ReceiverGroup.Others };
                if (targets != null && targets.Length > 0) opt.TargetActors = targets;
                PhotonNetwork.RaiseEvent(EvSkin, new object[] { graphic, grip }, opt, SendOptions.SendReliable);
            }
            catch (System.Exception e) { SkatePlugin.Log.LogWarning("Skin send failed: " + e.Message); }
        }

        // Our aura for the rank above our head (-1 = hidden). Sent at most once a second.
        public static void TickAura()
        {
            auraTimer -= Time.unscaledDeltaTime;
            if (auraTimer > 0f) return;
            auraTimer = 1f;
            try
            {
                if (!PhotonNetwork.InRoom || PhotonNetwork.LocalPlayer == null) { lastAuraSent = long.MinValue; return; }
                long a = SkatePlugin.ShareRank.Value ? System.Math.Max(0L, Progress.Aura) : -1L;
                if (a == lastAuraSent) return;
                PhotonNetwork.LocalPlayer.SetCustomProperties(new PhotonHashtable { { AuraKey, a } });
                lastAuraSent = a;
            }
            catch { }
        }

        // -1 = not shown (no mod, no board or hidden)
        public static long GetAura(Player p)
        {
            try
            {
                var owner = p.refs.view != null ? p.refs.view.Owner : null;
                if (owner == null || owner.CustomProperties == null) return -1;
                if (!owner.CustomProperties.TryGetValue(AuraKey, out object v)) return -1;
                if (v is long l) return l;
                if (v is int i) return i;
                return -1;
            }
            catch { return -1; }
        }

        private static void Send(byte code, object[] data)
        {
            try
            {
                if (!Online || PhotonNetwork.CurrentRoom.PlayerCount < 2) return;
                PhotonNetwork.RaiseEvent(code, data, new RaiseEventOptions { Receivers = ReceiverGroup.Others }, SendOptions.SendReliable);
            }
            catch (System.Exception e) { SkatePlugin.Log.LogWarning("RaiseEvent failed: " + e.Message); }
        }

        // Called every frame by the manager; (re)subscribes when Photon swaps its client.
        public static void EnsureSubscribed()
        {
            var client = PhotonNetwork.NetworkingClient;
            if (client == subscribed) return;
            if (subscribed != null) subscribed.EventReceived -= OnEvent;
            if (client != null) client.EventReceived += OnEvent;
            subscribed = client;
        }

        // Other mods use event numbers in this range too (e.g. a tripod mod), so everything is checked
        // before it's used: the right types, and news about a player only from that player.
        private static void OnEvent(EventData e)
        {
            if (e.Code < EvFlip || e.Code > EvPush) return;
            try
            {
                if (!(e.CustomData is object[] data) || data.Length < 1) return;
                if (e.Code == EvMock)
                {
                    if (data.Length == 1 && data[0] is int mv) Mockery.StartFromNetwork(mv);
                    return;
                }
                if (e.Code == EvActivity)
                {
                    if (data.Length >= 3 && data[0] is byte k && data[1] is int c && data[2] is string tr && k <= (byte)SkateAct.Kaboom)
                        SkateActivity.Record(e.Sender, (SkateAct)k, c, tr, data.Length > 3 && data[3] is float pw ? pw : 1f);
                    return;
                }
                if (e.Code == EvBoom)
                {
                    if (data.Length >= 2 && data[0] is Vector3 bp && data[1] is float bpow) Boom.Effects(bp, Mathf.Clamp(bpow, 0.3f, 3f));
                    return;
                }
                if (e.Code == EvSkin)
                {
                    if (data.Length >= 2 && data[0] is byte[] gfx && data[1] is byte[] grp)
                        Skins.OnRemoteSkin(e.Sender, gfx, grp);
                    return;
                }

                if (!(data[0] is int viewID)) return;
                var view = PhotonView.Find(viewID);
                if (view == null || view.OwnerActorNr != e.Sender) return; // only the player themselves tells us what they do
                if (e.Code == EvSwing)
                {
                    var swinger = view.GetComponent<Player>();
                    var held = swinger != null && swinger.data.currentItem != null ? swinger.data.currentItem.GetComponent<SkateboardItemBehaviour>() : null;
                    if (held != null) held.PlaySwing();
                    return;
                }
                if (e.Code == EvTotem)
                {
                    SponsorItems.PlayTotemFx(view.GetComponent<Player>()); // their insurance saved them
                    return;
                }
                var rb = view.GetComponent<RemoteBoard>();
                if (rb == null) return;
                if (e.Code == EvFlip && data.Length >= 3 && data[1] is byte fl && data[2] is byte fc && fl <= (byte)Flip.Impossible) rb.OnFlip((Flip)fl, fc);
                else if (e.Code == EvBail && data.Length >= 2 && data[1] is bool dr) rb.OnBail(dr);
                else if (e.Code == EvGrab && data.Length >= 2 && data[1] is byte gb && gb <= (byte)Grab.Superman) rb.OnGrab((Grab)gb);
                else if (e.Code == EvThrow && data.Length >= 2 && data[1] is Vector3 tv) rb.OnThrow(tv);
                else if (e.Code == EvSnap && data.Length >= 5 && data[1] is Vector3 snp && data[2] is Quaternion snr && data[3] is Vector3 snv && data[4] is float sns) rb.OnSnap(snp, snr, snv, sns);
                else if (e.Code == EvSkitch && data.Length >= 3 && data[1] is int stv && data[2] is bool sright) rb.OnSkitch(stv, sright, data.Length > 3 && data[3] is bool strain && strain);
                else if (e.Code == EvGrindFx && data.Length >= 3 && data[1] is bool gon && data[2] is bool gslide) rb.OnGrindFx(gon, gslide);
                else if (e.Code == EvRocket && data.Length >= 2 && data[1] is bool ron) rb.OnRocket(ron);
                else if (e.Code == EvHippie && data.Length >= 4 && data[1] is Vector3 hp && data[2] is float hy && data[3] is float hs) rb.OnHippie(hp, hy, hs);
                else if (e.Code == EvSalto && data.Length >= 4 && data[1] is int sd && data[2] is int sc && data[3] is float ss) rb.OnSalto(sd, sc, ss);
                else if (e.Code == EvPush && data.Length >= 2 && data[1] is float pd) rb.OnPush(pd);
            }
            catch (System.Exception ex) { SkatePlugin.Log.LogWarning("Bad skate event " + e.Code + ": " + ex.Message); }
        }
    }

    // Board under another player (only if they also run the mod).
    internal class RemoteBoard : MonoBehaviour
    {
        private Player player;
        private BoardVisual board;
        private Flip flip;
        private int flipCount;
        private float flipProgress;
        private float yaw;
        private Vector3 up = Vector3.up;
        private float feetOffset = 0.12f;
        private float bailTimer;
        private float checkTimer;
        private int state;
        private Grab grab, lastGrab;
        private float grabBlend, groundedFor, supermanBlend, hipToBoard = 0.9f;
        private BodypartType grabHand;
        private Vector3 grabLocal;

        private void Awake() => player = GetComponent<Player>();

        private float pushStart, pushDur;
        public void OnPush(float duration)
        {
            pushStart = Time.time;
            pushDur = Mathf.Clamp(duration, 0.2f, 0.5f);
        }

        public void OnGrab(Grab g)
        {
            grab = g;
            if (g != Grab.None) GrabPose.Target(g, false, out grabHand, out grabLocal);
        }

        private void FixedUpdate()
        {
            // their flip: the body turns over as one piece here too
            if (saltoDir != 0 && player != null && state == NetSync.StateRiding && player.data.fallTime <= 0f && !player.data.dead)
            {
                Vector3 right = Vector3.Cross(Vector3.up, SkateController.YawDir(yaw + 90f)).normalized;
                SaltoAssist.Spin(player.refs.ragdoll, right, saltoDir * saltoSpeed, SaltoRotation());
            }
            if (grab == Grab.None || board == null || board.Flying || state != NetSync.StateRiding || player == null) return;
            if (player.data.fallTime > 0f) { grab = Grab.None; return; }
            GrabPose.ApplyForces(player.refs.ragdoll, grab, false, board.model, grabBlend);
        }

        // called right before their body copies the animation skeleton (see GrabPose)
        public void ApplyPoseIK()
        {
            if (board == null || board.Flying || state != NetSync.StateRiding || player == null) return;
            if (player.data.fallTime > 0f || player.data.dead) return;
            // dancing on their board: feet stay on the deck
            if (skitchTo != null)
            {
                if (skitchTrain && skitchPlayer != null) GrabPose.ApplyTrainHold(player.refs.ragdoll, skitchPlayer, SkateController.YawDir(yaw + 90f), 1f);
                else GrabPose.ApplyReach(player.refs.ragdoll, skitchRight ? BodypartType.Hand_R : BodypartType.Hand_L, skitchTo.position + Vector3.up * 0.25f, 1f);
            }
            if (onGround) // their feet on the deck (the back one kicks the floor when they push)
            {
                float kick = pushDur > 0f ? (Time.time - pushStart) / pushDur : -1f;
                GrabPose.ApplyFeetOnDeck(player.refs.ragdoll, false, board.model, 1f, kick < 1f ? kick : -1f);
                return;
            }
            if (tuckBlend > 0.01f) { GrabPose.ApplyTuck(player.refs.ragdoll, tuckBlend, lastSaltoDir < 0); return; } // their flip: tucked in a ball
            if (lastGrab == Grab.None || grabBlend <= 0.01f) return;
            GrabPose.ApplyIK(player.refs.ragdoll, lastGrab, false, board.model, grabBlend);
        }

        private AuraHalo halo;

        private void OnDestroy()
        {
            if (board) Destroy(board.gameObject);
            if (halo) Destroy(halo.gameObject);
            if (sparks) Destroy(sparks.gameObject); // (the trails live on the board, they go with it)
            if (rocketSrc) Destroy(rocketSrc.gameObject);
        }

        // their aura glow, the same way they see it themselves
        private void UpdateHalo(float dt)
        {
            int glow = state == NetSync.StateRiding && player.data.fallTime <= 0f && !player.data.dead ? NetSync.GetGlow(player) : -1;
            if (glow < 0 || Perf.Low) // (the low-end PC mode shows nobody's glow)
            {
                if (halo) halo.Hide();
                return;
            }
            if (halo == null) halo = AuraHalo.Create(localPlayer: false);
            long aura = System.Math.Max(0L, NetSync.GetAura(player));
            float target = Aura.RankGlow(aura) * 0.35f;
            if (glow > 0) target += 0.2f + Mathf.Min(glow, 10) * 0.05f;
            Color c = glow >= 5 ? Color.HSVToRGB(Time.time * 0.6f % 1f, 0.5f, 1f) : Aura.RankColor(aura);
            var torso = player.refs.ragdoll.GetBodypart(BodypartType.Torso);
            if (torso != null) halo.Set(torso.transform.position, c, target, dt);
        }

        // skitching: their hand holds on to someone
        private Transform skitchTo;
        private Player skitchPlayer;
        private bool skitchRight, skitchTrain;
        private float trainBlend;
        private Vector3 bodySlopeUp = Vector3.up;

        // holding on to someone right now (for a longer train behind them)
        public bool Holding => skitchTo != null;

        public void OnSkitch(int targetViewID, bool rightHand, bool train)
        {
            skitchRight = rightHand;
            skitchTrain = train;
            skitchTo = null;
            skitchPlayer = null;
            if (targetViewID < 0) return;
            var view = PhotonView.Find(targetViewID);
            var p = view != null ? view.GetComponent<Player>() : null;
            var hip = p != null && p.refs.ragdoll != null ? p.refs.ragdoll.GetBodypart(BodypartType.Hip) : null;
            if (hip != null) { skitchTo = hip.transform; skitchPlayer = p; }
        }

        // their board broke in half
        public void OnSnap(Vector3 pos, Quaternion rot, Vector3 vel, float scale)
        {
            int actor = player != null && player.refs.view != null ? player.refs.view.OwnerActorNr : 0;
            BrokenBoard.Spawn(pos, rot, vel, scale, (NetSync.GetUpgrades(player) & BoardUpgrades.ProBoard) != 0 ? Skins.RideToHell : Skins.ForActor(actor));
            if (board != null) { board.StopFling(); board.gameObject.SetActive(false); }
            state = NetSync.StateNone;
            checkTimer = 0.6f;
            hippieTimer = 0f;
        }

        // their hippie jump: the board rolls on by itself until they land back on it
        private float hippieTimer, hippieYaw, hippieSpeed;
        private Vector3 hippiePos, hippieUp = Vector3.up;

        public void OnHippie(Vector3 pos, float yaw, float speed)
        {
            hippiePos = pos;
            hippieYaw = yaw;
            hippieSpeed = speed;
            hippieUp = Vector3.up;
            hippieTimer = 3f;
        }

        private bool UpdateHippie(float dt)
        {
            if (hippieTimer <= 0f || board == null) return false;
            hippieTimer -= dt;
            // they're down again (on it, or on the floor next to it)
            if (hippieTimer < 2.7f && player.data.sinceGrounded < 0.1f) { hippieTimer = 0f; return false; }
            float bs = SkatePlugin.RideBoardScale.Value;
            hippieSpeed = Mathf.MoveTowards(hippieSpeed, 0f, SkatePlugin.RollFriction.Value * dt);
            Vector3 dir = SkateController.YawDir(hippieYaw);
            Vector3 next = hippiePos + dir * (hippieSpeed * dt);
            if (Physics.Raycast(next + Vector3.up * 0.3f, Vector3.down, out RaycastHit hit, 2.5f, HelperFunctions.GetMask(HelperFunctions.LayerType.TerrainProp), QueryTriggerInteraction.Ignore))
            {
                next.y = hit.point.y + BoardVisual.WheelBase * bs;
                hippieUp = Vector3.Slerp(hippieUp, hit.normal, dt * 12f);
            }
            else next.y -= 4f * dt;
            hippiePos = next;
            Vector3 fwd = Vector3.ProjectOnPlane(dir, hippieUp);
            if (fwd.sqrMagnitude < 1e-4f) fwd = dir;
            board.transform.localScale = Vector3.one * bs;
            board.Place(next, Quaternion.LookRotation(fwd.normalized, hippieUp), Quaternion.identity, Vector3.zero);
            return true;
        }

        // their frontflip / backflip, played at their speed
        private int saltoDir, saltoCount, lastSaltoDir;
        private bool onGround;

        // their sparks on rails and glowing wheel tracks
        private GrindSparks sparks;
        private WheelTrails trails;
        private bool remoteGrinding, remoteSlide;

        public void OnGrindFx(bool on, bool slide)
        {
            remoteGrinding = on;
            remoteSlide = slide;
        }

        // their rocket: fire and smoke on their board, and the roar where they are
        private bool remoteRocket;
        private AudioSource rocketSrc;
        private float rocketVol;

        public void OnRocket(bool on) => remoteRocket = on;

        private void UpdateRocket(bool rolling, float flatSpeed, float dt)
        {
            bool on = remoteRocket && rolling;
            if (board != null) board.SetRocketFlame(on);
            if (rocketSrc == null)
            {
                if (!on) return;
                // its own little object that follows their hips (never move the player object itself)
                var go = new GameObject("CWSkate_RemoteRocketAudio");
                rocketSrc = go.AddComponent<AudioSource>();
                rocketSrc.clip = SkateSounds.Rocket;
                rocketSrc.loop = true;
                rocketSrc.spatialBlend = 1f; // heard from where they are
                rocketSrc.rolloffMode = AudioRolloffMode.Linear;
                rocketSrc.minDistance = 3f;
                rocketSrc.maxDistance = 45f;
                rocketSrc.playOnAwake = false;
                rocketSrc.volume = 0f;
                rocketSrc.Play();
            }
            rocketVol = Mathf.MoveTowards(rocketVol, on ? 1f : 0f, dt * (on ? 8f : 3f));
            rocketSrc.volume = rocketVol * 0.7f * SkatePlugin.Volume.Value;
            rocketSrc.pitch = 0.85f + Mathf.Min(flatSpeed, 40f) / 80f;
            var hip = player.refs.ragdoll.GetBodypart(BodypartType.Hip);
            if (hip != null) rocketSrc.transform.position = hip.transform.position;
        }

        private void UpdateFx(bool riding, float flatSpeed, float dt)
        {
            if (board == null) return;
            if (trails == null) trails = WheelTrails.Attach(board);
            if (sparks == null) sparks = GrindSparks.Create();
            long aura = System.Math.Max(0L, NetSync.GetAura(player));
            bool trailOn = SkatePlugin.ShowWheelTrails.Value && riding && onGround && !remoteGrinding && hippieTimer <= 0f && flatSpeed > 2f;
            trails.Tick(trailOn, Aura.RankColor(aura), dt, fire: (NetSync.GetUpgrades(player) & BoardUpgrades.ProBoard) != 0);
            bool sparkOn = SkatePlugin.ShowGrindSparks.Value && riding && remoteGrinding;
            sparks.Set(sparkOn, board.transform.position - Vector3.up * 0.05f, -SkateController.YawDir(yaw),
                (remoteSlide ? 25f : 70f) * Mathf.Clamp(flatSpeed / 6f, 0.4f, 1.6f));
        }
        private float saltoProgress, saltoSpeed = 600f, saltoWeld, tuckBlend;

        public void OnSalto(int dir, int count, float speed)
        {
            if (dir != saltoDir) saltoProgress = 0f;
            saltoDir = Mathf.Clamp(dir, -1, 1);
            lastSaltoDir = saltoDir;
            saltoCount = Mathf.Clamp(count, 1, 3);
            saltoSpeed = Mathf.Clamp(speed, 100f, 2000f);
        }

        private Quaternion SaltoRotation()
        {
            if (saltoDir == 0) return Quaternion.identity;
            Vector3 right = Vector3.Cross(Vector3.up, SkateController.YawDir(yaw + 90f)).normalized;
            return Quaternion.AngleAxis(saltoDir * saltoProgress * 360f, right);
        }

        public void OnFlip(Flip f, int count)
        {
            if (f != flip) flipProgress = 0;
            flip = f;
            flipCount = Mathf.Max(1, count);
        }

        // they threw their board ahead of them: it flies until they hop on (or a few seconds pass)
        private float thrownTimer, rollSpeed;
        private Vector3 rollDir;
        private bool rollLanded;

        public void OnThrow(Vector3 vel)
        {
            if (board == null) return;
            board.StopFling();
            board.gameObject.SetActive(true);
            board.transform.localScale = Vector3.one;
            Vector3 dir = new Vector3(vel.x, 0f, vel.z);
            if (dir.sqrMagnitude < 0.01f) dir = player.data.lookDirection;
            board.transform.SetPositionAndRotation(player.refs.ragdoll.GetBodypart(BodypartType.Hip).transform.position + dir.normalized * 0.6f, Quaternion.LookRotation(dir.normalized, Vector3.up));
            board.Fling(vel, 0f, slippery: true);
            if (board.FlyRig != null) board.FlyRig.constraints = RigidbodyConstraints.FreezeRotation;
            rollDir = dir.normalized;
            rollSpeed = new Vector3(vel.x, 0f, vel.z).magnitude;
            rollLanded = false;
            thrownTimer = 12f;
        }

        public void OnBail(bool dropped)
        {
            hippieTimer = 0f;
            remoteRocket = false;
            skitchTo = null;
            if (board == null || board.Flying) return;
            if (dropped)
            {
                // their board is a real item flying around now
                board.gameObject.SetActive(false);
                state = NetSync.StateNone;
                checkTimer = 0.6f;
                flip = Flip.None;
                grab = Grab.None;
                return;
            }
            var hip = player.refs.ragdoll.GetBodypart(BodypartType.Hip).rig;
            Vector3 v = hip.linearVelocity;
            board.Fling(new Vector3(v.x, 0, v.z) * 1.1f + SkateController.YawDir(yaw) * 2f);
            bailTimer = 1f;
            flip = Flip.None;
            saltoDir = 0;
        }

        private void LateUpdate()
        {
            if (player == null || !player.data.playerSetUpAndReady) return;
            checkTimer -= Time.deltaTime;
            if (checkTimer <= 0f)
            {
                checkTimer = 0.25f;
                state = NetSync.GetState(player);
            }
            UpdateHalo(Time.deltaTime);
            if (state != NetSync.StateRiding) skitchTo = null; // (in case the "let go" message got lost)
            if (state == NetSync.StateNone)
            {
                if (board) { board.StopFling(); board.gameObject.SetActive(false); }
                if (sparks) sparks.Set(false, Vector3.zero, Vector3.zero, 0f);
                remoteRocket = false;
                if (rocketSrc) rocketSrc.volume = 0f;
                return;
            }
            if (board == null)
                board = BoardVisual.Create("CWSkate_RemoteBoard");
            board.gameObject.SetActive(true);
            int upg = NetSync.GetUpgrades(player);
            if ((upg & BoardUpgrades.ProBoard) != 0) board.ApplySkin(Skins.RideToHell);
            else if (player.refs.view != null) board.ApplySkin(Skins.ForActor(player.refs.view.OwnerActorNr));
            board.ApplyUpgrades(upg);

            float dt = Time.deltaTime;
            {
                var hipBp = player.refs.ragdoll.GetBodypart(BodypartType.Hip);
                Vector3 hv = hipBp != null && hipBp.rig != null ? hipBp.rig.linearVelocity : Vector3.zero;
                hv.y = 0f;
                bool rolling = state == NetSync.StateRiding && !board.Flying && !player.data.dead && player.data.fallTime <= 0f;
                UpdateFx(rolling, hv.magnitude, dt);
                UpdateRocket(rolling, hv.magnitude, dt);
            }
            if (board.Flying && thrownTimer > 0f)
            {
                thrownTimer -= dt;
                // rolls the same way it does for the thrower
                Vector3 bp = board.transform.position;
                if (!rollLanded && thrownTimer < 11.85f && Physics.Raycast(bp + Vector3.up * 0.1f, Vector3.down, 0.35f, HelperFunctions.GetMask(HelperFunctions.LayerType.TerrainProp), QueryTriggerInteraction.Ignore))
                    rollLanded = true;
                if (rollLanded && board.FlyRig != null)
                {
                    rollSpeed = Mathf.MoveTowards(rollSpeed, 0f, 1.2f * dt);
                    board.FlyRig.linearVelocity = rollDir * rollSpeed + Vector3.up * Mathf.Min(board.FlyRig.linearVelocity.y, 0f);
                }
                if (state == NetSync.StateRiding || thrownTimer <= 0f) { thrownTimer = 0f; board.StopFling(); }
                else return;
            }
            if (board.Flying)
            {
                if (player.data.fallTime > 0f) bailTimer = 0.5f;
                else bailTimer -= dt;
                if (bailTimer <= 0f) board.StopFling();
                else return;
            }
            if (state != NetSync.StateRiding || player.data.dead)
            {
                board.PlaceOnBack(player);
                return;
            }

            if (UpdateHippie(dt)) return; // the board is rolling on its own under them
            board.transform.localScale = Vector3.one * SkatePlugin.RideBoardScale.Value;
            var rag = player.refs.ragdoll;
            Transform hipT = rag.GetBodypart(BodypartType.Hip).transform;
            Vector3 hipPos = hipT.position;
            Vector3 feet = SkateController.FeetPos(rag, hipPos);
            Vector3 vel = rag.GetBodypart(BodypartType.Hip).rig.linearVelocity;
            vel.y = 0;
            // board points where they travel; when standing still use where they look
            // (not the hip, which we rotate sideways ourselves)
            Vector3 look = player.data.lookDirection;
            Vector3 face = vel.magnitude > 1.5f ? vel : new Vector3(look.x, 0, look.z);
            if (face.sqrMagnitude > 1e-4f)
                yaw = Mathf.MoveTowardsAngle(yaw, Mathf.Atan2(face.x, face.z) * Mathf.Rad2Deg, 540f * dt);

            bool grounded = player.data.sinceGrounded < 0.1f;
            onGround = grounded;
            Vector3 pos;
            Vector3 targetUp = Vector3.up;
            if (grounded && Physics.Raycast(hipPos, Vector3.down, out RaycastHit hit, 2.2f,
                    HelperFunctions.GetMask(HelperFunctions.LayerType.TerrainProp), QueryTriggerInteraction.Ignore))
            {
                pos = hit.point + hit.normal * (BoardVisual.WheelBase * SkatePlugin.RideBoardScale.Value);
                targetUp = hit.normal;
                feetOffset = Mathf.Clamp(Mathf.Lerp(feetOffset, feet.y - pos.y, dt * 5f), 0.02f, 0.4f);
            }
            else pos = feet - Vector3.up * feetOffset;
            up = Vector3.Slerp(up, targetUp, dt * 12f);

            Quaternion local = Quaternion.identity;
            Vector3 offset = Vector3.zero;
            if (flip != Flip.None)
            {
                flipProgress += dt / Tricks.FlipDuration(flip);
                if (flipProgress >= flipCount || grounded && flipProgress > 0.3f) { flip = Flip.None; flipProgress = 0; }
                else
                {
                    local = Tricks.FlipRotation(flip, flipProgress);
                    offset.y = Mathf.Sin(Mathf.PI * Mathf.Clamp01(flipProgress / flipCount)) * 0.16f;
                }
            }
            Vector3 fwd = Vector3.ProjectOnPlane(SkateController.YawDir(yaw), up);
            if (fwd.sqrMagnitude < 1e-4f) fwd = SkateController.YawDir(yaw);
            Quaternion baseRot = Quaternion.LookRotation(fwd.normalized, up);

            groundedFor = grounded ? groundedFor + dt : 0f;
            if (groundedFor > 0.3f) grab = Grab.None; // in case the release event got lost
            if (grab != Grab.None) lastGrab = grab;
            grabBlend = Mathf.MoveTowards(grabBlend, grab != Grab.None ? 1f : 0f, dt * 9f);
            if (saltoDir != 0)
            {
                saltoProgress += dt * saltoSpeed / 360f;
                if (saltoProgress >= saltoCount || (grounded && saltoProgress > 0.3f)) { saltoDir = 0; saltoProgress = 0f; }
            }
            if (grabBlend <= 0.01f && saltoDir == 0) hipToBoard = Mathf.Clamp(hipPos.y - pos.y, 0.3f, 1.6f);
            if (grabBlend > 0f)
            {
                local = Quaternion.Slerp(Quaternion.identity, Tricks.GrabRotation(lastGrab), grabBlend) * local;
                // the feet follow the board in a grab, so the board hangs off the hips (or the hands)
                Vector3 anchor = GrabPose.IsBody(lastGrab) ? GrabPose.BodyAnchor(rag, lastGrab, hipPos, SkateController.YawDir(yaw))
                                                           : hipPos - Vector3.up * (hipToBoard * 0.66f);
                pos = Vector3.Lerp(pos, anchor, grabBlend);
                pos += GrabPose.MeetOffset(rag, grabHand, pos + baseRot * (offset + local * grabLocal), grabBlend);
            }
            Quaternion sq = SaltoRotation();
            // their flip: the board is welded to their feet
            saltoWeld = Mathf.MoveTowards(saltoWeld, saltoDir != 0 ? 1f : 0f, dt * 6f);
            tuckBlend = Mathf.MoveTowards(tuckBlend, saltoDir != 0 && !grounded ? 1f : 0f, dt * 8f);
            if (saltoWeld > 0f && SaltoAssist.WeldBoard(rag, false, sq * SkateController.YawDir(yaw), feetOffset, out Vector3 wp, out Quaternion wr))
            {
                pos = Vector3.Lerp(pos, wp, saltoWeld);
                baseRot = Quaternion.Slerp(baseRot, wr, saltoWeld);
            }
            board.Place(pos, baseRot, local, offset);

            // stand sideways on the board on our screen too (lying flat for a superman)
            supermanBlend = Mathf.MoveTowards(supermanBlend, grab == Grab.Superman && !grounded ? 1f : 0f, dt * 6f);
            if (player.data.fallTime <= 0f)
            {
                // (in a skate train they face forward)
                trainBlend = Mathf.MoveTowards(trainBlend, skitchTrain && skitchTo != null ? 1f : 0f, dt * 6f);
                // leaning with the board on slopes, same as on their screen
                Vector3 slopeTarget = grounded ? Vector3.SlerpUnclamped(Vector3.up, up, SkatePlugin.BodySlopeLean.Value) : Vector3.up;
                if (Vector3.Angle(Vector3.up, slopeTarget) > 45f) slopeTarget = Vector3.RotateTowards(Vector3.up, slopeTarget, 45f * Mathf.Deg2Rad, 0f);
                bodySlopeUp = Vector3.Slerp(bodySlopeUp, slopeTarget, dt * 8f);
                player.data.rotationOvveride = Quaternion.FromToRotation(Vector3.up, bodySlopeUp) * sq * GrabPose.SupermanRotation(Quaternion.LookRotation(SkateController.YawDir(yaw + 90f * (1f - trainBlend)), Vector3.up), SkateController.YawDir(yaw), supermanBlend);
                player.data.rotationOvverideStr = 2f;
            }
        }
    }
}
