using UnityEngine;

namespace ForestCombatV2
{
    // Title for the chapter 1 play build: new game / quit, and the chapter 2 placeholder after a clear.
    public sealed class Chapter1TitleScreen : MonoBehaviour
    {
        public Font uiFont;
        Chapter1RunHost host;

        void Start() { host = Chapter1RunHost.Ensure(); }

        public bool ExternalPresentation { get; set; }

        void OnGUI()
        {
            if (ExternalPresentation) return;
            if (!host) return;
            var oldFont = GUI.skin.font;
            try
            {
                if (uiFont) GUI.skin.font = uiFont;
                GUI.color = new Color(.12f, .1f, .08f, 1);
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
                GUI.color = Color.white;
                var title = new GUIStyle(GUI.skin.label) { fontSize = 48, alignment = TextAnchor.MiddleCenter };
                var line = new GUIStyle(GUI.skin.label) { fontSize = 20, alignment = TextAnchor.MiddleCenter };
                float x = (Screen.width - 260) / 2f, y = Screen.height * .5f;
                if (host.ChapterTwoPending)
                {
                    GUI.Label(new Rect(0, Screen.height * .28f, Screen.width, 60), "2챕터", title);
                    GUI.Label(new Rect(0, Screen.height * .28f + 70, Screen.width, 30), "준비 중입니다.", line);
                    if (GUI.Button(new Rect(x, y, 260, 52), "타이틀로")) host.LeaveChapterTwoPending();
                    return;
                }
                GUI.Label(new Rect(0, Screen.height * .28f, Screen.width, 60), "deviant", title);
                if (GUI.Button(new Rect(x, y, 260, 52), "시작하기")) host.GoToVillage(false);
                if (GUI.Button(new Rect(x, y + 66, 260, 52), "종료")) Quit();
            }
            finally { GUI.skin.font = oldFont; GUI.color = Color.white; }
        }

        static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
