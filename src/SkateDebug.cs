using System.Text;
using UnityEngine;

namespace CWSkate
{
    // What the developer tab's "ShowDebugInfo" prints in the corner of the screen.
    internal partial class SkateController
    {
        public string DebugInfo()
        {
            var sb = new StringBuilder();
            sb.Append("CW SKATE DEBUG\n");
            sb.Append("riding ").Append(Riding).Append("  bailed ").Append(bailed).Append("  gettingUp ").Append(getUpTimer.ToString("0.0")).Append('\n');
            sb.Append("speed ").Append(speed.ToString("0.0")).Append(" m/s (").Append(Mathf.RoundToInt(speed * 3.6f)).Append(" km/h)  heading ").Append(heading.ToString("0")).Append("  body ").Append(bodyYaw.ToString("0")).Append('\n');
            sb.Append("air ").Append(airborne).Append(" ").Append(airTime.ToString("0.00")).Append("s  ollie ").Append(ollieThisAir).Append("  vy ").Append(lastAirVy.ToString("0.0")).Append('\n');
            sb.Append("grind ").Append(grinding).Append("  wall ").Append(wallriding).Append("  monster ").Append(monsterGrinding).Append("  skitch ").Append(skitching).Append(train ? " (train)" : "").Append('\n');
            sb.Append("flip ").Append(flip).Append("  grab ").Append(grab).Append("  salto ").Append(salto).Append("  hippie ").Append(hippie).Append("  manual ").Append(manual).Append('\n');
            float bodyTilt = 0f;
            var torso = player != null ? player.refs.ragdoll.GetBodypart(BodypartType.Torso) : null;
            if (torso != null) bodyTilt = Vector3.Angle(Vector3.up, torso.transform.up);
            sb.Append("slope ").Append(Vector3.Angle(Vector3.up, boardUp).ToString("0")).Append("°  lean target ").Append(Vector3.Angle(Vector3.up, bodySlopeUp).ToString("0"))
              .Append("°  torso ").Append(bodyTilt.ToString("0")).Append("°  legs ghosted ").Append(legsGhosted).Append('\n');
            sb.Append("combo ").Append(combo.Count).Append(" tricks, ").Append(Mathf.RoundToInt(combo.Points)).Append(" pts\n");
            sb.Append("aura ").Append(Progress.Aura.ToString("N0")).Append("  ").Append(Aura.RankName(Progress.Aura)).Append('\n');
            sb.Append("board ").Append(Mathf.RoundToInt(BoardWear.Local * 100f)).Append("%  parts ").Append(BoardUpgrades.Local).Append(" (").Append(BoardUpgrades.Describe(BoardUpgrades.Local)).Append(")");
            if (HasRocket) sb.Append("  fuel ").Append(RocketFuel).Append(rocketOn ? " BURNING" : "");
            sb.Append('\n');
            sb.Append("escape ").Append(escapeTime.ToString("0.0")).Append("s  chaser ").Append(chaserDistance > 0f ? chaserDistance.ToString("0.0") + " m" : "-").Append('\n');
            sb.Append(DevTools.ActiveDealText());
            return sb.ToString();
        }
    }
}
