using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Deviant.Combat.Tests
{
    public class StaticArenaSurfaceIndexTests
    {
        readonly List<GameObject> objects=new List<GameObject>();
        readonly List<ArenaSurface> results=new List<ArenaSurface>();
        ArenaSurface Surface(float x,float y,float width)
        {
            var go=new GameObject("Indexed terrain");objects.Add(go);go.transform.position=new Vector3(x,y,0);
            var surface=go.AddComponent<ArenaSurface>();go.AddComponent<BoxCollider2D>().size=new Vector2(width,1);
            Physics2D.SyncTransforms();return surface;
        }
        [TearDown] public void TearDown(){foreach(var go in objects)if(go)Object.DestroyImmediate(go);objects.Clear();}
        [Test] public void NegativeCoordinatesAndBucketEdgesKeepTouchingGroundWithoutDuplicates()
        {
            var wide=Surface(-16,0,64);var distant=Surface(100,0,2);var above=Surface(-16,10,2);
            var index=new StaticArenaSurfaceIndex(new[]{wide,distant,above});
            index.Query(new Bounds(new Vector3(-32,0,0),new Vector3(32,1,1)),results);
            Assert.That(results,Is.EqualTo(new[]{wide}));
            index.Query(new Bounds(new Vector3(16.5f,0,0),new Vector3(1,1,1)),results);
            Assert.That(results,Is.EqualTo(new[]{wide}),"Inclusive edge contact survives the broad phase.");
        }
        [Test] public void DisabledInactiveAndDestroyedTerrainAreExcludedAndReenabledTerrainReturns()
        {
            var a=Surface(0,0,4);var b=Surface(0,0,4);var c=Surface(0,0,4);
            var index=new StaticArenaSurfaceIndex(new[]{a,b,c});var area=new Bounds(Vector3.zero,Vector3.one);
            a.GetComponent<Collider2D>().enabled=false;b.gameObject.SetActive(false);Object.DestroyImmediate(c.gameObject);
            index.Query(area,results);Assert.That(results,Is.Empty);
            a.GetComponent<Collider2D>().enabled=true;b.gameObject.SetActive(true);Physics2D.SyncTransforms();
            index.Query(area,results);Assert.That(results,Is.EquivalentTo(new[]{a,b}));
        }
        [Test] public void RebuildReplacesGeometryAndIndicesDoNotLeakBetweenWorlds()
        {
            var a=Surface(0,0,4);var b=Surface(50,0,4);var index=new StaticArenaSurfaceIndex(new[]{a});
            a.transform.position=new Vector3(50,0,0);Physics2D.SyncTransforms();index.Rebuild(new[]{a,b});
            index.Query(new Bounds(Vector3.zero,Vector3.one),results);Assert.That(results,Is.Empty);
            index.Query(new Bounds(new Vector3(50,0,0),Vector3.one),results);Assert.That(results,Is.EquivalentTo(new[]{a,b}));
            var other=new StaticArenaSurfaceIndex(new[]{b});
            other.Query(new Bounds(new Vector3(50,0,0),Vector3.one),results);Assert.That(results,Is.EqualTo(new[]{b}));
        }
        [Test] public void ThousandDistantSurfacesDoNotBecomeLocalPathCandidates()
        {
            var all=new List<ArenaSurface>();for(int i=0;i<1000;i++)all.Add(Surface(i*20,0,2));
            var index=new StaticArenaSurfaceIndex(all);
            index.Query(new Bounds(Vector3.zero,new Vector3(8,1,1)),results);
            Assert.That(results,Is.EqualTo(new[]{all[0]}));
        }
    }
}
