using System.Collections.Generic;
using Deviant.Combat;
using ForestCombatV2;
using UnityEngine;

namespace Deviant.Chapter2
{
    public sealed class Chapter2Terrain : MonoBehaviour
    {
        public readonly List<ArenaSurface> Surfaces=new List<ArenaSurface>();
        public void Build(C2Layout layout,CombatArena arena,Chapter1TeamTerrainPalette palette,Shader shader=null)
        {
            var bossOrigin=new Vector2(layout.boss.x,layout.boss.y-3);
            var visibleSurfaces=RoseBlueStage.Cutout(layout.surfaces,new Rect(bossOrigin.x,bossOrigin.y,30,18));
            foreach(var s in visibleSurfaces){
                var go=new GameObject(s.id);go.transform.SetParent(transform,false);go.transform.position=new Vector3(s.x+s.w/2,s.y+s.h/2);
                var surface=go.AddComponent<ArenaSurface>();surface.oneWay=s.oneWay;surface.footstepMaterial=FootstepMaterial.Stone;var box=go.AddComponent<BoxCollider2D>();box.size=new Vector2(s.w,s.h);arena.RegisterFeedbackSurface(box);Surfaces.Add(surface);
            }
            Physics2D.SyncTransforms();
            if(!shader)shader=Shader.Find("Deviant/Chapter2/Terrain");
            var artLayout=JsonUtility.FromJson<C2Layout>(JsonUtility.ToJson(layout));artLayout.surfaces=visibleSurfaces;
            gameObject.AddComponent<Chapter2WaterwayArt>().Build(artLayout,shader);
        }
    }
}
