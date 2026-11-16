using System.Collections;
using System.Collections.Generic;
using Deviant.Combat;
using Deviant.Combat.Contracts;
using Deviant.Control.Contracts;
using UnityEngine;

namespace ForestCombatV2
{
    // World hooks for the playable opening (docs/design/story/opening/opening-playable.md): hide the stations before the
    // village is erased and walk the player in (the road outside the village is in StartingVillageWorld.OpeningCourse,
    // the erasure and the awakening in StartingVillageWorld.OpeningScene). Everything here is undone by EndOpening, so
    // the lobby afterwards is the usual flat, open lane.
    public sealed partial class StartingVillageWorld
    {
        IControlScope walkScope;
        CombatArena.ManualSimulation walkDriver;
        Coroutine walk;

        // While true no station is offered (no F keycap, outline or interaction).
        public bool StationsSuppressed { get; set; }
        public bool ScriptedWalking => walk != null;
        public string OpeningMusicCue { get; private set; }

        // The movement tutorial after the notebook: the village music keeps its playhead and level.
        public void QuietOpeningRoad()
        {
            if (OpeningMusicCue == "opening_road") OpeningMusicCue = "opening_road_quiet";
        }

        // Teleports the player to fromX (NaN: from where it stands) and walks it to toX with the scripted walking input only.
        public void StartScriptedWalk(float fromX, float toX)
        {
            StopScriptedWalk();
            var player = World.arena.Player;
            if (!float.IsNaN(fromX))
            {
                player.Position = new Vector2(fromX, GroundTop); player.velocity = Vector2.zero;
                Physics2D.SyncTransforms(); player.grounded = player.ProbeGround();
                World.RefreshCamera(0, true);
            }
            walkScope = World.arena.Controls.OpenScope("opening walk");
            walkScope.BlockInput(InputChannels.Interaction | InputChannels.Navigation);
            walk = StartCoroutine(Walk(toX));
        }

        IEnumerator Walk(float toX)
        {
            walkDriver = World.arena.AcquireManualSimulation();
            var player = World.arena.Player;
            int axis = toX < player.Position.x ? -1 : 1;
            // Ends early if something blocks the way; a pause menu holds the walk.
            float walked = 0, stuck = 0;
            while ((toX - player.Position.x) * axis > 0 && walked < 5 && stuck < .25f)
            {
                if (!World.arena.Controls.Current.SimulationPaused)
                {
                    float step = Mathf.Min(Time.unscaledDeltaTime, .04f), before = player.Position.x; walked += step;
                    walkDriver.Step(step, new CombatInput(axis));
                    stuck = Mathf.Abs(player.Position.x - before) < .0001f ? stuck + step : 0;
                }
                yield return null;
            }
            walk = null; StopScriptedWalk();
        }

        public void StopScriptedWalk()
        {
            if (walk != null) { StopCoroutine(walk); walk = null; }
            walkDriver?.Dispose(); walkDriver = null;
            walkScope?.Dispose(); walkScope = null;
        }

        // Returns the lobby to its normal state: profile colours, stations, no opening road, no scripted walk.
        public void EndOpening()
        {
            OpeningMusicCue = null;
            StopScriptedWalk(); EndOpeningScene(); RemoveOpeningCourse();
            StationsSuppressed = false;
            ClearPreview();
        }
    }
}
