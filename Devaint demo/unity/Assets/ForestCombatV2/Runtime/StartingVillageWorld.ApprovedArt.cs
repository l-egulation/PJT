using System;
using System.Collections;
using System.Collections.Generic;
using Deviant.Combat;
using Deviant.Combat.Contracts;
using Deviant.Control.Contracts;
using UnityEngine;

namespace ForestCombatV2
{
    public sealed partial class StartingVillageWorld
    {
        sealed class BackLayer { public SpriteRenderer renderer; public float speed, width, y, origin; public int index; }
        readonly List<BackLayer> backLayers = new List<BackLayer>();
        static readonly int[] BirdFlap = { 0, 1, 2, 1 };
        readonly SpriteRenderer[] birds = new SpriteRenderer[2];
        // Each bird's own clock: it runs only while the bird is in colour, so a bird the sketch reaches stops there, in the
        // pose it had (HTML prototype 2026-11-05 「스케치화 됐을 때 새들은 왜 안 멈춰」).
        readonly float[] birdClock = new float[2];
        readonly bool[] birdStopped = new bool[2];
        Sprite[] birdFrames, birdSketchFrames;
        float skyAnimationStart;
        public bool Departing { get; private set; }
        Vector3 departureCamera;
        IControlScope departureScope;
        CombatArena.ManualSimulation departureDriver;
        Collider2D departureBoundary;

        // The village backdrop in the chapter-1 pixel bands (2026-11-12): colour is the user's colour design of the
        // mountains, far and near village, sky, clouds and birds (resources stage1-background-color-20261112); the
        // drained state is the cream pixel bands of the combat stages (stage1-background-cream-20261112). Horizontal
        // parallax as the combat stages' bands, each repeat mirrored. The far bands' pictures carry their bottom row
        // 3 units further down (the combat stages stretch it to the floor), so they stand at the same height as there.
        const string PixelSky = PixelVillage + "sky/", PixelBands = PixelVillage + "background/";
        void BuildApprovedBackground()
        {
            sky = Textured("Hub sky", PixelSky + "color/sky", PixelSky + "sketch/sky", new Vector2(.5f, .5f), -120);
            InstallDaylightSky();
            skyAnimationStart = Time.unscaledTime;
            BuildLivingClouds();
            AddLayer("mountains", .08f, 888, GroundTop - 2, -118, PixelBands + "color/mountains", PixelBands + "sketch/mountains");
            AddLayer("far-village", .18f, 444, GroundTop - 2, -117, PixelBands + "color/far-village", PixelBands + "sketch/far-village");
            AddLayer("near-village", .45f, 666, GroundTop - 2, -110, PixelBands + "color/near-village", PixelBands + "sketch/near-village");
            BuildBirds();
        }
        void AddLayer(string name, float speed, float width, float y, int order, string color, string sketch)
        {
            for (int i = -1; i < 3; i++)
            {
                var r = Textured("Hub background " + name, color, sketch, Vector2.zero, order);
                backLayers.Add(new BackLayer { renderer = r, speed = speed, width = width / 32f, y = y, origin = 0, index = i });
            }
        }

        void BuildBirds()
        {
            birdFrames = LoadBirdFrames(PixelSky + "color/bird");
            if (birdFrames == null) return;
            birdSketchFrames = LoadBirdFrames(PixelSky + "sketch/bird");
            for (int i = 0; i < birds.Length; i++)
            {
                birds[i] = Renderer("Village sky bird " + (i + 1), birdFrames[0], -116);
                birds[i].flipX = i == 1;
            }
        }

        Sprite[] LoadBirdFrames(string name)
        {
            var atlas = Resources.Load<Texture2D>(name);
            const int frameSize = 32;
            var frames = new Sprite[3];
            if (!atlas || atlas.width != frameSize * frames.Length || atlas.height != frameSize) return null;
            for (int i = 0; i < frames.Length; i++)
            {
                var frame = Sprite.Create(atlas, new Rect(i * frameSize, 0, frameSize, frameSize),
                    new Vector2(.5f, .5f), 32, 0, SpriteMeshType.FullRect);
                frames[i] = frame;
                made.Add(frame);
            }
            return frames;
        }

        void TickBirds(Vector3 view, float halfWidth, float time)
        {
            float span = 2 * halfWidth + 8;
            for (int i = 0; i < birds.Length; i++)
            {
                var bird = birds[i];
                if (!bird) continue;
                if (birdStopped[i])
                {
                    if (SketchAt(bird.transform.position.x)) continue;
                    birdStopped[i] = false;
                }
                birdClock[i] += Time.unscaledDeltaTime;
                float clock = birdClock[i], direction = bird.flipX ? -1 : 1;
                float x = view.x + direction * (Mathf.Repeat(clock * (1.6f + i * .35f) -
                    direction * view.x * .12f + span * (.35f + i * .22f), span) - span / 2);
                float y = GroundTop + 12 + i * 1.4f + Mathf.Sin(clock * 1.1f + i) * .18f;
                bird.transform.position = new Vector3(Mathf.Round(x * 32) / 32, Mathf.Round(y * 32) / 32, 0);
                bool sketch = SketchAt(x);
                var frames = sketch ? birdSketchFrames ?? birdFrames : birdFrames;
                bird.sprite = frames[BirdFlap[Mathf.FloorToInt(clock * 7 + i) % BirdFlap.Length]];
                bird.color = sketch ? new Color(1, 1, 1, .8f) : Color.white;
                birdStopped[i] = sketch;
            }
        }
        void TickApprovedBackground()
        {
            if(Departing)World.View.transform.position=departureCamera;
            var view=World.View.transform.position;sky.transform.position=new Vector3(view.x,view.y,0);
            float halfWidth = World.View.orthographicSize * World.View.aspect;
            float left=view.x-halfWidth;
            float time = Time.unscaledTime - skyAnimationStart;
            foreach(var layer in backLayers)
            {
                float offset=left*layer.speed;
                float x=(1-layer.speed)*left+layer.origin;
                if(layer.width>0)
                {
                    int segment=Mathf.FloorToInt(offset/layer.width)+layer.index;
                    x+=segment*layer.width;
                    bool mirror=segment%2!=0;
                    layer.renderer.transform.localScale=new Vector3(mirror?-1:1,1,1);
                    if(mirror)x+=layer.width;
                }
                layer.renderer.transform.position=new Vector3(x,layer.y,0);
            }
            TickLivingClouds(view, halfWidth, Time.unscaledDeltaTime);
            TickBirds(view, halfWidth, time);
        }
        public bool StartDeparture(Action enterChapter)
        {
            if(!Ready||Departing||enterChapter==null)return false;
            Departing=true;departureCamera=World.View.transform.position;
            var boundary=World.transform.Find("Right world limit");
            if(boundary){departureBoundary=boundary.GetComponent<Collider2D>();if(departureBoundary)departureBoundary.enabled=false;}
            departureScope=World.arena.Controls.OpenScope("village departure");
            // Combat is also the movement admission channel. The manual driver supplies only walking;
            // block UI interaction/navigation without discarding that scripted axis.
            departureScope.BlockInput(InputChannels.Interaction|InputChannels.Navigation);
            StartCoroutine(WalkOut(enterChapter));return true;
        }
        IEnumerator WalkOut(Action enterChapter)
        {
            departureDriver=World.arena.AcquireManualSimulation();
            float right=departureCamera.x+World.View.orthographicSize*World.View.aspect;
            while(World.arena.Player.Position.x<right+4f)
            {
                departureDriver.Step(Mathf.Min(Time.unscaledDeltaTime,.04f),new CombatInput(1));
                yield return null;
            }
            departureDriver.Dispose();departureDriver=null;
            ChapterSceneFade.Load(LoadingPlace.Chapter1, enterChapter);
        }
        void DisposeDeparture(){departureDriver?.Dispose();departureDriver=null;departureScope?.Dispose();departureScope=null;if(departureBoundary)departureBoundary.enabled=true;}
    }
}
