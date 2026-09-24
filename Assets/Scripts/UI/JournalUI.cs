using System.Text;
using TMPro;

namespace RPG
{
    /// <summary>"Bách Khoa Trùm" window (J): creatures met, kills and recorded skills.</summary>
    public class JournalUI : UIPanel
    {
        public TextMeshProUGUI body;

        protected override void OnShow()
        {
            if (body == null) return;
            var sb = new StringBuilder();
            bool any = false;
            foreach (var e in Bestiary.All)
            {
                any = true;
                sb.Append($"<color=#ffe07a><b>{e.name}</b></color>   <color=#9a93a8>đã hạ: {e.kills}</color>\n");
                if (e.skills.Count > 0)
                    sb.Append("<color=#cfc4ff>  Kỹ năng: </color>" + string.Join(", ", e.skills) + "\n");
                sb.Append("\n");
            }
            if (!any) sb.Append("<color=#9a93a8>Chưa ghi chép được gì. Hãy khám phá Rừng Thì Thầm!</color>");
            body.text = sb.ToString();
        }
    }
}
