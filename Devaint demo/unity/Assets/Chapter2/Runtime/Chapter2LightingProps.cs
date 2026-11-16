using System;
using System.Collections.Generic;
using Deviant.Combat;
using Deviant.Combat.Contracts;
using Deviant.Definitions;
using Deviant.Integration;
using Deviant.Presentation.Unity;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Deviant.Chapter2
{
    [Serializable] public sealed class Chapter2PropPlacement
    {
        public string id, room, origin;
        public float lanternX, torchX, centerHeight, torchHeight;
    }
    [Serializable] public sealed class Chapter2PropsData
    {
        public int schema;
        public PendulumData pendulum;
        public HangingConnectorData suspension;
        public LightEmitterData lantern, torch;
        public LightEmitterData window;
        public WindowLightData windowBeam;
        public ContactFlashData contactFlash;
        public Chapter2PropPlacement[] placements;
    }

    // Limited art trial in S/F01. Reuses the shared non-blocking contact bridge.
    // Actors/terrain retain their existing materials; only rear walls receive light.
    public sealed class Chapter2LightingProps : MonoBehaviour
    {
        readonly List<ReactiveLanternBridge> lanterns = new List<ReactiveLanternBridge>();
        readonly List<LightEmitterView> fixedLights = new List<LightEmitterView>();
        readonly List<SpriteRenderer> flames = new List<SpriteRenderer>();
        readonly List<WindowLightView> windows = new List<WindowLightView>();
        Chapter2World world;
        Chapter2WaterwayBackground background;
        Transform root;
        Material wallMaterial, connectorMaterial;
        public IReadOnlyList<ReactiveLanternBridge> Lanterns => lanterns;
        public int TorchCount => fixedLights.Count - windows.Count;

        public void Build(Chapter2World owner, Chapter2WaterwayBackground walls)
        {
            if (world) throw new InvalidOperationException("Lighting props already built.");
            world = owner; background = walls;
            root = Node("Waterway lighting trial", transform);
            var art = Resources.Load<Chapter2LightingArt>("Chapter2Lighting/Art");
            var settings = Resources.Load<TextAsset>("Chapter2Lighting/Settings");
            var bindings = Resources.Load<LightingBindings>("Lighting/Bindings");
            if (!art || !settings || !bindings || !art.chain)
                throw new InvalidOperationException("Restore the pinned lighting art and run the Chapter2 lighting importer.");
            var data = JsonUtility.FromJson<Chapter2PropsData>(settings.text);
            if (data.schema != 1 || data.placements == null) throw new ArgumentException("Invalid Chapter2 prop data.");
            var pendulum = new PendulumDefinition(data.pendulum);
            var suspension = new HangingConnectorDefinition(data.suspension);
            var lanternLight = new LightEmitterDefinition(data.lantern);
            var torchLight = new LightEmitterDefinition(data.torch);
            wallMaterial = new Material(bindings.litOutlineShader) { name = "Waterway trial rear-wall lighting" };
            wallMaterial.SetFloat("_OutlineStrength", 0);
            connectorMaterial = new Material(bindings.connectorShader);
            background.SetWallLightingMaterial(wallMaterial);
            var ambient = Node("Rear wall baseline", root).gameObject.AddComponent<Light2D>();
            ambient.lightType = Light2D.LightType.Global; ambient.intensity = 1; ambient.color = Color.white;
            var windowLight = new LightEmitterDefinition(data.window);
            var windowBeam = new WindowLightDefinition(data.windowBeam);
            foreach (var placement in background.Windows)
            {
                var anchor = Node("Waterway window light", root); anchor.position = placement.Position;
                var emitter = Instantiate(bindings.windowTemplate, anchor, false); emitter.Bind(windowLight, (uint)(fixedLights.Count + 1)); fixedLights.Add(emitter);
                var window = anchor.gameObject.AddComponent<WindowLightView>();
                var room = placement.Room; room.position -= placement.Position;
                window.Bind(art.windowOpening, room, windowBeam, emitter, bindings.windowBeamShader, 0, -35);
                var blocker = Instantiate(bindings.shadowTemplate, anchor, false).GetComponent<SpriteShadowSource>();
                blocker.Configure(null, new[] { new SpriteShadowFrame { vertices = art.grilleVertices, edges = art.grilleEdges } });
                window.SetShadowCasters(new[] { blocker });
                windows.Add(window);
                // Only register tiles actually intersecting this finite shaft (four windows per receiver).
                var footprint = new Bounds(window.GetFootprintWorldPoint(0), Vector3.zero);
                for (int i = 1; i < window.FootprintVertexCount; i++) footprint.Encapsulate(window.GetFootprintWorldPoint(i));
                foreach (var wall in background.Walls) if (wall.bounds.Intersects(footprint)) window.AddReceiver(wall);
            }
            foreach (var placement in data.placements)
            {
                var room = world.Layout.Room(placement.room);
                if (room == null) throw new ArgumentException("Missing trial room " + placement.room);
                var at = placement.origin == "spawn" ? world.Layout.spawn.Vector : Floor(room);
                var center = at + new Vector2(placement.lanternX, placement.centerHeight);
                // Avoid attaching a visible prop inside terrain in another layout variant.
                if (Clear(center, new Vector2(.95f, 1.3f)))
                    BuildLantern(placement.id, center, art, bindings, pendulum, suspension, lanternLight, data.contactFlash);
                var torchAt = at + new Vector2(placement.torchX, placement.torchHeight);
                if (Clear(torchAt, new Vector2(.8f, 1.2f))) BuildTorch(placement.id, torchAt, art, bindings, torchLight);
            }
            world.arena.SimulationCompleted += Step;
            world.arena.ResetCompleted += ResetLights;
            world.arena.Deactivated += ResetLights;
        }

        Vector2 Floor(C2Room room)
        {
            float x = room.x + room.w * .5f;
            float? y = world.arena.FindFloor(new Vector2(x, room.y + room.h - 2), room.h);
            return new Vector2(x, y ?? room.y + 1);
        }
        bool Clear(Vector2 center, Vector2 size)
        {
            var box = new Rect(center - size * .5f, size);
            foreach (var surface in world.Layout.surfaces) if (surface.Bounds.Overlaps(box)) return false;
            return true;
        }
        void BuildLantern(string id, Vector2 center, Chapter2LightingArt art, LightingBindings bindings,
            PendulumDefinition pendulum, HangingConnectorDefinition suspension, LightEmitterDefinition definition, ContactFlashData contactFlash)
        {
            var anchor = Node(id + " reactive lantern", root);
            anchor.position = center + Vector2.up * pendulum.Length;
            var mount = Draw("Fixed bracket", art.mount, anchor, 4);
            mount.transform.localPosition = -art.mountChain;
            var swing = Node("Swing", anchor);
            var body = Draw("Matte iron housing", art.housing, swing, 5);
            body.transform.localPosition = Vector2.down * pendulum.Length;
            var flame = Draw("Separate flame", art.flame, body.transform, 6);
            flame.transform.localPosition = art.housingFlame;
            flames.Add(flame);
            var socket = Node("Light socket", body.transform); socket.localPosition = art.housingFlame;
            var emitter = Instantiate(bindings.lanternTemplate, socket, false);
            emitter.Bind(definition, (uint)(lanterns.Count + 11));
            var top = Node("Housing top socket", body.transform); top.localPosition = art.housingTop;
            var connector = Node("Actual iron chain", anchor).gameObject.AddComponent<HangingConnectorView>();
            connector.Bind(anchor, top, suspension, connectorMaterial, 0, 4, art.chain);
            var bridge = anchor.gameObject.AddComponent<ReactiveLanternBridge>();
            bridge.Bind(world.arena, pendulum, swing, art.housing.bounds.size, emitter, connector);
            var flash = body.gameObject.AddComponent<LanternContactFlash>();
            flash.Bind(world.arena, bridge, body, Resources.Load<Shader>("Chapter2Lighting/ContactImpact"), contactFlash);
            lanterns.Add(bridge);
        }
        void BuildTorch(string id, Vector2 center, Chapter2LightingArt art, LightingBindings bindings, LightEmitterDefinition definition)
        {
            var torchRoot = Node(id + " fixed torch", root); torchRoot.position = center;
            Draw("Basket and bracket", art.torch, torchRoot, -29);
            var flame = Draw("Separate flame", art.flame, torchRoot, -28); flame.transform.localPosition = art.torchFlame;
            flames.Add(flame);
            var socket = Node("Light socket", torchRoot); socket.localPosition = art.torchFlame;
            var emitter = Instantiate(bindings.lanternTemplate, socket, false);
            emitter.Bind(definition, (uint)(fixedLights.Count + 37)); fixedLights.Add(emitter);
        }
        static Transform Node(string label, Transform parent)
        {
            var node = new GameObject(label).transform; node.SetParent(parent, false); return node;
        }
        SpriteRenderer Draw(string label, Sprite sprite, Transform parent, int order)
        {
            var renderer = Node(label, parent).gameObject.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite; renderer.sharedMaterial = world.spriteMaterial; renderer.sortingOrder = order;
            return renderer;
        }
        void Step(CombatCompletedStep step)
        {
            if (!isActiveAndEnabled) return;
            foreach (var light in fixedLights) light.Sample(step.Now);
            for (int i = 0; i < flames.Count; i++)
                flames[i].color = new Color(1, 1, 1, .9f + .1f * Mathf.Sin((float)step.Now * 9 + i * 1.7f));
        }
        void ResetLights()
        {
            foreach (var light in fixedLights) light.Sample(0);
            foreach (var flame in flames) flame.color = Color.white;
        }
        void OnEnable()
        {
            if (root) root.gameObject.SetActive(true);
            if (background && wallMaterial) background.SetWallLightingMaterial(wallMaterial);
        }
        void OnDisable()
        {
            if (root) root.gameObject.SetActive(false);
            if (background) background.SetWallLightingMaterial(null);
        }
        void OnDestroy()
        {
            if (world && world.arena)
            {
                world.arena.SimulationCompleted -= Step;
                world.arena.ResetCompleted -= ResetLights;
                world.arena.Deactivated -= ResetLights;
            }
            if (background) background.SetWallLightingMaterial(null);
            if (wallMaterial) Destroy(wallMaterial);
            if (connectorMaterial) Destroy(connectorMaterial);
            if (root) Destroy(root.gameObject);
        }
    }
}
