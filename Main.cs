using BepInEx;
using BingusDebugger.AdvancedPages;
using GorillaLocomotion;
using GorillaNetworking;
using HarmonyLib;
using Photon.Pun;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.XR;
using Valve.VR;

namespace BingusDebugger
{
    [BepInPlugin("bingus.debugger", "BingusDebugger", "1.2.0")]
    public class Plugin : BaseUnityPlugin
    {
        GameObject DebugCanvasObject = null;
        GameObject DebugCanvasTextObject = null;
        TMP_Text DebugCanvasText = null;

        List<ModInspector> Mods = new List<ModInspector>();
        ModInspector Selected = null;

        List<string> Log = new List<string>();

        public enum HUDDisplayType {
            Lobby,
            PlayerList,
            Log,
            ModInspector,
            VersionInfo,
        }
        
        public enum ModInspectorMode
        {
            ModInfo,
            ASMInfo,
        }

        public enum PlayerImportance
        {
            MasterClient,
            AACreator, // N/A
            FingerPainter,
            Illustrator,
            ForestGuide, // N/A
            Developer,
            AdminBadge,
            Modder,
        }

        private Quaternion lastHeadQuat = Quaternion.identity;

        bool desync = false;
        int desyncFrames = 0;

        bool steam = false;

        public void Start()
        {
            Harmony.CreateAndPatchAll(Assembly.GetAssembly(typeof(Plugin)));
            GorillaTagger.OnPlayerSpawned(GorillaInitialize);
        }

        public void GorillaInitialize() {
            initialized = true;

            Application.logMessageReceived += AddLogMessage;
            steam = PlayFabAuthenticator.instance.platform.PlatformTag.ToUpper().Contains("STEAM");

            foreach (PluginInfo Mod in BepInEx.Bootstrap.Chainloader.PluginInfos.Values)
            {
                ModInspector mi = new ModInspector(
                    Mod.Metadata.Name,
                    Mod.Metadata.Version.ToString(),
                    Mod.Metadata.GUID,
                    Mod.Instance.GetType().Assembly
                );

                Mods.Add(mi);
            }

            Selected = Mods[0];
        }

        public void AddLogMessage(string condition, string _, LogType messageType)
        {
            string text = condition;
            int charLimit = 45;

            if (text.Length > charLimit)
                text = text.Substring(0, charLimit) + "...";

            switch (messageType)
            {
                case LogType.Error:
                    Log.Add($"<color=red>[ERR]: {text}</color>");
                    break;
                case LogType.Exception:
                    Log.Add($"<color=red>[EXC]: {text}</color>");
                    break;
                case LogType.Warning:
                    Log.Add($"<color=yellow>[WRN]: {text}</color>");
                    break;
                case LogType.Log:
                    Log.Add($"<color=white>[MSG]: {text}</color>");
                    break;
                default:
                    Log.Add($"<color=grey>[?MSG]: {text}</color>");
                    break;
            }

            if (Log.Count > 8)
                Log.RemoveAt(0);
        }

        private static VRRig PlayerRig(NetPlayer p) =>
            GorillaGameManager.instance.FindPlayerVRRig(p);

        private static bool Null(object obj) =>
            obj == null;

        private HUDDisplayType display = HUDDisplayType.Lobby;

        private XRNode lNode = XRNode.LeftHand;
        private Vector2 lJoystickAxis = Vector2.zero;

        private XRNode rNode = XRNode.RightHand;
        private Vector2 rJoystickAxis = Vector2.zero;

        private int pausePageMoveUntil = 0;

        private ModInspectorMode modinspMode = ModInspectorMode.ModInfo;

        private int selectedPlayerIndex = 0;

        public bool initialized = false;

        void Update()
        {
            if (!initialized) return;
            if (Null(DebugCanvasObject) || Null(DebugCanvasText) || Null(DebugCanvasTextObject))
            {
                DebugCanvasObject = GameObject.Find("Player Objects/Player VR Controller/GorillaPlayer/TurnParent/Main Camera/DebugCanvas");
                DebugCanvasObject.GetComponent<DebugHudStats>().Destroy();

                DebugCanvasTextObject = GameObject.Find("Player Objects/Player VR Controller/GorillaPlayer/TurnParent/Main Camera/DebugCanvas/Text (TMP)");
                DebugCanvasText = DebugCanvasTextObject.GetComponent<TMP_Text>();
            }

            DebugCanvasObject?.SetActive(true);
            DebugCanvasTextObject?.SetActive(true);

            if (NetworkSystem.Instance.InRoom && Quaternion.Angle(PlayerRig(NetworkSystem.Instance.MasterClient).headMesh.transform.rotation, lastHeadQuat) <= 0.01f)
                desyncFrames++;
            else
                desyncFrames = 0;

            desync = (desyncFrames > 256);

            if (NetworkSystem.Instance.InRoom)
                lastHeadQuat = PlayerRig(NetworkSystem.Instance.MasterClient).headMesh.transform.rotation;

            float fpsF = Mathf.Round(1f / Time.smoothDeltaTime);

            StringBuilder DebugText = new StringBuilder();

            if (steam)
            {
                lJoystickAxis = SteamVR_Actions.gorillaTag_LeftJoystick2DAxis.axis;
                rJoystickAxis = SteamVR_Actions.gorillaTag_RightJoystick2DAxis.axis;
            }
            else
            {
                lJoystickAxis = ControllerInputPoller.Primary2DAxis(lNode);
                rJoystickAxis = ControllerInputPoller.Primary2DAxis(rNode);
            }

            switch (display)
            {
                case HUDDisplayType.Lobby:
                    if (NetworkSystem.Instance.InRoom && NetworkSystem.Instance.SessionIsPrivate)
                        DebugText.Append($"PRIVATE<color=grey>:</color> {NetworkSystem.Instance.RoomName} <color=grey>({NetworkSystem.Instance.RoomPlayerCount}/10)</color>\n");
                    else if (NetworkSystem.Instance.InRoom)
                        DebugText.Append($"PUBLIC<color=grey>:</color> {NetworkSystem.Instance.RoomName} <color=grey>({NetworkSystem.Instance.RoomPlayerCount}/10)</color>\n");
                    else
                        DebugText.Append($"<color=red>DISCONNECTED</color> (1/1)\n");

                    string Speed = $"{MathF.Round(GTPlayer.Instance.AveragedVelocity.magnitude, 1)} </color=grey>m/s</color>";
                    string FPS = $"{fpsF} </color=grey>fps</color>";
                    DebugText.Append($"{FPS} | {Speed}\n");

                    if (desync)
                        DebugText.Append("<color=red>DESYNC</color>  ");

                    if (fpsF < 60)
                        DebugText.Append("<color=red>LOW FPS</color>  ");

                    if (NetworkSystem.Instance.InRoom && NetworkSystem.Instance.IsMasterClient)
                        DebugText.Append("<color=grey>MASTER</color>  ");

                    break;
                case HUDDisplayType.PlayerList:
                    if (!PhotonNetwork.InRoom)
                    {
                        DebugText.AppendLine("<color=grey>NOT CONNECTED TO A ROOM</color>");
                    } else
                    {
                        if ((lJoystickAxis.y > 0.9f && xInRange(lJoystickAxis.x)) && pausePageMoveUntil == 0)
                        {
                            pausePageMoveUntil = 15;
                            selectedPlayerIndex++;
                        }
                        else if ((lJoystickAxis.y < -0.9f && xInRange(lJoystickAxis.x)) && pausePageMoveUntil == 0)
                        {
                            pausePageMoveUntil = 15;
                            selectedPlayerIndex--;
                        }

                        NetPlayer player = NetworkSystem.Instance.AllNetPlayers[selectedPlayerIndex];

                        if (player == null | !player.InRoom())
                        {
                            if (selectedPlayerIndex >= NetworkSystem.Instance.AllNetPlayers.Count())
                            {
                                selectedPlayerIndex = 0;
                            }
                            else if (selectedPlayerIndex < 0)
                            {
                                selectedPlayerIndex = NetworkSystem.Instance.AllNetPlayers.Count() - 1;
                            }
                            else
                            {
                                Debug.LogWarning($"[BGDEBUG]: Attempted to grab player at index {selectedPlayerIndex}, player count is {NetworkSystem.Instance.AllNetPlayers.Count()}.");
                                selectedPlayerIndex = 0;
                            }
                        }

                        VRRig rig = GetRigFromPlayer(player);
                        string hexCode = ColorUtility.ToHtmlStringRGB(rig.playerColor);
                        string specialStuff = ImportanceToString(GetPlayerImportances(player), 30);

                        // LOUDNESS BAR
                        // -------- XX% (segmented in 8 pieces)
                        // >>------ XX% (loudness indicated by green & bold > symbols
                        // XX% is percentage of max volume
                        StringBuilder loudnessIndicator = new StringBuilder("--------");

                        for (int i = 0; i < (rig.SpeakingLoudness) * (i + 1); i++) {
                            loudnessIndicator.Remove(i, 1);
                            string c = ">";
                            loudnessIndicator.Insert(i, $"<color=green>{c}</color>");
                        }

                        loudnessIndicator.Insert(0, "<color=red>");
                        loudnessIndicator.Append("</color>");
                        // ik real weird looking lmao
                        
                        DebugText.AppendLine($"<color=#{hexCode}>██ {player.NickName}</color>  {loudnessIndicator} <color=grey>{Mathf.FloorToInt(rig.SpeakingLoudness * 100)}%</color>");
                        DebugText.AppendLine($"<color=grey>Platform: {GetPlatformStringOfPlayer(player)}</color>");

                        DebugText.Append(specialStuff);
                    }

                    break;
                case HUDDisplayType.Log:
                    foreach (string log in Log)
                        DebugText.Append($"{log}\n");

                    break;
                case HUDDisplayType.ModInspector:
                    if (ControllerInputPoller.instance.rightControllerSecondaryButton && pausePageMoveUntil == 0)
                    {
                        pausePageMoveUntil = 30;

                        int j = Mods.IndexOf(Selected) + 1;
                        Selected = (Mods.Count == j) ? Mods[0] : Mods[j];
                    }

                    if (ControllerInputPoller.instance.leftControllerSecondaryButton && pausePageMoveUntil == 0)
                    {
                        pausePageMoveUntil = 30;

                        int j = Mods.IndexOf(Selected) - 1;
                        Selected = (j < 0) ? Mods[Mods.Count - 1] : Mods[j];
                    }

                    if ((lJoystickAxis.y > 0.9f && xInRange(lJoystickAxis.x)) && pausePageMoveUntil == 0)
                    {
                        pausePageMoveUntil = 30;
                        modinspMode = modinspMode.EnumNext();
                    }
                    else if ((lJoystickAxis.y < -0.9f && xInRange(lJoystickAxis.x)) && pausePageMoveUntil == 0)
                    {
                        pausePageMoveUntil = 30;
                        modinspMode = modinspMode.EnumLast();
                    }

                    DebugText.Append(Selected.UpdateDebugPage(modinspMode));

                    break;
                case HUDDisplayType.VersionInfo:
                    DebugText.Append($"BingusDebugger v{Info.Metadata.Version}\n");
                    DebugText.Append($"GT Version: {Application.version}\n");
                    DebugText.Append($"Unity Version: {Application.unityVersion}\n");
                    DebugText.Append($"Mods: {BepInEx.Bootstrap.Chainloader.PluginInfos.Count}\n");
                    DebugText.Append($"\n");

                    break;
                default:
                    display = HUDDisplayType.Lobby;
                    break;
            }

            // make highlighted player's name green cuz its cool looking
            if (PhotonNetwork.InRoom)
            {
                foreach (NetPlayer p in NetworkSystem.Instance.AllNetPlayers)
                    if ((p == NetworkSystem.Instance.AllNetPlayers[selectedPlayerIndex]) && display == HUDDisplayType.PlayerList)
                        GetRigFromPlayer(p).SetNameTagText($"<color=green>{p.NickName}</color>");
                    else
                        GetRigFromPlayer(p).SetNameTagText(p.NickName);
            }

            DebugText.Append("\n");

            DebugCanvasText.text = DebugText.ToString();

            // Change display modes
            if (ControllerInputPoller.instance.leftControllerPrimaryButton && pausePageMoveUntil == 0)
            {
                pausePageMoveUntil = 10;

                if (rJoystickAxis.x > 0.9f && yInRange(rJoystickAxis.y))
                    display = display.EnumNext();
                else if (rJoystickAxis.x < -0.9f && yInRange(rJoystickAxis.y))
                    display = display.EnumLast();
            }

            pausePageMoveUntil = pausePageMoveUntil > 0 ? pausePageMoveUntil - 1 : 0;
        }

        private bool yInRange(float y) => (y < 0.25 && y > -0.25);
        private bool xInRange(float x) => (x < 0.25 && x > -0.25);

        private VRRig GetRigFromPlayer(NetPlayer player) => GorillaGameManager.instance.FindPlayerVRRig(player);
        private List<PlayerImportance> GetPlayerImportances(NetPlayer player)
        {
            List<PlayerImportance> importances = new List<PlayerImportance>();
            VRRig rig = GetRigFromPlayer(player);

            if (player == NetworkSystem.Instance.MasterClient)
                importances.Add(PlayerImportance.MasterClient);
            if (rig.concatStringOfCosmeticsAllowed.Contains("LBADE."))
                importances.Add(PlayerImportance.FingerPainter);
            if (rig.concatStringOfCosmeticsAllowed.Contains("LBAGS."))
                importances.Add(PlayerImportance.Illustrator);
            if (rig.concatStringOfCosmeticsAllowed.Contains("LBAAD."))
                importances.Add(PlayerImportance.AdminBadge);
            if (rig.concatStringOfCosmeticsAllowed.Contains("LBAAK."))
                importances.Add(PlayerImportance.Developer);
            if (rig.Creator.GetPlayerRef().CustomProperties.Count > 1)
                importances.Add(PlayerImportance.Modder);

            return importances;
        }

        private string ImportanceToString(List<PlayerImportance> importances, int maxCharPerLine = 30)
        {
            StringBuilder sb = new StringBuilder();
            string thisLine = "";

            foreach (PlayerImportance pi in importances)
            {
                string rawInfo = pi.ToString().ToUpper();
                string addition = "";

                if (rawInfo == "MASTERCLIENT")
                    addition = "<color=grey>MASTER</color>";
                else if (rawInfo == "AACREATOR")
                    addition = "<color=purple>AAC</color>";
                else if (rawInfo == "FINGERPAINTER")
                    addition = "<color=cyan>FP</color>";
                else if (rawInfo == "ILLUSTRATOR")
                    addition = "<color=orange>ILLST</color>";
                else if (rawInfo == "FORESTGUIDE")
                    addition = "<color=green>GUIDE</color>";
                else if (rawInfo == "DEVELOPER")
                    addition = "<color=red>AA</color>";
                else if (rawInfo == "ADMINBADGE")
                    addition = "<color=white>ADMIN</color>";
                else if (rawInfo == "MODDER")
                    addition = "<color=purple>MODDER</color>";

                if (thisLine.Count() + addition.Count() > maxCharPerLine)
                {
                    sb.AppendLine(thisLine[..(thisLine.Count() - 2)]);
                    thisLine = addition + ", ";
                }
                else
                {
                    thisLine += addition + ", ";
                }
            }
            if (thisLine != "")
                sb.AppendLine(thisLine[..(thisLine.Count() - 2)]);

            return sb.ToString();
        }

        private string GetPlatformStringOfPlayer(NetPlayer player)
        {
            VRRig rig = GetRigFromPlayer(player);
            string concatStringOfCosmeticsAllowed = rig.concatStringOfCosmeticsAllowed;
            string result;

            if (concatStringOfCosmeticsAllowed.Contains("S. FIRST LOGIN"))
                result = "<color=#2a475e>SteamVR</color>";
            else if (concatStringOfCosmeticsAllowed.Contains("FIRST LOGIN") | rig.Creator.GetPlayerRef().CustomProperties.Count > 1)
                result = "<color=#0059FF>Oculus</color>";
            else
                result = "<color=#0081FB>Meta</color>";

            return result;
        }
    }
}
