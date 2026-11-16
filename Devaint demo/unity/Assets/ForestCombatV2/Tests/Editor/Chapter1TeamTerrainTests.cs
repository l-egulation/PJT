using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.Tilemaps;
using Object=UnityEngine.Object;

namespace ForestCombatV2.Tests
{
    public sealed class Chapter1TeamTerrainTests
    {
        [Test] public void PaletteReferencesApprovedSketchSprites()
        {
            var palette=Resources.Load<Chapter1TeamTerrainPalette>("Chapter1TeamSketch");
            Assert.That(palette,Is.Not.Null);palette.Validate();
            foreach(var sprite in palette.terrain.Concat(palette.platforms))
                Assert.That(AssetDatabase.GetAssetPath(sprite),Does.Contain("Assets/Stage1Village/").And.Contain("/Sketch/"));
        }
        [Test] public void BorderSelectionHandlesWallsCeilingsAndConcaveCorners()
        {
            var data=new ForestData{cols=3,rows=3,ground=Enumerable.Repeat(1,9).ToArray()};
            int Select(params int[] air)
            {
                data.ground=Enumerable.Repeat(1,9).ToArray();foreach(int index in air)data.ground[index]=0;
                return Chapter1TeamTerrain.SurfaceTile(data,1,1,out _);
            }
            Assert.That(Select(1,3),Is.EqualTo(3));Assert.That(Select(1,5),Is.EqualTo(2));
            Assert.That(Select(3),Is.EqualTo(7));Assert.That(Select(5),Is.EqualTo(6));
            Assert.That(Select(2),Is.EqualTo(10));Assert.That(Select(0),Is.EqualTo(13));
            Select(7);Chapter1TeamTerrain.SurfaceTile(data,1,1,out bool flip);Assert.That(flip,Is.True);
            Assert.That(Select(),Is.EqualTo(14));
        }
        [Test] public void PlatformsKeepNativeFootHeightIncludingHalfTiles()
        {
            var palette=Resources.Load<Chapter1TeamTerrainPalette>("Chapter1TeamSketch");
            var root=new GameObject("Test sketch platforms");var tiles=new List<Tile>();
            var material=new Material(Shader.Find("Sprites/Default"));
            try
            {
                var data=new ForestData{cols=12,rows=8,ground=new int[96],platforms=new[]{
                    new FoliagePlatform{x=32,y=96,w=160},new FoliagePlatform{x=192,y=144,w=160}}};
                Chapter1TeamTerrain.Build(data,palette,root.transform,material,tiles);
                foreach(var platform in data.platforms)
                {
                    var map=root.GetComponentsInChildren<Tilemap>().Single(m=>m.name=="Sketch drop-through platforms "+platform.y%32);
                    for(int x=platform.x/32;x<(platform.x+platform.w)/32;x++)
                    {
                        var cell=new Vector3Int(x,-Mathf.FloorToInt(platform.y/32f)-1,0);
                        Assert.That(map.HasTile(cell),Is.True);
                        Assert.That(map.CellToWorld(cell).y+1,Is.EqualTo(-platform.y/32f).Within(.001f));
                    }
                }
                Assert.That(root.GetComponentsInChildren<TilemapCollider2D>(),Is.Empty,"Gameplay owns platform collisions");
            }
            finally {Object.DestroyImmediate(root);foreach(var tile in tiles)Object.DestroyImmediate(tile);Object.DestroyImmediate(material);}
        }
        [UnityTest] public IEnumerator RuntimeUsesSketchWithoutChangingRoomsAndCapturesPreview()
        {
            EditorSceneManager.OpenScene(Chapter1MapSelector.ProceduralScene);
            var setup=Object.FindAnyObjectByType<ForestCombatWorld>();setup.proceduralSeed=42;
            yield return new EnterPlayMode();yield return null;
            var world=Object.FindAnyObjectByType<ForestCombatWorld>();
            for(int i=0;i<600&&!world.Ready;i++)yield return null;
            Assert.That(world.Ready,Is.True);Assert.That(world.useTeamSketchTerrain,Is.True);
            Assert.That(world.Visuals.name,Is.EqualTo("Sketch terrain edges"));
            var source=JsonUtility.FromJson<ForestData>(AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/MeadowDuskV3/MapVer2/Data/ForestWorld.json").text);
            var art=JsonUtility.FromJson<RoundedTerrainLayout>(AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/MeadowDuskV3/MapVer2/Data/RoundedTerrain.json").text);
            var expected=Chapter1ProceduralMap.Generate(source,art,world.Data.generationSeed);
            CollectionAssert.AreEqual(expected.ground,world.Data.ground);
            Assert.That(Chapter1RoundedTerrain.GeometryHash(world.Data),Is.EqualTo(Chapter1RoundedTerrain.GeometryHash(expected)));
            foreach(var enemy in world.arena.enemies)if(enemy)enemy.aggressive=false;
            foreach(var room in new[]{0,1,4})
            {
                world.Warp(room);yield return null;world.RefreshCamera(0,true);
                Capture(world.View,"room-"+room);
            }
            yield return new ExitPlayMode();
        }
        static void Capture(Camera camera,string name)
        {
            if(SystemInfo.graphicsDeviceType==UnityEngine.Rendering.GraphicsDeviceType.Null)return;
            var texture=new RenderTexture(1280,720,24);var image=new Texture2D(1280,720,TextureFormat.RGB24,false);
            var oldTarget=camera.targetTexture;var oldActive=RenderTexture.active;
            try
            {
                camera.targetTexture=texture;camera.Render();RenderTexture.active=texture;
                image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();
                string output=System.IO.Path.GetFullPath("../outputs/team-sketch-terrain");System.IO.Directory.CreateDirectory(output);
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(output,name+".png"),image.EncodeToPNG());
            }
            finally {camera.targetTexture=oldTarget;RenderTexture.active=oldActive;texture.Release();Object.Destroy(image);Object.Destroy(texture);}
        }
    }
}
