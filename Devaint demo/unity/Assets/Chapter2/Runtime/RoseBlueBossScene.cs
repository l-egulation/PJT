using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Deviant.Chapter2
{
    // Dedicated testing entry. Reloading gives every phase a fresh encounter and player.
    public sealed class RoseBlueBossScene : MonoBehaviour
    {
        public const string ScenePath = "Assets/Chapter2/Scenes/RoseBlueBoss.unity";
        static int requestedPhase;
        Chapter2World world;
        bool loading;

        IEnumerator Start()
        {
            while (!(world = FindAnyObjectByType<Chapter2World>()) || !world.Ready)
                yield return null;
            foreach (var actor in world.Actors.Values)
                if (actor && !(actor is RoseBlueActor))
                    actor.gameObject.SetActive(false);
            world.WarpToRoom("B");
            if (requestedPhase != 0)
            {
                int phase = requestedPhase;
                requestedPhase = 0;
                world.Boss.StartTestPhase(phase);
            }
            Debug.Log("ROSE_BLUE_BOSS_SCENE_STARTED: dedicated boss scene, normal encounters disabled.");
        }

        public void SelectPhase(int phase)
        {
            if (phase < 1 || phase > 3)
                throw new System.ArgumentOutOfRangeException(nameof(phase));
            if (loading) return;
            loading = true;
            requestedPhase = phase;
#if UNITY_EDITOR
            if (!Application.isBatchMode && UnityEditor.EditorApplication.isPaused) UnityEditor.EditorApplication.isPaused = false;
#endif
            SceneManager.LoadScene(gameObject.scene.path);
        }

        void OnGUI()
        {
            if (!world || !world.Ready) return;
            var previousMatrix = GUI.matrix;
            var previousFont = GUI.skin.font;
            bool previousEnabled = GUI.enabled;
            if (world.arena.uiFont) GUI.skin.font = world.arena.uiFont;
            GUI.matrix = Matrix4x4.Scale(Vector3.one * Mathf.Max(.7f, Screen.height / 720f));
            GUILayout.BeginArea(new Rect(12, 170, 300, 118), GUI.skin.box);
            GUILayout.Label("보스 테스트 / 페이즈 바로 시작");
            GUI.enabled = !loading;
            GUILayout.BeginHorizontal();
            for (int phase = 1; phase <= 3; phase++)
                if (GUILayout.Button(phase + "페이즈", GUILayout.Height(34)))
                    SelectPhase(phase);
            GUILayout.EndHorizontal();
            GUILayout.Label("선택하면 체력, 공격 상태를 초기화합니다.");
            GUI.enabled = previousEnabled;
            GUILayout.EndArea();
            GUI.matrix = previousMatrix;
            GUI.skin.font = previousFont;
        }
    }
}
