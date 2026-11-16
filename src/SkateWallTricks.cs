using UnityEngine;

namespace CWSkate
{
    // Wallride, wallplant, drop-in and the board throw.
    internal partial class SkateController
    {
        // ---------------------------------------------------------------- shared

        private static int wallMask = -1;
        private static int WallMask => wallMask != -1 ? wallMask : wallMask = LayerMask.GetMask("Terrain", "Prop", "Default");

        // a wall-ish surface (not floor, not ceiling) that isn't a person or a loose object
        private static bool IsWall(RaycastHit h)
        {
            if (h.collider == null || Mathf.Abs(h.normal.y) > 0.35f) return false;
            if (h.collider.GetComponentInParent<Player>() != null) return false;
            var rb = h.rigidbody;
            return rb == null || rb.isKinematic;
        }

        private float wallTrickCooldown;

        // ---------------------------------------------------------------- wallride

        private bool wallriding;
        private Vector3 wallNormal, wallAlong, wallPoint;
        private float wallTime, wallVy, wallLost;
        public bool Wallriding => wallriding;
        private const float WallrideMaxTime = 2.6f;
        private const float WallrideMinInto = 0.2f;    // at least ~12 degrees towards the wall
        private const float WallGravity = 0.14f;       // share of the air gravity pulling you down the wall

        // Ollie into a wall at an angle (not head-on) and you ride along it for a moment.
        // (also called when you'd otherwise crash into a wall in the air: a glancing hit becomes a wallride)
        private static readonly float[] WallRayAngles = { 90f, 60f, 35f, 15f }; // from straight ahead, to each side
        private bool TryStartWallride(Rigidbody hip, Vector3 v, float minSpeed = 3f)
        {
            if (!SkatePlugin.WallridesEnabled.Value || wallTrickCooldown > 0f || airTime < 0.03f) return false;
            if (grab != Grab.None) return false;
            Vector3 flat = new Vector3(airVel.x, 0f, airVel.z);
            if (flat.magnitude < minSpeed) return false;
            Vector3 dir = flat.normalized;
            float reach = 1.0f + flat.magnitude * 0.04f; // faster = look further ahead
            foreach (float h0 in new[] { 0.45f, 0.1f })   // knees, then waist
            {
                Vector3 origin = hip.position - Vector3.up * h0;
                foreach (float a in WallRayAngles)
                {
                    foreach (float side in new[] { 1f, -1f })
                    {
                        Vector3 ray = Quaternion.AngleAxis(a * side, Vector3.up) * dir;
                        if (!Physics.Raycast(origin, ray, out RaycastHit h, reach, WallMask, QueryTriggerInteraction.Ignore) || !IsWall(h)) continue;
                        Vector3 n = new Vector3(h.normal.x, 0f, h.normal.z).normalized;
                        float into = Vector3.Dot(dir, -n); // 0 = flying alongside it, 1 = straight at it
                        // riding along it, not flying straight into it (head-on = crash or wallplant)
                        if (into > 0.85f) continue;
                        // and you have to be going INTO it: a wall you just fly past next to you isn't a wallride
                        if (into < WallrideMinInto) continue;
                        StartWallride(n, h.point, flat.magnitude, v.y);
                        return true;
                    }
                }
            }
            return false;
        }

        private void StartWallride(Vector3 n, Vector3 point, float spd, float vy)
        {
            if (flip != Flip.None) flipProgress = flipCount; // the flip finishes as the wheels hit the wall
            ScoreAir(); // tricks done on the way in still count
            wallriding = true;
            airborne = false;
            wallNormal = n;
            wallPoint = point;
            wallTime = 0f;
            wallLost = 0f;
            wallVy = Mathf.Max(vy, 2f + spd * 0.12f); // the wheels catch the wall and carry you up a bit (faster = higher)
            wallAlong = Vector3.ProjectOnPlane(new Vector3(airVel.x, 0f, airVel.z), n).normalized;
            speed = Mathf.Max(spd * 0.95f, 4f);
            heading = Yaw(wallAlong);
            Play(SkateSounds.Land, 0.6f);
            combo.KeepAlive();
        }

        private void WallFixed(Rigidbody hip, Vector3 v, float comp, float dt)
        {
            var d = player.data;
            wallTime += dt;
            combo.KeepAlive();
            d.movementVector = Vector3.zero;

            if (pendingOllie)
            {
                pendingOllie = false;
                EndWallride(pop: true, hip);
                return;
            }
            // still a wall next to us? (knees, then waist: a short gap or a frame in the wall doesn't end it)
            Vector3 origin = hip.position - Vector3.up * 0.45f;
            bool wall = Physics.Raycast(origin, -wallNormal, out RaycastHit h, 1.4f, WallMask, QueryTriggerInteraction.Ignore) && IsWall(h);
            if (!wall) wall = Physics.Raycast(hip.position - Vector3.up * 0.1f, -wallNormal, out h, 1.4f, WallMask, QueryTriggerInteraction.Ignore) && IsWall(h);
            wallLost = wall ? 0f : wallLost + dt;
            player.refs.ragdoll.GroundRaycast();
            bool grounded = d.isGrounded && wallVy <= 0f; // (not while still going up the wall)
            if (wallLost > 0.15f || wallTime > WallrideMaxTime || speed < 1.5f || grounded)
            {
                EndWallride(pop: false, hip);
                return;
            }
            if (wall)
            {
                wallPoint = h.point;
                wallNormal = Vector3.Lerp(wallNormal, new Vector3(h.normal.x, 0f, h.normal.z).normalized, 0.3f).normalized;
                wallAlong = Vector3.ProjectOnPlane(wallAlong, wallNormal).normalized;
            }

            // slide down the wall slowly (it gets steeper at the end), lose a little speed
            float late = Mathf.InverseLerp(WallrideMaxTime * 0.6f, WallrideMaxTime, wallTime);
            wallVy -= DevTools.AirGravity * Mathf.Lerp(WallGravity, 0.35f, late) * dt;
            wallVy = Mathf.Max(wallVy, -3.5f);
            speed = Mathf.MoveTowards(speed, 0f, 0.6f * dt);
            // the FEET stay on the board, right against the wall (the body leans out from it)
            Vector3 feet = FeetPos(player.refs.ragdoll, hip.position);
            // (wallPoint = the last place the wall was seen: with a short gap in the wall this ray hit nothing)
            float feetGap = Vector3.Dot(feet - wallPoint, wallNormal);
            float wantGap = BoardVisual.WheelBase * SkatePlugin.RideBoardScale.Value + 0.05f;
            Vector3 keepOff = Vector3.ClampMagnitude(wallNormal * ((wantGap - feetGap) * 9f), 5f);
            Vector3 desired = wallAlong * speed + Vector3.up * wallVy + keepOff;
            Vector3 acc = (desired - v) * 12f + desired * comp;
            AddToAllRigs(Vector3.ClampMagnitude(acc, 300f));
        }

        private void EndWallride(bool pop, Rigidbody hip)
        {
            if (!wallriding) return;
            wallriding = false;
            wallTrickCooldown = 0.6f;
            if (wallTime > 0.2f)
            {
                combo.Add("Wallride", 100f + wallTime * 220f);
                Progress.MaxStat("wall_ms", (long)(wallTime * 1000f));
                SkateActivity.Local(SkateAct.Wallride, combo.Count, "Wallride", Mathf.Clamp(1f + wallTime * 0.5f, 1f, 1.8f));
            }
            heading = Yaw(wallAlong);
            if (pop)
            {
                // jump off the wall, away from it
                Vector3 vel = wallAlong * speed + wallNormal * 3.5f;
                SetAllVelocities(rv => new Vector3(vel.x, SkatePlugin.OllieMin.Value, vel.z));
                player.data.sinceJump = 0f;
                TakeOff(true);
                airVel = vel;
                Play(SkateSounds.Pop, 0.9f);
            }
            else
            {
                TakeOff(false);
                airVel = wallAlong * speed + wallNormal * 1f;
            }
            combo.KeepAlive();
        }

        // ---------------------------------------------------------------- wallplant

        // Flying straight at a wall with CROUCH held: plant a foot on it and kick off backwards, turning around.
        private bool TryWallplant(Rigidbody hip)
        {
            if (wallTrickCooldown > 0f || !rawCrouch || grab != Grab.None || airTime < 0.08f || GettingUp) return false;
            Vector3 flat = new Vector3(airVel.x, 0f, airVel.z);
            Vector3 dir = flat.sqrMagnitude > 1f ? flat.normalized : YawDir(heading);
            Vector3 origin = hip.position - Vector3.up * 0.3f;
            if (!Physics.Raycast(origin, dir, out RaycastHit h, 1.15f, WallMask, QueryTriggerInteraction.Ignore) || !IsWall(h)) return false;
            Vector3 n = new Vector3(h.normal.x, 0f, h.normal.z).normalized;
            if (Vector3.Dot(n, -dir) < 0.5f) return false; // must be facing the wall

            wallTrickCooldown = 0.7f;
            float spd = Mathf.Max(3f, flat.magnitude * 0.6f);
            airVel = n * spd;
            SetAllVelocities(rv => new Vector3(airVel.x, Mathf.Max(rv.y, 0f) + 6.5f, airVel.z));
            heading = Yaw(n);
            bodyYaw += 180f; // turn around off the wall
            spinAccum = 0f;
            lastAirVy = 0f;
            combo.Add("Wallplant", 120f);
            SkateActivity.Local(SkateAct.Wallplant, combo.Count, "Wallplant");
            Play(SkateSounds.Pop, 1f);
            pushDip = 0.2f;
            return true;
        }

        // ---------------------------------------------------------------- drop-in

        private bool dropIn;      // rolled off an edge (not an ollie)
        private float dropStartY;

        private void StartDrop()
        {
            dropIn = true;
            dropStartY = Hip().position.y;
        }

        // CROUCH while dropping = leaning into it: the legs take a much bigger landing
        // (a worn board takes a quarter less)
        // (Easy: the legs take half as much again, Normal: a quarter)
        private float LandingLimit => SkatePlugin.MaxLandingSpeed.Value * (dropIn && rawCrouch ? 1.8f : 1f) * (1f - 0.25f * WearWeakness) * ByDifficulty(1.5f, 1.25f, 1f);

        // after landing: a big enough roll-off counts as a drop-in
        private bool ScoreDropIn()
        {
            if (!dropIn) return false;
            dropIn = false;
            float height = dropStartY - Hip().position.y;
            if (height < 0.8f) return false;
            combo.Add(height > 3f ? "Big Drop In" : "Drop In", 60f + height * 40f);
            SkateActivity.Local(SkateAct.DropIn, combo.Count, height > 3f ? "Big Drop In" : "Drop In", Mathf.Clamp(1f + (height - 0.8f) * 0.12f, 1f, 2f)); // higher drop, more views
            return true;
        }

        // ---------------------------------------------------------------- board throw

        private bool thrown;
        private float thrownTime;
        public bool BoardThrown => thrown;

        // Not on the board: throw it ahead of you, run after it and you hop on automatically.
        private void ThrowBoard()
        {
            if (Riding || thrown || !Owns || player.data.dead || player.Ragdoll()) return;
            if (SkateItem.HoldingBoard(player)) player.data.selectedItemSlot = -1;
            EnsureBoard();
            board.StopFling();
            board.gameObject.SetActive(true);
            board.transform.localScale = Vector3.one;
            Vector3 look = player.data.lookDirection;
            Vector3 dir = new Vector3(look.x, 0f, look.z);
            if (dir.sqrMagnitude < 0.01f) dir = YawDir(bodyYaw);
            dir.Normalize();
            Vector3 runVel = Hip().linearVelocity;
            runVel.y = 0f;
            Vector3 start = Hip().position + dir * 0.6f;
            board.transform.SetPositionAndRotation(start, Quaternion.LookRotation(dir, Vector3.up));
            Vector3 vel = dir * (4.5f + runVel.magnitude * 0.35f) + Vector3.up * 1.0f;
            board.Fling(vel, 0f, slippery: true);
            // flies level, wheels down; after landing we roll it ourselves (see UpdateThrownBoard)
            if (board.FlyRig != null) board.FlyRig.constraints = RigidbodyConstraints.FreezeRotation;
            rollDir = dir;
            rollSpeed = new Vector3(vel.x, 0f, vel.z).magnitude;
            rollBlocked = 0f;
            thrown = true;
            thrownLanded = false;
            thrownTime = Time.time;
            NetSync.SendThrow(player, vel);
            Play(SkateSounds.Whoosh, 0.6f);
        }

        // Called from LateUpdate while the board is out there.
        private void UpdateThrownBoard()
        {
            if (!thrown || board == null) return;
            Vector3 hip = Hip().position;
            Vector3 b = board.transform.position;
            Vector3 flat = new Vector3(b.x - hip.x, 0f, b.z - hip.z);
            // fell out of the world: back on your back
            if (!board.Flying || board.OutOfWorld)
            {
                thrown = false;
                board.StopFling();
                return;
            }
            float age = Time.time - thrownTime;
            // it has to land first (no hopping on it in the air)
            if (!thrownLanded && age > 0.15f && Physics.Raycast(b + Vector3.up * 0.1f, Vector3.down, 0.35f, WallMask, QueryTriggerInteraction.Ignore))
                thrownLanded = true;
            if (thrownLanded && board.FlyRig != null)
            {
                // rolling on its wheels: keeps going straight and slowly loses speed
                Vector3 actual = board.FlyVelocity;
                actual.y = 0f;
                if (rollSpeed > 1f && actual.magnitude < rollSpeed * 0.3f) rollBlocked += Time.deltaTime; else rollBlocked = 0f;
                if (rollBlocked > 0.25f) rollSpeed = 0f; // ran into something
                rollSpeed = Mathf.MoveTowards(rollSpeed, 0f, 1.2f * Time.deltaTime);
                board.FlyRig.linearVelocity = rollDir * rollSpeed + Vector3.up * Mathf.Min(board.FlyRig.linearVelocity.y, 0f);
            }
            // it stopped rolling (or you left it): now it's just an item lying there, pick it up with E
            bool stopped = thrownLanded && rollSpeed < 0.3f;
            stoppedFor = stopped ? stoppedFor + Time.deltaTime : 0f;
            if (stoppedFor > 0.3f || flat.magnitude > 40f || age > 12f)
            {
                LeaveThrownBoard();
                return;
            }
            // caught up with it while it still rolls: hop on
            if (thrownLanded && age > 0.5f && !stopped && flat.magnitude < 1.1f && Mathf.Abs(b.y - (hip.y - standHipHeight)) < 1.2f)
                CatchBoard();
        }

        private float stoppedFor;
        private bool thrownLanded;
        private Vector3 rollDir;
        private float rollSpeed, rollBlocked;

        private void LeaveThrownBoard()
        {
            thrown = false;
            stoppedFor = 0f;
            Transform m = board.model;
            Vector3 pos = m.position + Vector3.up * 0.05f;
            Quaternion rot = m.rotation;
            board.StopFling();
            // the board leaves your inventory and becomes the normal item on the ground
            if (SkateItem.DropBoard(player, pos, rot, Vector3.zero, Vector3.zero))
                board.gameObject.SetActive(false);
        }

        private void CatchBoard()
        {
            Vector3 boardVel = board.FlyVelocity;
            Vector3 run = Hip().linearVelocity;
            run.y = 0f;
            boardVel.y = 0f;
            thrown = false;
            StartRiding();
            if (!Riding) return;
            Vector3 go = run.sqrMagnitude > 1f ? run : boardVel;
            if (go.sqrMagnitude > 0.1f) { heading = Yaw(go); bodyYaw = heading; }
            speed = Mathf.Clamp(Mathf.Max(run.magnitude * 1.15f, boardVel.magnitude, 3f), 0f, SkatePlugin.PushSpeed.Value + 1.5f);
            combo.Add("Board Throw", 80f);
            combo.KeepAlive();
            SkateActivity.Local(SkateAct.BoardThrow, combo.Count, "Board Throw");
            Play(SkateSounds.Land, 0.8f);
        }
    }
}
