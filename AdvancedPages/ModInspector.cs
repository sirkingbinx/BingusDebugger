using System.Reflection;
using System.Text;

namespace BingusDebugger.AdvancedPages
{
    public class ModInspector
    {
        public string Name;
        public string Version;
        public string GUID;
        public string Types;

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
                    if (this.Types == null)
                    {
                        // this should fix the lag probably
                        StringBuilder types = new StringBuilder();

                        int tPerLine = 4;
                        string thisLine = "";

                        for (int i = 0; i < Source.GetTypes().Length; i++)
                        {
                            if (i % tPerLine == 0 && i != 0)
                            {
                                types.AppendLine(thisLine);
                                thisLine = "";
                            }
                            else
                            {
                                if (i == Source.GetTypes().Length - 1)
                                    thisLine += Source.GetTypes()[i].Name;
                                else
                                    thisLine += Source.GetTypes()[i].Name + ", ";
                            }
                        }

                        Types = types.ToString();
                    }

                    sb.AppendLine($"ASM: {Source.FullName}");
                    sb.AppendLine($"Ver: {Source.GetName().Version}");
                    sb.AppendLine($"Types: {this.Types}");

                    break;
            }

            sb.AppendLine($"<color=blue>\n= Y/B: CHANGE INSPECTED MOD =</color>\n");

            return sb.ToString();
        }

        public ModInspector(string n, string v, string guid, Assembly src)
        {
            Name = n;
            Version = v;
            GUID = guid;
            Source = src;
        }
    }
}
