using HarmonyLib;

namespace CWSkate
{
    // Replace normal walking with skateboard physics while riding.
    [HarmonyPatch(typeof(PlayerController), "Movement")]
    internal static class Patch_Movement
    {
        private static bool Prefix(PlayerController __instance)
        {
            var skate = SkateController.Local;
            if (skate == null || !skate.Riding || skate.player == null || skate.player.refs.controller != __instance)
                return true;
            skate.SkateFixedUpdate();
            return false;
        }
    }

    // While riding we use our own (constant) air gravity, so jumps are predictable.
    [HarmonyPatch(typeof(PlayerController), "Gravity")]
    internal static class Patch_Gravity
    {
        private static bool Prefix(PlayerController __instance)
        {
            var skate = SkateController.Local;
            if (skate == null || !skate.Riding || skate.player == null || skate.player.refs.controller != __instance)
                return true;
            return false;
        }
    }

    // No normal jumping while on the board (we ollie instead).
    [HarmonyPatch(typeof(PlayerController), nameof(PlayerController.TryJump))]
    internal static class Patch_TryJump
    {
        private static bool Prefix(PlayerController __instance)
        {
            var skate = SkateController.Local;
            return skate == null || !skate.Riding || skate.player == null || skate.player.refs.controller != __instance;
        }
    }

    // Grab the raw input for the skate controls, then hide it from the walking code
    // (so the character stands still on the board instead of running in place).
    [HarmonyPatch(typeof(Player.PlayerInput), nameof(Player.PlayerInput.SampeInput))]
    internal static class Patch_SampleInput
    {
        private static void Postfix(Player.PlayerInput __instance, Player player)
        {
            var skate = SkateController.Local;
            if (skate == null || skate.player != player)
                return;

            skate.ReceiveInput(__instance);

            if (!skate.Riding)
                return;
            __instance.movementInput = UnityEngine.Vector2.zero;
            __instance.jumpWasPressed = false;
            __instance.sprintIsPressed = false;
            __instance.crouchWasPressed = false;
        }
    }

    // Lift the player up a bit so they stand on top of the board.
    [HarmonyPatch(typeof(PlayerAnimationHandler), "HandleAnimationTarget")]
    internal static class Patch_AnimTarget
    {
        private static void Postfix(PlayerAnimationHandler __instance)
        {
            var skate = SkateController.Local;
            if (skate == null || !skate.Riding || skate.player == null || skate.player.refs.animationHandler != __instance)
                return;
            if (skate.player.data.physicsAreReady)
                skate.player.data.targetHeight += SkatePlugin.RideHeight.Value + (SkatePlugin.RideBoardScale.Value - 1f) * 0.08f; // bigger board = a bit higher
        }
    }
}
