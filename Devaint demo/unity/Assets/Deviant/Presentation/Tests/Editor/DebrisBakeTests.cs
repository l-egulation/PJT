using System;
using System.IO;
using System.Linq;
using Deviant.Presentation.Unity;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace Deviant.Presentation.Tests
{
    public sealed class DebrisBakeTests
    {
        [Test]public void AlphaPartitionKeepsEveryBodyPixelAndUsesSharedTextureCoordinates()
        {
            string folder="Assets/__HitFeedbackBakeFixture_"+Guid.NewGuid().ToString("N");Directory.CreateDirectory(folder);
            var image=new Texture2D(16,16,TextureFormat.RGBA32,false);var pixels=new Color[256];int opaque=0;
            for(int y=2;y<14;y++)for(int x=3;x<13;x++){pixels[y*16+x]=Color.white;opaque++;}
            image.SetPixels(pixels);image.Apply();string path=folder+"/body.png";File.WriteAllBytes(path,image.EncodeToPNG());Object.DestroyImmediate(image);
            DebrisBakeAsset bake=null;
            try
            {
                AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
                var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.textureType=TextureImporterType.Sprite;importer.spritePixelsPerUnit=16;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();
                var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(path);
                var type=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("Deviant.Presentation.Editor.DebrisBaker")).First(t=>t!=null);
                bake=(DebrisBakeAsset)type.GetMethod("Bake").Invoke(null,new object[]{sprite,7,109});
                Assert.That(bake.pieces.Length,Is.EqualTo(7));Assert.That(bake.pieces.Sum(p=>p.area),Is.EqualTo(opaque/256f).Within(.00001f));
                Assert.That(bake.source,Is.SameAs(sprite));Assert.That(bake.sourceHash.Length,Is.EqualTo(64));Assert.That(importer.isReadable,Is.False,"Baking must not enable runtime texture pixel reads.");
                foreach(var piece in bake.pieces)
                {
                    Assert.That(piece.hull.Length,Is.GreaterThanOrEqualTo(3));Assert.That(piece.mesh.vertexCount,Is.EqualTo(piece.mirroredMesh.vertexCount));
                    var vertices=piece.mesh.vertices;var mirrored=piece.mirroredMesh.vertices;
                    for(int i=0;i<vertices.Length;i++){Assert.That(mirrored[i].x,Is.EqualTo(-vertices[i].x));Assert.That(mirrored[i].y,Is.EqualTo(vertices[i].y));}
                    foreach(var uv in piece.mesh.uv)Assert.That(uv.x>=0&&uv.x<=1&&uv.y>=0&&uv.y<=1,Is.True);
                }
            }
            finally
            {
                if(bake){foreach(var piece in bake.pieces){Object.DestroyImmediate(piece.mesh);Object.DestroyImmediate(piece.mirroredMesh);}Object.DestroyImmediate(bake);}
                AssetDatabase.DeleteAsset(folder);
            }
        }
    }
}
