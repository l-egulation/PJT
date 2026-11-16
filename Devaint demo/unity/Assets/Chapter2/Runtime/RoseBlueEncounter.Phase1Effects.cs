using UnityEngine;

namespace Deviant.Chapter2
{
    public sealed partial class RoseBlueEncounter
    {
        Sprite glowSprite,aimTrackSprite,aimLockSprite,liftBubbleSprite;
        SpriteRenderer liftBubble;
        Sprite ProceduralArt(string kind)
        {
            int n=128;var t=new Texture2D(n,n,TextureFormat.RGBA32,false){filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp};
            var pixels=new Color[n*n];
            for(int y=0;y<n;y++)for(int x=0;x<n;x++)
            {
                float dx=(x-63.5f)/63.5f,dy=(y-63.5f)/63.5f,r=Mathf.Sqrt(dx*dx+dy*dy);Color c=Color.clear;
                if(kind=="glow"){c=Color.white;c.a=Mathf.Pow(Mathf.Clamp01(1-r),2)*.9f;}
                if(kind=="bubble"&&r<.98f)
                {
                    c=new Color(.38f,.50f,.88f,.12f);
                    if(r>.92f)c=new Color(.66f,.73f,1,.78f);
                    else if(r>.85f)c=new Color(.39f,.46f,.84f,.32f);
                    // A short curved glint leaves the character readable through the sphere.
                    if(r>.77f&&r<.83f&&dx<-.28f&&dy>.25f)c=new Color(.83f,.88f,1,.88f);
                }
                pixels[y*n+x]=c;
            }
            t.SetPixels(pixels);t.Apply();ownedTextures.Add(t);var s=Sprite.Create(t,new Rect(0,0,n,n),new Vector2(.5f,.5f),32);ownedSprites.Add(s);return s;
        }
        // The tracking sniper scope: four ring arcs, eight tick marks, an inner broken ring, a hollow centre diamond and a cross
        // that stops short of it. Vivid rose over a dark outline so it reads over the pale player; the locked scope turns redder.
        Sprite AimArt(Color ink)
        {
            const int n=128;const float r=56;var t=new Texture2D(n,n,TextureFormat.RGBA32,false){filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp};
            var outline=new Color(26/255f,6/255f,16/255f,1);var pixels=new Color[n*n];
            float Seg(Vector2 p,Vector2 a,Vector2 b){var ab=b-a;float u=Mathf.Clamp01(Vector2.Dot(p-a,ab)/ab.sqrMagnitude);return Vector2.Distance(p,a+ab*u);}
            bool InArc(float angle,float from,float span){float d=Mathf.Repeat(angle-from,Mathf.PI*2);return d<=span;}
            // Distance in pixels to the nearest stroke; the coloured stroke is narrower than its outline.
            float Stroke(Vector2 p,float width)
            {
                float d=float.MaxValue,radius=p.magnitude,angle=Mathf.Atan2(p.y,p.x);
                for(int k=0;k<4;k++)if(InArc(angle,k*Mathf.PI/2+.1f,1.2f))d=Mathf.Min(d,Mathf.Abs(radius-r*.65f)-width*1.75f);
                for(int k=0;k<4;k++)if(InArc(angle,k*Mathf.PI/2+.95f,.45f))d=Mathf.Min(d,Mathf.Abs(radius-r*.4f)-width*.75f);
                for(int k=0;k<8;k++){float a=k*Mathf.PI/4+Mathf.PI/4,r0=r*(k%2==1?.78f:.74f),r1=r*(k%2==1?.9f:1f);var dir=new Vector2(Mathf.Cos(a),Mathf.Sin(a));d=Mathf.Min(d,Seg(p,dir*r0,dir*r1)-width);}
                float dm=r*.11f;var top=new Vector2(0,dm);var right=new Vector2(dm,0);
                d=Mathf.Min(d,Mathf.Min(Mathf.Min(Seg(p,top,right),Seg(p,right,-top)),Mathf.Min(Seg(p,-top,-right),Seg(p,-right,top)))-width);
                float g=r*.17f,e=r*.34f;
                foreach(var axis in new[]{Vector2.up,Vector2.down,Vector2.left,Vector2.right})d=Mathf.Min(d,Seg(p,axis*g,axis*e)-width*1.5f);
                return d;
            }
            for(int y=0;y<n;y++)for(int x=0;x<n;x++)
            {
                var p=new Vector2(x-63.5f,y-63.5f);
                pixels[y*n+x]=Stroke(p,1)<=0?ink:Stroke(p,2.2f)<=0?outline:Color.clear;
            }
            t.SetPixels(pixels);t.Apply();ownedTextures.Add(t);var s=Sprite.Create(t,new Rect(0,0,n,n),new Vector2(.5f,.5f),32);ownedSprites.Add(s);return s;
        }
        void DrawLiftBubble()
        {
            if(Model.Phase!=Deviant.Chapter2.RoseBlue.Phase.Introduction){if(liftBubble)liftBubble.enabled=false;return;}
            if(!liftBubbleSprite)liftBubbleSprite=ProceduralArt("bubble");
            var p=arena.Player;
            if(!liftBubble)liftBubble=ArtView("player lift bubble",liftBubbleSprite,p.Center,p.visual.sortingOrder+3);
            liftBubble.enabled=true;liftBubble.sortingLayerID=p.visual.sortingLayerID;
            liftBubble.transform.position=p.Center;
            liftBubble.transform.localScale=Vector3.one*(Mathf.Max(p.size.x,p.size.y)+.65f)/4;
            float alpha=Mathf.Min(Mathf.Clamp01(cutAge/.18f),Mathf.Clamp01((cutLength-cutAge)/.22f));
            liftBubble.color=new Color(1,1,1,alpha);
        }
        void HideLiftBubble(){if(liftBubble)liftBubble.enabled=false;}
        void StylePhase1Warning(Cast c,string key,SpriteRenderer view,Rect box)
        {
            if(Model.Phase!=Deviant.Chapter2.RoseBlue.Phase.Machine)return;
            if(!glowSprite)glowSprite=ProceduralArt("glow");
            if(key=="sniper")
            {
                bool locked=c.Clock.Age>=Deviant.Chapter2.RoseBlue.Tuning.TrapTell+Deviant.Chapter2.RoseBlue.Tuning.SniperDelay+Deviant.Chapter2.RoseBlue.Tuning.SniperTrack;
                if(!aimTrackSprite){aimTrackSprite=AimArt(new Color(1,61/255f,139/255f));aimLockSprite=AimArt(new Color(1,31/255f,79/255f));}
                view.sprite=locked?aimLockSprite:aimTrackSprite;view.color=Color.white;view.transform.position=box.center;
                // Drawn above the player and the boss. The 56 px art radius (1.75u) shows as 1.125u while tracking and 1.5u once locked.
                view.sortingLayerID=arena.Player.visual.sortingLayerID;view.sortingOrder=AbovePlayer(10);
                view.transform.localScale=Vector3.one*(locked?1.5f:1.125f)/1.75f;
                view.transform.localRotation=Quaternion.Euler(0,0,locked?0:(float)c.Clock.Age*50);return;
            }
            // Platform tells are looping sheets sitting on the platform top: Blue chain-trap circles, the machine platform's ink,
            // Rose's arrow-rain shadows, and the sequence tells (odd order Rose, even order Blue). Numbers are not shown.
            string sheet=key.StartsWith("trap-")?"trap-warn":key=="machine"?"machine-warn":key.StartsWith("platform-")?"rain-warn"
                :key.StartsWith("sequence-")?(int.Parse(key.Substring("sequence-".Length))%2==0?"sequence-warn-rose":"sequence-warn-blue"):null;
            if(sheet!=null)
            {
                if(!loopingViews.ContainsKey(view))loopingViews[view]=(sheet,arena.Now);
                view.sprite=VfxFrame(sheet,arena.Now-loopingViews[view].started);view.color=Color.white;
                view.transform.position=new Vector2(box.center.x,box.yMin);view.transform.localScale=Vector3.one;view.transform.localRotation=Quaternion.identity;return;
            }
            view.sprite=glowSprite;view.color=key=="rose-line"?new Color(.88f,.40f,.63f,.9f):new Color(.61f,.39f,.88f,.9f);
            view.transform.position=new Vector2(box.center.x,box.yMin+.12f);
            view.transform.localScale=new Vector3(box.width*1.3f/4,.8f/4,1);
        }
        // Arrow rain over one platform. Each of the 15 arrows has its own start delay and period, and every drop picks a new
        // spot, height and tilt, so the shower lands all over the platform instead of sweeping left to right.
        // Children 0-14 are the falling arrows, 15-29 their landing sparks.
        void DrawRainArt(ArtFx fx)
        {
            if(fx.View.transform.childCount==0)
            {
                fx.View.enabled=false;
                for(int i=0;i<15;i++){var v=ArtView("rain bolt",Vfx("rose-rain-arrow-flight")[0],fx.At);v.transform.SetParent(fx.View.transform,true);}
                for(int i=0;i<15;i++){var v=ArtView("rain spark",Vfx("rose-rain-impact")[0],fx.At);v.transform.SetParent(fx.View.transform,true);}
            }
            float left=fx.At.x-fx.Size.x/2,seed=Mathf.Round(fx.At.x*7+fx.At.y*13);
            for(int i=0;i<15;i++)
            {
                var v=fx.View.transform.GetChild(i).GetComponent<SpriteRenderer>();var spark=fx.View.transform.GetChild(15+i).GetComponent<SpriteRenderer>();
                float Hash(int k,int cycle=0){float s=Mathf.Sin((seed+i*13.7f+k*7.31f+cycle*3.17f)*12.9898f)*43758.5453f;return s-Mathf.Floor(s);}
                float local=fx.Age-Hash(1)*.3f;
                if(local<0){v.enabled=spark.enabled=false;continue;}
                float period=.3f+Hash(2)*.12f;int cycle=Mathf.FloorToInt(local/period);float t=local-cycle*period;
                float x=left+.35f/6*fx.Size.x+Hash(3,cycle)*(fx.Size.x-.7f/6*fx.Size.x),flight=.13f+Hash(4,cycle)*.05f,height=(110+Hash(5,cycle)*30)/32f,tilt=(Hash(6,cycle)-.5f)*8,u=Mathf.Min(1,t/flight);
                v.enabled=t<flight;
                // The arrow's pivot sits 0.375u above its tip, so the tip reaches the platform as the pivot lands at that height.
                v.transform.position=new Vector2(x+tilt/32f*(1-u),fx.At.y+height*(1-u)+.375f);
                v.transform.rotation=Quaternion.Euler(0,0,-90+tilt);
                v.sprite=VfxFrame("rose-rain-arrow-flight",t);
                var landed=t>=flight?VfxFrame("rose-rain-impact",t-flight):null;
                spark.enabled=landed;if(landed){spark.sprite=landed;spark.transform.position=new Vector2(x,fx.At.y);}
            }
        }
    }
}
