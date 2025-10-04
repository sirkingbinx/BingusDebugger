using System.Reflection;
using System.Text;
using BingusDebugger;

namespace BingusDebugger
{
    public class ModInfo
    {
        public string Name;
        public string Version;
        public string GUID;

        public Assembly Source;

        public string UpdateDebugPage(Plugin.ModInspectorMode page)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"<color=blue>======== MOD INSPECTOR ========</color>\n");

            switch (page)
            {
                case Plugin.ModInspectorMode.ModInfo:
                    sb.AppendLine($"Name: {Name}");
                    sb.AppendLine($"Version: {Version}");
                    sb.AppendLine($"GUID: {GUID}");
                    sb.AppendLine($"<color=yellow>ASM: {Source.GetName().Name}</color>");
                    break;
                case Plugin.ModInspectorMode.ASMInfo:
                    StringBuilder types = new StringBuilder();

                    int tPerLine = 4;
                    string thisLine = "";

                    for (int i = 0; i < Source.GetTypes().Length; i++)
                    {
                        if (i % tPerLine == 0 && i != 0)
                        {
                            types.AppendLine(thisLine);
                            thisLine = "";
                        } else
                        {
                            if (i == Source.GetTypes().Length - 1)
                                thisLine += Source.GetTypes()[i].Name;
                            else
                                thisLine += Source.GetTypes()[i].Name + ", ";
                        }
                    }

                    sb.AppendLine($"ASM: {Source.FullName}");
                    sb.AppendLine($"Ver: {Source.GetName().Version}");
                    sb.AppendLine($"Types: {types}");
                    break;
            }

            sb.AppendLine($"<color=blue>\n= Y/B: CHANGE INSPECTED MOD =</color>\n");

            return sb.ToString();
        }

        public ModInfo(string n, string v, string guid, Assembly src)
        {
            Name = n;
            Version = v;
            GUID = guid;
            Source = src;
        }
    }
}
