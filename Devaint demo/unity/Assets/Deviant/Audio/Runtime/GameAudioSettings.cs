using UnityEngine;

namespace Deviant.Music
{
    // The player's volume (playtest 2026-11-04): 전체 scales every sound through the listener, 음악 the background music
    // player on top of it. Steps of 10%, kept per device in PlayerPrefs, apart from the run and profile saves.
    public static class GameAudioSettings
    {
        public const string MasterKey = "deviant.audio.master", MusicKey = "deviant.audio.music";
        public const float Step = .1f;
        public static float Master { get; private set; } = 1;
        public static float Music { get; private set; } = 1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Load()
        {
            Master = Snap(PlayerPrefs.GetFloat(MasterKey, 1));
            Music = Snap(PlayerPrefs.GetFloat(MusicKey, 1));
            Apply();
        }

        public static float Snap(float value) => Mathf.Round(Mathf.Clamp01(value) * 10) / 10f;
        public static void SetMaster(float value) { Master = Snap(value); PlayerPrefs.SetFloat(MasterKey, Master); PlayerPrefs.Save(); Apply(); }
        public static void SetMusic(float value) { Music = Snap(value); PlayerPrefs.SetFloat(MusicKey, Music); PlayerPrefs.Save(); Apply(); }
        public static string Percent(float value) => Mathf.RoundToInt(value * 100) + "%";

        public static void Apply()
        {
            AudioListener.volume = Master;
            var player = BackgroundMusicPlayer.Instance;
            if (player) player.volume = Music;
        }
    }
}
