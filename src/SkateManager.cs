using UnityEngine;

namespace CWSkate
{
    // Persistent object: attaches the skate components to players and draws the HUD.
    internal class SkateManager : MonoBehaviour
    {
        private float scanTimer;

        private void Update()
        {
            try
            {
                SkateItem.EnsureRegistered();
                SkateParts.EnsureRegistered(); // (after the board: everyone registers in the same order)
                SkateDeal.EnsureRegistered();
                NetSync.EnsureSubscribed();
                SettingsMenu.Tick();
                Lang.Tick();
                ThirdPerson.Tick();
                Progress.Tick();
                Skins.Tick();
                NetSync.TickAura();
                Melee.Tick();
                Boom.Tick();
                var lp = Player.localPlayer;
                if (lp != null && lp.data != null && lp.data.playerSetUpAndReady && lp.GetComponent<SkateController>() == null)
                    lp.gameObject.AddComponent<SkateController>();

                scanTimer -= Time.unscaledDeltaTime;
                if (scanTimer <= 0f)
                {
                    scanTimer = 0.5f;
                    var ph = PlayerHandler.instance;
                    if (ph != null && ph.players != null)
                    {
                        foreach (var p in ph.players)
                        {
                            if (p == null || p.ai || p == lp || p.refs.view == null || p.refs.view.IsMine) continue;
                            if (!p.data.playerSetUpAndReady) continue;
                            if (p.GetComponent<RemoteBoard>() == null) p.gameObject.AddComponent<RemoteBoard>();
                        }
                    }
                }
            }
            catch (System.Exception e)
            {
                SkatePlugin.Log.LogError(e);
            }
        }

        private void LateUpdate() => SettingsMenu.ShowCursor();

        private void OnGUI()
        {
            try { Mockery.Draw(); }
            catch (System.Exception e) { SkatePlugin.Log.LogError(e); }
            try { RankTags.Draw(); }
            catch (System.Exception e) { SkatePlugin.Log.LogError(e); }
            try { SkateHUD.Draw(SkateController.Local); }
            catch (System.Exception e) { SkatePlugin.Log.LogError(e); }
            try { Records.Draw(); }
            catch (System.Exception e) { SkatePlugin.Log.LogError(e); }
            try { SettingsMenu.Draw(); }
            catch (System.Exception e) { SkatePlugin.Log.LogError(e); }
        }
    }
}
