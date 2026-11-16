using System.Linq;
using System.Text.RegularExpressions;
namespace Deviant.UI.Theme
{
    public static class ApprovedInkText
    {
        // Presentation only: keep catalog IDs, stat delimiters and editable names intact.
        public static string Display(string text)=>text?.Replace(" · "," / ").Replace("　·　","　/　")
            .Replace("·",", ").Replace("ㆍ",", ").Replace("・",", ");
        const string Values=@"(\d+(?:\.\d+)?(?:%|초)?)\(강화 (\d+(?:\.\d+)?(?:%|초)?)\)";
        public static string Value(string description,bool match)=>string.Join(" / ",Regex.Matches(description,Values).Cast<Match>().Select(m=>m.Groups[match?2:1].Value));
        public static string Description(string description,bool match)
        {
            string s=Regex.Replace(description,Values,m=>m.Groups[match?2:1].Value);
            int end=s.IndexOf(" 시",System.StringComparison.Ordinal);string prefix="";
            if(end>=0)
            {
                string trigger=s.Substring(0,end+2);s=s.Substring(end+2);
                if(trigger.StartsWith("이 무기로 ")){prefix="이 무기로 ";trigger=trigger.Substring(6);}
                trigger=trigger.Replace("적에게","</color>적에게<color=#ACD4E0>");
                prefix+="<color=#ACD4E0>"+trigger+"</color>";
            }
            s=Regex.Replace(s,@"(이동속도가 \d+(?:\.\d+)?% 증가|방어막을 \d+|체력을 \d+ 회복|공격 피해가 \d+(?:\.\d+)?% 증가|스킬 쿨타임 \d+(?:\.\d+)?%|스킬 피해가 \d+(?:\.\d+)?% 증가|\d+(?:\.\d+)?초 동안 (?:기절|속박)|\d+(?:\.\d+)?초|\d+(?:\.\d+)?% 감소)","<color=#B4D38D>$1</color>");
            return Display(prefix+s);
        }
    }
}
