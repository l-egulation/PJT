using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Deviant.Definitions;
using Deviant.Presentation.Unity;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace Deviant.Presentation.Editor
{
    public static class HitFeedbackAssetBuilder
    {
        [Serializable] sealed class ChapterBodyCatalog { public ChapterBodyMonster[] monsters; }
        [Serializable] sealed class ChapterBodyMonster { public ChapterBodyClip[] clips; }
        [Serializable] sealed class ChapterBodyClip { public string id; public string[] paths; }
        static IEnumerable<Sprite> ChapterTwoBodies()
        {
            var source=Resources.Load<TextAsset>("Chapter2MonsterArt");if(!source)yield break;
            foreach(var monster in JsonUtility.FromJson<ChapterBodyCatalog>(source.text).monsters) {
                var clip=Array.Find(monster.clips,c=>c.id=="idle");
                var sprite=Resources.Load<Sprite>(clip.paths[0]);
                if(!sprite)throw new InvalidOperationException("Missing Chapter2 debris body: "+clip.paths[0]);
                yield return sprite;
            }
        }
        const string Output="Assets/Deviant/Presentation/Resources/HitFeedback/Generated";
        // Generated bindings are git-ignored, so every checkout must build them locally.
        public static bool BindingsExist
        {
            get
            {
                var bindings=AssetDatabase.LoadAssetAtPath<HitFeedbackBindings>(Output+"/Bindings.asset");
                var source=Resources.Load<TextAsset>("HitFeedback/HitFeedback");
                if(!bindings||!source||bindings.sourceHash!=Hash128.Compute(source.text).ToString()||bindings.prefabs==null)return false;
                var settings=new HitFeedbackSettings(JsonUtility.FromJson<HitFeedbackData>(source.text));
                var debris=AssetDatabase.LoadAssetAtPath<DebrisCatalog>(Output+"/Debris.asset");
                if(ChapterTwoBodies().Any(sprite=>!debris||!debris.Find(sprite)||!debris.Find(sprite).IsCurrentSource))return false;
                if(StaleRecipeOutputs(settings).Any())return false;
                return bindings.prefabs.Length==settings.RecipeKeys.Count()&&settings.RecipeKeys.All(key=>
                    bindings.prefabs.Count(row=>row!=null&&row.key==key&&row.prefab)==1);
            }
        }
        // Recipe prefabs/materials left from an older catalog. The output sits under Resources, so a leftover
        // would still ship in the player even though no recipe binds it.
        static IEnumerable<string> StaleRecipeOutputs(HitFeedbackSettings settings)
        {
            var keys=new HashSet<string>(settings.RecipeKeys,StringComparer.Ordinal);
            if(!AssetDatabase.IsValidFolder(Output))yield break;
            foreach(var path in AssetDatabase.FindAssets("",new[]{Output}).Select(AssetDatabase.GUIDToAssetPath).Distinct())
            {
                string name=Path.GetFileNameWithoutExtension(path),extension=Path.GetExtension(path);
                string key=extension==".prefab"?name:extension==".mat"&&name.LastIndexOf('-')>0?name.Substring(0,name.LastIndexOf('-')):null;
                if(key!=null&&!keys.Contains(key))yield return path;
            }
        }
        [MenuItem("Tools/deviant/Presentation/Build Hit Feedback Bindings")]
        public static void Build()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Generate feedback bindings outside Play Mode; active sessions keep their starting media.");
            Directory.CreateDirectory(Output);AssetDatabase.Refresh();
            var source=Resources.Load<TextAsset>("HitFeedback/HitFeedback");
            if(!source)throw new InvalidOperationException("Export canonical hit-feedback settings first.");
            var settings=new HitFeedbackSettings(JsonUtility.FromJson<HitFeedbackData>(source.text));
            foreach(var stale in StaleRecipeOutputs(settings).ToArray())AssetDatabase.DeleteAsset(stale);
            var shader=Shader.Find("Deviant/HitFeedback/Particle");
            if(!shader)throw new InvalidOperationException("Hit particle shader is missing.");
            var bindings=AssetDatabase.LoadAssetAtPath<HitFeedbackBindings>(Output+"/Bindings.asset");
            if(!bindings){bindings=ScriptableObject.CreateInstance<HitFeedbackBindings>();AssetDatabase.CreateAsset(bindings,Output+"/Bindings.asset");}
            var rows=new List<HitPrefabBinding>();
            foreach(var key in settings.RecipeKeys)
            {
                string filename=settings.SourcePrefab(key);
                var match=AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/ThirdParty/Wallcoeur"}).Select(AssetDatabase.GUIDToAssetPath)
                    .Where(p=>Path.GetFileNameWithoutExtension(p)==filename).ToArray();
                if(match.Length!=1)throw new InvalidOperationException("Restore the licensed VFX 1.0 package; expected one prefab: "+filename);
                var sourcePrefab=AssetDatabase.LoadAssetAtPath<GameObject>(match[0]);
                var root=new GameObject(key);float lifetime=0;
                try
                {
                    var instance=Object.Instantiate(sourcePrefab,root.transform);instance.name=filename;
                    foreach(var component in root.GetComponentsInChildren<Component>(true))
                        if(!component || !(component is Transform || component is ParticleSystem || component is ParticleSystemRenderer))
                            throw new InvalidOperationException("Unsupported source component in "+filename);
                    foreach(var system in root.GetComponentsInChildren<ParticleSystem>(true))
                    {
                        system.Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);var main=system.main;
                        if(main.loop)throw new InvalidOperationException("Looping source requires an explicitly authored variant.");
                        main.playOnAwake=false;main.stopAction=ParticleSystemStopAction.None;main.simulationSpeed=1;
                        main.useUnscaledTime=false;main.cullingMode=ParticleSystemCullingMode.AlwaysSimulate;
                        main.maxParticles=Mathf.Min(main.maxParticles,128);
                        var collision=system.collision;if(collision.enabled)throw new InvalidOperationException("Particle collisions are not permitted in hit media.");
                        // Conservative sum includes nested emitter and trail tails; never truncates a child.
                        lifetime+=Maximum(main.startDelay)+main.duration+Maximum(main.startLifetime)+(system.trails.enabled?Maximum(system.trails.lifetime):0);
                        var renderer=system.GetComponent<ParticleSystemRenderer>();var original=renderer.sharedMaterials;var materials=new Material[original.Length];
                        for(int i=0;i<original.Length;i++)
                        {
                            var old=original[i];if(!old)throw new InvalidOperationException("Source particle material missing.");
                            string path=Output+"/"+key+"-"+AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(old))+".mat";
                            var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
                            if(!mat){mat=new Material(shader);AssetDatabase.CreateAsset(mat,path);}else mat.shader=shader;
                            mat.SetTexture("_MainTex",old.mainTexture);
                            mat.SetColor("_Color",old.HasProperty("_TintColor")?old.GetColor("_TintColor"):old.HasProperty("_Color")?old.GetColor("_Color"):Color.white);
                            mat.SetFloat("_DstBlend",old.name.IndexOf("Alpha",StringComparison.OrdinalIgnoreCase)>=0?10:1);
                            EditorUtility.SetDirty(mat);materials[i]=mat;
                        }
                        renderer.sharedMaterials=materials;renderer.sortingOrder=60;
                    }
                    root.SetActive(false);
                    string prefabPath=Output+"/"+key+".prefab";
                    var prefab=PrefabUtility.SaveAsPrefabAsset(root,prefabPath);
                    PrefabCueFactory.Validate(prefab);
                    rows.Add(new HitPrefabBinding{key=key,prefab=prefab,duration=lifetime,version=AssetDatabase.GetAssetDependencyHash(prefabPath).ToString()});
                }
                finally{Object.DestroyImmediate(root);}
            }
            AssetDatabase.SaveAssets();
            foreach(var row in rows)row.version=AssetDatabase.GetAssetDependencyHash(AssetDatabase.GetAssetPath(row.prefab)).ToString();
            bindings.prefabs=rows.ToArray();bindings.sourceHash=Hash128.Compute(source.text).ToString();
            bindings.distortionShader=Shader.Find("Hidden/Deviant/HitWorldDistortion");EditorUtility.SetDirty(bindings);
            BuildDebris(settings.DebrisPieces);
            AssetDatabase.SaveAssets();AssetDatabase.Refresh();
            Debug.Log("HIT_FEEDBACK_BUILD_OK bindings="+rows.Count+" durations="+string.Join(",",rows.Select(r=>r.key+":"+r.duration)));
        }
        static float Maximum(ParticleSystem.MinMaxCurve curve)
        {
            float maximum=Mathf.Max(curve.constantMin,curve.constantMax);
            if(curve.mode==ParticleSystemCurveMode.Curve || curve.mode==ParticleSystemCurveMode.TwoCurves)
                for(int i=0;i<=128;i++)maximum=Mathf.Max(maximum,curve.Evaluate(i/128f,1));
            if(float.IsNaN(maximum)||float.IsInfinity(maximum)||maximum<0)throw new InvalidOperationException("Non-finite particle lifetime.");
            return maximum;
        }
        static void BuildDebris(int count)
        {
            var catalog=AssetDatabase.LoadAssetAtPath<DebrisCatalog>(Output+"/Debris.asset");
            if(!catalog){catalog=ScriptableObject.CreateInstance<DebrisCatalog>();AssetDatabase.CreateAsset(catalog,Output+"/Debris.asset");}
            var bakes=new List<DebrisBakeAsset>();
            var previousBakes=catalog.bakes==null?Array.Empty<DebrisBakeAsset>():catalog.bakes.ToArray();
            var sprites=new HashSet<Sprite>();
            // Only explicitly authored body bindings, never every asset named Monster.
            foreach(var guid in AssetDatabase.FindAssets("t:Chapter1MonsterCatalog"))
            {
                string path=AssetDatabase.GUIDToAssetPath(guid);if(!path.StartsWith("Assets/ForestCombatV2/",StringComparison.Ordinal))continue;
                var so=new SerializedObject(AssetDatabase.LoadAssetAtPath<Object>(path));var clips=so.FindProperty("clips");
                if(clips==null||!clips.isArray)continue;
                for(int i=0;i<clips.arraySize;i++)
                {
                    var clip=clips.GetArrayElementAtIndex(i);if(clip.FindPropertyRelative("motion").stringValue!="idle")continue;
                    var frames=clip.FindPropertyRelative("frames");
                    if(frames.arraySize>0&&frames.GetArrayElementAtIndex(0).objectReferenceValue is Sprite body)sprites.Add(body);
                }
            }
            // Existing forest data explicitly supplies its actual first body frame.
            foreach(var guid in AssetDatabase.FindAssets("t:InkMonsterData"))
            {
                string path=AssetDatabase.GUIDToAssetPath(guid);if(!path.StartsWith("Assets/Deviant/World/",StringComparison.Ordinal))continue;
                var data=AssetDatabase.LoadAssetAtPath<Object>(path);
                var so=new SerializedObject(data);var frames=so.FindProperty("move.frames");
                if(frames!=null && frames.isArray && frames.arraySize>0 && frames.GetArrayElementAtIndex(0).objectReferenceValue is Sprite sprite)sprites.Add(sprite);
            }
            // Current chapter monsters explicitly bind their approved idle body.
            var village=AssetDatabase.LoadAssetAtPath<Object>("Assets/Deviant/Stage1Monsters/Data/ApprovedStage1Monsters.asset");
            if(village)
            {
                var monsters=new SerializedObject(village).FindProperty("monsters");
                for(int i=0;i<monsters.arraySize;i++)
                {
                    var motions=monsters.GetArrayElementAtIndex(i).FindPropertyRelative("motions");
                    for(int j=0;j<motions.arraySize;j++)
                    {
                        var motion=motions.GetArrayElementAtIndex(j);
                        if(motion.FindPropertyRelative("id").stringValue!="idle")continue;
                        var frames=motion.FindPropertyRelative("frames");
                        if(frames.arraySize>0&&frames.GetArrayElementAtIndex(0).objectReferenceValue is Sprite body)sprites.Add(body);
                    }
                }
            }
            foreach(var body in ChapterTwoBodies())sprites.Add(body);
            var pending=new List<Tuple<string,DebrisBakeAsset>>();
            try
            {
                foreach(var sprite in sprites.OrderBy(AssetDatabase.GetAssetPath,StringComparer.Ordinal))
                {
                    AssetDatabase.TryGetGUIDAndLocalFileIdentifier(sprite,out string guid,out long local);
                    pending.Add(Tuple.Create(Output+"/debris-"+guid+"-"+local+".asset",DebrisBaker.Bake(sprite,count,109)));
                }
            }
            catch
            {
                foreach(var candidate in pending){foreach(var piece in candidate.Item2.pieces){Object.DestroyImmediate(piece.mesh);Object.DestroyImmediate(piece.mirroredMesh);}Object.DestroyImmediate(candidate.Item2);}
                throw;
            }
            // Geometry validation above completes before replacing any owned cache.
            foreach(var candidate in pending)
            {
                if(AssetDatabase.LoadAssetAtPath<DebrisBakeAsset>(candidate.Item1))AssetDatabase.DeleteAsset(candidate.Item1);
                var bake=candidate.Item2;AssetDatabase.CreateAsset(bake,candidate.Item1);
                foreach(var piece in bake.pieces){AssetDatabase.AddObjectToAsset(piece.mesh,bake);AssetDatabase.AddObjectToAsset(piece.mirroredMesh,bake);}bakes.Add(bake);
            }
            catalog.bakes=bakes.ToArray();EditorUtility.SetDirty(catalog);
            foreach(var old in previousBakes)if(old&&!bakes.Contains(old))
            {
                string path=AssetDatabase.GetAssetPath(old);
                if(path.StartsWith(Output+"/debris-",StringComparison.Ordinal))AssetDatabase.DeleteAsset(path);
            }
            Debug.Log("HIT_FEEDBACK_DEBRIS_BAKES="+bakes.Count);
        }
    }
    public static class DebrisBaker
    {
        public static DebrisBakeAsset Bake(Sprite sprite,int count,int seed)
        {
            if(!sprite || sprite.packed || count<2 || count>32)throw new ArgumentException("Unpacked source Sprite and 2..32 pieces required.");
            string path=AssetDatabase.GetAssetPath(sprite.texture);
            byte[] bytes=File.ReadAllBytes(path);var image=new Texture2D(2,2,TextureFormat.RGBA32,false);
            try
            {
                if(!image.LoadImage(bytes) || image.width!=sprite.texture.width || image.height!=sprite.texture.height)
                    throw new InvalidOperationException("Debris source must match the imported Sprite pixels without atlas/downscaling.");
                var rect=sprite.rect;int w=(int)rect.width,h=(int)rect.height;var colors=image.GetPixels32();var opaque=new List<Vector2Int>();
                for(int y=0;y<h;y++)for(int x=0;x<w;x++)if(colors[((int)rect.y+y)*image.width+(int)rect.x+x].a>2)opaque.Add(new Vector2Int(x,y));
                if(opaque.Count<count*4)throw new InvalidOperationException("Insufficient opaque body area for debris bake.");
                var random=new System.Random(seed);var sites=new List<Vector2Int>();sites.Add(opaque[random.Next(opaque.Count)]);
                while(sites.Count<count)
                {var site=opaque.OrderByDescending(p=>sites.Min(s=>(p-s).sqrMagnitude)).First();if(sites.Contains(site))break;sites.Add(site);}
                var regions=sites.Select(_=>new List<Vector2Int>()).ToArray();
                foreach(var p in opaque){int best=0;float distance=float.MaxValue;for(int i=0;i<sites.Count;i++){float d=(p-sites[i]).sqrMagnitude;if(d<distance){distance=d;best=i;}}regions[best].Add(p);}
                var pieces=new List<DebrisPiece>();int accounted=0;
                foreach(var region in regions)
                {
                    if(region.Count==0)continue;accounted+=region.Count;
                    Vector2 center=Vector2.zero;foreach(var p in region)center+=(new Vector2(p.x+.5f,p.y+.5f)-sprite.pivot)/sprite.pixelsPerUnit;center/=region.Count;
                    var vertices=new List<Vector3>();var uv=new List<Vector2>();var triangles=new List<int>();var corners=new List<Vector2>();
                    foreach(var p in region)
                    {
                        int n=vertices.Count;
                        foreach(var delta in new[]{Vector2.zero,Vector2.right,Vector2.one,Vector2.up})
                        {
                            var pixel=new Vector2(p.x,p.y)+delta;var vertex=(pixel-sprite.pivot)/sprite.pixelsPerUnit-center;
                            vertices.Add(vertex);corners.Add(vertex);uv.Add(new Vector2((rect.x+pixel.x)/image.width,(rect.y+pixel.y)/image.height));
                        }
                        triangles.AddRange(new[]{n,n+2,n+1,n,n+3,n+2});
                    }
                    var mesh=new Mesh{name="Debris piece"};mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();
                    var mirrored=new Mesh{name="Debris mirrored piece"};mirrored.SetVertices(vertices.Select(v=>new Vector3(-v.x,v.y,v.z)).ToList());mirrored.SetUVs(0,uv);
                    var indices=triangles.ToArray();for(int i=0;i<indices.Length;i+=3){int temp=indices[i+1];indices[i+1]=indices[i+2];indices[i+2]=temp;}mirrored.SetTriangles(indices,0);mirrored.RecalculateBounds();
                    pieces.Add(new DebrisPiece{center=center,mesh=mesh,mirroredMesh=mirrored,hull=Hull(corners),area=region.Count/(sprite.pixelsPerUnit*sprite.pixelsPerUnit)});
                }
                if(accounted!=opaque.Count)throw new InvalidOperationException("Debris bake lost or duplicated opaque area.");
                var bake=ScriptableObject.CreateInstance<DebrisBakeAsset>();bake.source=sprite;bake.seed=seed;bake.pieces=pieces.ToArray();
                bake.sourceRect=sprite.rect;bake.sourcePivot=sprite.pivot;bake.pixelsPerUnit=sprite.pixelsPerUnit;
                bake.dependencyHash=AssetDatabase.GetAssetDependencyHash(AssetDatabase.GetAssetPath(sprite)).ToString();
                using(var sha=SHA256.Create())bake.sourceHash=BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();
                return bake;
            }
            finally{Object.DestroyImmediate(image);}
        }
        static float Cross(Vector2 a,Vector2 b,Vector2 c)=>(b.x-a.x)*(c.y-a.y)-(b.y-a.y)*(c.x-a.x);
        static Vector2[] Hull(List<Vector2> points)
        {
            var sorted=points.Distinct().OrderBy(p=>p.x).ThenBy(p=>p.y).ToArray();var hull=new List<Vector2>();
            foreach(var p in sorted){while(hull.Count>=2 && Cross(hull[hull.Count-2],hull[hull.Count-1],p)<=0)hull.RemoveAt(hull.Count-1);hull.Add(p);}
            int lower=hull.Count;
            for(int i=sorted.Length-2;i>=0;i--){var p=sorted[i];while(hull.Count>lower && Cross(hull[hull.Count-2],hull[hull.Count-1],p)<=0)hull.RemoveAt(hull.Count-1);hull.Add(p);}
            hull.RemoveAt(hull.Count-1);return hull.ToArray();
        }
    }
}
