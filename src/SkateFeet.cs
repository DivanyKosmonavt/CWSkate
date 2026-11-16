using UnityEngine;

namespace CWSkate
{
    // The feet stay on the deck: they don't sink through the board and don't catch on the floor.
    //  - the skeleton puts both feet on their spots on the deck (IK, see ApplyPoseIK);
    //  - the physical feet are pulled to those spots (PinFeet);
    //  - shins and feet don't collide with anything while you ride (the board is what touches the floor).
    // The body leans with the board on slopes: the hip and the torso are turned to the tilted skeleton (LeanBody).
    internal partial class SkateController
    {
        private bool legsGhosted;

        // feet on the board right now (not when the board rolls on by itself or is on a wall)
        private bool FeetOnDeck => Riding && !bailed && board != null && !board.Flying && !airborne && !hippie && !wallriding;

        // where each foot goes on the deck (the ankle, a bit above the grip)
        private Vector3 DeckFootSpot(bool front)
        {
            var deck = board.model;
            return deck.TransformPoint(new Vector3(0f, 0.012f, front ? 0.19f : -0.19f)) + deck.up * 0.075f;
        }

        private void PinFeet(PlayerRagdoll rag, Vector3 hipVel)
        {
            if (!FeetOnDeck) return;
            bool frontIsLeft = !SkatePlugin.GoofyStance.Value;
            Pin(rag.GetBodypart(frontIsLeft ? BodypartType.Foot_L : BodypartType.Foot_R), DeckFootSpot(true), hipVel);
            // pushing: the back foot follows the kick down to the floor instead
            Transform face = player.refs.animatorTransform;
            bool kicking = kickT >= 0f && kickT < 1f && face != null;
            Vector3 backSpot = kicking ? GrabPose.KickFootSpot(board.model, face.forward, kickT) : DeckFootSpot(false);
            Pin(rag.GetBodypart(frontIsLeft ? BodypartType.Foot_R : BodypartType.Foot_L), backSpot, hipVel);
        }

        private static void Pin(Bodypart bp, Vector3 target, Vector3 refVel)
        {
            if (bp == null || bp.rig == null || bp.rig.isKinematic) return;
            Vector3 err = target - bp.rig.position;
            if (err.sqrMagnitude > 1f) return; // way off (teleport, a bail starting): don't yank
            Vector3 acc = err * 320f - (bp.rig.linearVelocity - refVel) * 26f;
            bp.rig.AddForce(Vector3.ClampMagnitude(acc, 700f), ForceMode.Acceleration);
        }

        // shins and feet pass through the floor while you're on the board, so bumps don't kick them around
        private void UpdateLegCollisions()
        {
            bool want = Riding && !bailed && board != null && !board.Flying && !hippie;
            if (want == legsGhosted || player == null) return;
            legsGhosted = want;
            SetLegCollisions(!want);
        }

        private void SetLegCollisions(bool on)
        {
            var rag = player != null ? player.refs.ragdoll : null;
            if (rag == null) return;
            foreach (var t in new[] { BodypartType.Knee_L, BodypartType.Knee_R, BodypartType.Foot_L, BodypartType.Foot_R })
            {
                var bp = rag.GetBodypart(t);
                if (bp != null && bp.rig != null) bp.rig.detectCollisions = on;
            }
        }

        // The game only turns the hip to the skeleton's tilt, and weakly, so on a slope the body stayed upright.
        // Turn the hip and the torso to the tilted skeleton ourselves.
        private void LeanBody(PlayerRagdoll rag)
        {
            if (airborne || grinding || wallriding || monsterGrinding || SkatePlugin.BodySlopeLean.Value <= 0f) return;
            if (Vector3.Angle(Vector3.up, bodySlopeUp) < 1f) return;
            foreach (var t in new[] { BodypartType.Hip, BodypartType.Torso })
            {
                var bp = rag.GetBodypart(t);
                if (bp == null || bp.rig == null || bp.animationTarget == null) continue;
                Vector3 from = bp.rig.transform.up, to = bp.animationTarget.transform.up;
                Vector3 axis = Vector3.Cross(from, to);
                float ang = Vector3.Angle(from, to);
                if (ang < 0.5f) continue;
                axis.Normalize();
                // (only damp the tipping, not turning around)
                Vector3 torque = axis * (ang * Mathf.Deg2Rad * 60f) - Vector3.Project(bp.rig.angularVelocity, axis) * 6f;
                bp.rig.AddTorque(torque, ForceMode.Acceleration);
            }
        }
    }
}
