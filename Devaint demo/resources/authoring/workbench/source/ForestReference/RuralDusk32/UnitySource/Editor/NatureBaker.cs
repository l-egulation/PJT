using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Tilemaps;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace RuralNature32.Editor
{
    public sealed class NatureTextureImporter : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if(!assetPath.StartsWith(NatureBaker.Root+"/",StringComparison.Ordinal))return;
            var t=(TextureImporter)assetImporter;
            t.textureType=TextureImporterType.Sprite;t.spriteImportMode=SpriteImportMode.Single;
            t.spritePixelsPerUnit=32;t.filterMode=FilterMode.Point;t.mipmapEnabled=false;
            t.textureCompression=TextureImporterCompression.Uncompressed;t.alphaIsTransparency=true;
            t.npotScale=TextureImporterNPOTScale.None;t.wrapMode=TextureWrapMode.Clamp;
            var s=new TextureImporterSettings();t.ReadTextureSettings(s);
            s.spriteAlignment=(int)SpriteAlignment.Center;s.spritePivot=new Vector2(.5f,.5f);s.spriteMeshType=SpriteMeshType.FullRect;t.SetTextureSettings(s);
        }
    }
    [InitializeOnLoad]
    public static class NatureBaker
    {
        public const string Root="Assets/RuralNature32";
        public static bool Busy;
        static readonly Dictionary<RuralNatureTerrain,double> pending=new Dictionary<RuralNatureTerrain,double>();
        public static int automaticBakes;
        static NatureBaker()
        {
            Tilemap.tilemapTileChanged+=Changed;
            Undo.undoRedoPerformed+=QueueAll;
            EditorApplication.update+=Tick;
            EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.ExitingEditMode)Flush();};
            EditorSceneManager.sceneOpened+=(s,m)=>QueueAll();
        }
        static void Changed(Tilemap map,Tilemap.SyncTile[] changes)
        {
            if(Busy||EditorApplication.isPlaying)return;
            var owner=map.GetComponentInParent<RuralNatureTerrain>();
            if(owner&&owner.autoConnect&&(map==owner.ground||map==owner.platforms))Queue(owner);
        }
        public static void Queue(RuralNatureTerrain t){if(t&&t.autoConnect){pending[t]=EditorApplication.timeSinceStartup+.25;EditorApplication.QueuePlayerLoopUpdate();}}
        static void QueueAll(){if(!Busy)foreach(var t in UnityEngine.Object.FindObjectsByType<RuralNatureTerrain>(FindObjectsSortMode.None))Queue(t);}
        static void Tick()
        {
            if(Busy||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlaying)return;
            foreach(var t in pending.Where(p=>p.Value<=EditorApplication.timeSinceStartup).Select(p=>p.Key).ToArray())
            {pending.Remove(t);if(!t)continue;try{Bake(t);automaticBakes++;}catch(Exception e){Debug.LogException(e,t);}}
        }
        public static void Flush(){foreach(var t in pending.Keys.ToArray()){pending.Remove(t);if(t)Bake(t);}}
        internal static bool IsQueued(RuralNatureTerrain t)=>pending.ContainsKey(t);
        internal static void ProcessQueuedForVerification()
        {
            if(!Application.isBatchMode)throw new InvalidOperationException("Batch verification only.");
            foreach(var t in pending.Keys.ToArray())pending[t]=0;
            Tick();
        }
        public static HashSet<Vector2Int> ReadCells(Tilemap map)
        {
            var cells=new HashSet<Vector2Int>();if(!map)return cells;
            var b=map.cellBounds;if((long)b.size.x*b.size.y>4000000)throw new InvalidOperationException("Input Tilemap bounds exceed 4 million cells.");
            foreach(var p in b.allPositionsWithin)if(map.HasTile(p))cells.Add(new Vector2Int(p.x,-p.y-1));
            return cells;
        }
        public static NatureComposer Compose(RuralNatureTerrain t)
        {
            var c=new NatureComposer(ReadCells(t.ground),ReadCells(t.platforms),NatureComposer.LoadImages(Root+"/Sources"),(int)t.platformStyle,(int)t.groundStyle,t.sketchRegions);c.Render();return c;
        }
        public static byte[] Bytes(Color32[] pixels)
        {var b=new byte[pixels.Length*4];for(int i=0;i<pixels.Length;i++){b[i*4]=pixels[i].r;b[i*4+1]=pixels[i].g;b[i*4+2]=pixels[i].b;b[i*4+3]=pixels[i].a;}return b;}
        public static string Hash(byte[] bytes){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();}
        public static string CompositionHash(Dictionary<Vector2Int,Color32[]> tiles)
        {
            using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream))
            {foreach(var pair in tiles.OrderBy(p=>p.Key.y).ThenBy(p=>p.Key.x)){writer.Write(pair.Key.x);writer.Write(pair.Key.y);writer.Write(Bytes(pair.Value));}return Hash(stream.ToArray());}
        }
        public static void WritePNG(string file,Color32[] topDown,int w=32,int h=32)
        {
            var texture=new Texture2D(w,h,TextureFormat.RGBA32,false);var bottomUp=new Color32[topDown.Length];
            try{for(int y=0;y<h;y++)Array.Copy(topDown,y*w,bottomUp,(h-1-y)*w,w);texture.SetPixels32(bottomUp);texture.Apply();File.WriteAllBytes(file,texture.EncodeToPNG());}
            finally{UnityEngine.Object.DestroyImmediate(texture);}
        }
        public static NatureComposer Bake(RuralNatureTerrain t)
        {
            if(!t||!t.ground||!t.platforms||!t.visuals)throw new InvalidOperationException("Assign Ground, Platforms and Visuals Tilemaps.");
            var grid=t.GetComponent<Grid>();if(!grid||grid.cellSize.x!=1||grid.cellSize.y!=1)throw new InvalidOperationException("Rural Nature uses Grid cell size (1,1,0), PPU32.");
            bool previous=Busy;Busy=true;
            try
            {
                var result=Compose(t);
                var unique=new Dictionary<string,Color32[]>();var cells=new Dictionary<Vector2Int,string>();
                foreach(var pair in result.tiles){if(!pair.Value.Any(p=>p.a>0))continue;string id=Hash(Bytes(pair.Value));unique[id]=pair.Value;cells[pair.Key]=id;}
                Directory.CreateDirectory(Root+"/Generated/Textures");Directory.CreateDirectory(Root+"/Generated/Tiles");
                bool wrote=false;
                AssetDatabase.StartAssetEditing();
                try{foreach(var pair in unique){string file=Root+"/Generated/Textures/t_"+pair.Key+".png";if(!File.Exists(file)){WritePNG(file,pair.Value);wrote=true;}}}
                finally{AssetDatabase.StopAssetEditing();}
                if(wrote)AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                var assets=new Dictionary<string,Tile>();
                foreach(string id in unique.Keys)
                {
                    string file=Root+"/Generated/Tiles/t_"+id+".asset";
                    var tile=AssetDatabase.LoadAssetAtPath<Tile>(file);
                    if(!tile){tile=ScriptableObject.CreateInstance<Tile>();tile.sprite=AssetDatabase.LoadAssetAtPath<Sprite>(Root+"/Generated/Textures/t_"+id+".png");if(!tile.sprite)throw new InvalidOperationException("Sprite import failed: "+id);tile.colliderType=Tile.ColliderType.None;AssetDatabase.CreateAsset(tile,file);}
                    assets[id]=tile;
                }
                t.visuals.ClearAllTiles();
                var positions=cells.Keys.Select(p=>new Vector3Int(p.x,-p.y-1,0)).ToArray();
                var values=cells.Values.Select(id=>(TileBase)assets[id]).ToArray();t.visuals.SetTiles(positions,values);
                t.visuals.CompressBounds();t.visuals.GetComponent<TilemapRenderer>().enabled=true;
                t.ground.GetComponent<TilemapRenderer>().enabled=false;t.platforms.GetComponent<TilemapRenderer>().enabled=false;
                foreach(var m in new[]{t.ground,t.platforms}){var collider=m.GetComponent<TilemapCollider2D>();if(collider)collider.ProcessTilemapChanges();}
                t.bakedTileCount=unique.Count;t.lastBakeHash=CompositionHash(result.tiles);
                EditorUtility.SetDirty(t);EditorUtility.SetDirty(t.visuals);
                if(t.gameObject.scene.IsValid())EditorSceneManager.MarkSceneDirty(t.gameObject.scene);
                AssetDatabase.SaveAssets();SceneView.RepaintAll();return result;
            }
            finally{Busy=previous;}
        }
        public static Tile LogicTile(bool platform)
        {
            string name=platform?"Platform":"Ground",file=Root+"/Palette/"+name+".asset";
            var tile=AssetDatabase.LoadAssetAtPath<Tile>(file);if(tile)return tile;
            Directory.CreateDirectory(Root+"/Palette");
            var pixels=Enumerable.Repeat(new Color32(33,27,36,255),1024).ToArray();
            for(int y=0;y<(platform?5:12);y++)for(int x=0;x<32;x++)pixels[y*32+x]=platform?new Color32(88,204,202,255):new Color32(224,177,66,255);
            string png=Root+"/Palette/"+name+".png";WritePNG(png,pixels);AssetDatabase.ImportAsset(png,ImportAssetOptions.ForceSynchronousImport);
            tile=ScriptableObject.CreateInstance<Tile>();tile.sprite=AssetDatabase.LoadAssetAtPath<Sprite>(png);tile.colliderType=Tile.ColliderType.Grid;AssetDatabase.CreateAsset(tile,file);return tile;
        }
        public static Tilemap Layer(Transform parent,string name,bool collision,bool oneWay=false)
        {
            var obj=new GameObject(name,typeof(Tilemap),typeof(TilemapRenderer));obj.transform.SetParent(parent,false);var map=obj.GetComponent<Tilemap>();
            if(collision)
            {
                obj.GetComponent<TilemapRenderer>().enabled=false;
                obj.AddComponent<Rigidbody2D>().bodyType=RigidbodyType2D.Static;
                var combined=obj.AddComponent<CompositeCollider2D>();combined.geometryType=CompositeCollider2D.GeometryType.Polygons;
                var collider=obj.AddComponent<TilemapCollider2D>();collider.compositeOperation=Collider2D.CompositeOperation.Merge;collider.extrusionFactor=.001f;
                if(oneWay){var effector=obj.AddComponent<PlatformEffector2D>();effector.useOneWay=true;effector.useOneWayGrouping=true;effector.surfaceArc=180;combined.usedByEffector=true;}
            }
            return map;
        }
        [MenuItem("Tools/Rural Nature 32/Create Empty Terrain")]
        public static void CreateEmptyMenu(){var t=CreateEmpty();Undo.RegisterCreatedObjectUndo(t.gameObject,"Create Rural Terrain");Selection.activeGameObject=t.gameObject;}
        public static RuralNatureTerrain CreateEmpty()
        {
            var obj=new GameObject("Rural Nature 32",typeof(Grid),typeof(RuralNatureTerrain));obj.GetComponent<Grid>().cellSize=new Vector3(1,1,0);
            var t=obj.GetComponent<RuralNatureTerrain>();t.ground=Layer(obj.transform,"Ground - paint yellow tile",true);t.platforms=Layer(obj.transform,"Platforms - paint cyan tile",true,true);t.visuals=Layer(obj.transform,"Visuals - automatic",false);return t;
        }
        public static void CreatePalette()
        {
            LogicTile(false);LogicTile(true);
            string p=Root+"/Palette/RuralNature32.prefab";
            if(!File.Exists(p))GridPaletteUtility.CreateNewPalette(Root+"/Palette","RuralNature32",GridLayout.CellLayout.Rectangle,GridPalette.CellSizing.Manual,new Vector3(1,1,0),GridLayout.CellSwizzle.XYZ);
            var obj=PrefabUtility.LoadPrefabContents(p);
            try{var map=obj.GetComponentInChildren<Tilemap>();map.SetTile(Vector3Int.zero,LogicTile(false));map.SetTile(new Vector3Int(2,0,0),LogicTile(true));PrefabUtility.SaveAsPrefabAsset(obj,p);}
            finally{PrefabUtility.UnloadPrefabContents(obj);}
        }
        public static void StartPainting(RuralNatureTerrain t,bool platform)
        {
            CreatePalette();Selection.activeGameObject=(platform?t.platforms:t.ground).gameObject;
            EditorApplication.ExecuteMenuItem("Window/2D/Tile Palette");
            GridPaintingState.palette=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Palette/RuralNature32.prefab");
            GridPaintingState.scenePaintTarget=Selection.activeGameObject;
        }
        public static void CreateVisualPalette(RuralNatureTerrain terrain)
        {
            string p=Root+"/Palette/RuralNature32_Individual.prefab";
            if(!File.Exists(p))GridPaletteUtility.CreateNewPalette(Root+"/Palette","RuralNature32_Individual",GridLayout.CellLayout.Rectangle,GridPalette.CellSizing.Manual,new Vector3(1,1,0),GridLayout.CellSwizzle.XYZ);
            var tiles=new TileBase[terrain.visuals.GetUsedTilesCount()];terrain.visuals.GetUsedTilesNonAlloc(tiles);
            tiles=tiles.OrderBy(t=>AssetDatabase.GetAssetPath(t)).ToArray();
            var obj=PrefabUtility.LoadPrefabContents(p);
            try{var map=obj.GetComponentInChildren<Tilemap>();map.ClearAllTiles();for(int i=0;i<tiles.Length;i++)map.SetTile(new Vector3Int(i%16,-i/16,0),tiles[i]);PrefabUtility.SaveAsPrefabAsset(obj,p);}
            finally{PrefabUtility.UnloadPrefabContents(obj);}
        }
    }
    [CustomEditor(typeof(RuralNatureTerrain))]
    public sealed class NatureTerrainInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var t=(RuralNatureTerrain)target;if(DrawDefaultInspector())NatureBaker.Queue(t);
            EditorGUILayout.HelpBox("32px grid / PPU32. Paint or erase Ground/Platforms; grass, mirrored ceilings and corners update automatically. Visuals is generated. Save the scene after editing.",MessageType.Info);
            if(GUILayout.Button("Paint Ground - yellow tile"))NatureBaker.StartPainting(t,false);
            if(GUILayout.Button("Paint One-Way Platforms - cyan tile"))NatureBaker.StartPainting(t,true);
            if(GUILayout.Button("Rebuild 32x32 Tiles"))NatureBaker.Bake(t);
            if(GUILayout.Button("Open Named 32x32 Sketch Tiles"))NatureModular.OpenPalette();
            if(GUILayout.Button("Open Current Color + Partial Sketch Tiles"))
            {
                NatureBaker.CreateVisualPalette(t);EditorApplication.ExecuteMenuItem("Window/2D/Tile Palette");
                GridPaintingState.palette=AssetDatabase.LoadAssetAtPath<GameObject>(NatureBaker.Root+"/Palette/RuralNature32_Individual.prefab");
            }
            EditorGUILayout.LabelField("Distinct 32px tiles",t.bakedTileCount.ToString());
        }
    }
}
