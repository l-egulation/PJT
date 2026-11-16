using UnityEngine;
using UnityEngine.SceneManagement;

namespace Deviant.Game
{
    public sealed class GameEntry : MonoBehaviour
    {
        public const string ScenePath = "Assets/ForestCombatV2/Scenes/Chapter1Title.unity";
        void Start() { SceneManager.LoadScene(ScenePath, LoadSceneMode.Single); }
    }
}
