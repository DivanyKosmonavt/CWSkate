using System;
using HarmonyLib;
using UnityEngine;

namespace CWSkate
{
    // Some emotes make no sense on a board (push-ups, a backflip that throws you off it): they're blocked while riding.
    internal static class EmoteBlock
    {
        private static string cachedSetting;
        private static string[] blocked = new string[0];
        private static float lastPopup = -10f;

        public static bool IsBlocked(Item emote)
        {
            if (emote == null) return false;
            string setting = SkatePlugin.BlockedEmotes.Value ?? "";
            if (setting != cachedSetting)
            {
                cachedSetting = setting;
                blocked = setting.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
                for (int i = 0; i < blocked.Length; i++) blocked[i] = blocked[i].Trim().ToLowerInvariant();
            }
            string anim = emote.emoteInfo != null ? emote.emoteInfo.animationName ?? "" : "";
            string names = (anim + "|" + emote.name + "|" + emote.displayName).ToLowerInvariant();
            foreach (var b in blocked)
            {
                if (b.Length == 0) continue;
                if (anim.ToLowerInvariant() == b || names.Contains(b)) return true;
            }
            return false;
        }

        private static bool Riding(Player p)
        {
            if (p == null) return false;
            if (p.IsLocal) return SkateController.Local != null && SkateController.Local.player == p && SkateController.Local.Riding;
            return NetSync.GetState(p) == NetSync.StateRiding;
        }

        // our own emote key: don't even send it (the game would ask every frame while the key is held)
        [HarmonyPatch(typeof(PlayerEmotes), "Update")]
        private static class Patch_EmoteKey
        {
            private static bool Prefix(PlayerEmotes __instance)
            {
                try
                {
                    var p = __instance.GetComponent<Player>();
                    if (p == null || !p.IsLocal || !p.input.emoteIsPressed || p.data.emoteTime > 0f || !Riding(p)) return true;
                    if (!p.TryGetInventory(out var inv) || !IsBlocked(inv.emote)) return true;
                    if (Time.time - lastPopup > 2f)
                    {
                        lastPopup = Time.time;
                        SkateHUD.Popup(Lang.T("НА ДОСКЕ ТАК НЕЛЬЗЯ", "CAN'T DO THAT ON A BOARD"), new Color(1f, 0.7f, 0.4f), 1.6f, small: true);
                    }
                    return false;
                }
                catch { return true; }
            }
        }

        // anyone's emote (they may have a different emote than us): skip it if they're on a board
        [HarmonyPatch(typeof(PlayerEmotes), "DoEmote")]
        private static class Patch_DoEmote
        {
            private static bool Prefix(PlayerEmotes __instance, Item emote)
            {
                try
                {
                    var p = __instance.GetComponent<Player>();
                    return !(Riding(p) && IsBlocked(emote));
                }
                catch { return true; }
            }
        }
    }
}
