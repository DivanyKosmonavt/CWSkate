using UnityEngine;

namespace CWSkate
{
    // Rocket booster (a board part from the shop): hold the rocket key and it pushes you way past the normal top speed.
    // The faster you go, the less you can turn. It burns fuel (refill with a fuel canister from the shop).
    // Crashing at rocket speed hurts more and wears the board more.
    internal partial class SkateController
    {
        private bool rocketOn;
        private float rocketTime, rocketVideoTick, rocketPopup;
        private AudioSource rocketSrc;
        private float rocketVol;

        private const float RocketAccel = 14f;   // m/s² of extra push
        private const float FuelPerSecond = 5f;  // a full tank = 20 s of burning

        public bool HasRocket => BoardUpgrades.Has(BoardUpgrades.Rocket);
        public bool RocketOn => rocketOn;
        public int RocketFuel => BoardUpgrades.LocalFuel;

        // top speed with the rocket on
        private float RocketTop => Mathf.Max(SkatePlugin.MaxSpeed.Value * BoardUpgrades.MaxSpeedMul * 1.6f, 30f);

        // normal top speed (without the rocket)
        private float NormalTop => SkatePlugin.MaxSpeed.Value * BoardUpgrades.MaxSpeedMul;

        // Update: the key
        private void TickRocket(float dt, bool canInput)
        {
            bool want = canInput && HasRocket && Input.GetKey(SkatePlugin.RocketKey.Value)
                        && !bailed && !grinding && !wallriding && !monsterGrinding && !hippie && !skitching;
            if (want && RocketFuel <= 0)
            {
                want = false;
                if (Input.GetKeyDown(SkatePlugin.RocketKey.Value) && Time.time - rocketPopup > 1.5f)
                {
                    rocketPopup = Time.time;
                    SkateHUD.Popup(Lang.T("ТОПЛИВО КОНЧИЛОСЬ - КУПИ КАНИСТРУ", "OUT OF FUEL - BUY A CANISTER"), new Color(1f, 0.6f, 0.3f), 1.8f, small: true);
                    Play(SkateSounds.Pop, 0.4f); // a sad little pop
                }
            }
            if (want != rocketOn) SetRocket(want);
            if (!rocketOn) return;
            rocketTime += dt;
            if (!(SkatePlugin.DevTools.Value && SkatePlugin.DevInfiniteFuel.Value)) BoardUpgrades.BurnLocal(FuelPerSecond * dt);
            combo.KeepAlive();
            rocketVideoTick -= dt;
            if (rocketVideoTick <= 0f)
            {
                rocketVideoTick = 0.5f;
                // faster = better video
                SkateActivity.Local(SkateAct.RocketRide, combo.Count, "Rocket Ride", Mathf.Clamp(1f + speed * 0.04f, 1f, 2.5f));
            }
        }

        private void SetRocket(bool on)
        {
            if (on == rocketOn) return;
            rocketOn = on;
            if (player != null && player.refs.view != null) NetSync.SendRocket(player, on);
            if (on)
            {
                rocketTime = 0f;
                rocketVideoTick = 0f;
                Play(SkateSounds.Whoosh, 0.9f);
                return;
            }
            // the burn is over: it counts as a trick
            // (not when you just got off the board: the combo is already cashed in, it would hang around for the next ride)
            if (rocketTime > 0.5f && !bailed && Riding)
            {
                combo.Add("Rocket Ride", 150f + rocketTime * 150f);
                Progress.MaxStat("rocket_ms", (long)(rocketTime * 1000f));
            }
            rocketTime = 0f;
        }

        // FixedUpdate: the push
        private void RocketFixed(float dt)
        {
            if (!rocketOn) return;
            if (airborne)
            {
                // in the air it still pushes you along (you fly much further)
                Vector3 flat = new Vector3(airVel.x, 0f, airVel.z);
                Vector3 dir = flat.sqrMagnitude > 1f ? flat.normalized : YawDir(heading);
                flat = Vector3.ClampMagnitude(flat + dir * (RocketAccel * 0.7f * dt), RocketTop);
                airVel = new Vector3(flat.x, airVel.y, flat.z);
                // push the body too, or it lags behind and that looks like hitting a wall mid-air
                if (flat.magnitude < RocketTop - 0.1f) AddToAllRigs(dir * (RocketAccel * 0.7f));
            }
            else speed = Mathf.Min(speed + RocketAccel * dt, RocketTop);
        }

        // how hard you can still turn at this speed: above the normal top speed it drops to a fifth
        private float RocketTurnMul(float normalTop)
        {
            if (speed <= normalTop) return 1f;
            return Mathf.Lerp(1f, 0.2f, (speed - normalTop) / Mathf.Max(1f, RocketTop - normalTop));
        }

        // speed cap: up to the rocket's top while it burns, then it slowly comes back down to normal
        private float CapSpeed(float s, float normalTop, float dt)
        {
            if (rocketOn) return Mathf.Clamp(s, 0f, RocketTop);
            if (s > normalTop) return Mathf.MoveTowards(s, normalTop, 4f * dt);
            return Mathf.Max(0f, s);
        }

        // You crashed while it was burning: half the time the rocket blows up (and it's gone from the board).
        private void MaybeBlowUpRocket()
        {
            if (!SkatePlugin.RocketCanExplode.Value || !HasRocket || Random.value > 0.5f) return;
            var data = BoardWear.LocalData();
            if (data == null) return;
            BoardUpgrades.Set(data, BoardUpgrades.Of(data) & ~BoardUpgrades.Rocket);
            BoardUpgrades.SetFuel(data, 0);
            Vector3 at = board != null ? board.model.position : Hip().position;
            SkateHUD.Popup(Lang.T("РАКЕТА ВЗОРВАЛАСЬ!", "THE ROCKET BLEW UP!"), new Color(1f, 0.45f, 0.1f), 3f);
            Boom.Explode(at, 1.2f);
        }

        // a crash at rocket speed (or with the rocket on) hurts more and wears the board more
        private bool RocketCrash(float crashSpeed) => rocketOn || (HasRocket && crashSpeed > NormalTop * 1.02f);
    }
}
