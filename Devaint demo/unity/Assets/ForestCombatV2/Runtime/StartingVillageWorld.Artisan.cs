using System;
using System.Collections.Generic;
using UnityEngine;

namespace ForestCombatV2
{
    // The village artisan of the palette-piece visits (docs/design/progression/village-palette-pieces.md, scenes from the HTML
    // opening prototype). Standing, it is the maintenance shop's adopted merchant (MaintenanceShops/npc1) breathing with
    // its feet fixed; walking, the adopted 20-frame walk (Hub/opening/artisan-walk) at the visits' pace. It can sort
    // luggage, hammer at a door (each blow a thud that is quieter far away) or look up at the sky. Both pictures face
    // left. Also here: the village palette stand the pieces go into, the camera's focus and pace during the visits, and
    // the hero and the artisan walking over to a workbench together.
    public sealed partial class StartingVillageWorld
    {
        const float ArtisanSpeed = 4.6f, ArtisanWalkFps = 24, ArtisanWalkPace = 3.2f, ArtisanFade = .7f;
        const float SortBeat = 1.1f, HammerBeat = .55f, ArtisanHead = 4f;
        public const string VillagePalette = "village_palette";

        Transform artisanRoot;
        SpriteRenderer artisanFeet, artisanUpper, artisanWalk;
        Sprite[] artisanWalkFrames;
        readonly List<UnityEngine.Object> artisanMade = new List<UnityEngine.Object>();
        float artisanX, artisanTarget = float.NaN, artisanWalked, artisanFadeAt = -1, artisanLastBeat = -1;
        int artisanFace = -1, artisanFaceAfter;
        string artisanBusy = "";
        bool artisanLeaving;

        public float ArtisanPace { get; set; } = ArtisanSpeed;
        public bool ArtisanShown => artisanRoot;
        public bool LastArtisanExitOffscreen { get; private set; }
        public float ArtisanX => artisanX;
        public string ArtisanBusy => artisanBusy;
        public bool ArtisanMoving => artisanRoot && (!float.IsNaN(artisanTarget) || artisanFadeAt >= 0);
        // Where the artisan's speech bubble points.
        public Vector2 ArtisanTop => new Vector2(artisanX, GroundTop + ArtisanHead);

        // place: stand at x; enter: come in from the right edge of the view to x; walk: go to x; then face (0 keeps).
        public void ArtisanAt(float x, int face, string busy, bool enter, bool walk)
        {
            if (!artisanRoot) BuildArtisan();
            if (enter)
            {
                var view = World.View;
                artisanX = view ? view.transform.position.x + view.orthographicSize * view.aspect + 1 : x + 15;
            }
            else if (!walk) artisanX = x;
            artisanTarget = enter || walk ? x : float.NaN; artisanFaceAfter = face;
            if (!enter && !walk && face != 0) artisanFace = face;
            if (busy != null) artisanBusy = busy;
            artisanLeaving = false; artisanFadeAt = -1;
            TickArtisan();
        }

        public void ArtisanBusyAs(string busy, int face) { artisanBusy = busy ?? ""; if (face != 0) artisanFace = face; }
        public void ArtisanFace(int face) { if (face != 0) artisanFace = face; }

        // Walks off to the right; the screen dims meanwhile (the GUI fades) and it is gone.
        public void ArtisanLeave()
        {
            if (!artisanRoot) return;
            var view = World.View;
            artisanTarget = ArtisanExitTarget(); artisanFaceAfter = 0;
            ArtisanPace = ArtisanSpeed; LastArtisanExitOffscreen = false;
            artisanLeaving = true; artisanBusy = "";
        }
        float ArtisanExitTarget()
        {
            var view=World.View;
            float right=view ? view.transform.position.x+view.orthographicSize*view.aspect : artisanX;
            float half=artisanWalk && artisanWalk.sprite ? artisanWalk.sprite.bounds.extents.x : 2;
            return right+Mathf.Max(4,half+1);
        }

        // Fades into the building it stands at.
        public void ArtisanInside() { if (artisanRoot) { artisanFadeAt = SceneTime; artisanBusy = ""; } }

        public void RemoveArtisan()
        {
            if (artisanRoot) Destroy(artisanRoot.gameObject);
            foreach (var made in artisanMade) if (made) Destroy(made);
            ArtisanPace = ArtisanSpeed; artisanMade.Clear(); artisanRoot = null; artisanTarget = float.NaN; artisanFadeAt = -1; artisanBusy = ""; artisanLeaving = false;
        }

        void BuildArtisan()
        {
            var still = Resources.Load<Texture2D>("MaintenanceShops/npc1");
            if (!still) { Debug.LogWarning("Village artisan art missing: MaintenanceShops/npc1"); return; }
            still.filterMode = FilterMode.Point;
            Sprite Cut(int y, int height) { var s = Sprite.Create(still, new Rect(0, y, still.width, height), new Vector2(.5f, 0), 32, 0, SpriteMeshType.FullRect); artisanMade.Add(s); return s; }
            artisanRoot = new GameObject("Village artisan").transform; artisanRoot.SetParent(transform, false);
            int order = World.arena.Player.visual.sortingOrder - 1;
            artisanFeet = Renderer("Artisan grounded feet", Cut(0, 20), order); artisanFeet.transform.SetParent(artisanRoot, false);
            artisanUpper = Renderer("Artisan breathing upper body", Cut(20, still.height - 20), order); artisanUpper.transform.SetParent(artisanRoot, false);
            artisanUpper.transform.localPosition = new Vector3(0, 20 / 32f, 0);
            var frames = new List<Sprite>();
            for (int i = 1; ; i++) { var frame = Resources.Load<Sprite>($"Hub/opening/artisan-walk/F{i:00}"); if (!frame) break; frames.Add(frame); }
            artisanWalkFrames = frames.ToArray();
            artisanWalk = Renderer("Artisan walking", frames.Count > 0 ? frames[0] : null, order); artisanWalk.transform.SetParent(artisanRoot, false);
            // The walk frames' foot pivot stands one pixel above the ground line.
            artisanWalk.transform.localPosition = new Vector3(0, 1 / 32f, 0);
        }

        SpriteRenderer exclaim;
        float exclaimAt = -1;
        static Sprite exclaimSprite;
        bool exclaimOnHero;
        public void ArtisanExclaim()
        {
            if (!artisanRoot) return;
            exclaimAt = SceneTime; exclaimOnHero = false;
            if (!exclaim) { exclaim = Renderer("Artisan notices", ExclaimSprite(), 8); }
            exclaim.enabled = true;
        }
        static Sprite exclaimNoTail;
        // The 「!」 without its tail, for the guide's mark at the screen edge where nothing below it is meant.
        static Sprite ExclaimSprite(bool tail)
        {
            if (tail) return ExclaimSprite();
            return exclaimNoTail ? exclaimNoTail : exclaimNoTail = BuildExclaim(false);
        }
        static Sprite ExclaimSprite() => exclaimSprite ? exclaimSprite : exclaimSprite = BuildExclaim(true);
        static Sprite BuildExclaim(bool tail)
        {
            const int w = 22, h = 28;
            var px = new Color32[w * h];
            Color32 ink = new Color32(0x1f, 0x1b, 0x19, 255), paper = new Color32(0xff, 0xff, 0xff, 255);
            void Fill(int x, int y, int fw, int fh, Color32 c) { for (int yy = y; yy < y + fh; yy++) for (int xx = x; xx < x + fw; xx++) px[(h - 1 - yy) * w + xx] = c; }
            // Top-down, as the prototype draws it about (11, 22): ink frame with cut corners, a tail, paper inside, the 「!」.
            Fill(2, 0, 18, 24, ink); Fill(0, 2, 22, 20, ink); if (tail) Fill(9, 24, 4, 4, ink);
            Fill(4, 2, 14, 20, paper); Fill(2, 4, 18, 16, paper);
            Fill(9, 5, 4, 10, ink); Fill(9, 17, 4, 3, ink);
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, hideFlags = HideFlags.DontSave };
            t.SetPixels32(px); t.Apply(false, true);
            var sprite = Sprite.Create(t, new Rect(0, 0, w, h), new Vector2(.5f, 0), 32, 0, SpriteMeshType.FullRect);
            sprite.hideFlags = HideFlags.DontSave;
            return sprite;
        }
        // The same 「!」 over the hero (the opening: the second front at the well, playtest 2026-11-06).
        public void HeroExclaim()
        {
            exclaimAt = SceneTime; exclaimOnHero = true;
            if (!exclaim) { exclaim = Renderer("Artisan notices", ExclaimSprite(), 8); }
            exclaim.enabled = true;
            TickExclaim();
        }
        void TickExclaim()
        {
            if (!exclaim || exclaimAt < 0) return;
            float k = SceneTime - exclaimAt;
            var hero = World.arena.Player;
            if (k > 1.2f || (exclaimOnHero ? !hero : !artisanRoot)) { exclaim.enabled = false; exclaimAt = -1; return; }
            float pop = k < .12f ? k / .12f * 1.3f : k < .22f ? 1.3f - (k - .12f) / .1f * .3f : 1, hop = k < .22f ? Mathf.Round(6 * Mathf.Sin(k / .22f * Mathf.PI)) / 32f : 0;
            exclaim.transform.position = exclaimOnHero
                ? new Vector3(hero.Position.x, hero.Position.y + hero.size.y + .45f + hop, 0)
                : new Vector3(artisanX, GroundTop + ArtisanHead + .45f + hop, 0);
            exclaim.transform.localScale = new Vector3(pop, pop, 1);
            exclaim.color = new Color(1, 1, 1, k > .9f ? 1 - (k - .9f) / .3f : 1);
        }

        void TickArtisan()
        {
            TickExclaim();
            if (!artisanRoot) return;
            float dt = World.arena.Controls.Current.SimulationPaused ? 0 : Time.deltaTime, now = Time.time;
            bool walking = !float.IsNaN(artisanTarget);
            if (walking)
            {
                if(artisanLeaving) artisanTarget=Mathf.Max(artisanTarget,ArtisanExitTarget());
                float d = artisanTarget - artisanX, step = ArtisanPace * dt;
                artisanFace = d > 0 ? 1 : d < 0 ? -1 : artisanFace;
                artisanWalked += dt;
                if (Mathf.Abs(d) <= step)
                {
                    artisanX = artisanTarget; artisanTarget = float.NaN; walking = false;
                    if (artisanFaceAfter != 0) artisanFace = artisanFaceAfter;
                    if (artisanLeaving)
                    {
                        var view=World.View;
                        float right=view ? view.transform.position.x+view.orthographicSize*view.aspect : float.NegativeInfinity;
                        LastArtisanExitOffscreen=artisanWalk.bounds.min.x>right;
                        if(LastArtisanExitOffscreen) { RemoveArtisan(); return; }
                        artisanTarget=ArtisanExitTarget()+1; walking=true;
                    }
                }
                else artisanX += Mathf.Sign(d) * step;
            }
            artisanRoot.position = new Vector3(Mathf.Round(artisanX * 32) / 32, GroundTop, 0);
            bool flip = artisanFace > 0;
            artisanWalk.enabled = walking && artisanWalkFrames.Length > 0; artisanFeet.enabled = artisanUpper.enabled = !artisanWalk.enabled;
            if (artisanWalk.enabled)
            {
                artisanWalk.sprite = artisanWalkFrames[Mathf.FloorToInt(artisanWalked * ArtisanWalkFps * ArtisanSpeed / ArtisanWalkPace) % artisanWalkFrames.Length];
                artisanWalk.flipX = flip;
            }
            // Standing: the upper body breathes on a 1.1 s cycle over fixed feet; busy poses take over the lift.
            float phase = Mathf.Repeat(Mathf.Floor(now * 10) / 10, 1.1f) / 1.1f;
            float lift = Mathf.Round((1 - Mathf.Cos(phase * Mathf.PI * 2)) * 2), squash = 1, lean = 0;
            if (artisanBusy == "sort") { float q = Mathf.Repeat(now, SortBeat) / SortBeat; lift = q < .5f ? -3 : 0; lean = q < .5f ? .06f : 0; }
            if (artisanBusy == "hammer")
            {
                float q = Mathf.Repeat(now, HammerBeat) / HammerBeat; squash = q < .15f ? .9f : 1; lift = q > .6f ? 3 : 0;
                // Each blow is heard, quieter far away; close by it also shakes the screen a little.
                int beat = Mathf.FloorToInt(now / HammerBeat);
                if (beat != artisanLastBeat)
                {
                    artisanLastBeat = beat;
                    float near = Mathf.Max(.25f, 1 - Mathf.Abs(artisanX - World.arena.Player.Position.x) / 45);
                    Thud(.24f * near); if (near > .7f) RequestShake(.05f, 2);
                }
            }
            if (artisanBusy == "look") lift = 2 + Mathf.Round(Mathf.Sin(now * 1.5f));
            float upper = artisanUpper.sprite ? artisanUpper.sprite.rect.height : 100;
            artisanUpper.transform.localScale = new Vector3(1, (upper + lift) / upper, 1);
            artisanRoot.localScale = new Vector3(1 + (1 - squash) * .6f, squash, 1);
            artisanRoot.localEulerAngles = new Vector3(0, 0, lean * Mathf.Rad2Deg * (flip ? -1 : 1));
            artisanFeet.flipX = artisanUpper.flipX = flip;
            float alpha = artisanFadeAt >= 0 ? Mathf.Max(0, 1 - (SceneTime - artisanFadeAt) / ArtisanFade) : 1;
            foreach (var r in new[] { artisanFeet, artisanUpper, artisanWalk }) r.color = new Color(1, 1, 1, alpha);
            if (artisanFadeAt >= 0 && alpha <= 0) RemoveArtisan();
        }

        // ---- The hammer's thud (no sound file for it: a short low-passed noise burst with a 95 Hz body, as the prototype).

        AudioSource thudSource;
        AudioClip thudClip;
        public int ThudCount { get; private set; }

        public void Thud(float volume)
        {
            ThudCount++;
            if (!thudClip)
            {
                int rate = AudioSettings.outputSampleRate > 0 ? AudioSettings.outputSampleRate : 44100, n = Mathf.RoundToInt(rate * .16f);
                var data = new float[n]; var noise = new System.Random(95); float low = 0, k = 1 - Mathf.Exp(-2 * Mathf.PI * 700f / rate);
                for (int i = 0; i < n; i++)
                {
                    float t = i / (float)rate, raw = ((float)noise.NextDouble() * 2 - 1) * .6f + Mathf.Sin(t * 2 * Mathf.PI * 95) * .8f;
                    low += (raw - low) * k; data[i] = low * Mathf.Exp(-t * 28);
                }
                thudClip = AudioClip.Create("Village hammer thud", n, 1, rate, false); thudClip.SetData(data, 0);
            }
            if (!thudSource) { thudSource = gameObject.AddComponent<AudioSource>(); thudSource.playOnAwake = false; thudSource.spatialBlend = 0; }
            thudSource.PlayOneShot(thudClip, Mathf.Clamp01(volume));
        }

        // Screen shakes the village asks for (hammer blows); the GUI owns the shake.
        float shakeAsked, shakeAskedAmp;
        void RequestShake(float seconds, float amplitude) { shakeAsked = Mathf.Max(shakeAsked, seconds); shakeAskedAmp = Mathf.Max(shakeAskedAmp, amplitude); }
        public bool TakeShake(out float seconds, out float amplitude)
        {
            seconds = shakeAsked; amplitude = shakeAskedAmp; shakeAsked = shakeAskedAmp = 0;
            return seconds > 0;
        }

        // ---- The camera during the visits: the follow eases from its usual pace to a slow one over about a second, and a
        // focus holds the view where the hero usually stands; the target itself moves smoothly too, so a line starting or
        // the cutscene bars coming in never jolts the view.

        const float CameraUsual = 12.5f, CameraScene = 3.5f;
        public bool SceneCamera { get; set; }
        float? cameraFocus;
        float focusX = float.NaN;

        public void FocusCamera(float? x) => cameraFocus = x;

        void TickCamera()
        {
            float dt = Time.unscaledDeltaTime;
            float target = SceneCamera || cameraFocus.HasValue ? CameraScene : CameraUsual;
            World.CameraFollowOmega += (target - World.CameraFollowOmega) * Mathf.Min(1, dt * 1.6f);
            // A guide ghost's lesson frames the ghost's move together with the hero (StartingVillageWorld.OpeningGuide).
            // The lesson stops game time, which also stops the world's camera, so the view glides there in real time.
            if (GhostFraming.HasValue && !cameraFocus.HasValue)
            {
                focusX = float.NaN; World.FixedCameraCenter = GhostFraming;
                if (Time.timeScale == 0) World.RefreshCamera(dt);
                return;
            }
            if (!cameraFocus.HasValue) { focusX = float.NaN; World.FixedCameraCenter = null; return; }
            // Where the hero usually stands on screen (432 of 960 px), at the view's current height.
            float want = cameraFocus.Value + 1.5f;
            focusX = float.IsNaN(focusX) ? World.View.transform.position.x : focusX + (want - focusX) * Mathf.Min(1, dt * 4);
            World.FixedCameraCenter = new Vector2(focusX, World.View.transform.position.y);
        }

        // ---- Walking over to a workbench together (never a cut): the hero stops 1.9 left of it, the artisan at heroOffset
        // right of it; the camera follows the hero and settles on the bench once both have arrived.

        float panStation = float.NaN;
        int panFace;
        public bool Panning => !float.IsNaN(panStation);

        public void PanTo(string stationId, float heroOffset, int face)
        {
            var station = Find(stationId); if (station == null) { Debug.LogWarning("Visit station missing: " + stationId); return; }
            float x = station.Position.x;
            FocusCamera(null);
            if (Mathf.Abs(World.arena.Player.Position.x - (x - 1.9f)) > .05f) StartScriptedWalk(float.NaN, x - 1.9f);
            if (artisanRoot && Mathf.Abs(artisanX - (x + heroOffset)) > .05f) { artisanTarget = x + heroOffset; artisanFaceAfter = face; }
            else if (face != 0) artisanFace = face;
            panStation = x; panFace = face;
        }

        void TickPan()
        {
            if (!Panning || ScriptedWalking || !float.IsNaN(artisanTarget)) return;
            World.arena.Player.facing = 1;
            FocusCamera(panStation); panStation = float.NaN;
        }

        // Legacy scripts may still request removal; the collection now lives in the record farmhouse.
        public bool PaletteShown => false;
        public void HidePalette() => stations.RemoveAll(station => station.Id == VillagePalette);

        // ---- A workbench a visit is about to open stays closed until its lock breaks in the scene.

        public string HeldClosed { get; set; }

        void TickVillageScenes() { TickArtisan(); TickPan(); TickCamera(); TickGuideMarker(); }
    }
}
