using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Deviant.Stage1Monsters.Editor
{
    public static class Stage1MonsterImporter
    {
        public const string Root="Assets/Deviant/Stage1Monsters";
        public const string CatalogPath=Root+"/Data/ApprovedStage1Monsters.asset";
        public const string ScenePath="Assets/MeadowDuskV3/MapVer2/Scenes/Chapter1Procedural.unity";
        [Serializable] class Source { public string sourceManifestSHA256,polishManifest,polishSHA256;public string[] palette;public MonsterSource[] monsters;public ClipSource playerReference; }
        [Serializable] class MonsterSource { public string id,displayName;public ClipSource[] motions; }
        [Serializable] class ClipSource { public string id;public int width,height,ppu,ground;public float[] pivot;public bool loop;public int[] milliseconds;public FrameSource[] frames; }
        [Serializable] class FrameSource { public string path,sha256; }
        static string Hash(byte[] data) { using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(data)).Replace("-","").ToLowerInvariant(); }
        public static void ValidatePolish(string path,string sha)
        {
            if(string.IsNullOrEmpty(path))return;
            string root=Path.GetFullPath(Path.Combine(Application.dataPath,"../../resources"));
            string full=Path.GetFullPath(Path.Combine(root,path));
            if(!full.StartsWith(root+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase) || Hash(File.ReadAllBytes(full))!=sha)
                throw new InvalidDataException("Stale or invalid selected monster polish");
        }
        static void Ensure(string path) { Directory.CreateDirectory(path);AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport); }

        [MenuItem("Tools/deviant/Stage 1 Monsters/Import Approved Motions")]
        public static void Import()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var source=JsonUtility.FromJson<Source>(File.ReadAllText(Root+"/Data/import.json"));
            ValidatePolish(source.polishManifest,source.polishSHA256);
            foreach(string folder in new[]{"Animations","Prefabs","Materials"})Ensure(Root+"/"+folder);
            var catalog=AssetDatabase.LoadAssetAtPath<Stage1MonsterCatalog>(CatalogPath);
            if(!catalog) { catalog=ScriptableObject.CreateInstance<Stage1MonsterCatalog>();AssetDatabase.CreateAsset(catalog,CatalogPath); }
            catalog.sourceManifestSHA256=source.sourceManifestSHA256;
            catalog.monsters=source.monsters.Select(mon=>new Stage1MonsterDefinition{
                id=mon.id,displayName=mon.displayName,motions=mon.motions.Select(m=>ImportClip(mon.id,m)).ToArray()}).ToArray();
            var shader=Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default")??Shader.Find("Sprites/Default");
            if(!shader)throw new InvalidOperationException("Sprite unlit shader unavailable");
            string matPath=Root+"/Materials/Stage1PixelUnlit.mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if(!material) { material=new Material(shader);AssetDatabase.CreateAsset(material,matPath); }
            foreach(var mon in catalog.monsters)
            {
                string controllerPath=Root+"/Animations/"+mon.id+".controller";
                var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
                if(!controller)controller=AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
                var machine=controller.layers[0].stateMachine;
                foreach(var child in machine.states)machine.RemoveState(child.state);
                foreach(var motion in mon.motions)
                {
                    var state=machine.AddState(motion.id);state.motion=motion.animation;state.writeDefaultValues=false;
                    if(motion.id==(mon.Find("idle")!=null?"idle":"base"))machine.defaultState=state;
                }
                EditorUtility.SetDirty(controller);mon.controller=controller;
                var go=new GameObject(mon.id);var renderer=go.AddComponent<SpriteRenderer>();renderer.sharedMaterial=material;renderer.sprite=mon.Find("base").frames[0];
                var player=go.AddComponent<Stage1MonsterPlayer>();player.catalog=catalog;player.monsterId=mon.id;
                mon.prefab=PrefabUtility.SaveAsPrefabAsset(go,Root+"/Prefabs/"+mon.id+".prefab");UnityEngine.Object.DestroyImmediate(go);
            }
            EditorUtility.SetDirty(catalog);AssetDatabase.SaveAssets();
            Debug.Log("STAGE1_IMPORTED "+catalog.monsters.Length+" monsters / "+catalog.monsters.Sum(m=>m.motions.Length)+" clips / "+catalog.monsters.Sum(m=>m.motions.Sum(c=>c.frames.Length))+" frames");
        }

        static Stage1MonsterClip ImportClip(string monster,ClipSource source)
        {
            if(source.frames.Length!=source.milliseconds.Length || source.milliseconds.Any(ms=>ms<=0))throw new InvalidDataException(monster+"/"+source.id+": timing mismatch");
            var pivot=new Vector2(source.pivot[0],source.pivot[1]);
            var sprites=source.frames.Select(f=>ImportSprite(f,source.width,source.height,source.ppu,pivot)).ToArray();
            var clip=new AnimationClip { name=monster+"_"+source.id,frameRate=1000,wrapMode=source.loop?WrapMode.Loop:WrapMode.Once };
            var curve=new ObjectReferenceKeyframe[sprites.Length+1];int elapsed=0;
            for(int i=0;i<sprites.Length;i++) { curve[i]=new ObjectReferenceKeyframe{time=elapsed/1000f,value=sprites[i]};elapsed+=source.milliseconds[i]; }
            // Unity extends a sprite PPtr curve by one sample interval after its
            // last key. End one 1ms sample early to preserve the exact source duration.
            curve[sprites.Length]=new ObjectReferenceKeyframe{time=(elapsed-1)/1000f,value=sprites[sprites.Length-1]};
            AnimationUtility.SetObjectReferenceCurve(clip,EditorCurveBinding.PPtrCurve("",typeof(SpriteRenderer),"m_Sprite"),curve);
            var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.loopTime=source.loop;AnimationUtility.SetAnimationClipSettings(clip,settings);
            string path=Root+"/Animations/"+monster+"_"+source.id+".anim";var old=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if(old) { EditorUtility.CopySerialized(clip,old);UnityEngine.Object.DestroyImmediate(clip);clip=old;EditorUtility.SetDirty(clip); }
            else AssetDatabase.CreateAsset(clip,path);
            AssetDatabase.SaveAssetIfDirty(clip);
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate|ImportAssetOptions.ForceSynchronousImport);
            clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            return new Stage1MonsterClip{id=source.id,frames=sprites,milliseconds=source.milliseconds,loop=source.loop,animation=clip,pivot=pivot};
        }

        static Sprite ImportSprite(FrameSource frame,int width,int height,int ppu,Vector2 pivot)
        {
            if(Hash(File.ReadAllBytes(frame.path))!=frame.sha256)throw new InvalidDataException("Source hash mismatch: "+frame.path);
            AssetDatabase.ImportAsset(frame.path,ImportAssetOptions.ForceSynchronousImport);
            var importer=(TextureImporter)AssetImporter.GetAtPath(frame.path);
            importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
            importer.spritePixelsPerUnit=ppu;importer.filterMode=FilterMode.Point;importer.textureCompression=TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled=false;importer.npotScale=TextureImporterNPOTScale.None;importer.alphaIsTransparency=true;importer.isReadable=true;importer.maxTextureSize=1024;
            var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);settings.spriteMeshType=SpriteMeshType.FullRect;settings.spriteAlignment=(int)SpriteAlignment.Custom;settings.spritePivot=pivot;importer.SetTextureSettings(settings);importer.SaveAndReimport();
            var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(frame.path);
            if(!sprite || sprite.rect.size!=new Vector2(width,height) || Vector2.Distance(sprite.pivot,pivot*new Vector2(width,height))>.001f || sprite.pixelsPerUnit!=ppu)throw new InvalidOperationException("Sprite dimensions or pivot mismatch: "+frame.path);
            return sprite;
        }

        [MenuItem("Tools/deviant/Stage 1 Monsters/Open Current Stage")]
        public static void Open()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)return;
            if(!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
            EditorSceneManager.OpenScene(ScenePath);
        }

        public static void ImportAndValidate() { Import();Validate(); }
        // Pixel-only revisions retain the already approved clips, controller subasset
        // IDs, prefabs and showcase scene. Rebuilding them is unnecessary and noisy.
        public static void RefreshApprovedPixelsAndValidate()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var source=JsonUtility.FromJson<Source>(File.ReadAllText(Root+"/Data/import.json"));
            var catalog=AssetDatabase.LoadAssetAtPath<Stage1MonsterCatalog>(CatalogPath);
            if(!catalog)throw new InvalidDataException("Import the existing monster catalog first.");
            foreach(var monster in source.monsters)
            {
                var current=catalog.monsters.Single(m=>m.id==monster.id);
                foreach(var motion in monster.motions)
                {
                    var clip=current.motions.Single(c=>c.id==motion.id);
                    if(!clip.milliseconds.SequenceEqual(motion.milliseconds)||clip.frames.Length!=motion.frames.Length)
                        throw new InvalidDataException("Pixel update must preserve motion timing and count: "+monster.id+"/"+motion.id);
                    for(int i=0;i<motion.frames.Length;i++)
                    {
                        var frame=motion.frames[i];
                        if(Hash(File.ReadAllBytes(frame.path))!=frame.sha256||AssetDatabase.GetAssetPath(clip.frames[i])!=frame.path)
                            throw new InvalidDataException("Unregistered pixel update: "+frame.path);
                    }
                }
            }
            catalog.sourceManifestSHA256=source.sourceManifestSHA256;
            EditorUtility.SetDirty(catalog);AssetDatabase.SaveAssetIfDirty(catalog);
            Validate();
        }
        public static void PlayAndCapture()
        {
            Open();SetCaptureResolution();EditorApplication.delayCall+=()=>{ EditorApplication.ExecuteMenuItem("Window/General/Game");EditorApplication.EnterPlaymode(); };
        }
        internal static void SetCaptureResolution()
        {
            var asm=typeof(EditorWindow).Assembly;var sizesType=asm.GetType("UnityEditor.GameViewSizes");
            var singleton=sizesType.GetProperty("instance",System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.FlattenHierarchy).GetValue(null);
            var group=sizesType.GetMethod("GetGroup").Invoke(singleton,new[]{Enum.Parse(asm.GetType("UnityEditor.GameViewSizeGroupType"),"Standalone")});
            var size=Activator.CreateInstance(asm.GetType("UnityEditor.GameViewSize"),System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic,null,new object[]{Enum.Parse(asm.GetType("UnityEditor.GameViewSizeType"),"FixedResolution"),2304,720,"Stage1 pixel capture"},null);
            group.GetType().GetMethod("AddCustomSize").Invoke(group,new[]{size});
            int count=(int)group.GetType().GetMethod("GetTotalCount").Invoke(group,null);
            var view=EditorWindow.GetWindow(asm.GetType("UnityEditor.GameView"));view.GetType().GetProperty("selectedSizeIndex",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic).SetValue(view,count-1);view.Repaint();
        }
        [MenuItem("Tools/deviant/Stage 1 Monsters/Play Current Stage")]
        public static void Play()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)return;
            Open();EditorApplication.delayCall+=()=>{ EditorApplication.ExecuteMenuItem("Window/General/Game");EditorApplication.EnterPlaymode(); };
        }
        [MenuItem("Tools/deviant/Stage 1 Monsters/Validate Imported Motions")]
        public static void Validate()
        {
            var catalog=AssetDatabase.LoadAssetAtPath<Stage1MonsterCatalog>(CatalogPath);
            if(!catalog || catalog.monsters.Length!=5)throw new InvalidOperationException("Missing five-monster catalog");
            var source=JsonUtility.FromJson<Source>(File.ReadAllText(Root+"/Data/import.json"));
            string original=Path.GetFullPath(Path.Combine(Application.dataPath,"../../resources/authoring/workbench/source/MonsterReference/approved/manifest.json"));
            if(Hash(File.ReadAllBytes(original))!=catalog.sourceManifestSHA256)throw new InvalidDataException("Stale approved source export");
            int frames=0,clips=0;var go=new GameObject("Temporary Sprite Validation");var renderer=go.AddComponent<SpriteRenderer>();var animator=go.AddComponent<Animator>();
            try
            {
                foreach(var mon in catalog.monsters)
                {
                    animator.runtimeAnimatorController=mon.controller;animator.Rebind();
                    if(!mon.prefab || !mon.controller)throw new InvalidOperationException("Missing prefab/controller: "+mon.id);
                    foreach(var clip in mon.motions)
                    {
                        if(Mathf.Abs(clip.animation.length-clip.DurationMs/1000f)>.00001f)throw new InvalidOperationException(mon.id+"/"+clip.id+": animation duration "+clip.animation.length+" differs from "+clip.DurationMs/1000f);
                        int elapsed=0;
                        for(int i=0;i<clip.frames.Length;i++)
                        {
                            var sprite=clip.frames[i];var importer=(TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(sprite));
                            if(importer.filterMode!=FilterMode.Point || importer.textureCompression!=TextureImporterCompression.Uncompressed || importer.mipmapEnabled || sprite.pixelsPerUnit!=32)throw new InvalidOperationException("Pixel import settings changed");
                            foreach(var pixel in sprite.texture.GetPixels32())
                            {
                                if(pixel.a!=0 && pixel.a!=255)throw new InvalidOperationException("Partial alpha introduced by Unity import");
                                if(pixel.a==0)continue;
                                string hex="#"+pixel.r.ToString("X2")+pixel.g.ToString("X2")+pixel.b.ToString("X2");
                                if(!source.palette.Contains(hex))throw new InvalidOperationException("Off-palette imported pixel: "+hex);
                            }
                            float t=(elapsed+clip.milliseconds[i]*.5f)/1000f;clip.animation.SampleAnimation(go,t);
                            if(renderer.sprite!=clip.frames[i] || clip.FrameAt(t)!=i)throw new InvalidOperationException(mon.id+"/"+clip.id+" frame "+i+" mismatch: sampled="+(renderer.sprite?AssetDatabase.GetAssetPath(renderer.sprite):"null")+", expected="+AssetDatabase.GetAssetPath(clip.frames[i]));
                            elapsed+=clip.milliseconds[i];frames++;
                        }
                        if(clip.loop && clip.FrameAt(clip.DurationMs/1000f+.0001f)!=0)throw new InvalidOperationException("Loop seam mismatch");
                        clips++;
                    }
                    var instance=(GameObject)PrefabUtility.InstantiatePrefab(mon.prefab);var player=instance.GetComponent<Stage1MonsterPlayer>();player.tickAutomatically=false;player.Initialize();
                    foreach(string state in new[]{"idle","walk","attack","hit","death"})
                    {
                        if(mon.Find(state)==null) { if(player.Play(state))throw new InvalidOperationException("Invented missing motion");continue; }
                        Vector3 origin=instance.transform.position,scale=instance.transform.localScale;
                        player.Play(state,true);player.SampleAt(player.Clip.DurationMs/1000f+.01f);
                        if(state=="death" && instance.GetComponent<SpriteRenderer>().enabled)throw new InvalidOperationException("Death failed to hide");
                        player.faceRight=true;player.SampleAt(0);
                        if(!instance.GetComponent<SpriteRenderer>().flipX || instance.transform.position!=origin || instance.transform.localScale!=scale)throw new InvalidOperationException("Presentation changed body transform");
                    }
                    player.Play("hit",false);player.autoReturnToIdle=true;player.Advance(.5f);
                    if(player.Motion!=(mon.Find("idle")!=null?"idle":"base"))throw new InvalidOperationException("Hit failed to recover");
                    UnityEngine.Object.DestroyImmediate(instance);
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
            string output=Path.GetFullPath("../outputs/stage1-monsters");Directory.CreateDirectory(output);
            File.WriteAllText(Path.Combine(output,"unity-validation.json"),"{\"result\":\"PASS\",\"unity\":\""+Application.unityVersion+"\",\"monsters\":5,\"clips\":"+clips+",\"sampledFrames\":"+frames+",\"sourceSHA256\":\""+catalog.sourceManifestSHA256+"\"}");
            Debug.Log("STAGE1_VALIDATION_PASS "+clips+" clips / "+frames+" sampled native frames; hit/death/reset/facing checks passed");
        }
    }
}
