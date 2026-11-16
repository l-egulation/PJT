using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RuralNature32.Editor
{
    public static class NatureDemo
    {
        public const string ScenePath=NatureBaker.Root+"/Scenes/RuralNature32_Assembly.unity";
        [Serializable] public sealed class Cell {public int x,y;public bool oneWay;}
        [Serializable] public sealed class Tree {public string name;public int x,y,surfaceY;}
        [Serializable] public sealed class Actor {public int x,row;}
        [Serializable] public sealed class ReviewMap {public Cell[] cells;public Tree[] trees;public Actor[] actors;}
        static Material material;
        public static Material Unlit
        {
            get
            {
                if(material)return material;string file=NatureBaker.Root+"/Data/NatureUnlit.mat";
                material=AssetDatabase.LoadAssetAtPath<Material>(file);
                if(!material){var shader=Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default")??Shader.Find("Sprites/Default");material=new Material(shader);AssetDatabase.CreateAsset(material,file);}return material;
            }
        }
        static GameObject Sprite(string name,string file,int x,int y,int sorting,Transform parent,bool flipY=false)
        {
            var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(NatureBaker.Root+"/Preview/"+file+".png");
            if(!sprite)throw new InvalidOperationException("Missing preview sprite "+file);
            var obj=new GameObject(name,typeof(SpriteRenderer));obj.transform.SetParent(parent,false);
            obj.transform.localPosition=new Vector3((x+sprite.rect.width/2)/32f,-(y+sprite.rect.height/2)/32f,0);
            var sr=obj.GetComponent<SpriteRenderer>();sr.sprite=sprite;sr.sortingOrder=sorting;sr.sharedMaterial=Unlit;sr.flipY=flipY;return obj;
        }
        [MenuItem("Tools/Rural Nature 32/Open Assembly Scene")]
        public static void OpenScene()
        {
            if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
            if(File.Exists(ScenePath))EditorSceneManager.OpenScene(ScenePath);else Build();
        }
        public static RuralNatureTerrain Build()
        {
            NatureBaker.Busy=true;
            try
            {
                NatureBaker.CreatePalette();
                var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                var t=NatureBaker.CreateEmpty();var source=JsonUtility.FromJson<ReviewMap>(File.ReadAllText(NatureBaker.Root+"/Data/review-map.json"));
                foreach(var c in source.cells)(c.oneWay?t.platforms:t.ground).SetTile(new Vector3Int(c.x,c.y,0),NatureBaker.LogicTile(c.oneWay));
                t.visuals.GetComponent<UnityEngine.Tilemaps.TilemapRenderer>().sharedMaterial=Unlit;
                NatureBaker.Bake(t);
                var decor=new GameObject("Review scenery - original pixels");
                foreach(var d in source.trees)Sprite(d.name,d.name,d.x,d.y,-10,decor.transform);
                foreach(var a in source.actors)Sprite("Approved idle reference - no animation edits","ApprovedIdleReference",a.x-64,a.row*32-115,10,decor.transform);
                var bg=new GameObject("Review backdrop");
                for(int x=0;x<1536;x+=768){Sprite("Far forest","far_forest",x,0,-30,bg.transform);Sprite("Bottom overscan","far_forest",x,512,-30,bg.transform,true);}
                var camera=new GameObject("Review Camera",typeof(Camera)).GetComponent<Camera>();
                camera.orthographic=true;camera.orthographicSize=540f/64;camera.nearClipPlane=.1f;camera.farClipPlane=100;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color32(209,155,130,255);
                SetCamera(camera,bg.transform,0,360);
                Directory.CreateDirectory(NatureBaker.Root+"/Scenes");Directory.CreateDirectory(NatureBaker.Root+"/Prefabs");
                EditorSceneManager.SaveScene(scene,ScenePath);
                PrefabUtility.SaveAsPrefabAsset(t.gameObject,NatureBaker.Root+"/Prefabs/RuralNature32_Example.prefab");
                var empty=NatureBaker.CreateEmpty();empty.visuals.GetComponent<UnityEngine.Tilemaps.TilemapRenderer>().sharedMaterial=Unlit;
                PrefabUtility.SaveAsPrefabAsset(empty.gameObject,NatureBaker.Root+"/Prefabs/RuralNature32_Empty.prefab");UnityEngine.Object.DestroyImmediate(empty.gameObject);
                EditorSceneManager.SaveScene(scene,ScenePath);AssetDatabase.SaveAssets();return t;
            }
            finally{NatureBaker.Busy=false;}
        }
        public static void SetCamera(Camera c,Transform bg,int x,int y)
        {c.transform.position=new Vector3((x+480)/32f,-(y+270)/32f,-10);bg.position=new Vector3(x/32f,-y/32f,0);}
        public static void CaptureAll()
        {
            var c=UnityEngine.Object.FindFirstObjectByType<Camera>();var bg=GameObject.Find("Review backdrop").transform;
            int[,] points={{0,360},{960,520},{1920,80}};
            for(int n=0;n<3;n++)
            {
                SetCamera(c,bg,points[n,0],points[n,1]);
                var target=RenderTexture.GetTemporary(960,540,24,RenderTextureFormat.ARGB32);target.filterMode=FilterMode.Point;
                var previous=RenderTexture.active;var tex=new Texture2D(960,540,TextureFormat.RGBA32,false);
                try{c.targetTexture=target;c.Render();RenderTexture.active=target;tex.ReadPixels(new Rect(0,0,960,540),0,0);tex.Apply();File.WriteAllBytes(NatureBaker.Root+"/Reports/unity-scene-"+(n+1)+".png",tex.EncodeToPNG());}
                finally{c.targetTexture=null;RenderTexture.active=previous;RenderTexture.ReleaseTemporary(target);UnityEngine.Object.DestroyImmediate(tex);}
            }
            SetCamera(c,bg,0,360);EditorSceneManager.SaveScene(SceneManager.GetActiveScene(),ScenePath);
        }
    }
}
