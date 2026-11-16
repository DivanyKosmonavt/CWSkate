using UnityEngine;

namespace CWSkate
{
    // (saved by name in the config, so the order here doesn't matter for old configs)
    internal enum SkateDifficulty { Easy, Normal, Hard }

    // Easy / Normal / Hard. Hard is the Skate 3 way: curbs, steps and bumps at the wheels throw you over the board,
    // landings have to be clean. The game has lots of uneven, bulging floors, so the lower levels forgive:
    //  Easy:
    //  - the board hops over bumps, curbs and steps by itself (StepOver) at any speed;
    //  - walls only throw you off a bit above pushing speed, your body has to stay stuck for longer;
    //  - only a really sideways landing fails, flips / frontflips / backflips land with more left to go,
    //    the legs take 1.5x the drop, a grab you're still holding doesn't matter, a hippie jump lands near the board.
    //  Normal (in between):
    //  - the board hops over bumps at pushing speed, but going faster (downhill, rocket) a curb still throws you;
    //  - walls throw you off above 5 m/s, a stuck body and sideways landings count like on Hard;
    //  - a bit more slack on flips and drops, you must still let go of grabs.
    internal partial class SkateController
    {
        private bool Easy => SkatePlugin.Difficulty.Value == SkateDifficulty.Easy;
        private bool Hard => SkatePlugin.Difficulty.Value == SkateDifficulty.Hard;

        // picks a value for the current difficulty
        private static float ByDifficulty(float easy, float normal, float hard)
        {
            switch (SkatePlugin.Difficulty.Value)
            {
                case SkateDifficulty.Easy: return easy;
                case SkateDifficulty.Normal: return normal;
                default: return hard;
            }
        }

        // Normal: hop over bumps up to this speed (a bit above pushing speed), faster - trip like on Hard
        private float StepOverMaxSpeed => Easy ? float.MaxValue : SkatePlugin.TripSpeed.Value * BoardUpgrades.TripMul * 3.6f;

        // riding into a wall: Easy - a bit above pushing speed, Normal - above 5 m/s (18 km/h), Hard - WallBailSpeed
        private float WallGroundBailSpeed
        {
            get
            {
                float hard = SkatePlugin.WallBailSpeed.Value;
                if (Easy) return Mathf.Max(hard, SkatePlugin.PushSpeed.Value * 1.15f);
                if (!Hard) return Mathf.Max(hard, 5f);
                return hard;
            }
        }

        private float stepTimer;

        // Something at the wheels: a little hop over it, keeping almost all the speed.
        private void StepOver()
        {
            if (Time.time < stepTimer) return;
            stepTimer = Time.time + 0.3f;
            // enough to get over a curb or a step (up to ~35 cm), not a real jump
            SetAllVelocities(rv => new Vector3(rv.x, Mathf.Max(rv.y, 4.2f), rv.z));
            speed *= 0.95f;
            if (sliding) EndSlide();
        }
    }
}
