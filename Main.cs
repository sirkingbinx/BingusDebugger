using BepInEx;
using GorillaLocomotion;
using GorillaNetworking;
using HarmonyLib;
using Photon.Pun;
using System;
using System.Collections.Generic;
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

        List<ModInfo> Mods = new List<ModInfo>();
        ModInfo Selected = null;

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

        private Quaternion lastHeadQuat = Quaternion.identity;

        bool desync = false;
        int desyncFrames = 0;

        bool steam = false;

        bool init = false;
        bool visible = true;

        public void Start()
        {
            Harmony.CreateAndPatchAll(Assembly.GetAssembly(typeof(Plugin)));
            GorillaTagger.OnPlayerSpawned(GorillaInitialize);
        }

        public void GorillaInitialize() {
            init = true;

            Application.logMessageReceived += AddLogMessage;
            steam = PlayFabAuthenticator.instance.platform.PlatformTag.ToUpper().Contains("STEAM");

            foreach (PluginInfo Mod in BepInEx.Bootstrap.Chainloader.PluginInfos.Values)
            {
                ModInfo mi = new ModInfo(
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

        public void OnEnable() => visible = true;
        public void OnDisable() => visible = false;

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

        void Update()
        {
            if (Null(DebugCanvasObject) || Null(DebugCanvasText) || Null(DebugCanvasTextObject))
            {
                DebugCanvasObject = GameObject.Find("Player Objects/Player VR Controller/GorillaPlayer/TurnParent/Main Camera/DebugCanvas");
                DebugCanvasObject.GetComponent<DebugHudStats>().Destroy();

                DebugCanvasTextObject = GameObject.Find("Player Objects/Player VR Controller/GorillaPlayer/TurnParent/Main Camera/DebugCanvas/Text (TMP)");
                DebugCanvasText = DebugCanvasTextObject.GetComponent<TMP_Text>();
            }

            DebugCanvasObject?.SetActive(visible);
            DebugCanvasTextObject?.SetActive(visible);

            if (!init || !visible) return;

            if (NetworkSystem.Instance.InRoom && Quaternion.Angle(PlayerRig(NetworkSystem.Instance.MasterClient).headMesh.transform.rotation, lastHeadQuat) <= 0.01f)
                desyncFrames++;
            else
                desyncFrames = 0;

            desync = (desyncFrames > 256);

            if (NetworkSystem.Instance.InRoom)
                lastHeadQuat = PlayerRig(NetworkSystem.Instance.MasterClient).headMesh.transform.rotation;

            float fpsF = Mathf.Round(1f / Time.smoothDeltaTime);

            StringBuilder DebugText = new StringBuilder();

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

                    break;
                case HUDDisplayType.PlayerList:
                    if (!PhotonNetwork.InRoom)
                    {
                        DebugText.AppendLine("<color=red>NOT CONNECTED TO A ROOM</color>");
                    } else
                    {
                        int width = 30;
                        int playersPerPage = 2;
                        int players = 0;

                        foreach (NetPlayer player in NetworkSystem.Instance.AllNetPlayers)
                        {
                            players++;
                            if (player == NetworkSystem.Instance.LocalPlayer) continue;
                            if (players % playersPerPage == 0 && players < NetworkSystem.Instance.RoomPlayerCount)
                                DebugText.Append($"{PlayerList.MakeSpaces(PlayerList.GetPlayerString(player), width / 2)}  ");
                            else
                                DebugText.Append($"{PlayerList.MakeSpaces(PlayerList.GetPlayerString(player), width / 2)}\n");
                        }
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

                    if (steam)
                        lJoystickAxis = SteamVR_Actions.gorillaTag_LeftJoystick2DAxis.axis;
                    else
                        lJoystickAxis = ControllerInputPoller.Primary2DAxis(lNode);

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

            if (desync)
                DebugText.Append("<color=red>DESYNC</color>  ");

            if (fpsF < 60)
                DebugText.Append("<color=red>LOW FPS</color>  ");

            if (NetworkSystem.Instance.InRoom && NetworkSystem.Instance.IsMasterClient)
                DebugText.Append("<color=grey>MASTER</color>  ");

            DebugText.Append("\n");

            DebugCanvasText.text = DebugText.ToString();

            // Change display modes
            if (ControllerInputPoller.instance.leftControllerPrimaryButton && pausePageMoveUntil == 0)
            {
                pausePageMoveUntil = 10;

                if (steam)
                    rJoystickAxis = SteamVR_Actions.gorillaTag_RightJoystick2DAxis.axis;
                else
                    rJoystickAxis = ControllerInputPoller.Primary2DAxis(rNode);

                if (rJoystickAxis.x > 0.9f && yInRange(rJoystickAxis.y))
                    display = display.EnumNext();
                else if (rJoystickAxis.x < -0.9f && yInRange(rJoystickAxis.y))
                    display = display.EnumLast();
            }

            pausePageMoveUntil = pausePageMoveUntil > 0 ? pausePageMoveUntil - 1 : 0;
        }

        private bool yInRange(float y) => (y < 0.25 && y > -0.25);
        private bool xInRange(float x) => (x < 0.25 && x > -0.25);
    }
}
