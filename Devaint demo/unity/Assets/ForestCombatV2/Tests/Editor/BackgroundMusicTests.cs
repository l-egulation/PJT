using System;
using System.Collections;
using System.Linq;
using Deviant.Chapter2;
using Deviant.Music;
using Deviant.Run;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ForestCombatV2.Tests
{
    public sealed class BackgroundMusicTests
    {
        static MusicLibrary ShortLibrary() => new MusicLibrary
        {
            schema = MusicLibrary.Schema,
            fadeOutSeconds = .12f,
            fadeInSeconds = .12f,
            loopCrossfadeSeconds = .2f,
            scheduleLeadSeconds = .03f,
            fadeStepLimitSeconds = .1f,
            tracks = new[]
            {
                new MusicTrack
                {
                    id = "title",
                    resource = "tone-a",
                    volume = .3f,
                    startSeconds = 0,
                    endSeconds = .7f,
                    loopStartSeconds = .25f,
                    loopCrossfadeSeconds = .15f
                },
                new MusicTrack
                {
                    id = "village",
                    resource = "tone-b",
                    volume = .25f,
                    startSeconds = 0,
                    endSeconds = .7f
                }
            }
        };

        // Long windows so cue fades can be watched without a repeat in between; "a_quiet" is the same file at a lower level.
        const float FadeOut = 1.2f, FadeIn = .9f, LevelA = .4f, LevelQuiet = .2f, LevelB = .3f, LongWindow = 19.8f;
        static MusicLibrary FadeLibrary() => new MusicLibrary
        {
            schema = MusicLibrary.Schema,
            fadeOutSeconds = FadeOut,
            fadeInSeconds = FadeIn,
            loopCrossfadeSeconds = .2f,
            scheduleLeadSeconds = .03f,
            fadeStepLimitSeconds = .1f,
            tracks = new[]
            {
                new MusicTrack { id = "a", resource = "long-a", group = "a", volume = LevelA, endSeconds = LongWindow },
                new MusicTrack { id = "a_quiet", resource = "long-a", group = "a", volume = LevelQuiet, endSeconds = LongWindow },
                new MusicTrack { id = "b", resource = "long-b", volume = LevelB, endSeconds = LongWindow }
            }
        };

        static void ConfigureShort(BackgroundMusicPlayer music, AudioClip a, AudioClip b) => music.Configure(ShortLibrary(), p => p == "tone-a" ? a : b);
        static AudioClip Tone(string name, int frequency, float seconds = 1)
        {
            int frames = Mathf.RoundToInt(48000 * seconds);
            var clip = AudioClip.Create(name, frames, 2, 48000, false);
            var values = new float[frames * 2];
            for (int i = 0; i < frames; i++)
                values[i * 2] = values[i * 2 + 1] = .1f * Mathf.Sin(2 * Mathf.PI * frequency * i / 48000f);
            clip.SetData(values, 0);
            return clip;
        }

        static float Rms(AudioSource source)
        {
            var samples = new float[1024];
            source.GetOutputData(samples, 0);
            double sum = 0;
            foreach (float x in samples)
                sum += x * x;
            return (float)Math.Sqrt(sum / samples.Length);
        }

        static IEnumerator WaitForCue(BackgroundMusicPlayer music, string cue, string clip)
        {
            float until = Time.realtimeSinceStartup + 10;
            bool heard = false;
            do
            {
                yield return null;
                foreach (var s in music.Voices)
                    if (s.clip && s.clip.name == clip && s.isPlaying && s.volume > .001f && Rms(s) > .00001f)
                        heard = true;
            }
            while ((music.CurrentCue != cue || music.IsTransitioning || !heard) && Time.realtimeSinceStartup < until);
            Assert.AreEqual(cue, music.CurrentCue);
            Assert.IsFalse(music.IsTransitioning);
            Assert.IsTrue(heard, "the mapped clip must produce actual PCM output: " + clip);
        }

        static AudioSource Playing(BackgroundMusicPlayer music, string clip) => music.Voices.Single(s => s.clip && s.clip.name == clip && s.isPlaying);

        // EnterPlayMode must be yielded by the test method itself; the test framework ignores it inside a nested iterator.
        static void PrepareEmptyPlayMode()
        {
            EditorSettings.enterPlayModeOptionsEnabled = false;
            EditorSceneManager.playModeStartScene = null;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            Time.timeScale = 1;
            if (Application.isPlaying)
                yield return new ExitPlayMode();
            Chapter1ProfileHost.UseStore(null);
        }

        [Test]
        public void MusicLibraryRejectsInvalidPlaybackWindowsFadesAndDuplicateIds()
        {
            var data = ShortLibrary();
            data.Validate();
            data.tracks[1].id = "title";
            Assert.Throws<ArgumentException>(() => data.Validate());
            data = ShortLibrary();
            data.tracks[0].endSeconds = .1f;
            Assert.Throws<ArgumentException>(() => data.Validate());
            data = ShortLibrary();
            data.tracks[0].volume = float.NaN;
            Assert.Throws<ArgumentException>(() => data.Validate());
            data = ShortLibrary();
            data.tracks[0].loopStartSeconds = .5f;
            Assert.Throws<ArgumentException>(() => data.Validate());
            data = ShortLibrary();
            data.schema = 1;
            Assert.Throws<ArgumentException>(() => data.Validate());
            data = ShortLibrary();
            data.fadeInSeconds = 0;
            Assert.Throws<ArgumentException>(() => data.Validate());
            data = ShortLibrary();
            data.fadeStepLimitSeconds = float.PositiveInfinity;
            Assert.Throws<ArgumentException>(() => data.Validate());
            data = ShortLibrary();
            data.tracks[0].fadeOutSeconds = -1;
            Assert.Throws<ArgumentException>(() => data.Validate());

            var shipped = JsonUtility.FromJson<MusicLibrary>(Resources.Load<TextAsset>("BackgroundMusic").text);
            shipped.Validate();
            Assert.AreEqual(2, shipped.fadeOutSeconds);
            Assert.AreEqual(3, shipped.fadeInSeconds);
            var village = shipped.tracks.Single(t => t.id == "village");
            foreach (var id in new[] { "opening_road", "opening_road_quiet" })
            {
                var road = shipped.tracks.Single(t => t.id == id);
                Assert.IsTrue(road.SharesPlayback(village));
                Assert.AreEqual(village.volume, road.volume);
            }
            foreach (var track in shipped.tracks)
            {
                var clip = BackgroundMusicPlayer.LoadClip(track.resource);
                Assert.IsNotNull(clip, track.id);
                Assert.GreaterOrEqual(clip.length, track.endSeconds, track.id);
                if (track.id == "chapter1_boss") continue;
                Assert.Greater(track.RepeatStart, track.startSeconds, track.id);
                Assert.AreEqual(AudioClipLoadType.DecompressOnLoad, clip.loadType, track.id);
            }
            var sfx = Resources.Load<PresentationSfx>("PresentationSfx");
            Assert.AreEqual("player-death", sfx.entries.Single(e => e.id == "player-death").clip.name);
        }

        [UnityTest]
        public IEnumerator RealAudioVoicesLoopCrossfadeAndKeepPlayingDuringSimulationPause()
        {
            PrepareEmptyPlayMode();
            yield return new EnterPlayMode();
            yield return null;
            var listener = new GameObject("Music test listener", typeof(AudioListener));
            var a = Tone("tone-a", 220);
            var b = Tone("tone-b", 330);
            var music = BackgroundMusicPlayer.Ensure();
            music.AutomaticSceneSelection = false;
            ConfigureShort(music, a, b);
            try
            {
                Assert.AreSame(music, BackgroundMusicPlayer.Ensure());
                Assert.AreEqual(1, Object.FindObjectsByType<BackgroundMusicPlayer>().Length);
                music.Request("title");
                yield return WaitForCue(music, "title", "tone-a");
                int loops = music.LoopCount;
                var introVoice = music.Voices.Single(s => s.isPlaying);
                Assert.Less(introVoice.time, .25f, "the first pass starts at the intro");
                float deadline = Time.realtimeSinceStartup + 4;
                while (music.LoopCount == loops && Time.realtimeSinceStartup < deadline)
                    yield return null;
                Assert.Greater(music.LoopCount, loops);
                var repeatVoice = music.Voices.Single(s => s != introVoice && s.isPlaying);
                Assert.That(repeatVoice.timeSamples, Is.InRange(12000, 16800), "the repeat skips the intro");

                while (music.LoopCount < loops + 2 && Time.realtimeSinceStartup < deadline)
                    yield return null;
                Assert.That(music.LoopCount, Is.GreaterThanOrEqualTo(loops + 2), "the actual playback window repeats through the two sources");
                int sameLoops = music.LoopCount;
                music.Request("title");
                Assert.AreEqual(sameLoops, music.LoopCount, "same cue does not restart or fabricate a loop");
                Time.timeScale = 0;
                double dsp = AudioSettings.dspTime;
                yield return new WaitForSecondsRealtime(.3f);
                Assert.That(AudioSettings.dspTime, Is.GreaterThan(dsp + .15));
                Assert.IsTrue(music.Voices.Any(s => s.isPlaying));
                foreach (var s in music.Voices)
                {
                    Assert.AreEqual(1, s.pitch);
                    Assert.AreEqual(0, s.spatialBlend);
                    Assert.IsTrue(s.ignoreListenerPause);
                }

                music.Request("village");
                yield return WaitForCue(music, "village", "tone-b");
                Assert.AreEqual(1, music.Voices.Count(s => s.isPlaying));
                music.Request(null);
                yield return new WaitForSecondsRealtime(.3f);
                Assert.IsNull(music.CurrentCue);
                Assert.IsFalse(music.Voices.Any(s => s.isPlaying));
            }
            finally
            {
                Time.timeScale = 1;
                ConfigureShort(music, a, b);
                Object.DestroyImmediate(a);
                Object.DestroyImmediate(b);
                Object.Destroy(listener);
            }
        }

        [UnityTest]
        public IEnumerator CueFadesStartQuietlyFadeFromTheHeardLevelAndSurviveStalls()
        {
            PrepareEmptyPlayMode();
            yield return new EnterPlayMode();
            yield return null;
            yield return CueFadesInPlayMode();
        }

        // Runs after the domain reload: lambdas here capture locals that the reload would otherwise drop.
        static IEnumerator CueFadesInPlayMode()
        {
            var listener = new GameObject("Music test listener", typeof(AudioListener));
            var a = Tone("long-a", 220, 20);
            var b = Tone("long-b", 330, 20);
            var music = BackgroundMusicPlayer.Ensure();
            music.AutomaticSceneSelection = false;
            music.Configure(FadeLibrary(), p => p == "long-a" ? a : b);
            try
            {
                music.Request("a");
                yield return WaitForCue(music, "a", "long-a");
                var voiceA = Playing(music, "long-a");
                Assert.That(music.GainOf(voiceA), Is.EqualTo(LevelA).Within(1e-4f));

                // A new cue starts quietly: below a straight line through the first half of its fade-in.
                double asked = AudioSettings.dspTime;
                music.Request("b");
                AudioSource voiceB = null;
                float lastB = 0;
                while (AudioSettings.dspTime - asked < FadeIn * .5f)
                {
                    yield return null;
                    voiceB = voiceB ? voiceB : music.Voices.FirstOrDefault(s => s.clip == b);
                    if (!voiceB)
                        continue;
                    float progress = (float)((AudioSettings.dspTime - asked) / FadeIn);
                    lastB = music.GainOf(voiceB);
                    Assert.That(lastB, Is.LessThanOrEqualTo(LevelB * progress + 1e-4f), "slow start");
                }
                Assert.IsNotNull(voiceB);
                Assert.That(lastB, Is.GreaterThan(0));
                Assert.IsTrue(voiceA.isPlaying, "the old cue is still fading out");
                float heardA = music.GainOf(voiceA);
                Assert.That(heardA, Is.GreaterThan(0).And.LessThan(LevelA));

                // Back to "a" mid-fade: its voice is taken back without a restart and "b" fades out from where it is.
                float timeA = voiceA.time;
                music.Request("a");
                Assert.AreEqual("a", music.CurrentCue);
                yield return null;
                Assert.IsTrue(voiceB.isPlaying, "the abandoned cue is faded, not cut");
                Assert.That(music.GainOf(voiceB), Is.GreaterThan(0).And.LessThanOrEqualTo(lastB + 1e-4f));
                // AudioSource.time moves per audio buffer, so one frame may not advance it; a restart would move it back.
                Assert.That(voiceA.time, Is.GreaterThanOrEqualTo(timeA), "the cue that came back was not restarted");
                yield return WaitForCue(music, "a", "long-a");
                Assert.AreSame(voiceA, Playing(music, "long-a"));
                Assert.IsFalse(voiceB.isPlaying);

                // The quieter version of the same file only moves the level, continuously, on the same voice.
                int loops = music.LoopCount;
                timeA = voiceA.time;
                float previous = music.GainOf(voiceA);
                music.Request("a_quiet");
                float until = Time.realtimeSinceStartup + 3;
                while ((music.IsTransitioning || music.CurrentCue != "a_quiet") && Time.realtimeSinceStartup < until)
                {
                    yield return null;
                    Assert.IsTrue(voiceA.isPlaying);
                    float gain = music.GainOf(voiceA);
                    Assert.That(gain, Is.LessThanOrEqualTo(previous + 1e-4f));
                    Assert.That(previous - gain, Is.LessThan(.25f * (LevelA - LevelQuiet)), "no jump in level");
                    previous = gain;
                }
                Assert.AreEqual("a_quiet", music.CurrentCue);
                Assert.That(music.GainOf(voiceA), Is.EqualTo(LevelQuiet).Within(1e-4f));
                Assert.That(voiceA.time, Is.GreaterThan(timeA), "the playback went on");
                Assert.AreEqual(loops, music.LoopCount);
                Assert.AreEqual(1, music.Voices.Count(s => s.isPlaying));

                // A main-thread stall (a scene load) does not jump a fade to its end.
                music.Request("b");
                yield return null;
                yield return null;
                voiceB = music.Voices.First(s => s.clip == b);
                System.Threading.Thread.Sleep(600);
                yield return null;
                Assert.IsTrue(music.IsTransitioning);
                Assert.That(music.GainOf(voiceB), Is.LessThan(LevelB * .5f), "the fade-in goes on from where it was heard");
                Assert.That(music.GainOf(voiceA), Is.GreaterThan(LevelQuiet * .3f), "the fade-out goes on from where it was heard");
                yield return WaitForCue(music, "b", "long-b");
            }
            finally
            {
                music.Configure(FadeLibrary(), p => p == "long-a" ? a : b);
                Object.DestroyImmediate(a);
                Object.DestroyImmediate(b);
                Object.Destroy(listener);
            }
        }

        [UnityTest]
        public IEnumerator WaterwayMaintenanceAndRoseBlueEncounterPlayTheirOwnTracks()
        {
            EditorSettings.enterPlayModeOptionsEnabled = false;
            EditorSceneManager.playModeStartScene = null;
            EditorSceneManager.OpenScene(Chapter2EntryBridge.ScenePath);
            yield return new EnterPlayMode();

            var music = BackgroundMusicPlayer.Ensure();
            yield return WaitForCue(music, "chapter2_field", "final-chapter2-field");
            var world = Object.FindAnyObjectByType<Chapter2World>();
            Assert.IsTrue(world && world.Ready);
            Assert.IsFalse(world.Boss.MusicActive);
            var field = music.Voices.Single(s => s.isPlaying).clip;
            Assert.AreEqual(AudioClipLoadType.DecompressOnLoad, field.loadType);

            world.arena.Player.maxHealth = world.arena.Player.health = 10000;
            world.WarpToRoom("M");
            yield return WaitForCue(music, "maintenance", "final-maintenance");
            Assert.AreEqual("M", world.CurrentRoom);
            world.WarpToRoom("S");
            yield return WaitForCue(music, "chapter2_field", "final-chapter2-field");

            using (var simulation = world.arena.AcquireManualSimulation())
            {
                world.WarpToRoom("B");
                Assert.IsTrue(world.Boss.MusicActive);
                yield return WaitForCue(music, "chapter2_boss", "final-chapter2-boss");
                var bossVoice = music.Voices.Single(s => s.isPlaying);
                Assert.AreEqual(AudioClipLoadType.DecompressOnLoad, bossVoice.clip.loadType);

                float beforePhaseChange = bossVoice.time;
                world.Boss.StartTestPhase(3);
                yield return null;
                Assert.AreEqual("chapter2_boss", music.CurrentCue);
                Assert.That(bossVoice.time, Is.GreaterThanOrEqualTo(beforePhaseChange));
                Assert.IsTrue(world.Boss.Blue.Receive(10000, world.arena.Player, 1, false, null));
                simulation.Step(1f / 120);
                yield return WaitForCue(music, "chapter2_field", "final-chapter2-field");
            }
            Assert.IsTrue(world.Boss.Dead);
            Assert.IsFalse(music.Voices.Any(s => s.clip && s.clip.name == "final-chapter2-boss" && s.isPlaying));
            Assert.AreEqual(0, music.DeathStings, "a boss kill is not a death");
        }

        [UnityTest]
        public IEnumerator DedicatedRoseBlueScenePlaysBossMusicAndTheDeathSound()
        {
            EditorSettings.enterPlayModeOptionsEnabled = false;
            EditorSceneManager.playModeStartScene = null;
            EditorSceneManager.OpenScene(RoseBlueBossScene.ScenePath);
            yield return new EnterPlayMode();

            var music = BackgroundMusicPlayer.Ensure();
            yield return WaitForCue(music, "chapter2_boss", "final-chapter2-boss");
            var world = Object.FindAnyObjectByType<Chapter2World>();
            Assert.IsTrue(world.Boss.MusicActive);

            yield return DeathHoldsSilence(music, "final-chapter2-boss", Kill(world.arena.Player));
        }

        // Built outside the test iterators: a lambda capturing an iterator local is lost by the domain reload of
        // EnterPlayMode.
        static Action Kill(Deviant.Combat.CombatActor player) => () => player.health = 0;

        // Within the death motion (the scene changes only after Chapter1RunBinding.GameOverDelaySeconds).
        static IEnumerator DeathHoldsSilence(BackgroundMusicPlayer music, string bossClip, Action kill)
        {
            int stings = music.DeathStings;
            int sfxBefore = PresentationSfx.CountOf("player-death");
            float bossGain = music.GainOf(Playing(music, bossClip));
            Assert.That(bossGain, Is.GreaterThan(0));
            kill();
            float until = Time.realtimeSinceStartup + 1;
            while (!music.DeathLatched && Time.realtimeSinceStartup < until)
                yield return null;
            Assert.IsTrue(music.DeathLatched);
            Assert.AreEqual(stings + 1, music.DeathStings, "the death sting is recorded once");
            Assert.IsNull(music.RequestedCue);
            float hold = Time.realtimeSinceStartup + 1.5f;
            while (Time.realtimeSinceStartup < hold)
            {
                yield return null;
                Assert.IsNull(music.RequestedCue, "no return to the field music");
            }
            Assert.IsNull(music.CurrentCue);
            foreach (var s in music.Voices)
                if (s.isPlaying)
                {
                    Assert.AreEqual(bossClip, s.clip.name, "nothing else starts");
                    Assert.That(music.GainOf(s), Is.LessThan(bossGain * .5f), "the music fades to silence");
                }
            Assert.AreEqual(stings + 1, music.DeathStings, "the death sting does not repeat");
            Assert.AreEqual(sfxBefore + 1, PresentationSfx.CountOf("player-death"));
        }

        [UnityTest]
        public IEnumerator Chapter1BossDeathPlaysTheStingAndHoldsMusicSilent()
        {
            EditorSettings.enterPlayModeOptionsEnabled = false;
            EditorSceneManager.playModeStartScene = null;
            EditorSceneManager.OpenScene(Chapter1MapSelector.ProceduralScene);
            yield return new EnterPlayMode();
            ForestCombatWorld world = null;
            for (int i = 0; i < 900 && !(world && world.Ready); i++)
            {
                yield return null;
                world = Object.FindAnyObjectByType<ForestCombatWorld>();
            }

            Assert.IsTrue(world && world.Ready);
            var music = BackgroundMusicPlayer.Ensure();
            world.arena.Player.maxHealth = world.arena.Player.health = 10000;
            foreach (var e in world.arena.enemies)
                if (e)
                {
                    e.aggressive = false;
                    e.CancelAttack();
                }

            yield return WaitForCue(music, "chapter1_field", "final-chapter1-field");
            var encounter = world.GetComponent<ClockExecutionerEncounter>();
            world.arena.Player.Position = new Vector2(encounter.Room.x + 3, encounter.Boss.Floor);
            world.arena.Player.velocity = Vector2.zero;
            Physics2D.SyncTransforms();
            yield return WaitForCue(music, "chapter1_boss", "chapter1-boss");
            yield return DeathHoldsSilence(music, "chapter1-boss", Kill(world.arena.Player));
        }

        [UnityTest]
        public IEnumerator TitleVillageFieldMaintenanceAndActualBossEncounterSelectTheProvidedTracks()
        {
            EditorSettings.enterPlayModeOptionsEnabled = false;
            EditorSceneManager.playModeStartScene = null;
            EditorSceneManager.OpenScene(Chapter1RunHost.TitleScene);
            yield return new EnterPlayMode();
            Chapter1ProfileHost.UseStore(new MemoryProfileStore());
            yield return null;
            var music = BackgroundMusicPlayer.Ensure();
            yield return WaitForCue(music, "title", "final-title");
            // The return goes through the notebook loading.
            Chapter1RunHost.Ensure().GoToVillage();
            StartingVillageWorld village = null;
            for (float waitFrom = Time.realtimeSinceStartup; Time.realtimeSinceStartup - waitFrom < 20 && !(village = Object.FindAnyObjectByType<StartingVillageWorld>());) yield return null;
            Assert.AreSame(music, BackgroundMusicPlayer.Instance);
            Assert.IsNotNull(village);
            village.BuildOpeningCourse(-30, System.Array.Empty<StartingVillageWorld.CoursePiece>());
            yield return WaitForCue(music, "opening_road", "final-village");
            // The movement tutorial after the notebook shares the hub playhead and level.
            var road = Playing(music, "final-village");
            float roadTime = road.time, roadGain = music.GainOf(road);
            village.QuietOpeningRoad();
            yield return WaitForCue(music, "opening_road_quiet", "final-village");
            Assert.AreSame(road, Playing(music, "final-village"), "no restart for the quieter road");
            Assert.That(road.time, Is.GreaterThan(roadTime));
            Assert.That(music.GainOf(road), Is.EqualTo(roadGain).Within(.005f));
            // The chase music starts with the colour drain, before the front is released.
            village.StartErasureChase(16.5f, float.NaN, float.NaN);
            Assert.AreEqual("opening_erase", village.OpeningMusicCue);
            yield return WaitForCue(music, "opening_erase", "final-chase");
            village.EndOpening();
            yield return WaitForCue(music, "village", "final-village");
            Chapter1RunHost.Ensure().StartNewRun();
            ForestCombatWorld world = null;
            for (int i = 0; i < 900 && !(world && world.Ready); i++)
            {
                yield return null;
                world = Object.FindAnyObjectByType<ForestCombatWorld>();
            }

            Assert.IsTrue(world && world.Ready);
            world.arena.Player.maxHealth = world.arena.Player.health = 10000;
            foreach (var e in world.arena.enemies)
                if (e)
                {
                    e.aggressive = false;
                    e.CancelAttack();
                }

            yield return WaitForCue(music, "chapter1_field", "final-chapter1-field");
            var player = world.arena.Player;
            var outside = player.Position;
            var facility = world.GetComponent<Chapter1MaintenanceFacility>();
            var shop = world.Data.rooms.First(r => Chapter1MaintenanceFacility.IsFacility(r.kind));
            player.Position = new Vector2(shop.x + shop.w / 2f, -(shop.y + shop.entryY));
            player.velocity = Vector2.zero;
            Physics2D.SyncTransforms();
            Assert.IsTrue(facility.PlayerInside());
            yield return WaitForCue(music, "maintenance", "final-maintenance");
            player.Position = outside;
            player.velocity = Vector2.zero;
            Physics2D.SyncTransforms();
            yield return WaitForCue(music, "chapter1_field", "final-chapter1-field");

            var encounter = world.GetComponent<ClockExecutionerEncounter>();
            Assert.IsNotNull(encounter);
            Assert.IsFalse(encounter.MusicActive);
            player.Position = new Vector2(encounter.Room.x + 3, encounter.Boss.Floor);
            player.velocity = Vector2.zero;
            Physics2D.SyncTransforms();
            yield return WaitForCue(music, "chapter1_boss", "chapter1-boss");
            Assert.IsTrue(encounter.MusicActive);
            encounter.Boss.health = 0;
            yield return WaitForCue(music, "chapter1_field", "final-chapter1-field");
            Assert.IsFalse(music.Voices.Any(s => s.clip && s.clip.name == "chapter1-boss" && s.isPlaying));
            Assert.AreEqual(0, music.DeathStings);
            Assert.AreEqual(1, Object.FindObjectsByType<BackgroundMusicPlayer>().Length);
            yield return DeathHoldsSilence(music, "final-chapter1-field", Kill(player));
            SceneManager.LoadScene(Chapter1RunHost.TitleScene);
            yield return WaitForCue(music, "title", "final-title");
            Assert.IsFalse(music.DeathLatched);
        }
    }
}
