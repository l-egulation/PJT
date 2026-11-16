using UnityEngine;

namespace Deviant.Stage1Monsters.Game
{
    // Presentation only. The burrow execution remains the owner of placement and accepted hits.
    public sealed class Stage1BurrowInk : MonoBehaviour
    {
        const int Width=192,Height=176,Floor=144;
        static readonly Color32 Ink=new Color32(23,23,22,255),Rim=new Color32(65,65,61,255),Light=new Color32(87,84,81,255);
        Stage1VillageMonsterAttack attack;
        SpriteRenderer origin,destination;
        Texture2D originTexture,destinationTexture;
        Color32[] first=new Color32[Width*Height],second=new Color32[Width*Height],body;
        int bodyWidth,bodyHeight,bodyPivotX,bodyPivotY;
        bool ownsBody;
        public float WarningWidth=>attack.Burrow.DamageBounds.size.x;
        public bool WarningVisible=>destination&&destination.enabled&&attack.Burrow.Locked&&attack.Burrow.Age>=attack.Burrow.Spec.warningStart&&attack.Burrow.Age<attack.Burrow.Spec.eruptionStart;
        public void Bind(Stage1VillageMonsterAttack owner,Stage1MonsterPlayer player)
        {
            attack=owner;
            var sprite=player.Definition.Find("idle").frames[0];
            body=sprite.texture.GetPixels32();bodyWidth=sprite.texture.width;bodyHeight=sprite.texture.height;
            bodyPivotX=Mathf.RoundToInt(sprite.pivot.x);bodyPivotY=bodyHeight-Mathf.RoundToInt(sprite.pivot.y);
            origin=Make("Submerge ink",out originTexture);destination=Make("Eruption ink",out destinationTexture);
        }
        SpriteRenderer Make(string label,out Texture2D texture)
        {
            texture=new Texture2D(Width,Height,TextureFormat.RGBA32,false){filterMode=FilterMode.Point};
            var node=new GameObject(label);node.transform.SetParent(transform,false);
            var renderer=node.AddComponent<SpriteRenderer>();renderer.sharedMaterial=attack.Actor.visual.sharedMaterial;
            renderer.sortingLayerID=attack.Actor.visual.sortingLayerID;renderer.sortingOrder=attack.Actor.visual.sortingOrder;
            renderer.sprite=Sprite.Create(texture,new Rect(0,0,Width,Height),new Vector2(.5f,(Height-Floor)/(float)Height),32);
            renderer.enabled=false;return renderer;
        }
        static void Pixel(Color32[] pixels,int x,int y,Color32 color)
        { if(x>=0&&x<Width&&y>=0&&y<Height)pixels[(Height-1-y)*Width+x]=color; }
        static void Oval(Color32[] pixels,int cx,int cy,int rx,int ry,Color32 color)
        {
            rx=Mathf.Max(1,rx);ry=Mathf.Max(1,ry);
            for(int y=-ry;y<=ry;y++)for(int x=-rx;x<=rx;x++)
                if(x*x/(float)(rx*rx)+y*y/(float)(ry*ry)<=1)Pixel(pixels,cx+x,cy+y,color);
        }
        static void Ripple(Color32[] pixels,int radius,Color32 color)
        {
            for(int x=-radius;x<=radius;x++)
            {
                int y=Mathf.RoundToInt(3*Mathf.Sqrt(Mathf.Max(0,1-x*x/(float)(radius*radius))));
                Pixel(pixels,96+x,Floor-y,color);Pixel(pixels,96+x,Floor+y,color);
            }
        }
        void Body(Color32[] pixels,float submerged)
        {
            int down=Mathf.RoundToInt(bodyPivotY*Mathf.SmoothStep(0,1,submerged));
            for(int y=0;y<bodyHeight;y++)for(int x=0;x<bodyWidth;x++)
            {
                int yy=Floor-bodyPivotY+y+down;if(yy>=Floor)continue;
                var color=body[(bodyHeight-1-y)*bodyWidth+x];
                if(color.a>0)Pixel(pixels,96-bodyPivotX+x,yy,color);
            }
        }
        public void Clear()
        {
            if(origin)origin.enabled=false;if(destination)destination.enabled=false;
            if(ownsBody&&attack&&attack.Actor.visual)attack.Actor.visual.enabled=true;ownsBody=false;
        }
        public void Draw()
        {
            Clear();var burrow=attack.Burrow;
            if(!burrow||!burrow.Active||attack.Actor.Dead)return;
            float age=burrow.Age;var spec=burrow.Spec;
            System.Array.Clear(first,0,first.Length);System.Array.Clear(second,0,second.Length);
            if(age<spec.meltEnd)
            {
                float u=age/spec.meltEnd;int width=Mathf.RoundToInt(Mathf.Lerp(22,47,Mathf.Sin(u*Mathf.PI/2)));
                Oval(first,96,Floor,width,3,Ink);Ripple(first,width+5,Rim);Body(first,u);
                Oval(first,96,Floor,width-4,1,Ink);
            }
            else
            {
                float residual=age-spec.meltEnd;
                if(residual<.9f)
                {
                    Oval(first,96,Floor,Mathf.RoundToInt(47*(1-residual/.9f)),1,Ink);
                    for(int i=0;i<3;i++)
                    {float t=residual-i*.19f;if(t>=0&&t<.48f)Ripple(first,Mathf.RoundToInt(20+t*78),Rim);}
                }
            }
            if(burrow.Locked&&age>=spec.warningStart&&age<spec.eruptionStart)
            {
                // The puddle edge is exactly the horizontal damage footprint; bubbles stay inside it.
                int half=Mathf.RoundToInt(WarningWidth*16);float elapsed=age-spec.warningStart;
                Oval(second,96,Floor,half,2,Ink);Ripple(second,half,Light);
                float urgency=Mathf.InverseLerp(spec.warningStart,spec.eruptionStart,age);
                for(int i=0;i<15;i++)
                {
                    float phase=Mathf.Repeat(elapsed*(3+urgency*2)+i*.381f,1);
                    int radius=Mathf.Max(1,Mathf.RoundToInt((3+i%3)*Mathf.Sin(phase*Mathf.PI)));
                    int x=96+Mathf.RoundToInt(Mathf.Sin(i*8.13f)*(half-6));
                    Oval(second,x,Floor-2-radius,radius,radius,Ink);
                    if(phase<.7f)Pixel(second,x-radius/2,Floor-2-radius*2,Light);
                }
            }
            if(age>=spec.eruptionStart)
            {
                float t=age-spec.eruptionStart;
                // Same approved body rises in 0.18 seconds; no black cover replacing its silhouette.
                Body(second,1-Mathf.Clamp01(t/.18f));
                if(t<.1f)for(int side=-1;side<=1;side+=2)
                    Oval(second,96+side*Mathf.RoundToInt(28+t*120),Floor,Mathf.RoundToInt(10+t*150),Mathf.Max(1,Mathf.RoundToInt(8*(1-t/.1f))),Ink);
                if(t<.48f)for(int i=0;i<16;i++)
                {
                    float vx=Mathf.Sin(i*5.18f)*160,vy=-65-Mathf.Repeat(i*.719f,1)*125;
                    int x=96+Mathf.RoundToInt(vx*t),y=Floor-3+Mathf.RoundToInt(vy*t+360*t*t);
                    if(y<Floor)Oval(second,x,y,1+i%2,1+i%2,Ink);
                }
            }
            originTexture.SetPixels32(first);originTexture.Apply(false);
            destinationTexture.SetPixels32(second);destinationTexture.Apply(false);
            origin.transform.position=burrow.Origin;destination.transform.position=burrow.Destination;
            origin.flipX=destination.flipX=attack.Actor.facing>0;origin.enabled=destination.enabled=true;
            attack.Actor.visual.enabled=false;ownsBody=true;
        }
        void OnDisable()=>Clear();
        void OnDestroy()
        {
            Clear();if(origin){Destroy(origin.sprite);Destroy(origin.gameObject);}if(destination){Destroy(destination.sprite);Destroy(destination.gameObject);}
            if(originTexture)Destroy(originTexture);if(destinationTexture)Destroy(destinationTexture);
        }
    }
}
