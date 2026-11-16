using UnityEngine;

namespace CWSkate
{
    // Holding on to someone and getting towed. Two keys, two things:
    //  - the skitch key: one hand holds on to whoever is closest (monster or player);
    //  - the train key: both hands on the shoulders of the player right in front of you, you ride along behind them
    //    (and someone can hold on to you the same way: a whole train).
    // Let go and you keep the speed. Too far behind and you lose your grip.
    internal partial class SkateController
    {
        private bool skitching, train;
        private Player skitchTarget;
        private Bodypart skitchHip;
        private float skitchTime, skitchLen, skitchBlend, skitchMissPopup, trainBlend;
        private BodypartType skitchHand = BodypartType.Hand_R;

        public bool Skitching => skitching;
        public bool Training => skitching && train;
        public float SkitchTime => skitching ? skitchTime : 0f;
        public bool SkitchingMonster => skitching && skitchTarget != null && skitchTarget.ai;
        public string SkitchName => SkitchTrickName(SkitchingMonster, Training);

        private const float SkitchReach = 3f;
        private const float TrainReach = 2.2f;  // how far in front someone can be for the train
        private const float TrainGap = 0.85f;   // hip to hip while in the train
        // The train is for players only (true = monsters too: handy for testing it alone, on a zombie).
        private const bool TrainBehindMonsters = false;

        private static string SkitchTrickName(bool monster, bool isTrain) =>
            isTrain ? (monster ? "Monster Train" : "Skate Train") : (monster ? "Monster Skitch" : "Skitch");

        // input (Update). Works anywhere on the board - on the ground or in the air, whoever it is, moving or not.
        private void TickSkitch(float dt, bool canInput)
        {
            skitchBlend = Mathf.MoveTowards(skitchBlend, skitching ? 1f : 0f, dt * 8f);
            trainBlend = Mathf.MoveTowards(trainBlend, Training ? 1f : 0f, dt * 6f);
            bool can = canInput && !grinding && !wallriding && !monsterGrinding && !bailed;
            if (!skitching)
            {
                if (!can) return;
                if (Input.GetKeyDown(SkatePlugin.TrainKey.Value)) TryStartSkitch(true);
                else if (Input.GetKeyDown(SkatePlugin.SkitchKey.Value)) TryStartSkitch(false);
                return;
            }
            // holding on as long as the key of what you're doing is held
            bool held = can && Input.GetKey(train ? SkatePlugin.TrainKey.Value : SkatePlugin.SkitchKey.Value);
            if (!held || skitchTarget == null || skitchHip == null || skitchTarget.data.dead) EndSkitch(true);
        }

        private static Vector3 FlatVel(Bodypart bp)
        {
            if (bp == null || bp.rig == null) return Vector3.zero;
            Vector3 v = bp.rig.linearVelocity;
            v.y = 0f;
            return v;
        }

        // wantTrain = the train key: only the player right in front counts; the skitch key: whoever is closest
        private void TryStartSkitch(bool wantTrain)
        {
            Vector3 me = Hip().position;
            Vector3 ahead = YawDir(heading); // the way we're rolling
            Player best = null, trainBest = null;
            Bodypart bestHip = null, trainHip = null;
            float bestD = SkitchReach, trainD = TrainReach;
            void Consider(Player p)
            {
                if (p == null || p == player || p.data == null || p.data.dead || p.refs.ragdoll == null) return;
                var h = p.refs.ragdoll.GetBodypart(BodypartType.Hip);
                if (h == null || h.rig == null) return;
                float d = Vector3.Distance(h.rig.position, me);
                if (d < bestD) { bestD = d; best = p; bestHip = h; }
                // right in front of us, at about our height: grab their shoulders
                if (p.ai && !TrainBehindMonsters) return;
                Vector3 to = h.rig.position - me;
                to.y = 0f;
                float fd = to.magnitude;
                if (fd > 0.3f && fd < trainD && Vector3.Dot(to / fd, ahead) > 0.75f && Mathf.Abs(h.rig.position.y - me.y) < 1f)
                {
                    trainD = fd;
                    trainBest = p;
                    trainHip = h;
                }
            }
            foreach (var bot in Monsters.Bots) if (bot != null) Consider(Monsters.PlayerOf(bot));
            var ph = PlayerHandler.instance;
            if (ph != null && ph.players != null) foreach (var p in ph.players) Consider(p);

            bool isTrain = wantTrain;
            if (isTrain) { best = trainBest; bestHip = trainHip; bestD = trainD; }
            if (best == null)
            {
                // (in the air the skitch key is also the kickflip: no message there)
                if ((wantTrain || !airborne) && Time.time - skitchMissPopup > 2f)
                {
                    skitchMissPopup = Time.time;
                    SkateHUD.Popup(wantTrain ? Lang.T("ПРЯМО ПЕРЕД ТОБОЙ НИКОГО", "NOBODY RIGHT IN FRONT OF YOU") : Lang.T("РЯДОМ НЕКОГО СХВАТИТЬ", "NOBODY CLOSE ENOUGH TO GRAB"), new Color(1f, 0.8f, 0.5f), 1.4f, small: true);
                }
                return;
            }
            skitching = true;
            train = isTrain;
            skitchTarget = best;
            skitchHip = bestHip;
            skitchTime = 0f;
            // (grabbing from further away pulls you in to arm's length)
            skitchLen = isTrain ? TrainGap : Mathf.Clamp(bestD, 0.9f, 1.6f);
            if (manual) EndManual();
            if (sliding) EndSlide();
            // skitching: the hand on their side
            Vector3 right = YawDir(bodyYaw + StanceOffset + 90f);
            skitchHand = Vector3.Dot(bestHip.rig.position - me, right) > 0f ? BodypartType.Hand_R : BodypartType.Hand_L;
            Play(SkateSounds.Whoosh, 0.5f);
            string name = SkitchTrickName(best.ai, isTrain);
            SkateHUD.Popup(name.ToUpperInvariant() + "!", new Color(0.6f, 0.9f, 1f), 1.4f, small: true);
            SkateActivity.Local(isTrain ? SkateAct.Train : SkateAct.Skitch, combo.Count, name, (best.ai ? 1.8f : 1f) * TrainChainBonus(best, isTrain));
            if (best.refs.view != null) NetSync.SendSkitch(player, best.refs.view.ViewID, skitchHand == BodypartType.Hand_R, isTrain);
        }

        // a longer train is a better video: the one in front rides a board too (x1.2), or is holding on to someone themselves (x1.5)
        private static float TrainChainBonus(Player front, bool isTrain)
        {
            if (!isTrain || front == null || front.ai) return 1f;
            var rb = front.GetComponent<RemoteBoard>();
            if (rb != null && rb.Holding) return 1.5f;
            return NetSync.GetState(front) == NetSync.StateRiding ? 1.2f : 1f;
        }

        // physics: the tow rope (FixedUpdate, before the normal rolling)
        private void SkitchFixed(float dt)
        {
            if (!skitching || skitchHip == null || skitchHip.rig == null) return;
            Vector3 to = skitchHip.rig.position - Hip().position;
            to.y = 0f;
            float d = to.magnitude;
            // a firm grip: you only lose it if something really tears you apart (a teleport, a wall between you)
            if (d > 10f) { EndSkitch(false); SkateHUD.Popup(Lang.T("СОРВАЛСЯ", "LOST YOUR GRIP"), new Color(1f, 0.6f, 0.5f), 1.4f, small: true); return; }
            combo.KeepAlive();
            if (d < 0.05f) return;
            Vector3 dir = to / d;
            // you go exactly as fast as they do, plus whatever it takes to stay at arm's length
            Vector3 tv = FlatVel(skitchHip);
            Vector3 want = tv + dir * ((d - skitchLen) * 6f);
            float maxTow = SkatePlugin.MaxSpeed.Value * 1.5f;
            want = Vector3.ClampMagnitude(want, maxTow);
            Rigidbody hip = Hip();
            Vector3 flat = new Vector3(hip.linearVelocity.x, 0f, hip.linearVelocity.z);
            // the arm itself pulls the body along (no waiting for the board to pick up speed)
            AddToAllRigs((want - flat) * 8f);
            if (airborne)
            {
                airVel = new Vector3(want.x, airVel.y, want.z);
            }
            else if (want.sqrMagnitude > 0.04f)
            {
                heading = Yaw(want);
                speed = want.magnitude;
            }
            else
            {
                speed = Mathf.MoveTowards(speed, 0f, 4f * dt);
                if (train && d > 0.3f) heading = Yaw(dir); // standing in the train: face the one in front
            }
            if (want.magnitude > 2f || airborne) skitchTime += dt; // only being towed counts as a trick, not just holding on
        }

        // let go: score it (keep the speed)
        private void EndSkitch(bool letGo)
        {
            if (!skitching) return;
            bool monster = skitchTarget != null && skitchTarget.ai;
            if (skitchTime > 0.5f)
            {
                string name = SkitchTrickName(monster, train);
                if (train)
                {
                    combo.Add(name, (monster ? 180f : 140f) + skitchTime * (monster ? 260f : 200f));
                    float power = (monster ? 1.8f : 1f) * TrainChainBonus(skitchTarget, true) * Mathf.Clamp(1f + skitchTime * 0.15f + speed * 0.03f, 1f, 2f);
                    SkateActivity.Local(SkateAct.Train, combo.Count, name, power);
                    Progress.AddStat("trains", 1, save: false);
                    Progress.MaxStat("train_ms", (long)(skitchTime * 1000f));
                }
                else
                {
                    combo.Add(name, (monster ? 150f : 100f) + skitchTime * (monster ? 220f : 150f));
                    float power = (monster ? 1.8f : 1f) * Mathf.Clamp(1f + skitchTime * 0.15f + speed * 0.03f, 1f, 2f);
                    SkateActivity.Local(SkateAct.Skitch, combo.Count, name, power);
                    Progress.MaxStat("skitch_ms", (long)(skitchTime * 1000f));
                }
            }
            if (letGo) speed = Mathf.Min(speed * 1.08f, SkatePlugin.MaxSpeed.Value); // a little slingshot when you let go yourself
            StopSkitch();
        }

        private void StopSkitch()
        {
            if (skitching && player != null && player.refs.view != null) NetSync.SendSkitch(player, -1, false, false);
            skitching = false;
            train = false;
            skitchTarget = null;
            skitchHip = null;
            skitchTime = 0f;
        }

        // pose: the arm reaches for whoever is towing you (both hands on their shoulders in a train)
        private void SkitchPose()
        {
            if (skitchBlend <= 0.01f || skitchHip == null) return;
            if (train && skitchTarget != null)
            {
                GrabPose.ApplyTrainHold(player.refs.ragdoll, skitchTarget, YawDir(heading + 90f), skitchBlend);
                return;
            }
            GrabPose.ApplyReach(player.refs.ragdoll, skitchHand, skitchHip.transform.position + Vector3.up * 0.25f, skitchBlend);
        }
    }
}
