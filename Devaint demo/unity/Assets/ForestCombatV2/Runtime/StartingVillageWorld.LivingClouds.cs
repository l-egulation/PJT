using System.Collections.Generic;
using UnityEngine;

namespace ForestCombatV2
{
    public sealed partial class StartingVillageWorld
    {
        public interface IPixelCloudAnimation
        {
            Texture2D Colour { get; }
            Texture2D Sketch { get; }
            void Sample(float seconds);
            void Release();
        }

        // The title's six native silhouettes, at the 960/1600 game-view scale. Each texel is a 2x2 dot.
        static readonly RectInt[] LivingCloudCuts = {
            new RectInt(46,91,106,18), new RectInt(332,28,130,41), new RectInt(612,119,86,28),
            new RectInt(893,57,88,17), new RectInt(1173,117,57,10), new RectInt(1448,51,20,7) };
        static readonly float[] LivingCloudScales = { 1.5f, 2.4f, 1.5f, 2.1f, 1.8f, 1.5f };
        static readonly Color32[] LivingCloudColour = {
            new Color32(255,253,244,255), new Color32(253,249,235,255),
            new Color32(247,243,230,255), new Color32(240,242,232,255) };
        static readonly Color32[] LivingCloudSketch = {
            new Color32(248,242,231,255), new Color32(244,238,227,255),
            new Color32(236,231,220,255), new Color32(231,226,215,255) };
        sealed class LivingCloud : IPixelCloudAnimation
        {
            public RectInt cut; public int index, width, height, frame = -1;
            public float scale, clock; public float[] distance; public byte[] tone;
            public readonly Vector2[] roots = new Vector2[3];
            public Texture2D colour, sketch; public Color32[] colourPixels, sketchPixels;
            public Piece piece;
            public Texture2D Colour => colour;
            public Texture2D Sketch => sketch;
            public void Sample(float seconds) { int f=(Mathf.FloorToInt(seconds*8)+index*9)%96; if(f!=frame)PaintLivingCloud(this,f); }
            public void Release() { if(colour)Object.Destroy(colour); if(sketch)Object.Destroy(sketch); }
        }
        readonly List<LivingCloud> livingClouds = new List<LivingCloud>();

        public static IPixelCloudAnimation CreateCloudAnimation(int index, float scale)
        {
            if(index<0 || index>=LivingCloudCuts.Length || scale<=0)throw new System.ArgumentOutOfRangeException();
            var atlas=Resources.Load<Texture2D>(PixelSky+"color/clouds");
            if(!atlas || !atlas.isReadable)throw new System.InvalidOperationException("The approved pixel cloud atlas must be readable.");
            return CreateCloudData(atlas,atlas.GetPixels32(),index,scale);
        }

        static LivingCloud CreateCloudData(Texture2D atlas, Color32[] pixels, int i, float scale)
        {
            bool Opaque(RectInt cut, int x, int y) => x>=0 && y>=0 && x<cut.width && y<cut.height &&
                pixels[(atlas.height-1-cut.y-y)*atlas.width+cut.x+x].a>=128;
                var c = new LivingCloud { index = i, cut = LivingCloudCuts[i], scale = scale };
                c.width = Mathf.CeilToInt((c.cut.width + 28) * c.scale / 2);
                c.height = Mathf.CeilToInt((c.cut.height + 10) * c.scale / 2);
                int count = c.width * c.height;
                var inside = new bool[count]; c.tone = new byte[count];
                for (int y = 0; y < c.height; y++) for (int x = 0; x < c.width; x++)
                {
                    int at = y * c.width + x, sx = Mathf.FloorToInt((x * 2 + 1) / c.scale - 14),
                        sy = Mathf.FloorToInt((y * 2 + 1) / c.scale - 5);
                    inside[at] = Opaque(c.cut, sx, sy); c.tone[at] = 1;
                    if (!inside[at]) continue;
                    var p = pixels[(atlas.height - 1 - c.cut.y - sy) * atlas.width + c.cut.x + sx];
                    int light = p.r + p.g + p.b;
                    c.tone[at] = (byte)(light > 730 ? 0 : light > 690 ? 1 : light > 660 ? 2 : 3);
                }
                var inward = CloudDistance(inside, c.width, c.height, false);
                var outward = CloudDistance(inside, c.width, c.height, true);
                c.distance = new float[count];
                for (int at = 0; at < count; at++)
                {
                    c.distance[at] = inside[at] ? inward[at] : -outward[at];
                }
                // Remove the original picture's white contour too. Carry interior colours out
                // to the silhouette, including where the bottom shadow meets the sky.
                var bodyTone = (byte[])c.tone.Clone();
                for (int y = 0; y < c.height; y++) for (int x = 0; x < c.width; x++)
                {
                    int at = y*c.width+x; if (c.distance[at] >= 3) continue;
                    int nearest = 100;
                    for (int dy = -5; dy <= 5; dy++) for (int dx = -5; dx <= 5; dx++)
                    {
                        int nx=x+dx, ny=y+dy, distance=dx*dx+dy*dy;
                        if (nx<0 || nx>=c.width || ny<0 || ny>=c.height || distance>=nearest || c.distance[ny*c.width+nx]<3) continue;
                        nearest=distance; c.tone[at]=bodyTone[ny*c.width+nx];
                    }
                }
                for (int j = 0; j < 3; j++)
                {
                    int sy = Mathf.RoundToInt(c.cut.height * new[] { .28f, .56f, .8f }[j]);
                    int root = 0; for (int sx = 0; sx < c.cut.width; sx++) if (Opaque(c.cut, sx, sy)) root = sx;
                    c.roots[j] = new Vector2(root, sy);
                }
                Texture2D Texture(string state)
                {
                    var texture = new Texture2D(c.width, c.height, TextureFormat.RGBA32, false) {
                        name = "Living cloud " + i + " " + state, filterMode = FilterMode.Point,
                        wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
                    return texture;
                }

            c.colour=Texture("colour"); c.sketch=Texture("sketch");
            c.colourPixels=new Color32[count]; c.sketchPixels=new Color32[count];
            PaintLivingCloud(c,0); return c;
        }

        void BuildLivingClouds()
        {
            var atlas=Resources.Load<Texture2D>(PixelSky+"color/clouds");
            if(!atlas || !atlas.isReadable)return;
            var pixels=atlas.GetPixels32();
            for(int i=0;i<LivingCloudCuts.Length;i++)
            {
                var c=CreateCloudData(atlas,pixels,i,LivingCloudScales[i]);
                skyBackdropMade.Add(c.colour); skyBackdropMade.Add(c.sketch);
                Sprite Sprite(Texture2D texture)
                {
                    var sprite=UnityEngine.Sprite.Create(texture,new Rect(0,0,c.width,c.height),new Vector2(0,1),16,0,SpriteMeshType.FullRect);
                    made.Add(sprite); return sprite;
                }
                var colour=Sprite(c.colour); var sketch=Sprite(c.sketch);
                string key="Living cloud/"+i; var pivot=new Vector2(0,1);
                wrapped[key+"@"+pivot]=colour;
                c.piece=new Piece { renderer=Renderer("Village living cloud "+(i+1),colour,-119),
                    color=key,sketchSprite=sketch,pivot=pivot,keepTransform=true };
                paired.Add(c.piece); livingClouds.Add(c);
            }
        }

        // Two chamfer passes keep a fixed interior and limit erosion to the exposed one-dot fringe.
        static float[] CloudDistance(bool[] mask, int width, int height, bool seedInside)
        {
            var d = new float[mask.Length];
            for (int i = 0; i < d.Length; i++) d[i] = mask[i] == seedInside ? 0 : 10000;
            for (int pass = 0; pass < 2; pass++)
            {
                int step = pass == 0 ? 1 : -1;
                for (int y = pass == 0 ? 0 : height-1; y >= 0 && y < height; y += step)
                    for (int x = pass == 0 ? 0 : width-1; x >= 0 && x < width; x += step)
                    {
                        int at = y*width+x;
                        void Take(int nx, int ny, float weight) {
                            if (nx>=0 && nx<width && ny>=0 && ny<height) d[at]=Mathf.Min(d[at],d[ny*width+nx]+weight); }
                        Take(x-step,y,1); Take(x,y-step,1); Take(x-step,y-step,1.414214f); Take(x+step,y-step,1.414214f);
                    }
            }
            return d;
        }

        static float CloudNoise(float x, float y)
        {
            int ix=Mathf.FloorToInt(x), iy=Mathf.FloorToInt(y); float fx=x-ix, fy=y-iy;
            fx=fx*fx*(3-2*fx); fy=fy*fy*(3-2*fy);
            float Hash(int a,int b) { unchecked { uint n=((uint)(a&7)*374761393u)^((uint)b*668265263u);
                n=(n^(n>>13))*1274126177u; return (n^(n>>16))/4294967295f; } }
            return Mathf.Lerp(Mathf.Lerp(Hash(ix,iy),Hash(ix+1,iy),fx),Mathf.Lerp(Hash(ix,iy+1),Hash(ix+1,iy+1),fx),fy);
        }

        static void PaintLivingCloud(LivingCloud c, int frame)
        {
            float phase=frame/96f;
            for(int y=0;y<c.height;y++) for(int x=0;x<c.width;x++)
            {
                int at=y*c.width+x; float sx=(x*2+1)/c.scale-14, sy=(y*2+1)/c.scale-5;
                bool visible=c.distance[at]>=2 || c.distance[at]+(CloudNoise(sx*.065f-phase*8,sy*.16f+c.index*1.73f)-.5f)*2.7f>0;
                for(int j=0;j<3 && !visible;j++)
                {
                    float age=Mathf.Repeat(phase+j/3f+c.index*.071f,1);
                    float cx=c.roots[j].x-Mathf.Min(6,c.cut.width*.08f)+age*Mathf.Min(18,c.cut.width*.15f);
                    float rx=Mathf.Max(.65f,Mathf.Min(7,c.cut.width*.1f)*(1-age)+.5f);
                    float ry=Mathf.Max(.28f,Mathf.Min(1.5f,c.cut.height*.06f)*(1-age));
                    float dx=(sx-cx)/rx,dy=(sy-c.roots[j].y)/ry;
                    visible=age<.88f && dx*dx+dy*dy<1;
                }
                int pixel=(c.height-1-y)*c.width+x;
                c.colourPixels[pixel]=visible?LivingCloudColour[c.tone[at]]:new Color32();
                c.sketchPixels[pixel]=visible?LivingCloudSketch[c.tone[at]]:new Color32();
            }
            c.colour.SetPixels32(c.colourPixels); c.colour.Apply(false);
            c.sketch.SetPixels32(c.sketchPixels); c.sketch.Apply(false); c.frame=frame;
        }

        void TickLivingClouds(Vector3 view, float halfWidth, float dt)
        {
            float left=view.x-halfWidth, span=halfWidth*2+18;
            foreach(var c in livingClouds)
            {
                if(!SketchAt(c.piece.renderer.bounds.center.x)) c.clock+=Mathf.Max(0,dt);
                int frame=(Mathf.FloorToInt(c.clock*8)+c.index*9)%96;
                if(frame!=c.frame) PaintLivingCloud(c,frame);
                float start=(c.cut.x*.6f-c.cut.width*c.scale*.5f)/32;
                float x=left+Mathf.Repeat(start+16+c.clock*(12+c.index*1.73f*.5f)*.6f/32-left*.05f,span)-16;
                float y=GroundTop+(750-(55+c.cut.y*.9f))*.6f/32;
                c.piece.renderer.transform.position=new Vector3(Mathf.Round((x-14*c.scale/32)*16)/16,Mathf.Round((y+5*c.scale/32)*16)/16,0);
            }
        }
    }
}
