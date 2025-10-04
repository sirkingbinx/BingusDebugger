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
            sb.AppendLine($"<color=aqua>======== MOD INSPECTOR ========</color>\n");

            switch (page)
            {
                case Plugin.ModInspectorMode.ModInfo:
                    sb.AppendLine($"Name: {Name}");
                    sb.AppendLine($"Version: {Version}");
                    sb.AppendLine($"GUID: {GUID}");
                    sb.AppendLine($"<color=yellow>ASM: {Source.GetName().Name}</color>");
                    break;
                case Plugin.ModInspectorMode.ASMInfo:
                    sb.AppendLine($"Assembly: {Source.FullName}");
                    sb.AppendLine($"\t@ {Source.Location}");
                    break;
            }

            sb.AppendLine($"<color=aqua>\n= Y/B: CHANGE INSPECTED MOD =</color>\n");

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
