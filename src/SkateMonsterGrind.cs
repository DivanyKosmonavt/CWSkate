using UnityEngine;

namespace CWSkate
{
    // Monster grind: come down onto a monster from an ollie and you ride on its back, wherever it runs.
    // JUMP pops you off (a trick in the combo); if it falls over, dies or shakes you off, you drop down.
    internal partial class SkateController
    {
        private bool monsterGrinding;
        private Player grindMonster;
        private Bodypart mgHip;
        private float monsterGrindTime, mgTop;
        private Vector3 mgOffset;

        public bool MonsterGrinding => monsterGrinding;
        public float MonsterGrindTime => monsterGrindTime;

        // in the air, coming down onto something alive: a monster? hop on
        private float monsterGrindEndedAt = -10f;

        private bool TryStartMonsterGrind(RaycastHit under)
        {
            if (!SkatePlugin.MonsterGrinds.Value || hippie || salto != 0 || grab != Grab.None || under.collider == null) return false;
            if (Time.time - monsterGrindEndedAt < 1.2f) return false; // just jumped off: don't stick right back on
            var m = under.collider.GetComponentInParent<Player>();
            if (m == null || !m.ai || m.data == null || m.data.dead || m.data.fallTime > 0f) return false;
            var mh = m.refs.ragdoll != null ? m.refs.ragdoll.GetBodypart(BodypartType.Hip) : null;
            if (mh == null || mh.rig == null) return false;

            ScoreAir(); // tricks on the way in still count
            grindMonster = m;
            mgHip = mh;
            mgTop = under.point.y - mh.rig.position.y; // how high its back is above its hips
            Vector3 off = Hip().position - mh.rig.position;
            off.y = 0f;
            mgOffset = Vector3.ClampMagnitude(off, 0.35f); // where on its back you stand
            monsterGrinding = true;
            airborne = false;
            monsterGrindTime = 0f;
            Play(SkateSounds.Land, 0.8f);
            Play(SkateSounds.Hit, 0.4f);
            SkateHUD.Popup("MONSTER GRIND!", new Color(1f, 0.5f, 0.9f), 1.6f, small: true);
            SkateActivity.Local(SkateAct.MonsterGrind, combo.Count, "Monster Grind");
            combo.KeepAlive();
            return true;
        }

        // physics: stay on its back
        private void MonsterGrindFixed(Rigidbody hip, Vector3 v, float comp, float dt)
        {
            if (grindMonster == null || mgHip == null || mgHip.rig == null || grindMonster.data.dead || grindMonster.data.fallTime > 0f)
            {
                EndMonsterGrind(false); // it fell over / died under you
                return;
            }
            Vector3 mpos = mgHip.rig.position, mvel = mgHip.rig.linearVelocity;
            Vector3 target = mpos + mgOffset + Vector3.up * (mgTop + standHipHeight);
            Vector3 err = target - hip.position;
            if (err.magnitude > 1.6f) { EndMonsterGrind(false); return; } // it shook you off
            Vector3 want = mvel + err * 8f;
            AddToAllRigs((want - v) * 15f + v * comp);
            player.data.isGrounded = true;
            player.data.sinceGrounded = 0f;
            player.data.movementVector = Vector3.zero;

            monsterGrindTime += dt;
            combo.KeepAlive();
            if (pendingOllie) { pendingOllie = false; EndMonsterGrind(true); return; }
            if (monsterGrindTime > 6f) EndMonsterGrind(true); // long enough: hop off by yourself before it gets you
        }

        private void EndMonsterGrind(bool pop)
        {
            if (!monsterGrinding) return;
            monsterGrinding = false;
            Vector3 mvel = mgHip != null && mgHip.rig != null ? mgHip.rig.linearVelocity : Vector3.zero;
            mvel.y = 0f;
            if (monsterGrindTime > 0.2f)
            {
                combo.Add("Monster Grind", 220f + monsterGrindTime * 320f);
                SkateActivity.Local(SkateAct.MonsterGrind, combo.Count, "Monster Grind", Mathf.Clamp(1f + monsterGrindTime * 0.3f, 1f, 2.5f));
                Progress.AddStat("monster_grinds", 1, save: false);
                Progress.MaxStat("mgrind_ms", (long)(monsterGrindTime * 1000f));
            }
            sinceGrind = 0f;
            monsterGrindEndedAt = Time.time;
            // off it the way the board points, clearly away from it (with only its speed you'd land right back on its back)
            Vector3 vel = mvel * 0.6f + YawDir(heading) * 4.5f;
            speed = vel.magnitude;
            if (pop)
            {
                SetAllVelocities(rv => new Vector3(vel.x, SkatePlugin.OllieMin.Value, vel.z));
                player.data.sinceJump = 0f;
                TakeOff(true);
                Play(SkateSounds.Pop, 0.9f);
            }
            else TakeOff(false);
            airVel = vel;
            grindMonster = null;
            mgHip = null;
            combo.KeepAlive();
        }
    }
}
