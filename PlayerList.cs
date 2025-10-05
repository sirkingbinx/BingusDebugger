using UnityEngine;

namespace BingusDebugger
{
    public class PlayerList
    {
        public static VRRig GetRigFromPlayer(NetPlayer player) => GorillaGameManager.instance.FindPlayerVRRig(player);

        public static string GetPlayerString(NetPlayer player)
        {
            string PlrString = "";
            bool talking = false;

            VRRig rig = GetRigFromPlayer(player);
            string hexCode = ColorUtility.ToHtmlStringRGB(rig.playerColor);

            talking = rig.SpeakingLoudness > 0.15f;

            PlrString = $"<color=#{hexCode}>{player.NickName}</color> {(talking ? "<color=green>#</color>" : " ")}";

            return PlrString;
        }

        public static string MakeSpaces(string thestring, int target)
        {
            string complete = thestring;

            for (int i = thestring.Length; i < target; i++)
                complete += " ";

            return complete;
        }
    }
}
