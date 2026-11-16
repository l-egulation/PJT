using System;
using System.Collections.Generic;
using ForestCombatV2;
using UnityEngine;
using UnityEngine.UI;

namespace Deviant.Game
{
    // Native uGUI version of the approved 1600x900 title. Original art stays in the resource package.
    public sealed class SketchTitleScene : MonoBehaviour
    {
        public const int FrameCount=48;
        public const float SplitProgress=.5f;
        public const float IdleLoopSeconds=3.6f;
        const string Village="Hub/pixel-village/",Art="TitleSketch/";
        static readonly int FrontId=Shader.PropertyToID("_SketchTitleProgress");
        static readonly RectInt[] Cuts={new RectInt(46,91,106,18),new RectInt(332,28,130,41),new RectInt(612,119,86,28),new RectInt(893,57,88,17),new RectInt(1173,117,57,10),new RectInt(1448,51,20,7)};
        static readonly float[] Scales={2.5f,4,2.5f,3.5f,3,2.5f};
        readonly List<Material> materials=new List<Material>();
        readonly List<Texture2D> made=new List<Texture2D>();
        readonly List<Cloud> clouds=new List<Cloud>();
        readonly List<Plant> flowers=new List<Plant>();
        readonly List<Plant> grass=new List<Plant>();
        readonly List<Leaf> leaves=new List<Leaf>();
        readonly List<Debris> debris=new List<Debris>();
        readonly Texture2D[] human=new Texture2D[FrameCount],ink=new Texture2D[FrameCount];
        RectTransform frame,heroRoot,inkHeroRoot;
        RawImage hero,inkHero;
        Material heroMaterial,inkHeroMaterial;
        Shader shader;
        float clock,progress;
        public RawImage Sky {get;private set;}
        public RawImage Hero=>hero;
        public RawImage InkHero=>inkHero;
        public int HeroFrame {get;private set;}
        public float Clock=>clock;
        public float Progress=>progress;
        public int AshAlive {get;private set;}
        sealed class Cloud {public StartingVillageWorld.IPixelCloudAnimation animation;public RectTransform root;public int index;public float frozen=float.NaN;}
        sealed class Plant {public RectTransform root;public float x,phase,frozen=float.NaN;public TitleGrassImage blades;}
        sealed class Leaf {public RawImage image;public float x,y,angle,phase,scale;}
        sealed class Debris {public RawImage image;public float seed,phase;}

        static Texture2D Required(string path)
        {
            var texture=Resources.Load<Texture2D>(path);
            if(!texture)throw new InvalidOperationException("Missing approved title texture: "+path);
            return texture;
        }
        Material Pair(Texture2D colour,Texture2D sketch,bool daylight=false,bool outline=false,bool front=true,Vector2 shift=default)
        {
            var material=new Material(shader){name="Sketch title pair",hideFlags=HideFlags.DontSave};
            material.SetTexture("_MainTex",colour);material.SetTexture("_SketchTex",sketch);
            material.SetFloat("_Daylight",daylight?1:0);material.SetFloat("_Outline",outline?1:0);material.SetFloat("_UseFront",front?1:0);
            material.SetVector("_SketchShift",new Vector4(shift.x,shift.y,0,0));materials.Add(material);return material;
        }
        RawImage Quad(string name,Transform parent,Rect area,Texture2D colour,Material material,Rect? uv=null,bool blades=false)
        {
            var rect=WellTitlePresentation.At(name,parent,area);
            RawImage image=blades?rect.gameObject.AddComponent<TitleGrassImage>():rect.gameObject.AddComponent<RawImage>();
            image.texture=colour;image.material=material;image.raycastTarget=false;
            if(uv.HasValue)image.uvRect=uv.Value;return image;
        }
        static Rect UV(Texture texture,int x,int y,int width,int height)=>new Rect(x/(float)texture.width,1-(y+height)/(float)texture.height,width/(float)texture.width,height/(float)texture.height);
        static int Canonical(int mask)
        {
            int result=mask&15;
            for(int i=0;i<4;i++)if((mask&(1<<i))!=0&&(mask&(1<<((i+1)%4)))!=0&&(mask&(16<<i))!=0)result|=16<<i;
            return result;
        }
        public void Build(RectTransform target)
        {
            frame=target;shader=Resources.Load<Shader>("SketchTitleBlend");
            if(!shader)throw new InvalidOperationException("Missing SketchTitleBlend shader");
            frame.gameObject.AddComponent<Canvas>();
            var sky=Required(Village+"sky/color/sky");var sketchSky=Required(Village+"sky/sketch/sky");
            Sky=Quad("Title daylight sky",frame,new Rect(0,0,1600,900),sky,Pair(sky,sketchSky,true));
            var mountain=Required(Village+"background/color/mountains");var sketchMountain=Required(Village+"background/sketch/mountains");
            Quad("Title distant mountains",frame,new Rect(0,200,1600,590),mountain,Pair(mountain,sketchMountain));
            for(int i=0;i<Cuts.Length;i++)
            {
                var c=new Cloud{index=i,animation=StartingVillageWorld.CreateCloudAnimation(i,Scales[i])};
                c.root=WellTitlePresentation.At("Title cloud "+(i+1),frame,Rect.zero);
                var texture=c.animation.Colour;
                Quad("Pixel cloud",c.root,new Rect(-14*Scales[i],-5*Scales[i],texture.width*2,texture.height*2),texture,Pair(texture,c.animation.Sketch));
                clouds.Add(c);
            }
            var atlas=Required(Village+"ground/color/terrain-atlas");var sketchAtlas=Required(Village+"ground/sketch/terrain-atlas");
            var terrainMaterial=Pair(atlas,sketchAtlas);
            var masks=new List<int>();for(int mask=0;mask<256;mask++){int canonical=Canonical(mask);if(!masks.Contains(canonical))masks.Add(canonical);}masks.Sort();
            for(int row=0;row<4;row++)for(int col=0;col<26;col++)
            {
                uint mark=unchecked((uint)(col*73856093)^((uint)row*19349663));mark^=mark>>13;
                int k=masks.IndexOf(Canonical(row==0?254:255))*4+(int)(mark%4),columns=atlas.width/32;
                Quad("Title ground",frame,row==0?new Rect(col*64,747,64,48):new Rect(col*64,731+row*64,64,64),atlas,terrainMaterial,
                    UV(atlas,k%columns*32,k/columns*32+(row==0?8:0),32,row==0?24:32));
                if(row==0)
                {
                    var plant=new Plant{x=col*64+32,phase=col*.47f};
                    plant.root=WellTitlePresentation.At("Rooted title grass",frame,new Rect(plant.x,747,0,0));
                    plant.blades=(TitleGrassImage)Quad("Grass tips",plant.root,new Rect(-32,-16,64,16),atlas,terrainMaterial,UV(atlas,k%columns*32,k/columns*32,32,8),true);
                    grass.Add(plant);
                }
            }
            var daisy=Required(Village+"ground/color/decor/daisy");var sketchDaisy=Required(Village+"ground/sketch/decor/daisy");var flowerMaterial=Pair(daisy,sketchDaisy);
            float[] flowerX={115,515,775,1010,1485},sizes={2,2.4f,1.8f,2.2f,1.9f};
            for(int i=0;i<flowerX.Length;i++)
            {
                var flower=new Plant{x=flowerX[i],phase=i*1.73f};flower.root=WellTitlePresentation.At("Title daisy",frame,new Rect(flower.x,749,0,0));
                Quad("Flower",flower.root,new Rect(-7*sizes[i],-22*sizes[i],14*sizes[i],22*sizes[i]),daisy,flowerMaterial,UV(daisy,8,2,14,22));flowers.Add(flower);
            }
            heroRoot=WellTitlePresentation.At("Title hero foot",frame,new Rect(400,747,0,0));
            var shadow=Shadow();var shadowImage=Quad("Title hero shadow",heroRoot,new Rect(-54,-5,108,16),shadow,Pair(shadow,shadow));shadowImage.color=new Color(1,1,1,.16f);
            for(int i=0;i<FrameCount;i++){human[i]=Required(Art+"hero/human/F"+(i+1).ToString("D2"));ink[i]=Required(Art+"hero/ink/F"+(i+1).ToString("D2"));}
            heroMaterial=Pair(human[0],human[0],front:false);
            hero=Quad("New title hero",heroRoot,new Rect(-165,-330*121/128f,330,330),human[0],heroMaterial,new Rect(0,0,1,1));
            inkHeroRoot=WellTitlePresentation.At("Ink title hero foot",frame,new Rect(1200,747,0,0));
            var inkShadow=Quad("Ink title hero shadow",inkHeroRoot,new Rect(-54,-5,108,16),shadow,Pair(shadow,shadow));inkShadow.color=new Color(1,1,1,.16f);
            inkHeroMaterial=Pair(ink[0],ink[0],outline:true);
            inkHero=Quad("Ink title hero",inkHeroRoot,new Rect(-165,-330*121/128f,330,330),ink[0],inkHeroMaterial,new Rect(1,0,-1,1));
            var particles=Required(Art+"leaf-ash");
            var leafMaterial=Pair(particles,particles,shift:new Vector2(0,-.5f));var ashMaterial=Pair(particles,particles,front:false);
            var random=new System.Random(417);
            for(int i=0;i<90;i++)
            {
                float r=(float)random.NextDouble();var leaf=new Leaf{x=(float)random.NextDouble()*1600,y=40+(float)random.NextDouble()*690,
                    angle=(float)random.NextDouble()*360,phase=(float)random.NextDouble()*7,scale=r<.8f?2:3};
                int variant=i%6;leaf.image=Quad("Title leaf and ash",frame,new Rect(leaf.x,leaf.y,4*leaf.scale,4*leaf.scale),particles,leafMaterial,UV(particles,variant*4,0,4,4));leaves.Add(leaf);
            }
            for(int i=0;i<104;i++)
            {
                float seed=Hash(i,827);var d=new Debris{seed=seed,phase=Hash(i,483)};
                float size=seed>.8f?10:seed>.4f?6:4;
                d.image=Quad("Split-boundary paper ash",frame,new Rect(0,0,size,size),particles,ashMaterial,UV(particles,i%6*4,4,4,4));
                debris.Add(d);
            }
            Tick(0,SplitProgress);Fit();
        }
        Texture2D Shadow()
        {
            var texture=new Texture2D(108,16,TextureFormat.RGBA32,false){filterMode=FilterMode.Bilinear,hideFlags=HideFlags.DontSave};
            var pixels=new Color32[108*16];for(int y=0;y<16;y++)for(int x=0;x<108;x++)
                pixels[y*108+x]=new Color32(44,49,39,(byte)(Mathf.Clamp01(1-Mathf.Pow((x-54)/54f,2)-Mathf.Pow((y-8)/8f,2))*255));
            texture.SetPixels32(pixels);texture.Apply(false,true);made.Add(texture);return texture;
        }
        public static float Hash(int x,int y)
        {
            unchecked{uint n=(uint)(x+71)*374761393u^(uint)(y+13)*668265263u;n=(n^(n>>13))*1274126177u;return(n^(n>>16))/4294967295f;}
        }
        public static float Threshold(int col,int row)=>(1600-col*12-6+(Hash(col/3,row/3)-.5f)*108+(Hash(col,row)-.5f)*28+90)/1780;
        bool Reached(float x,float y)=>progress>=1||(progress>0&&progress>=Threshold(Mathf.Clamp(Mathf.FloorToInt(x/12),0,133),Mathf.Clamp(Mathf.FloorToInt(y/12),0,74)));
        public static int FrameAt(float seconds)
        {
            int ms=Mathf.FloorToInt(Mathf.Repeat(seconds,IdleLoopSeconds)*1000),within=ms%300;
            return (ms/300*4+(within<80?0:within<160?1:within<240?2:3))%FrameCount;
        }
        public void Fit()
        {
            if(!frame)return;
            float x=Mathf.Abs(frame.localScale.x),y=Mathf.Abs(frame.localScale.y),uniform=Mathf.Min(x,y);
            var compensation=new Vector3(uniform/Mathf.Max(.0001f,x),uniform/Mathf.Max(.0001f,y),1);
            if(heroRoot)heroRoot.localScale=compensation;
            if(inkHeroRoot)inkHeroRoot.localScale=compensation;
            foreach(var cloud in clouds)cloud.root.localScale=compensation;
        }
        public void SetProgress(float value){progress=Mathf.Clamp01(value);Shader.SetGlobalFloat(FrontId,progress);}
        public void Tick(float dt,float value)
        {
            SetProgress(value);dt=Mathf.Max(0,dt);clock+=dt;Fit();
            foreach(var c in clouds)
            {
                var cut=Cuts[c.index];float scale=Scales[c.index];float time=float.IsNaN(c.frozen)?clock:c.frozen;
                float x=Mathf.Repeat(cut.x+time*(12+c.index*1.73f*.5f),1600+cut.width*scale)-cut.width*scale/2,y=55+cut.y*.9f;
                if(float.IsNaN(c.frozen)&&Reached(x+cut.width*scale/2,y+cut.height*scale/2))c.frozen=clock;
                c.animation.Sample(time);c.root.anchoredPosition=new Vector2(Mathf.Round(x/2)*2,-Mathf.Round(y/2)*2);
            }
            foreach(var plant in grass)
            {
                if(float.IsNaN(plant.frozen)&&Reached(plant.x,740))plant.frozen=clock;
                float time=float.IsNaN(plant.frozen)?clock:plant.frozen;
                plant.blades.Sway=Mathf.Round((Mathf.Sin(time*2.8f+plant.phase)+Mathf.Sin(time*1.7f+plant.phase*.43f)*.5f)*1.4f)*2;
            }
            foreach(var plant in flowers)
            {
                if(float.IsNaN(plant.frozen)&&Reached(plant.x,749))plant.frozen=clock;
                float time=float.IsNaN(plant.frozen)?clock:plant.frozen;
                plant.root.localRotation=Quaternion.Euler(0,0,-(Mathf.Sin(time*3.2f+plant.phase)*.16f+Mathf.Sin(time*1.7f+plant.phase)*.06f)*Mathf.Rad2Deg);
            }
            HeroFrame=FrameAt(clock);hero.texture=human[HeroFrame];heroMaterial.SetTexture("_SketchTex",human[HeroFrame]);
            inkHero.texture=ink[HeroFrame];inkHeroMaterial.SetTexture("_SketchTex",ink[HeroFrame]);
            foreach(var leaf in leaves)
            {
                leaf.x+=dt*(14+Mathf.Sin(clock*.8f+leaf.phase)*12);leaf.y+=dt*(7+Mathf.Cos(clock+leaf.phase)*5);leaf.angle+=dt*.35f*Mathf.Rad2Deg;
                if(leaf.x>1635)leaf.x=-20;if(leaf.y>920)leaf.y=-20;
                leaf.image.rectTransform.anchoredPosition=new Vector2(Mathf.Round(leaf.x),-Mathf.Round(leaf.y));
                leaf.image.rectTransform.localRotation=Quaternion.Euler(0,0,Mathf.Round(leaf.angle/90)*90);
                leaf.image.color=new Color(1,1,1,Reached(leaf.x,leaf.y)?.65f:.9f);
            }
            AshAlive=debris.Count;
            foreach(var d in debris)
            {
                float y=Mathf.Repeat(d.phase*900-clock*(9+d.phase*12),936)-18;
                int row=Mathf.FloorToInt(Mathf.Max(0,y)/12);
                float jag=(Hash(22,row/3)-.5f)*68;
                float x=800+jag+(d.seed-.5f)*112+Mathf.Sin(clock*.44f+d.phase*7)*10;
                d.image.rectTransform.anchoredPosition=new Vector2(Mathf.Round(x/2)*2,-Mathf.Round(y/2)*2);
                d.image.rectTransform.localRotation=Quaternion.Euler(0,0,Mathf.Floor(d.phase*4+clock*.12f)*90);
                d.image.color=new Color(1,1,1,.24f+d.seed*.33f);
            }
        }
        public void ResetScene()
        {
            clock=0;foreach(var c in clouds)c.frozen=float.NaN;foreach(var p in grass)p.frozen=float.NaN;foreach(var p in flowers)p.frozen=float.NaN;
            Tick(0,SplitProgress);
        }
        void OnDestroy(){foreach(var c in clouds)c.animation.Release();foreach(var m in materials)if(m)Destroy(m);foreach(var t in made)if(t)Destroy(t);}
    }

    sealed class TitleGrassImage : RawImage
    {
        float sway;
        public float Sway {get=>sway;set{if(Mathf.Approximately(sway,value))return;sway=value;SetVerticesDirty();}}
        protected override void OnPopulateMesh(VertexHelper vertices)
        {
            base.OnPopulateMesh(vertices);var rect=rectTransform.rect;
            for(int i=0;i<vertices.currentVertCount;i++){UIVertex v=default;vertices.PopulateUIVertex(ref v,i);v.position.x+=sway*Mathf.InverseLerp(rect.yMin,rect.yMax,v.position.y);vertices.SetUIVertex(v,i);}
        }
    }
}
