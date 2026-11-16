using ForestCombatV2;
using UnityEngine;

namespace Deviant.Game
{
    // The village artisan speaks with the maintenance shops' voice (docs/design/progression/village-palette-pieces.md): the same
    // person who runs chapter 1's workshop, so its lines click with that synthesized letter sound. The bubble itself is the
    // one every speaker uses (TemporaryGameGui.Opening).
    public sealed partial class TemporaryGameGui
    {
        const string ArtisanSpeaker = "artisan";
        const float ArtisanVoiceCutoff = 1450, ArtisanVoiceGap = .055f;
        bool artisanVoiceLoaded;
        AudioSource artisanVoiceSource;
        AudioClip artisanVoice;
        float artisanVoiceVolume, artisanVoiceAt;
        int artisanSpoken;

        void StartArtisanLine()
        {
            artisanSpoken = 0;
            if (bubbleSpeaker != ArtisanSpeaker || artisanVoiceLoaded) return;
            artisanVoiceLoaded = true;
            var json = Resources.Load<TextAsset>("MaintenanceShops/MaintenanceDialogue");
            var content = json ? JsonUtility.FromJson<MaintenanceRoomPresentation.Content>(json.text) : null;
            if (content?.speechPitch != null && content.speechPitch.Length > 0)
            {
                artisanVoice = MaintenanceRoomPresentation.SynthesizeVoice(content.speechPitch[0], ArtisanVoiceCutoff);
                artisanVoiceVolume = content.speechVolume;
                artisanVoiceSource = gameObject.AddComponent<AudioSource>(); artisanVoiceSource.playOnAwake = false; artisanVoiceSource.spatialBlend = 0;
            }
            else Debug.LogWarning("Maintenance dialogue voice missing: the village artisan speaks without a letter sound.");
        }

        // Typed like the workshop: a soft click on every second letter (not spaces or punctuation), at most every 0.055 s.
        // A line shown whole at once is silent.
        void TickArtisanLine(int chars)
        {
            float now = Time.unscaledTime;
            if (artisanVoice && chars > artisanSpoken && chars - artisanSpoken <= 2 && chars % 2 == 0)
            {
                char letter = bubbleLine[chars - 1];
                if (!char.IsWhiteSpace(letter) && !char.IsPunctuation(letter) && now - artisanVoiceAt > ArtisanVoiceGap)
                {
                    artisanVoiceAt = now; artisanVoiceSource.pitch = 1 + (chars % 5 - 2) * .035f;
                    artisanVoiceSource.PlayOneShot(artisanVoice, artisanVoiceVolume);
                }
            }
            artisanSpoken = chars;
        }

        void DisposeArtisanBubble() { if (artisanVoice) Destroy(artisanVoice); }
    }
}
