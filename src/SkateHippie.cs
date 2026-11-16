using UnityEngine;

namespace CWSkate
{
    // Hippie jump: S + tap JUMP while rolling. You jump straight up, the board keeps rolling on its own
    // (under a bar, a rope, a monster...), and you land back on it. Miss it and you eat the floor.
    internal partial class SkateController
    {
        private bool hippie, pendingHippie, hippieOver, hippieOverMonster;
        private Vector3 hippieBoardPos, hippieUp = Vector3.up;
        private float hippieYaw, hippieSpeed;

        public bool Hippie => hippie;
        public Vector3 HipPos => Hip().position;

        private static int TerrainMask => HelperFunctions.GetMask(HelperFunctions.LayerType.TerrainProp);

        // right after the pop (DoOllie): let go of the board
        private void StartHippie()
        {
            if (board == null) return;
            hippie = true;
            ollieThisAir = false; // it's not an ollie
            hippieOver = hippieOverMonster = false;
            hippieBoardPos = board.transform.position;
            hippieUp = Vector3.up;
            hippieYaw = heading;
            hippieSpeed = speed;
            NetSync.SendHippie(player, hippieBoardPos, hippieYaw, hippieSpeed);
        }

        // the board rolls on by itself (LateUpdate)
        private void UpdateHippieBoard(float dt, float bs)
        {
            hippieSpeed = Mathf.MoveTowards(hippieSpeed, 0f, SkatePlugin.RollFriction.Value * dt);
            Vector3 dir = YawDir(hippieYaw);
            Vector3 next = hippieBoardPos + dir * (hippieSpeed * dt);
            // follow the floor (only small steps: a bar it rolls under isn't the floor)
            if (Physics.Raycast(next + Vector3.up * 0.3f, Vector3.down, out RaycastHit hit, 2.5f, TerrainMask, QueryTriggerInteraction.Ignore))
            {
                next.y = hit.point.y + BoardVisual.WheelBase * bs;
                hippieUp = Vector3.Slerp(hippieUp, hit.normal, dt * 12f);
            }
            else next.y -= 4f * dt; // rolled off an edge
            hippieBoardPos = next;

            // something above the board and below you: you're jumping over it
            float gap = Hip().position.y - next.y;
            if (gap > 0.6f && Physics.Raycast(next + Vector3.up * 0.12f, Vector3.up, out RaycastHit above, gap - 0.4f, ~(1 << 29), QueryTriggerInteraction.Ignore))
            {
                var who = above.collider.GetComponentInParent<Player>();
                if (who != player)
                {
                    hippieOver = true;
                    if (who != null && who.ai) hippieOverMonster = true;
                }
            }

            Vector3 fwd = Vector3.ProjectOnPlane(dir, hippieUp);
            if (fwd.sqrMagnitude < 1e-4f) fwd = dir;
            board.Place(next, Quaternion.LookRotation(fwd.normalized, hippieUp), WearWobble(), Vector3.zero);
        }

        // landed: are your feet over the board?
        private bool HippieMissed()
        {
            var rag = player.refs.ragdoll;
            Vector3 feet = FeetPos(rag, Hip().position);
            Vector3 d = feet - hippieBoardPos;
            d.y = 0f;
            return d.magnitude > ByDifficulty(1.3f, 1.0f, 0.8f) * SkatePlugin.RideBoardScale.Value; // (Easy: land near it and you're on it)
        }

        private void LandHippie()
        {
            hippie = false;
            heading = hippieYaw;   // you're back on the board: its speed and direction
            speed = hippieSpeed;
            string name = hippieOver ? "Hippie Jump Over" : "Hippie Jump";
            combo.Add(name, hippieOverMonster ? 420f : hippieOver ? 280f : 160f);
            float power = hippieOverMonster ? 2.4f : hippieOver ? 1.6f : 1f; // over something (a monster!) is the real deal
            SkateActivity.Local(SkateAct.HippieJump, combo.Count, name, power);
            if (hippieOverMonster) SkateHUD.Popup(Lang.T("НАД МОНСТРОМ!", "OVER THE MONSTER!"), new Color(1f, 0.5f, 0.9f), 2f, small: true);
            Progress.AddStat("hippies", 1, save: false);
        }
    }
}
