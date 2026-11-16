using UnityEngine;

namespace Deviant.Game
{
    // 2스테이지 보스 컷씬의 말풍선과 위아래 띠(docs/design/enemies/chapter2/rose-blue/chapter2-rose-blue-cutscenes.md). 무엇을 언제 보여 줄지는
    // RoseBlueEncounter 가 정하고(CutsceneLines, CutsceneBars), 여기서는 기존 MaintenanceBubbleView 로 그리기만 한다.
    // 말을 끊는 줄(cover)은 끊긴 말풍선의 위쪽 모서리에 꼬리 없이 겹친다(HTML 검토판과 같은 자리).
    public sealed partial class TemporaryGameGui
    {
        MaintenanceBubbleView bossCutsceneUnder, bossCutsceneOver;

        void RefreshBossCutscene()
        {
            var encounter = chapter2 && chapter2.InBossRoom ? chapter2.Boss : null;
            bool playing = encounter && encounter.InBossCutscene;
            if (!playing)
            {
                if (bossCutsceneUnder) { bossCutsceneUnder.Hide(); bossCutsceneUnder.SetBars(false); }
                if (bossCutsceneOver) bossCutsceneOver.Hide();
                return;
            }
            if (!bossCutsceneUnder)
            {
                bossCutsceneUnder = MaintenanceBubbleView.Create(chapter2.sceneCamera); bossCutsceneUnder.name = "Rose Blue cutscene bubble";
                bossCutsceneOver = MaintenanceBubbleView.Create(chapter2.sceneCamera); bossCutsceneOver.name = "Rose Blue cutscene cut-in bubble";
                bossCutsceneOver.GetComponent<Canvas>().sortingOrder = bossCutsceneUnder.GetComponent<Canvas>().sortingOrder + 1;
            }
            bossCutsceneUnder.SetBars(encounter.CutsceneBars);
            var lines = encounter.CutsceneLines;
            if (lines.Count == 0) { bossCutsceneUnder.Hide(); bossCutsceneOver.Hide(); return; }
            var first = lines[0];
            bossCutsceneUnder.TagColor = first.Tag;
            bossCutsceneUnder.Show(first.Text, first.Visible, first.Speaker, false, first.Head, Time.unscaledTime, false, 40);
            if (lines.Count < 2) { bossCutsceneOver.Hide(); return; }
            var second = lines[1];
            bossCutsceneOver.TagColor = second.Tag;
            if (second.CoverOf == first.Id)
            {
                // 끊긴 말풍선의 가까운 위 모서리에 28px 걸치고, 높이의 40% 만큼 위로 올라탄다.
                var under = bossCutsceneUnder.Shown; var size = bossCutsceneOver.Size(second.Text);
                var screen = chapter2.sceneCamera.WorldToViewportPoint(second.Head);
                bool right = screen.x * 960 >= under.x + under.width / 2f;
                int x = Mathf.RoundToInt(Mathf.Clamp(right ? under.x + under.width - 28 : under.x - size.x + 28, 8, 952 - size.x));
                int y = Mathf.RoundToInt(Mathf.Clamp(under.y - Mathf.Round(size.y * .4f), 10, 540 - 40 - size.y));
                bossCutsceneOver.ShowAt(second.Text, second.Visible, second.Speaker, false, x, y, -1);
            }
            else bossCutsceneOver.Show(second.Text, second.Visible, second.Speaker, false, second.Head, Time.unscaledTime, false, 40);
        }
    }
}
