using System.Collections.Generic;
using Deviant.Combat;
using UnityEngine;
using UnityEngine.UI;

namespace Deviant.Game
{
    public sealed partial class TemporaryGameGui
    {
        sealed class EnemyBar
        {
            public Image Track, Fill;
        }
        // Only an enemy the player hit in the last few seconds shows a small bar, without numbers, so a crowded
        // room carries one bar per target rather than a numbered panel over every enemy. Bosses keep their own bar.
        const float EnemyBarSeconds = 3;
        readonly Dictionary<CombatActor,(int hits,float until)> enemyHits = new Dictionary<CombatActor,(int,float)>();
        readonly List<CombatActor> staleEnemyHits = new List<CombatActor>();
        // Reuse visible slots rather than creating a Canvas for every stage enemy.
        readonly List<EnemyBar> enemyBars = new List<EnemyBar>();
        void RefreshEnemyHealth()
        {
            int used=0;float now=host.Arena.Now;
            var camera=world?world.View:host.Arena.ViewCamera;
            staleEnemyHits.Clear();
            foreach(var seen in enemyHits)if(!seen.Key||seen.Key.Dead)staleEnemyHits.Add(seen.Key);
            foreach(var gone in staleEnemyHits)enemyHits.Remove(gone);
            if(camera && DevFeatures.ShowEnemyHealthBars)
            foreach(var enemy in host.Arena.enemies)
            {
                if(!enemy||!enemy.isActiveAndEnabled||enemy.Dead||enemy.Hidden||enemy.isBoss)continue;
                int hits=enemy.ReceivedHitCount;
                if(!enemyHits.TryGetValue(enemy,out var seen)){enemyHits[enemy]=(hits,float.NegativeInfinity);continue;}
                if(hits!=seen.hits)enemyHits[enemy]=seen=(hits,hits>seen.hits?now+EnemyBarSeconds:float.NegativeInfinity);
                if(now>=seen.until)continue;
                float top=enemy.Center.y+enemy.size.y*.5f;
                if(enemy.visual&&enemy.visual.enabled)top=Mathf.Max(top,enemy.visual.bounds.max.y);
                var anchor=new Vector3(enemy.Center.x,top+.3f,0);
                var visible=camera.WorldToViewportPoint(anchor);
                if(visible.z<=0||visible.x<0||visible.x>1||visible.y<0||visible.y>1)continue;
                var point=Deviant.Presentation.Unity.CameraDisplayProjection.WorldToScreenPoint(camera,anchor);
                if(point.z<=0||point.x<0||point.x>Screen.width||point.y<0||point.y>Screen.height)continue;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(stage,point,null,out var local);
                // Ignore letterbox margins outside the GUI's playable safe frame.
                if(local.x < -480||local.x > 480||local.y < -270||local.y > 270)continue;
                if(used==enemyBars.Count)
                {
                    var track=Picture("hp-track",hud.transform,0,0,48,8);
                    var fill=Picture("hp-fill",track.transform,2,2,44,4);
                    enemyBars.Add(new EnemyBar{Track=track,Fill=fill});
                }
                var bar=enemyBars[used++];bar.Track.gameObject.SetActive(true);
                bar.Track.rectTransform.anchoredPosition=new Vector2(local.x+480-24,local.y-270+8);
                bar.Fill.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,44*Mathf.Clamp01(enemy.health/Mathf.Max(.001f,enemy.maxHealth)));
            }
            for(int i=used;i<enemyBars.Count;i++)enemyBars[i].Track.gameObject.SetActive(false);
        }
    }
}
