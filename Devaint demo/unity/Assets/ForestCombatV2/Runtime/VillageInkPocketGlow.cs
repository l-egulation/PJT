using Deviant.Combat;
using UnityEngine;

namespace ForestCombatV2
{
    // A small carried-ink light at the hand/pocket. It follows turns and survives until the bottle is taken out.
    public sealed class VillageInkPocketGlow : MonoBehaviour
    {
        CombatPlayer player;
        SpriteRenderer halo, core;
        Texture2D texture;
        Sprite sprite;
        Material material;
        float began, releaseAt = -1;
        Color tint;
        public bool Visible => halo && halo.enabled && halo.color.a > .01f;
        public int Piece { get; private set; }
        static readonly Color32[] Colors = { new Color32(255,65,83,255), new Color32(255,205,66,255), new Color32(93,223,126,255), new Color32(80,179,255,255), new Color32(187,109,240,255) };
        public static VillageInkPocketGlow Carry(CombatPlayer hero, int piece)
        {
            var light = hero.GetComponent<VillageInkPocketGlow>() ?? hero.gameObject.AddComponent<VillageInkPocketGlow>();
            light.player = hero; light.Piece = Mathf.Clamp(piece,1,5); light.tint = Colors[light.Piece-1];
            light.began = Time.unscaledTime; light.releaseAt = -1;
            if (!light.halo) light.Build();
            light.LateUpdate(); return light;
        }
        void Build()
        {
            const int side = 32;
            texture = new Texture2D(side,side,TextureFormat.RGBA32,false) { name="Carried village ink light", filterMode=FilterMode.Bilinear, wrapMode=TextureWrapMode.Clamp, hideFlags=HideFlags.DontSave };
            var pixels = new Color[side*side];
            for(int y=0;y<side;y++) for(int x=0;x<side;x++)
            {
                float d = new Vector2((x+.5f-side/2f)/(side/2f),(y+.5f-side/2f)/(side/2f)).magnitude;
                pixels[y*side+x] = new Color(1,1,1,Mathf.Pow(Mathf.Clamp01(1-d),1.7f));
            }
            texture.SetPixels(pixels); texture.Apply(false,true);
            sprite = Sprite.Create(texture,new Rect(0,0,side,side),Vector2.one*.5f,32);
            var shader = Resources.Load<Shader>("Chapter1GimmickShaders/KeyCarrierLight") ?? Shader.Find("Sprites/Default");
            material = new Material(shader) { hideFlags=HideFlags.DontSave }; if(material.HasProperty("_Solid")) material.SetFloat("_Solid",0);
            halo=Layer("주머니의 마을 잉크 빛"); core=Layer("주머니 잉크 빛 중심");
        }
        SpriteRenderer Layer(string name)
        {
            var r=new GameObject(name).AddComponent<SpriteRenderer>(); r.transform.SetParent(transform,false);
            r.sprite=sprite; r.sharedMaterial=material; return r;
        }
        public void Release() { if(releaseAt<0) releaseAt=Time.unscaledTime; }
        void LateUpdate()
        {
            if(!player || !halo) return;
            float age=Time.unscaledTime-began;
            float fade=releaseAt<0 ? 1 : 1-Mathf.Clamp01((Time.unscaledTime-releaseAt)/.5f);
            bool show=!player.Dead && player.visual && player.visual.enabled && fade>0;
            halo.enabled=core.enabled=show; if(!show) return;
            var at=player.Position+new Vector2(player.facing*.02f,player.size.y*.32f);
            halo.transform.position=core.transform.position=new Vector3(at.x,at.y,-.02f);
            halo.sortingLayerID=core.sortingLayerID=player.visual.sortingLayerID;
            halo.sortingOrder=player.visual.sortingOrder+2; core.sortingOrder=halo.sortingOrder+1;
            float burst=1-Mathf.Clamp01(age/.6f), pulse=.72f+.12f*Mathf.Sin(Time.unscaledTime*3);
            halo.transform.localScale=Vector3.one*(.8f+burst*.35f); core.transform.localScale=Vector3.one*(.22f+burst*.08f);
            var c=tint; c.a=(pulse+burst*.2f)*fade; halo.color=c;
            c=Color.Lerp(tint,Color.white,.65f); c.a=.85f*fade; core.color=c;
        }
        void OnDestroy() { if(material) Destroy(material); if(sprite) Destroy(sprite); if(texture) Destroy(texture); }
    }
}
