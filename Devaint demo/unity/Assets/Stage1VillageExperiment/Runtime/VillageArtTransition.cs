using System;
using UnityEngine;
using UnityEngine.InputSystem;
using ForestCombatV2;

namespace Stage1VillageExperiment
{
    public enum VillageArtMode
    {
        Color = 0,
        Sketch = 1,
        Blend = 2,
        Region = 3,
        Authored = 4
    }

    // Presentation-only controls. Authored coordinates are top-left game pixels,
    // independent of camera position, parallax UVs and the common gameplay state.
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(650)]
    public sealed class VillageArtTransition : MonoBehaviour
    {
        public VillageArtMode initialMode = VillageArtMode.Authored;
        public VillageArtMode Mode { get; private set; } = VillageArtMode.Authored;
        public float Weight { get; private set; } = .5f;
        public float BoundaryPixels { get; private set; }
        public float AuthoredSketchUntilPixels { get; private set; }
        public float AuthoredNormalFromPixels { get; private set; }
        public bool CrispRegions { get; private set; }
        // Art-authoring preview split; only the total affected 2/3 is fixed by design.
        [Range(.05f,.6f)] public float completeSketchFraction=1f/3f;

        static readonly int ModeId = Shader.PropertyToID("_VillageArtMode");
        static readonly int WeightId = Shader.PropertyToID("_VillageArtWeight");
        static readonly int BoundaryId = Shader.PropertyToID("_VillageBoundaryPixels");
        static readonly int AuthoredRangeId = Shader.PropertyToID("_VillageAuthoredRange");
        static readonly int CrispId = Shader.PropertyToID("_VillageCrispRegions");
        Stage1VillageWorld world;
        bool boundaryInitialized, authoredInitialized;

        public void ConfigureTeamRegions(ForestData data)
        {
            if(data?.rooms==null||data.rooms.Length==0)throw new InvalidOperationException("Team art regions require the existing map rooms.");
            float left=float.MaxValue,right=float.MinValue;
            foreach(var room in data.rooms){left=Mathf.Min(left,room.x*32);right=Mathf.Max(right,(room.x+room.w)*32);}
            if(right<=left)throw new InvalidOperationException("Empty stage art extent.");
            AuthoredSketchUntilPixels=Mathf.Lerp(left,right,Mathf.Clamp(completeSketchFraction,.05f,.6f));
            AuthoredNormalFromPixels=Mathf.Lerp(left,right,2f/3f);
            authoredInitialized=true;CrispRegions=true;Publish();
        }

        [Serializable]
        sealed class AuthoredLayout
        {
            public InitialSketch initialSketch;
        }

        [Serializable]
        sealed class InitialSketch
        {
            public float untilX, normalFromX;
            public bool worldAnchored;
        }

        void Awake()
        {
            useGUILayout = false;
            world = GetComponent<Stage1VillageWorld>();
        }

        void OnEnable()
        {
            Mode = initialMode;
            InitializeAuthoredRegions();
            InitializeBoundary();
            Publish();
        }

        void InitializeAuthoredRegions()
        {
            if (authoredInitialized) return;
            var presentation = GetComponent<VillagePresentation>();
            var layoutAsset = presentation ? presentation.presentationLayout : null;
            if (!layoutAsset) layoutAsset = Resources.Load<TextAsset>("Village/presentation_layout");
            if (!layoutAsset) throw new InvalidOperationException("Village authored art regions require the presentation layout.");
            var layout = JsonUtility.FromJson<AuthoredLayout>(layoutAsset.text);
            var region = layout?.initialSketch;
            if (region == null || !region.worldAnchored || !(region.normalFromX > region.untilX))
                throw new InvalidOperationException("Village initialSketch requires an increasing, world-anchored pixel range.");
            AuthoredSketchUntilPixels = region.untilX;
            AuthoredNormalFromPixels = region.normalFromX;
            authoredInitialized = true;
        }

        void InitializeBoundary()
        {
            var presentation=GetComponent<VillagePresentation>();
            var data=world?world.Data:presentation?presentation.MapData:null;
            if (boundaryInitialized || data == null || data.spawn == null) return;
            // Only the Region review mode uses this convenient spawn-local boundary.
            BoundaryPixels = data.spawn.x + 150;
            boundaryInitialized = true;
            if (isActiveAndEnabled) Shader.SetGlobalFloat(BoundaryId, BoundaryPixels);
        }

        void Update()
        {
            if (!boundaryInitialized) InitializeBoundary();
            var keyboard = Keyboard.current;
            if (keyboard == null || !Deviant.Combat.DevFeatures.Enabled) return;
            if (keyboard.f9Key.wasPressedThisFrame)
            {
                UseAuthoredRegions();
                return;
            }
            if (keyboard.f6Key.wasPressedThisFrame)
                SetDisplay((VillageArtMode)(((int)Mode + 1) % 5), Weight);
            int direction = (keyboard.f8Key.wasPressedThisFrame ? 1 : 0) -
                (keyboard.f7Key.wasPressedThisFrame ? 1 : 0);
            if (direction == 0) return;
            if (Mode == VillageArtMode.Blend) SetDisplay(Mode, Weight + direction * .05f);
            else if (Mode != VillageArtMode.Authored) SetBoundary(BoundaryPixels + direction * 32);
        }

        public void SetDisplay(VillageArtMode mode, float weight = .5f)
        {
            InitializeAuthoredRegions();
            Mode = mode;
            Weight = Mathf.Clamp01(weight);
            InitializeBoundary();
            if (isActiveAndEnabled) Publish();
        }

        public void UseAuthoredRegions()
        {
            SetDisplay(VillageArtMode.Authored, Weight);
        }

        public void SetBoundary(float worldPixelX)
        {
            BoundaryPixels = worldPixelX;
            boundaryInitialized = true;
            if (isActiveAndEnabled) Shader.SetGlobalFloat(BoundaryId, BoundaryPixels);
        }

        public float SampleWeight(Vector2 worldPixels)
        {
            switch (Mode)
            {
                case VillageArtMode.Color: return 0;
                case VillageArtMode.Sketch: return 1;
                case VillageArtMode.Blend: return Weight;
                default:
                    // A world-position query returns the dominant ground state.
                    // Rendered object/foliage boundaries additionally follow their
                    // source material shapes in VillageCrispRegions.cginc.
                    if(CrispRegions)
                    {
                        float progress=Mode==VillageArtMode.Authored?
                            Mathf.InverseLerp(AuthoredSketchUntilPixels,AuthoredNormalFromPixels,worldPixels.x):
                            1-Mathf.Clamp01((worldPixels.x-BoundaryPixels+240)/480);
                        return progress<.9f?1:0;
                    }
                    // Keep both curves identical to VillageRegion.shader.
                    float curve = Mathf.Sin(worldPixels.y * .004f) * 28 +
                        Mathf.Sin(worldPixels.y * .010f + 1.4f) * 12;
                    if (Mode == VillageArtMode.Authored)
                    {
                        float width = AuthoredNormalFromPixels - AuthoredSketchUntilPixels;
                        float authored = Mathf.Clamp01((worldPixels.x - AuthoredSketchUntilPixels) / width);
                        // The bend vanishes at either endpoint, keeping the entire
                        // transition inside the authored range and monotonically fading.
                        float bend = Mathf.Clamp(curve / width, -.25f, .25f);
                        authored = Mathf.Clamp01(authored - bend * 4 * authored * (1 - authored));
                        return 1 - authored * authored * (3 - 2 * authored);
                    }
                    // Region is the explicit color-left/sketch-right 180px review band.
                    float t = Mathf.Clamp01((worldPixels.x - BoundaryPixels - curve + 90) / 180);
                    return t * t * (3 - 2 * t);
            }
        }

        void Publish()
        {
            Shader.SetGlobalFloat(ModeId, (float)Mode);
            Shader.SetGlobalFloat(CrispId,CrispRegions?1:0);
            Shader.SetGlobalFloat(WeightId, Weight);
            Shader.SetGlobalFloat(BoundaryId, BoundaryPixels);
            Shader.SetGlobalVector(AuthoredRangeId, new Vector4(AuthoredSketchUntilPixels, AuthoredNormalFromPixels, 0, 0));
        }

        static void ResetGlobals()
        {
            Shader.SetGlobalFloat(ModeId, (float)VillageArtMode.Color);
            Shader.SetGlobalFloat(CrispId,0);
            Shader.SetGlobalFloat(WeightId, .5f);
            Shader.SetGlobalFloat(BoundaryId, 0);
            Shader.SetGlobalVector(AuthoredRangeId, Vector4.zero);
        }

        void OnDisable() { ResetGlobals(); }
        void OnDestroy() { ResetGlobals(); }
    }
}
