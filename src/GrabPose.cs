using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace CWSkate
{
    // Grab pose: knees come up, the board rises to meet the hand, the hand reaches for its edge.
    // Airwalk and Superman are "body" grabs: the feet leave the board instead.
    // Used for the local rider and for other players' boards.
    internal static class GrabPose
    {
        // the legs come off the board (the board is held by the hands, not the feet)
        public static bool IsBody(Grab g) => g == Grab.Airwalk || g == Grab.Superman;

        // Which hand grabs and where (board model space: +z = nose, +x = right of the nose, +y = grip side).
        // Regular stance rides facing +x (left foot at the nose), goofy faces -x.
        public static void Target(Grab g, bool goofy, out BodypartType hand, out Vector3 local)
        {
            float toe = goofy ? -1f : 1f;
            BodypartType front = goofy ? BodypartType.Hand_R : BodypartType.Hand_L;
            BodypartType back = goofy ? BodypartType.Hand_L : BodypartType.Hand_R;
            switch (g)
            {
                case Grab.Melon: hand = front; local = new Vector3(-toe * 0.105f, -0.01f, 0.05f); break;     // front hand, heel edge
                case Grab.Nosegrab: hand = front; local = new Vector3(0f, -0.005f, 0.37f); break;            // front hand, nose
                case Grab.Tailgrab: hand = back; local = new Vector3(0f, -0.005f, -0.37f); break;            // back hand, tail
                case Grab.Stalefish: hand = back; local = new Vector3(-toe * 0.105f, -0.01f, -0.13f); break; // back hand behind the back leg, heel edge
                case Grab.Crail: hand = back; local = new Vector3(0f, -0.005f, 0.37f); break;                // back hand reaches across to the nose
                case Grab.Airwalk: hand = front; local = new Vector3(0f, -0.005f, 0.37f); break;             // front hand on the nose
                case Grab.Superman: hand = front; local = new Vector3(-toe * 0.1f, -0.01f, 0.28f); break;    // both hands near the nose
                default: hand = back; local = new Vector3(toe * 0.105f, -0.01f, -0.04f); break;              // Indy: back hand, toe edge
            }
        }

        // Superman holds the board with both hands
        private static bool SecondHand(Grab g, bool goofy, out BodypartType hand, out Vector3 local)
        {
            float toe = goofy ? -1f : 1f;
            hand = goofy ? BodypartType.Hand_L : BodypartType.Hand_R;
            local = new Vector3(toe * 0.1f, -0.01f, 0.28f);
            return g == Grab.Superman;
        }

        // Moves the board part of the way towards the hand, so hand and board meet in the middle.
        public static Vector3 MeetOffset(PlayerRagdoll rag, BodypartType hand, Vector3 grabPointWorld, float blend)
        {
            var h = rag.GetBodypart(hand);
            if (h == null) return Vector3.zero;
            Vector3 d = (h.transform.position - grabPointWorld) * (0.6f * blend);
            return Vector3.ClampMagnitude(d, 0.45f);
        }

        // Where the board hangs for a body grab (the feet aren't on it to carry it).
        // nose = where the board points, chest = where the rider faces (sideways to the nose).
        public static Vector3 BodyAnchor(PlayerRagdoll rag, Grab g, Vector3 hipPos, Vector3 nose)
        {
            if (g == Grab.Superman)
            {
                var torso = rag.GetBodypart(BodypartType.Torso);
                Vector3 chest = torso ? torso.transform.position : hipPos + Vector3.up * 0.3f;
                return chest - Vector3.up * 0.35f + nose * 0.3f; // out in front, under the chest
            }
            return hipPos - Vector3.up * 0.5f + nose * 0.12f;   // airwalk: under you, between the legs
        }

        // The body leans forward and lies flat in the air for Superman (0..1 blend), head towards the nose.
        public static Quaternion SupermanRotation(Quaternion normal, Vector3 nose, float blend)
        {
            if (blend <= 0.001f) return normal;
            Vector3 head = (nose * 0.9f + Vector3.up * 0.45f).normalized;
            Vector3 chest = Vector3.ProjectOnPlane(Vector3.down, head).normalized;
            return Quaternion.Slerp(normal, Quaternion.LookRotation(chest, head), blend * 0.85f);
        }

        // Physics part (FixedUpdate): an extra pull of the hand(s) onto the board.
        // The pose itself (arms, legs, back) is set on the animation skeleton, see ApplyIK.
        public static void ApplyForces(PlayerRagdoll rag, Grab g, bool goofy, Transform deck, float blend)
        {
            if (rag == null || deck == null || blend <= 0.01f || g == Grab.None) return;
            var hip = rag.GetBodypart(BodypartType.Hip);
            if (hip == null || hip.rig == null) return;
            Vector3 hipVel = hip.rig.linearVelocity;
            Target(g, goofy, out var hand, out var local);
            Hand(rag, hand, deck.TransformPoint(local), hipVel, blend);
            if (SecondHand(g, goofy, out var hand2, out var local2))
                Hand(rag, hand2, deck.TransformPoint(local2), hipVel, blend);
        }

        // ---------------------------------------------------------------- pose on the animation skeleton
        // The game's body copies an invisible animated skeleton every physics step (strong joint drives).
        // Pushing the body around fights that, so instead we bend the skeleton itself, right before the copy:
        // two-bone IK for the arms (hand on the board) and legs (feet on the deck, or off it for airwalk / superman).

        private class BoneMemo { public Quaternion baseRot, setRot; }
        private static readonly Dictionary<Transform, BoneMemo> memo = new Dictionary<Transform, BoneMemo>();
        private static bool warnedChain;

        private static Transform Anim(PlayerRagdoll rag, BodypartType t)
        {
            var b = rag.GetBodypart(t);
            return b != null && b.animationTarget != null ? b.animationTarget.transform : null;
        }

        // Start from the animator's pose: if the animator hasn't written a new one since our last change, undo ours.
        private static void Restore(Transform t)
        {
            if (t == null) return;
            if (!memo.TryGetValue(t, out var m))
            {
                memo[t] = new BoneMemo { baseRot = t.localRotation, setRot = t.localRotation };
                return;
            }
            if (Quaternion.Angle(t.localRotation, m.setRot) > 0.05f) m.baseRot = t.localRotation; // fresh pose from the animator
            else t.localRotation = m.baseRot;
        }

        private static void Remember(Transform t)
        {
            if (t != null && memo.TryGetValue(t, out var m)) m.setRot = t.localRotation;
        }

        public static void ApplyIK(PlayerRagdoll rag, Grab g, bool goofy, Transform deck, float blend)
        {
            if (rag == null || deck == null || g == Grab.None || blend <= 0.01f || rag.player == null) return;
            var hipPart = rag.GetBodypart(BodypartType.Hip);
            Transform animRoot = rag.player.refs.animatorTransform;
            if (hipPart == null || hipPart.animationTarget == null || animRoot == null) return;
            if (memo.Count > 400) memo.Clear(); // players come and go

            Vector3 fwd = animRoot.forward, upA = animRoot.up, right = animRoot.right;
            Vector3 physHip = hipPart.transform.position, animHip = hipPart.animationTarget.transform.position;
            // the body copies the skeleton's offsets from its hip, so world targets move over the same way
            Vector3 ToAnim(Vector3 world) => animHip + (world - physHip);

            Transform torso = Anim(rag, BodypartType.Torso);
            Transform armL = Anim(rag, BodypartType.Arm_L), elbowL = Anim(rag, BodypartType.Elbow_L), handL = Anim(rag, BodypartType.Hand_L);
            Transform armR = Anim(rag, BodypartType.Arm_R), elbowR = Anim(rag, BodypartType.Elbow_R), handR = Anim(rag, BodypartType.Hand_R);
            Transform legL = Anim(rag, BodypartType.Leg_L), kneeL = Anim(rag, BodypartType.Knee_L), footL = Anim(rag, BodypartType.Foot_L);
            Transform legR = Anim(rag, BodypartType.Leg_R), kneeR = Anim(rag, BodypartType.Knee_R), footR = Anim(rag, BodypartType.Foot_R);
            Transform[] bones = { torso, armL, elbowL, armR, elbowR, legL, kneeL, legR, kneeR };
            foreach (var b in bones) Restore(b);

            bool body = IsBody(g);
            // lean over to reach the board (the body grabs stay stretched out)
            if (!body && torso != null)
            {
                float bend = g == Grab.Stalefish || g == Grab.Melon ? 20f : 30f;
                torso.rotation = Quaternion.Slerp(Quaternion.identity, Quaternion.AngleAxis(bend, right), blend) * torso.rotation;
            }

            // legs
            bool frontIsLeft = !goofy;
            Transform fLeg = frontIsLeft ? legL : legR, fKnee = frontIsLeft ? kneeL : kneeR, fFoot = frontIsLeft ? footL : footR;
            Transform bLeg = frontIsLeft ? legR : legL, bKnee = frontIsLeft ? kneeR : kneeL, bFoot = frontIsLeft ? footR : footL;
            Vector3 kneePole = fwd + upA * 0.3f;
            if (g == Grab.Airwalk)
            {
                // scissor kick: front leg forward (where you face), back leg behind you
                LegOut(fLeg, fKnee, fFoot, fwd * 0.6f - upA * 0.8f, kneePole, blend);
                LegOut(bLeg, bKnee, bFoot, -fwd * 0.55f - upA * 0.83f, kneePole, blend);
            }
            else if (g == Grab.Superman)
            {
                // legs straight out behind (the whole body is already lying along the board)
                LegOut(fLeg, fKnee, fFoot, -upA + right * 0.08f, kneePole, blend);
                LegOut(bLeg, bKnee, bFoot, -upA - right * 0.08f, kneePole, blend);
            }
            else
            {
                // feet stay on the deck: the knees bend as the board comes up
                Vector3 ankle = deck.up * 0.075f;
                Solve(fLeg, fKnee, fFoot, ToAnim(deck.TransformPoint(new Vector3(0f, 0.012f, 0.19f)) + ankle), kneePole, blend);
                Solve(bLeg, bKnee, bFoot, ToAnim(deck.TransformPoint(new Vector3(0f, 0.012f, -0.19f)) + ankle), kneePole, blend);
            }

            // arms: the hand on its grab point
            Target(g, goofy, out var hand, out var local);
            Arm(hand, ToAnim(deck.TransformPoint(local)), right, upA, armL, elbowL, handL, armR, elbowR, handR, blend);
            if (SecondHand(g, goofy, out var hand2, out var local2))
                Arm(hand2, ToAnim(deck.TransformPoint(local2)), right, upA, armL, elbowL, handL, armR, elbowR, handR, blend);

            foreach (var b in bones) Remember(b);
        }

        // Skitching: one arm reaches out and holds on to whoever is towing you.
        public static void ApplyReach(PlayerRagdoll rag, BodypartType hand, Vector3 worldPoint, float blend)
        {
            if (rag == null || blend <= 0.01f || rag.player == null) return;
            var hipPart = rag.GetBodypart(BodypartType.Hip);
            Transform animRoot = rag.player.refs.animatorTransform;
            if (hipPart == null || hipPart.animationTarget == null || animRoot == null) return;
            if (memo.Count > 400) memo.Clear();
            Vector3 point = hipPart.animationTarget.transform.position + (worldPoint - hipPart.transform.position);
            bool left = hand == BodypartType.Hand_L;
            Transform a = Anim(rag, left ? BodypartType.Arm_L : BodypartType.Arm_R);
            Transform e = Anim(rag, left ? BodypartType.Elbow_L : BodypartType.Elbow_R);
            Transform h = Anim(rag, hand);
            if (a == null || e == null || h == null) return;
            Restore(a);
            Restore(e);
            Vector3 wrist = point + (a.position - point).normalized * 0.05f;
            Solve(a, e, h, wrist, (left ? -animRoot.right : animRoot.right) * 0.6f - animRoot.up * 0.4f, blend);
            Remember(a);
            Remember(e);
        }

        // Skate train: both hands on the shoulders of the one in front.
        // myRight = the rider's right; each hand takes the shoulder on its own side (whichever way they face).
        public static void ApplyTrainHold(PlayerRagdoll rag, Player front, Vector3 myRight, float blend)
        {
            if (rag == null || front == null || front.refs.ragdoll == null || blend <= 0.01f) return;
            var fr = front.refs.ragdoll;
            var hip = fr.GetBodypart(BodypartType.Hip);
            var torso = fr.GetBodypart(BodypartType.Torso);
            if (hip == null) return;
            Vector3 centre = torso != null ? torso.transform.position + Vector3.up * 0.15f : hip.transform.position + Vector3.up * 0.55f;
            var s1 = fr.GetBodypart(BodypartType.Shoulder_L);
            var s2 = fr.GetBodypart(BodypartType.Shoulder_R);
            Vector3 a = s1 != null ? s1.transform.position : centre - myRight * 0.18f;
            Vector3 b = s2 != null ? s2.transform.position : centre + myRight * 0.18f;
            if (Vector3.Dot(a - b, myRight) > 0f) { var t = a; a = b; b = t; } // a = the one on our left
            ApplyReach(rag, BodypartType.Hand_L, a + Vector3.up * 0.04f, blend);
            ApplyReach(rag, BodypartType.Hand_R, b + Vector3.up * 0.04f, blend);
        }

        // Pushing: the back foot steps off the deck onto the floor next to the board (the side you face),
        // pushes back along it and comes back up onto the tail. t = 0..1 through the kick. Returns where the ankle goes.
        public static Vector3 KickFootSpot(Transform deck, Vector3 faceDir, float t)
        {
            Vector3 onDeck = deck.TransformPoint(new Vector3(0f, 0.012f, -0.19f)) + deck.up * 0.075f;
            Vector3 fwd = deck.forward;
            Vector3 side = Vector3.ProjectOnPlane(Vector3.ProjectOnPlane(faceDir, deck.up), fwd);
            side = side.sqrMagnitude > 0.01f ? side.normalized : deck.right;
            // the floor next to the board
            Vector3 probe = deck.position + side * 0.24f;
            Vector3 floor = Physics.Raycast(probe + Vector3.up * 0.4f, Vector3.down, out RaycastHit hit, 1.2f,
                                            HelperFunctions.GetMask(HelperFunctions.LayerType.TerrainProp), QueryTriggerInteraction.Ignore)
                ? hit.point : probe - deck.up * 0.1f;
            Vector3 up = Vector3.up * 0.07f; // the ankle above the sole
            Vector3 plant = floor + fwd * 0.12f + up;              // the foot comes down level with the middle of the board
            Vector3 end = floor - fwd * 0.42f - side * 0.04f + up; // and pushes back past the tail
            if (t < 0.22f)
            {
                float u = t / 0.22f;
                return Vector3.Lerp(onDeck, plant, Mathf.SmoothStep(0f, 1f, u)) + Vector3.up * Mathf.Sin(u * Mathf.PI) * 0.08f;
            }
            if (t < 0.72f) return Vector3.Lerp(plant, end, (t - 0.22f) / 0.5f); // on the floor: the push itself
            float w = Mathf.Clamp01((t - 0.72f) / 0.28f);
            return Vector3.Lerp(end, onDeck, Mathf.SmoothStep(0f, 1f, w)) + Vector3.up * Mathf.Sin(w * Mathf.PI) * 0.12f;
        }

        // Riding (and dancing on the board, a game emote while riding): the feet stay on the deck.
        // kick = 0..1 while pushing: the back foot goes down to the floor and pushes (see KickFootSpot), below 0 = no push.
        public static void ApplyFeetOnDeck(PlayerRagdoll rag, bool goofy, Transform deck, float blend, float kick = -1f)
        {
            if (rag == null || deck == null || blend <= 0.01f || rag.player == null) return;
            var hipPart = rag.GetBodypart(BodypartType.Hip);
            Transform animRoot = rag.player.refs.animatorTransform;
            if (hipPart == null || hipPart.animationTarget == null || animRoot == null) return;
            if (memo.Count > 400) memo.Clear();
            Vector3 physHip = hipPart.transform.position, animHip = hipPart.animationTarget.transform.position;
            Vector3 ToAnim(Vector3 world) => animHip + (world - physHip);

            Transform legL = Anim(rag, BodypartType.Leg_L), kneeL = Anim(rag, BodypartType.Knee_L), footL = Anim(rag, BodypartType.Foot_L);
            Transform legR = Anim(rag, BodypartType.Leg_R), kneeR = Anim(rag, BodypartType.Knee_R), footR = Anim(rag, BodypartType.Foot_R);
            Transform[] bones = { legL, kneeL, legR, kneeR };
            foreach (var b in bones) Restore(b);
            bool frontIsLeft = !goofy;
            Vector3 ankle = deck.up * 0.075f;
            Vector3 pole = animRoot.forward + animRoot.up * 0.3f;
            Solve(frontIsLeft ? legL : legR, frontIsLeft ? kneeL : kneeR, frontIsLeft ? footL : footR,
                ToAnim(deck.TransformPoint(new Vector3(0f, 0.012f, 0.19f)) + ankle), pole, blend);
            Vector3 back = kick >= 0f && kick < 1f ? KickFootSpot(deck, animRoot.forward, kick)
                                                   : deck.TransformPoint(new Vector3(0f, 0.012f, -0.19f)) + ankle;
            Solve(frontIsLeft ? legR : legL, frontIsLeft ? kneeR : kneeL, frontIsLeft ? footR : footL, ToAnim(back), pole, blend);
            foreach (var b in bones) Remember(b);
        }

        // Tuck for a frontflip / backflip: knees pulled up to the chest, hands on the shins, back rounded,
        // so the body is a small ball and the head doesn't swing into the floor.
        // back = backflip: the back stays almost straight. Curled forward, the head comes down late in a backflip,
        // when you're already falling, and hits the floor.
        public static void ApplyTuck(PlayerRagdoll rag, float blend, bool back)
        {
            if (rag == null || blend <= 0.01f || rag.player == null) return;
            Transform animRoot = rag.player.refs.animatorTransform;
            if (animRoot == null) return;
            if (memo.Count > 400) memo.Clear();
            Vector3 fwd = animRoot.forward, upA = animRoot.up, right = animRoot.right; // turns over with the flip

            Transform torso = Anim(rag, BodypartType.Torso);
            Transform armL = Anim(rag, BodypartType.Arm_L), elbowL = Anim(rag, BodypartType.Elbow_L), handL = Anim(rag, BodypartType.Hand_L);
            Transform armR = Anim(rag, BodypartType.Arm_R), elbowR = Anim(rag, BodypartType.Elbow_R), handR = Anim(rag, BodypartType.Hand_R);
            Transform legL = Anim(rag, BodypartType.Leg_L), kneeL = Anim(rag, BodypartType.Knee_L), footL = Anim(rag, BodypartType.Foot_L);
            Transform legR = Anim(rag, BodypartType.Leg_R), kneeR = Anim(rag, BodypartType.Knee_R), footR = Anim(rag, BodypartType.Foot_R);
            Transform[] bones = { torso, armL, elbowL, armR, elbowR, legL, kneeL, legR, kneeR };
            foreach (var b in bones) Restore(b);

            // round the back over the knees
            if (torso != null)
                torso.rotation = Quaternion.Slerp(Quaternion.identity, Quaternion.AngleAxis(back ? 8f : 38f, right), blend) * torso.rotation;

            // heels up under the bum, knees up in front of the chest
            Vector3 kneePole = fwd + upA * 0.8f;
            // backflip: the feet (and the board on them) stay under the hips instead of out in front,
            // so nothing sticks out on the side that faces the floor at the end of the turn
            float reach = back ? 0.12f : 0.42f;
            Tuck(legL, kneeL, footL, fwd, upA, -right * 0.04f, kneePole, reach, blend);
            Tuck(legR, kneeR, footR, fwd, upA, right * 0.04f, kneePole, reach, blend);

            // hands hug the shins
            if (kneeL != null && footL != null && armL != null)
                Solve(armL, elbowL, handL, Vector3.Lerp(kneeL.position, footL.position, 0.4f) + fwd * 0.05f, -right * 0.7f - upA * 0.2f, blend);
            if (kneeR != null && footR != null && armR != null)
                Solve(armR, elbowR, handR, Vector3.Lerp(kneeR.position, footR.position, 0.4f) + fwd * 0.05f, right * 0.7f - upA * 0.2f, blend);

            foreach (var b in bones) Remember(b);
        }

        private static void Tuck(Transform leg, Transform knee, Transform foot, Vector3 fwd, Vector3 up, Vector3 side, Vector3 pole, float reach, float blend)
        {
            if (leg == null || knee == null || foot == null) return;
            float len = (knee.position - leg.position).magnitude + (foot.position - knee.position).magnitude;
            Vector3 target = leg.position + (-up * 0.62f + fwd * reach).normalized * (len * 0.4f) + side;
            Solve(leg, knee, foot, target, pole, blend);
        }

        private static void Arm(BodypartType hand, Vector3 grabPoint, Vector3 right, Vector3 up,
            Transform armL, Transform elbowL, Transform handL, Transform armR, Transform elbowR, Transform handR, float blend)
        {
            bool left = hand == BodypartType.Hand_L;
            Transform a = left ? armL : armR, e = left ? elbowL : elbowR, h = left ? handL : handR;
            if (a == null) return;
            // the wrist stops a few cm short of the grab point, so the palm is on the edge
            Vector3 wrist = grabPoint + (a.position - grabPoint).normalized * 0.05f;
            Vector3 pole = (left ? -right : right) * 0.7f - up * 0.2f; // elbow out to the side
            Solve(a, e, h, wrist, pole, blend);
        }

        // a straight-ish leg pointing in a direction (from the hip joint), full length
        private static void LegOut(Transform leg, Transform knee, Transform foot, Vector3 dir, Vector3 pole, float blend)
        {
            if (leg == null || knee == null || foot == null) return;
            float len = (knee.position - leg.position).magnitude + (foot.position - knee.position).magnitude;
            Solve(leg, knee, foot, leg.position + dir.normalized * len * 0.96f, pole, blend);
        }

        // Two-bone IK: turns upper and lower so the end lands on the target, bending towards the pole direction.
        private static void Solve(Transform upper, Transform lower, Transform end, Vector3 target, Vector3 poleDir, float blend)
        {
            if (upper == null || lower == null || end == null) return;
            if (!lower.IsChildOf(upper) || !end.IsChildOf(lower))
            {
                if (!warnedChain) { warnedChain = true; SkatePlugin.Log.LogWarning("Grab pose: bones aren't a chain: " + upper.name + " / " + lower.name + " / " + end.name); }
                return;
            }
            Vector3 a = upper.position, b = lower.position, c = end.position;
            float l1 = (b - a).magnitude, l2 = (c - b).magnitude;
            if (l1 < 1e-3f || l2 < 1e-3f) return;
            Vector3 toT = target - a;
            float dist = Mathf.Clamp(toT.magnitude, Mathf.Abs(l1 - l2) + 0.01f, (l1 + l2) * 0.999f);
            Vector3 d = toT.sqrMagnitude > 1e-6f ? toT.normalized : (c - a).normalized;
            Vector3 bend = Vector3.ProjectOnPlane(poleDir, d);
            if (bend.sqrMagnitude < 1e-6f) bend = Vector3.ProjectOnPlane(b - a, d);
            if (bend.sqrMagnitude < 1e-6f) return;
            bend.Normalize();
            float cosA = Mathf.Clamp((l1 * l1 + dist * dist - l2 * l2) / (2f * l1 * dist), -1f, 1f);
            float angA = Mathf.Acos(cosA);
            Vector3 joint = a + (d * Mathf.Cos(angA) + bend * Mathf.Sin(angA)) * l1;

            Quaternion u0 = upper.rotation;
            upper.rotation = Quaternion.Slerp(u0, Quaternion.FromToRotation(b - a, joint - a) * u0, blend);
            b = lower.position;
            c = end.position;
            Quaternion l0 = lower.rotation;
            lower.rotation = Quaternion.Slerp(l0, Quaternion.FromToRotation(c - b, a + d * dist - b) * l0, blend);
        }

        // hand -> grab point, the forearm helps
        private static void Hand(PlayerRagdoll rag, BodypartType hand, Vector3 point, Vector3 hipVel, float blend)
        {
            Pull(rag.GetBodypart(hand), point, hipVel, 260f * blend, 24f, 700f);
            var elbow = rag.GetBodypart(hand == BodypartType.Hand_L ? BodypartType.Elbow_L : BodypartType.Elbow_R);
            var shoulder = rag.GetBodypart(hand == BodypartType.Hand_L ? BodypartType.Shoulder_L : BodypartType.Shoulder_R);
            if (elbow != null && shoulder != null)
                Pull(elbow, Vector3.Lerp(shoulder.transform.position, point, 0.5f), hipVel, 90f * blend, 14f, 300f);
        }

        private static void Pull(Bodypart bp, Vector3 target, Vector3 refVel, float k, float damp, float max)
        {
            if (bp == null || bp.rig == null || bp.rig.isKinematic) return;
            Vector3 acc = (target - bp.rig.position) * k - (bp.rig.linearVelocity - refVel) * damp;
            bp.rig.AddForce(Vector3.ClampMagnitude(acc, max), ForceMode.Acceleration);
        }

        public static void Lift(Bodypart bp, float targetY, Vector3 refVel, float blend)
        {
            if (bp == null || bp.rig == null || bp.rig.isKinematic) return;
            float dy = targetY - bp.rig.position.y;
            if (dy <= 0f) return; // already high enough
            float acc = dy * 140f * blend - (bp.rig.linearVelocity.y - refVel.y) * 12f;
            bp.rig.AddForce(Vector3.up * Mathf.Clamp(acc, -200f, 250f), ForceMode.Acceleration);
        }

        // right before the body copies the skeleton: bend the skeleton into the grab
        [HarmonyPatch(typeof(PlayerRagdoll), "FollowAnim")]
        private static class Patch_GrabIK
        {
            private static void Prefix(PlayerRagdoll __instance)
            {
                try
                {
                    var p = __instance.player;
                    if (p == null || p.ai) return;
                    if (p.IsLocal)
                    {
                        var sc = SkateController.Local;
                        if (sc != null && sc.player == p) sc.ApplyPoseIK();
                    }
                    else
                    {
                        var rb = p.GetComponent<RemoteBoard>();
                        if (rb != null) rb.ApplyPoseIK();
                    }
                }
                catch (System.Exception e) { SkatePlugin.Log.LogWarning("Grab pose: " + e.Message); }
            }
        }
    }
}
