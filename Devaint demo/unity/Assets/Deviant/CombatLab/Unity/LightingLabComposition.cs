using System;
using System.Collections.Generic;
using Deviant.Combat;
using Deviant.Combat.Contracts;
using Deviant.Control.Contracts;
using Deviant.Definitions;
using Deviant.Integration;
using Deviant.Presentation;
using Deviant.Presentation.Unity;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace Deviant.CombatLab
{
    // Deliberately synthetic diagnostic art, generated in memory only. This does not
    // replace, downsample or claim approval of any resources/ production artwork.
    [DefaultExecutionOrder(800)]
    public sealed class LightingLabComposition : MonoBehaviour
    {
        readonly List<Object> owned = new List<Object>();
        readonly List<SpriteLightingBinding> spriteBindings = new List<SpriteLightingBinding>();
        readonly List<LightEmitterView> emitters = new List<LightEmitterView>();
        readonly List<ActorFixture> actors = new List<ActorFixture>();
        readonly List<LightingZoneTracker> zones = new List<LightingZoneTracker>();
        sealed class ActorFixture { internal CombatActor Actor; internal SpriteLightingFrame Idle, Attack; }
        LightingBindings bindings;
        LightingCatalog catalog;
        LightingDirector director;
        Light2D ambient;
        Material lit, outlined, actorLit, unlit;
        IDisposable sunset;
        IControlScope pause;
        bool lightsOn = true, normalsOn = true, showControls = true, secondWindowOn;
        int openingMode;
        LightEmitterView lanternEmitter;
        Sprite diagnosticCookie;
        bool lanternMaskOn;
        bool zonesEnabled;
        public CombatArena Arena { get; private set; }
        public ReactiveLanternBridge Lantern { get; private set; }
        public HangingConnectorView Suspension { get; private set; }
        public WindowLightView Window { get; private set; }
        public WindowLightView SecondWindow { get; private set; }
        public HangingConnectorArt connectorArt;
        public bool Ready { get; private set; }
        public LightingDirector Director => director;
        public IReadOnlyList<LightingZoneDefinition> ZoneDefinitions => catalog.Zones;
        public int ActiveZoneCount { get { int count = 0; foreach (var zone in zones) if (zone.IsActive) count++; return count; } }
        public int ZoneEntryCount { get { int count = 0; foreach (var zone in zones) count += zone.EntryCount; return count; } }
        public IReadOnlyList<SpriteLightingBinding> SpriteBindings => spriteBindings;
        public Camera View { get; private set; }
        void Start() => Initialize();
        public void Initialize()
        {
            if (Ready) return;
            bindings = Resources.Load<LightingBindings>("Lighting/Bindings");
            if (!bindings) throw new InvalidOperationException("Build the Lighting Lab bindings with Tools > deviant > Experiments > Lighting > Open Lab.");
            var json = Resources.Load<TextAsset>("Lighting/Lab");
            if (!json) throw new InvalidOperationException("Generate Lighting/Lab.json first.");
            catalog = new LightingCatalog(JsonUtility.FromJson<LightingCatalogData>(json.text));
            lit = Own(new Material(bindings.litOutlineShader)); lit.SetFloat("_OutlineStrength", 0);
            outlined = Own(new Material(bindings.litOutlineShader)); unlit = Own(new Material(bindings.unlitShader));
            actorLit = Own(new Material(bindings.litOutlineShader)); actorLit.SetFloat("_OutlineStrength", 0);
            View = Node("Lighting lab camera").AddComponent<Camera>(); View.tag = "MainCamera";
            View.orthographic = true; View.orthographicSize = 4.5f; View.transform.position = new Vector3(0, 2.5f, -10);
            View.backgroundColor = new Color(.04f, .05f, .08f); View.clearFlags = CameraClearFlags.SolidColor; View.allowMSAA = false;
            Arena = Node("Shared combat runtime").AddComponent<CombatArena>(); Arena.loadCharacterArt = false; Arena.showDebugUI = false;
            var content = CombatContentLoader.Load(null, false);
            content.Profile = CombatCompositionProfile.Lab; content.Loadout = Deviant.Equipment.WeaponResources.CreateLoadout();
            content.Camera = View; content.SpriteMaterial = lit; content.PlayerSpawn = new Vector2(-2, 0);
            content.PlayerSize = new Vector2(.9375f, 2); content.PlayerOffset = new Vector2(.1875f, 1); content.PlayerSortingOrder = 10;
            content.Enemies = new[] { new CombatEnemySpawn("Lighting monster fixture", 0, new Vector2(2.6f, 0), new Vector2(1.5f, 1.4f), new Vector2(0, .7f), 1000, false, false, 10) };
            content.PrepareEnvironment = owner =>
            {
                Surface("Floor", new Vector2(0, -.25f), new Vector2(16, .5f));
                Surface("Left wall", new Vector2(-8.25f, 3), new Vector2(.5f, 7));
                Surface("Right wall", new Vector2(8.25f, 3), new Vector2(.5f, 7));
            };
            content.BindPlayerInput = player => player.gameObject.AddComponent<CombatKeyboardInput>().Player = player;
            if (!Arena.Initialize(content)) throw new InvalidOperationException("Lighting lab combat initialization failed.");
            director = new LightingDirector(catalog.Profile("indoor"));
            BuildZones();
            ambient = Node("Environment light").AddComponent<Light2D>(); ambient.lightType = Light2D.LightType.Global;
            ambient.blendStyleIndex = 0;
            AddActor(Arena.Player, new Color(.84f, .79f, .65f), true);
            AddActor(Arena.enemies[0], new Color(.48f, .64f, .58f), false);
            BuildWindow(); BuildLantern(); BuildTorch();
            var windowCasters = new List<SpriteShadowSource>(GetComponentsInChildren<SpriteShadowSource>());
            // CombatArena owns actor roots outside this composition's hierarchy.
            // Register their actual views explicitly instead of assuming a scene search.
            foreach (var fixture in actors)
            {
                windowCasters.AddRange(fixture.Actor.visual.GetComponentsInChildren<SpriteShadowSource>());
                Window.AddReceiver(fixture.Actor.visual);
                SecondWindow.AddReceiver(fixture.Actor.visual);
            }
            Window.SetShadowCasters(windowCasters);
            SecondWindow.SetShadowCasters(windowCasters);
            foreach (var renderer in GetComponentsInChildren<SpriteRenderer>())
                if (renderer.sharedMaterial && renderer.sharedMaterial.HasProperty("_WindowCount")) { Window.AddReceiver(renderer); SecondWindow.AddReceiver(renderer); }
            Arena.SimulationCompleted += StepLighting; Arena.ResetCompleted += ResetLighting;
            Ready = true; ApplyEnvironment(); DrawActors();
        }
        T Own<T>(T value) where T : Object { owned.Add(value); return value; }
        GameObject Node(string label, Transform parent = null)
        { var go = new GameObject(label); go.transform.SetParent(parent ? parent : transform, false); return go; }
        SpriteRenderer RectArt(string name, Vector2 at, Vector2 size, Color color, int order, Transform parent = null, Material material = null)
        {
            var renderer = Node(name, parent).AddComponent<SpriteRenderer>(); renderer.sprite = Arena.TechnicalSprite;
            renderer.sharedMaterial = material ? material : lit; renderer.color = color; renderer.sortingOrder = order;
            renderer.transform.localPosition = at; renderer.transform.localScale = new Vector3(size.x, size.y, 1); return renderer;
        }
        void Surface(string name, Vector2 at, Vector2 size)
        {
            var go = Node(name); go.transform.position = at; go.AddComponent<ArenaSurface>();
            var collider = go.AddComponent<BoxCollider2D>(); collider.size = size;
            // TechnicalSprite is initialized before PrepareEnvironment is invoked.
            var renderer = Node("Surface art", go.transform).AddComponent<SpriteRenderer>(); renderer.sprite = Arena.TechnicalSprite;
            renderer.sharedMaterial = lit; renderer.color = new Color(.4f, .38f, .34f); renderer.sortingOrder = 0; renderer.transform.localScale = new Vector3(size.x, size.y, 1);
            BindShadow(renderer, new[] { new SpriteShadowFrame { sprite = renderer.sprite, vertices = Box(.5f, .5f), edges = Loop(4) } });
        }
        SpriteShadowSource BindShadow(SpriteRenderer renderer, SpriteShadowFrame[] frames)
        {
            var root = Instantiate(bindings.shadowTemplate, renderer.transform, false); root.name = "Cached shadow output";
            var source = root.GetComponent<SpriteShadowSource>(); source.Configure(renderer, frames); return source;
        }
        void AddActor(CombatActor actor, Color color, bool player)
        {
            int w = player ? 40 : 64, h = player ? 72 : 56;
            Vector2 pivot = new Vector2(.5f, 4f / h);
            Vector2[] idle = player ? new[] { new Vector2(11,4), new Vector2(11,46), new Vector2(14,68), new Vector2(29,68), new Vector2(32,46), new Vector2(30,4) }
                : new[] { new Vector2(7,4), new Vector2(3,22), new Vector2(13,42), new Vector2(33,49), new Vector2(54,37), new Vector2(60,17), new Vector2(54,4) };
            var active = (Vector2[])idle.Clone(); active[2] += new Vector2(player ? 5 : 3, -4); active[3] += new Vector2(player ? 5 : 3, -4);
            var first = MakeFrame(w, h, pivot, idle, color, "idle"); var second = MakeFrame(w, h, pivot, active, color, "action");
            actor.visual.sprite = first.Original; actor.visual.transform.localPosition = Vector3.zero; actor.visual.transform.localScale = Vector3.one;
            var source = BindShadow(actor.visual, new[] { new SpriteShadowFrame { sprite = first.Original, vertices = first.ShadowVertices, edges = first.ShadowEdges } });
            var binding = actor.visual.gameObject.AddComponent<SpriteLightingBinding>(); binding.Bind(actor.visual, player ? outlined : actorLit, new[] { first, second }, source);
            spriteBindings.Add(binding); actors.Add(new ActorFixture { Actor = actor, Idle = first, Attack = second });
        }
        SpriteLightingFrame MakeFrame(int width, int height, Vector2 pivot, Vector2[] polygon, Color color, string label)
        {
            var texture = Own(new Texture2D(width, height, TextureFormat.RGBA32, false) { name = "Diagnostic " + label, filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp });
            var normal = Own(new Texture2D(width, height, TextureFormat.RGBA32, false, true) { name = "Diagnostic normal " + label, filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp });
            var flat = Own(new Texture2D(width, height, TextureFormat.RGBA32, false, true) { name = "Diagnostic flat normal " + label, filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp });
            var pixels = new Color[width * height]; var normals = new Color[width * height]; var flatNormals = new Color[width * height];
            for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
            {
                bool inside = Inside(new Vector2(x + .5f, y + .5f), polygon);
                pixels[y * width + x] = inside ? color : Color.clear;
                var n = new Vector3((x / (float)width - .5f) * 1.2f, (y / (float)height - .5f) * .5f, 1).normalized;
                normals[y * width + x] = new Color(n.x * .5f + .5f, n.y * .5f + .5f, n.z * .5f + .5f, 1);
                flatNormals[y * width + x] = new Color(.5f, .5f, 1, 1);
            }
            texture.SetPixels(pixels); texture.Apply(); normal.SetPixels(normals); normal.Apply(); flat.SetPixels(flatNormals); flat.Apply();
            var rect = new Rect(0, 0, width, height);
            // Explicit flat secondary data avoids retaining a previous sprite's
            // secondary texture in runtime-created SpriteRenderer batches.
            var flatSprite = Own(Sprite.Create(texture, rect, pivot, 32, 0, SpriteMeshType.FullRect, Vector4.zero, false,
                new[] { new SecondarySpriteTexture { name = "_NormalMap", texture = flat } }));
            var litSprite = Own(Sprite.Create(texture, rect, pivot, 32, 0, SpriteMeshType.FullRect, Vector4.zero, false,
                new[] { new SecondarySpriteTexture { name = "_NormalMap", texture = normal } }));
            var points = new Vector3[polygon.Length];
            for (int i = 0; i < points.Length; i++) points[i] = (polygon[i] - pivot * new Vector2(width, height)) / 32;
            return new SpriteLightingFrame { Original = flatSprite, Flat = flatSprite, Lit = litSprite, ShadowVertices = points, ShadowEdges = Loop(points.Length) };
        }
        static bool Inside(Vector2 p, Vector2[] polygon)
        {
            bool inside = false;
            for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
                if ((polygon[i].y > p.y) != (polygon[j].y > p.y) && p.x < (polygon[j].x - polygon[i].x) * (p.y - polygon[i].y) / (polygon[j].y - polygon[i].y) + polygon[i].x) inside = !inside;
            return inside;
        }
        public static Vector3[] Box(float halfWidth, float halfHeight) => new[] { new Vector3(-halfWidth, -halfHeight), new Vector3(-halfWidth, halfHeight), new Vector3(halfWidth, halfHeight), new Vector3(halfWidth, -halfHeight) };
        public static int[] Loop(int count) { var edges = new int[count * 2]; for (int i = 0; i < count; i++) { edges[i * 2] = i; edges[i * 2 + 1] = (i + 1) % count; } return edges; }
        LightEmitterView Emitter(LightEmitterView template, string id, Vector2 at, Transform parent = null)
        {
            var view = Instantiate(template, parent ? parent : transform, false); view.transform.localPosition = at;
            view.Bind(catalog.Emitter(id), (uint)(emitters.Count + 1)); emitters.Add(view); return view;
        }
        void BuildWindow()
        {
            // Four rear-wall pieces leave a real render aperture; no fake transparent
            // window sprite is placed over an intact opaque wall.
            Color wall = new Color(.47f, .49f, .53f);
            RectArt("Rear wall left", new Vector2(-6.75f, 3), new Vector2(2.5f, 6), wall, -30);
            RectArt("Rear wall right", new Vector2(2.25f, 3), new Vector2(11.5f, 6), wall, -30);
            RectArt("Rear wall below aperture", new Vector2(-4.5f, 1), new Vector2(2, 2), wall, -30);
            RectArt("Rear wall above aperture", new Vector2(-4.5f, 5), new Vector2(2, 2), wall, -30);
            RectArt("Window exterior", new Vector2(-4.5f, 3), new Vector2(2, 2), new Color(.17f, .27f, .44f), -40, material: unlit);
            foreach (var p in new[] { new Vector2(-5.5f, 3), new Vector2(-3.5f, 3), new Vector2(-4.5f, 2), new Vector2(-4.5f, 4) })
            {
                bool vertical = p.y == 3;
                // These pieces trim the emitting aperture; treating the whole trim
                // as a point-light occluder cuts holes in its own area illumination.
                // Structural walls, floors and actors retain their shadow casters.
                RectArt("Window frame", p, vertical ? new Vector2(.12f, 2.1f) : new Vector2(2.1f, .12f), new Color(.3f, .24f, .18f), -20);
            }
            var window = Node("Angled aperture window").transform; window.localPosition = new Vector2(-4.5f, 3);
            var emitter = Emitter(bindings.windowTemplate, "window", Vector2.zero, window);
            Window = window.gameObject.AddComponent<WindowLightView>();
            Window.Bind(new Rect(-1, -1, 2, 2), new Rect(-3.5f, -3.5f, 16, 6.5f), catalog.WindowBeam, emitter, bindings.windowBeamShader, 0, -25);
            var second = Node("Second diagnostic window").transform; second.localPosition = new Vector2(4.5f, 3);
            SecondWindow = second.gameObject.AddComponent<WindowLightView>();
            SecondWindow.Bind(new Rect(-1, -1, 2, 2), new Rect(-12.5f, -3.5f, 16, 6.5f), catalog.WindowBeam,
                Emitter(bindings.windowTemplate, "window", Vector2.zero, second), bindings.windowBeamShader, 0, -25);
            SecondWindow.SetAngle(180); SecondWindow.SetEnabled(false);
        }
        void BuildLantern()
        {
            var root = Node("Reactive ceiling lantern"); root.transform.localPosition = new Vector2(0, 3.4f);
            RectArt("Ceiling mount", new Vector2(0, .1f), new Vector2(.6f, .15f), new Color(.3f, .28f, .24f), 5, root.transform);
            var swing = Node("Swing pivot", root.transform).transform; float length = catalog.Pendulum.Length;
            var top = Node("Ceiling connection socket", root.transform).transform; top.localPosition = new Vector2(0, .025f);
            var bottom = Node("Lantern connection socket", swing).transform; bottom.localPosition = new Vector2(0, -length + .45f);
            if (!bindings.connectorShader) throw new InvalidOperationException("Rebuild Lighting Lab bindings to include the connector shader.");
            Suspension = Node("Chain or rope connection", root.transform).AddComponent<HangingConnectorView>();
            Suspension.Bind(top, bottom, catalog.Suspension, Own(new Material(bindings.connectorShader)), 0, 5, connectorArt);
            RectArt("Neutral lantern glass", new Vector2(0, -length), new Vector2(.55f, .75f), new Color(.45f, .42f, .34f), 5, swing);
            RectArt("Flame", new Vector2(0, -length), new Vector2(.16f, .28f), new Color(1, .65f, .2f), 6, swing, unlit);
            RectArt("Lantern cap", new Vector2(0, -length + .4f), new Vector2(.7f, .1f), new Color(.3f, .25f, .18f), 7, swing);
            RectArt("Lantern base", new Vector2(0, -length - .4f), new Vector2(.7f, .1f), new Color(.3f, .25f, .18f), 7, swing);
            lanternEmitter = Emitter(bindings.lanternTemplate, "lantern", new Vector2(0, -length), swing);
            Lantern = root.AddComponent<ReactiveLanternBridge>(); Lantern.Bind(Arena, catalog.Pendulum, swing, new Vector2(.7f, .9f), lanternEmitter, Suspension);
        }
        void BuildTorch()
        {
            RectArt("Fixed torch handle", new Vector2(5, 2.2f), new Vector2(.15f, .8f), new Color(.3f, .2f, .12f), 5);
            RectArt("Torch flame", new Vector2(5, 2.7f), new Vector2(.25f, .4f), new Color(1, .6f, .2f), 6, material: unlit);
            Emitter(bindings.lanternTemplate, "lantern", new Vector2(5, 2.7f));
        }
        void StepLighting(CombatCompletedStep step)
        {
            if (!isActiveAndEnabled) return;
            if (zonesEnabled && step.Delta > 0) foreach (var zone in zones) zone.Sample(Arena.Player.Position.x, Arena.Player.Position.y);
            director.Advance(step.Delta); foreach (var emitter in emitters) if (emitter) emitter.Sample(step.Now); ApplyEnvironment();
        }
        void BuildZones()
        { foreach (var zone in catalog.Zones) zones.Add(new LightingZoneTracker(director, zone, catalog.Profile(zone.ProfileId))); }
        void DisposeZones()
        { foreach (var zone in zones) zone.Dispose(); zones.Clear(); }
        void ResetLighting()
        { sunset?.Dispose(); sunset = null; pause?.Dispose(); pause = null; DisposeZones(); director?.Dispose(); director = new LightingDirector(catalog.Profile("indoor")); BuildZones(); ApplyEnvironment(); }
        void ApplyEnvironment()
        {
            if (!ambient || director == null) return;
            var state = director.Current; ambient.color = new Color(state.Color.R, state.Color.G, state.Color.B); ambient.intensity = state.Intensity;
        }
        public void SetSunset(bool enabled) { sunset?.Dispose(); sunset = enabled ? director.RequestProfile("lighting lab sunset", catalog.Profile("sunset"), 10, .8f) : null; }
        public void SetZones(bool enabled)
        { zonesEnabled = enabled; if (!enabled) foreach (var zone in zones) zone.Reset(); }
        public void SetPaused(bool enabled) { pause?.Dispose(); pause = null; if (enabled) { pause = Arena.Controls.OpenScope("lighting lab pause"); pause.PauseSimulation(); pause.BlockInput(InputChannels.Combat); } }
        public void SetNormals(bool enabled) { normalsOn = enabled; foreach (var binding in spriteBindings) { binding.NormalsEnabled = enabled; binding.Apply(); } }
        public void SetLights(bool enabled) { lightsOn = enabled; foreach (var emitter in emitters) if (emitter) emitter.Output.enabled = enabled; Window?.SetEnabled(enabled); SecondWindow?.SetEnabled(enabled && secondWindowOn); }
        public void SetSecondWindow(bool enabled) { secondWindowOn = enabled; SecondWindow.SetEnabled(enabled && lightsOn); }
        public void SetLanternMask(bool enabled)
        {
            if (enabled && !diagnosticCookie)
            {
                var texture = Own(new Texture2D(64, 64, TextureFormat.RGBA32, false, true) { name = "Diagnostic lantern transmission", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Point });
                var pixels = new Color[64 * 64];
                for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++)
                {
                    float dx = x - 31.5f, dy = y - 31.5f;
                    bool open = Mathf.Abs(dx) + Mathf.Abs(dy) < 31 && Mathf.Abs(dx) > 2 && Mathf.Abs(dy) > 2;
                    pixels[y * 64 + x] = open ? Color.white : Color.black;
                }
                texture.SetPixels(pixels); texture.Apply();
                diagnosticCookie = Own(Sprite.Create(texture, new Rect(0, 0, 64, 64), Vector2.one * .5f, 32));
            }
            lanternEmitter.SetTransmissionMask(enabled ? diagnosticCookie : null); lanternMaskOn = enabled;
        }
        public void SetWindowOpening(int mode)
        {
            WindowAperture shape;
            if (mode == 0) shape = WindowAperture.Rectangle(new WindowRect(-1, -1, 1, 1));
            else if (mode == 1)
            {
                var pieces = new WindowPoint[4][]; int index = 0;
                foreach (float x in new[] { -1f, .14f }) foreach (float y in new[] { -1f, .14f })
                    pieces[index++] = new[] { new WindowPoint(x, y), new WindowPoint(x + .86f, y), new WindowPoint(x + .86f, y + .86f), new WindowPoint(x, y + .86f) };
                shape = new WindowAperture(pieces);
            }
            else if (mode == 2) shape = new WindowAperture(new[] { new WindowPoint(-1, -1), new WindowPoint(1, -1), new WindowPoint(1, 0),
                new WindowPoint(.866f, .5f), new WindowPoint(.5f, .866f), new WindowPoint(0, 1), new WindowPoint(-.5f, .866f), new WindowPoint(-.866f, .5f), new WindowPoint(-1, 0) });
            else throw new ArgumentOutOfRangeException(nameof(mode));
            Window.SetAperture(shape); openingMode = mode;
        }
        public void SetWindowAngle(float degrees) => Window.SetAngle(degrees);
        public void SetConnectorStyle(HangingConnectorStyle style) => Suspension.SetStyle(style);
        void LateUpdate() { if (Ready) SamplePresentation(); }
        public void SamplePresentation() { DrawActors(); foreach (var binding in spriteBindings) binding.Apply(); Suspension?.SampleNow(); if (Window) Window.SampleNow(); if (SecondWindow) SecondWindow.SampleNow(); }
        void DrawActors()
        {
            foreach (var fixture in actors)
            {
                var actor = fixture.Actor; if (!actor || !actor.visual) continue;
                actor.visual.sprite = actor.currentCast ? fixture.Attack.Original : fixture.Idle.Original;
                actor.visual.flipX = actor.facing < 0;
            }
        }
        void OnGUI()
        {
            if (!Ready) return;
            float panelX = Mathf.Max(12, Screen.width - 367);
            if (GUI.Button(new Rect(panelX, 12, 355, 24), showControls ? "Hide lighting controls" : "Show lighting controls")) showControls = !showControls;
            if (!showControls) return;
            GUILayout.BeginArea(new Rect(panelX, 42, 355, 470), GUI.skin.box);
            GUILayout.Label("Lighting Lab - synthetic diagnostic art");
            GUILayout.Label(Deviant.Combat.PlayerControls.Prompt(Arena.Player, "{Move} move | {Jump} jump | {Weapon1Basic}/{Weapon2Basic} attack | {DevelopmentReset} reset"));
            if (GUILayout.Button(sunset == null ? "Blend to sunset" : "Return to indoor")) SetSunset(sunset == null);
            if (GUILayout.Button(Arena.Controls.Current.SimulationPaused ? "Resume simulation" : "Pause simulation")) SetPaused(!Arena.Controls.Current.SimulationPaused);
            if (GUILayout.Button(normalsOn ? "Normals: ON (click for flat A/B)" : "Normals: OFF")) SetNormals(!normalsOn);
            if (GUILayout.Button(lightsOn ? "Local lights: ON" : "Local lights: OFF")) SetLights(!lightsOn);
            if (GUILayout.Button(Suspension.Style == HangingConnectorStyle.Chain ? "Connection: CHAIN (switch to rope)" : "Connection: ROPE (switch to chain)"))
                SetConnectorStyle(Suspension.Style == HangingConnectorStyle.Chain ? HangingConnectorStyle.Rope : HangingConnectorStyle.Chain);
            GUILayout.Label($"Window angle: {Window.AngleDegrees:F0} degrees (0=right, negative=down)");
            float angle = GUILayout.HorizontalSlider(Window.AngleDegrees, -180, 180);
            if (!Mathf.Approximately(angle, Window.AngleDegrees)) SetWindowAngle(angle);
            if (GUILayout.Button("Opening: " + new[] { "RECTANGLE", "FOUR PANES", "POLYGON ARCH" }[openingMode])) SetWindowOpening((openingMode + 1) % 3);
            if (GUILayout.Button(secondWindowOn ? "Second window: ON" : "Second window: OFF")) SetSecondWindow(!secondWindowOn);
            if (GUILayout.Button(lanternMaskOn ? "Lantern transmission: POLYGON" : "Lantern transmission: ROUND")) SetLanternMask(!lanternMaskOn);
            if (GUILayout.Button(zonesEnabled ? "Zone blending: ON" : "Zone blending: OFF")) SetZones(!zonesEnabled);
            var zoneDefinition = catalog.Zones.Count > 0 ? catalog.Zones[0] : null;
            if (zoneDefinition != null) GUILayout.Label($"Warm zone x={zoneDefinition.Left:F1}..{zoneDefinition.Right:F1} | active {ActiveZoneCount} | entries {ZoneEntryCount}");
            GUILayout.Label($"Lantern angle {Lantern.State.Angle * Mathf.Rad2Deg:F1} | body {Lantern.BodyContacts} | attacks {Lantern.State.AcceptedAttacks}");
            if (Lantern.SampleOverflow) GUILayout.Label("WARNING: environment sweep sample capacity exceeded");
            GUILayout.EndArea();
        }
        void OnDestroy()
        {
            if (Arena) { Arena.SimulationCompleted -= StepLighting; Arena.ResetCompleted -= ResetLighting; }
            pause?.Dispose(); sunset?.Dispose(); DisposeZones(); director?.Dispose();
            foreach (var value in owned) if (value) Destroy(value);
        }
        void OnDisable()
        {
            if (!Ready) return;
            foreach (var zone in zones) zone.Reset();
            sunset?.Dispose(); sunset = null; pause?.Dispose(); pause = null;
        }
    }
}
