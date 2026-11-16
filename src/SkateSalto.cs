using System.Collections.Generic;
using UnityEngine;

namespace CWSkate
{
    // Frontflip / backflip: you and the board go head over heels together.
    // In the air: W + JUMP = frontflip, S + JUMP = backflip, the same again mid-flip = double (then triple).
    internal partial class SkateController
    {
        private int salto;               // 0 = none, +1 = frontflip, -1 = backflip
        private int saltoCount;          // full turns asked for
        private float saltoProgress;     // turns done so far
        private readonly List<KeyValuePair<int, int>> saltosThisAir = new List<KeyValuePair<int, int>>();

        public bool Salto => salto != 0;
        public string SaltoLabel => salto > 0 ? "FRONTFLIP" : "BACKFLIP";

        // What's left of a turn when you land and it still counts.
        // A backflip gets more: its second half has the chest side (knees, board, face) facing down
        // just when you're falling, so it meets the floor a little early.
        // (Easy: a quarter of a turn more, Normal: an eighth)
        private float SaltoLandSlack => (salto < 0 ? 0.22f : 0.12f) + ByDifficulty(0.25f, 0.12f, 0f);

        // degrees per second; the backflip turns faster so its second half happens higher up
        private float SaltoRate => SkatePlugin.SaltoSpeed.Value * (salto < 0 ? 1.3f : 1f);

        public static string SaltoName(int dir, int count) =>
            (count == 2 ? "Double " : count >= 3 ? "Triple " : "") + (dir > 0 ? "Frontflip" : "Backflip");

        private static float SaltoPoints(int dir, int count) =>
            (dir > 0 ? 650f : 600f) * (count == 2 ? 2.4f : count >= 3 ? 4f : 1f);

        private void StartSalto(int dir)
        {
            if (salto != 0)
            {
                // the same key again mid-flip = one more turn
                if (dir == salto && saltoCount < 3 && saltoProgress < saltoCount)
                {
                    saltoCount++;
                    Play(SkateSounds.Whoosh, 0.7f);
                    SkateActivity.Local(SkateAct.Flip, combo.Count, SaltoName(salto, saltoCount));
                    NetSync.SendSalto(player, salto, saltoCount, SaltoRate);
                }
                return;
            }
            salto = dir;
            lastSaltoDir = dir;
            saltoCount = 1;
            saltoProgress = 0f;
            // throwing yourself into it: a small push up, so a normal ollie has time for the turn
            if (!saltoPopped)
            {
                saltoPopped = true;
                // (not when already dropping fast from something high: no jumping off thin air)
                float pop = SaltoPop + (dir < 0 ? 0.8f : 0f); // the backflip needs a bit more time, the head comes down late
                SetAllVelocities(rv => new Vector3(rv.x, rv.y > -3f ? rv.y + pop : rv.y, rv.z));
            }
            Play(SkateSounds.Whoosh, 0.8f);
            SkateActivity.Local(SkateAct.Flip, combo.Count, SaltoName(salto, 1));
            NetSync.SendSalto(player, salto, saltoCount, SaltoRate);
        }

        private void TickSalto(float dt)
        {
            if (salto == 0) return;
            saltoProgress += dt * SaltoRate / 360f;
            if (saltoProgress >= saltoCount) FinishSalto();
            combo.KeepAlive();
        }

        private void FinishSalto()
        {
            if (salto == 0) return;
            saltosThisAir.Add(new KeyValuePair<int, int>(salto, saltoCount));
            salto = 0;
            saltoCount = 0;
            saltoProgress = 0f;
        }

        private const float SaltoPop = 2.2f; // m/s up when the flip starts

        private void ClearSalto()
        {
            saltoPopped = false;
            salto = 0;
            saltoCount = 0;
            saltoProgress = 0f;
            saltosThisAir.Clear();
        }

        // didn't come round in time
        private bool SaltoUnfinished => salto != 0 && saltoProgress < saltoCount - SaltoLandSlack;

        // the turn of the whole body (and the board) right now, around the rider's left-right axis
        private Quaternion SaltoRotation()
        {
            if (salto == 0) return Quaternion.identity;
            Vector3 chest = YawDir(bodyYaw + StanceOffset);
            Vector3 right = Vector3.Cross(Vector3.up, chest).normalized;
            return Quaternion.AngleAxis(salto * saltoProgress * 360f, right);
        }

        private float saltoWeld; // 0..1, the board sits on the real feet
        private float tuckBlend; // 0..1, knees to the chest
        private bool saltoPopped; // the little jump into the flip, once per air
        private int lastSaltoDir; // which way the last flip went (the tuck opens up after it ends)

        // physics: the body turns over as one piece, so it keeps up with the flip
        private void SaltoFixed()
        {
            if (salto == 0) return;
            Vector3 chest = YawDir(bodyYaw + StanceOffset);
            Vector3 right = Vector3.Cross(Vector3.up, chest).normalized;
            SaltoAssist.Spin(player.refs.ragdoll, right, salto * SaltoRate, SaltoRotation());
        }

        // into the combo when you land
        private void ScoreSaltos()
        {
            if (salto != 0) FinishSalto();
            foreach (var s in saltosThisAir)
                combo.Add(SaltoName(s.Key, s.Value), SaltoPoints(s.Key, s.Value));
            if (saltosThisAir.Count > 0)
            {
                int turns = 0;
                foreach (var s in saltosThisAir) turns += s.Value;
                Progress.AddStat("saltos", turns, save: false);
            }
            saltosThisAir.Clear();
        }
    }

    // shared by your own flips and other players'
    internal static class SaltoAssist
    {
        // Turns the whole body around the hips at the flip speed (deg/s) and pulls it onto the angle it should be at,
        // so the body doesn't trail behind its own animation.
        // The angle is measured on the hips against the skeleton's hips (the head is curled forward in the tuck,
        // measuring on it made the hips turn too far in a backflip).
        // (the frontflip already worked well measured on the head, so it keeps that)
        public static void Spin(PlayerRagdoll rag, Vector3 right, float degPerSec, Quaternion target)
        {
            if (rag == null) return;
            var hip = rag.GetBodypart(BodypartType.Hip);
            if (hip == null || hip.rig == null) return;
            Vector3 hp = hip.rig.position, hv = hip.rig.linearVelocity;
            float err = 0f;
            var head = rag.GetBodypart(BodypartType.Head);
            if (degPerSec > 0f && head != null && head.rig != null)
            {
                Vector3 bodyUp = Vector3.ProjectOnPlane(head.rig.position - hp, right);
                Vector3 wantUp = Vector3.ProjectOnPlane(target * Vector3.up, right);
                if (bodyUp.sqrMagnitude > 1e-4f && wantUp.sqrMagnitude > 1e-4f) err = Vector3.SignedAngle(bodyUp, wantUp, right);
            }
            else if (hip.animationTarget != null)
            {
                Quaternion delta = hip.animationTarget.transform.rotation * Quaternion.Inverse(hip.rig.rotation);
                delta.ToAngleAxis(out float ang, out Vector3 axis);
                if (ang > 180f) ang -= 360f;
                if (Mathf.Abs(ang) > 0.5f && !float.IsNaN(axis.x)) err = ang * Vector3.Dot(axis.normalized, right);
            }
            float rate = degPerSec + Mathf.Clamp(err * 8f, -900f, 900f);
            Vector3 w = right * (rate * Mathf.Deg2Rad);
            var rigs = rag.rigList;
            for (int i = 0; i < rigs.Count; i++)
            {
                var rb = rigs[i];
                if (rb == null || rb.isKinematic) continue;
                if (rb != hip.rig)
                    rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, hv + Vector3.Cross(w, rb.position - hp), 0.6f);
                rb.angularVelocity = Vector3.Lerp(rb.angularVelocity, w, 0.6f);
            }
        }

        // Where the board goes to sit right under the feet, whatever the body is doing.
        // noseHint = roughly where the nose should point (used when the feet are too close together).
        public static bool WeldBoard(PlayerRagdoll rag, bool goofy, Vector3 noseHint, float feetOffset, out Vector3 pos, out Quaternion rot)
        {
            pos = Vector3.zero;
            rot = Quaternion.identity;
            var fl = rag.GetBodypart(BodypartType.Foot_L);
            var fr = rag.GetBodypart(BodypartType.Foot_R);
            var hip = rag.GetBodypart(BodypartType.Hip);
            if (fl == null || fr == null || hip == null) return false;
            Vector3 front = (goofy ? fr : fl).transform.position, back = (goofy ? fl : fr).transform.position;
            Vector3 mid = (front + back) * 0.5f;
            Vector3 up = hip.transform.position - mid; // the deck faces the hips
            if (up.sqrMagnitude < 0.01f) return false;
            up.Normalize();
            Vector3 along = Vector3.ProjectOnPlane(front - back, up); // front foot is at the nose
            if (along.magnitude < 0.08f) along = Vector3.ProjectOnPlane(noseHint, up);
            if (along.sqrMagnitude < 1e-4f) return false;
            rot = Quaternion.LookRotation(along.normalized, up);
            pos = mid - up * feetOffset;
            return true;
        }
    }
}
