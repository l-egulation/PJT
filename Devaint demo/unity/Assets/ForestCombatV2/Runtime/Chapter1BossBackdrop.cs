using System;
using System.Collections.Generic;
using UnityEngine;

namespace ForestCombatV2
{
    // Decorative rear layers only. Terrain, door colliders, actor scale and combat own their data.
    public sealed class Chapter1BossBackdrop : MonoBehaviour
    {
        [Serializable] public sealed class TextureSpec { public string id, resource; public int width, height; }
        [Serializable] public sealed class Layer
        {
            public string id; public float x,y,height,light=1,alpha=1,rotation,speed,bob,phase;
            public bool flip; public int order;
        }
        [Serializable] public sealed class Layout
        {
            public float roomWidth,approachWidth,maximumPassageHeadroom;
            public TextureSpec[] textures; public Layer[] room,approach;
        }
        sealed class Motion { public Transform transform;public Vector3 origin;public Layer layer; }
        readonly List<Motion> motions=new List<Motion>();
        readonly List<UnityEngine.Object> owned=new List<UnityEngine.Object>();
        readonly Dictionary<string,Sprite> sprites=new Dictionary<string,Sprite>();
        ForestCombatWorld world;
        float age, left, right;
        public bool Ready { get; private set; }
        public int LayerCount { get; private set; }
        public static Layout ReadLayout()
        {
            var asset=Resources.Load<TextAsset>("ClockBossEnvironment/layout");
            return asset?JsonUtility.FromJson<Layout>(asset.text):null;
        }
        public void Initialize(ForestCombatWorld owner,ForestRoom room)
        {
            if(Ready)return;
            world=owner;var layout=ReadLayout();
            if(layout==null){Debug.LogError("Approved clock boss environment is missing.",this);return;}
            foreach(var spec in layout.textures)
            {
                var bytes=Resources.Load<TextAsset>(spec.resource);
                if(!bytes){Debug.LogError("Missing boss environment texture: "+spec.resource,this);return;}
                var texture=new Texture2D(2,2,TextureFormat.RGBA32,false);
                owned.Add(texture);
                if(!Deviant.Media.CompressedTextureData.LoadImage(texture,bytes.bytes)){Debug.LogError("Invalid boss environment texture: "+spec.resource,this);return;}
                texture.name=spec.id;texture.filterMode=FilterMode.Bilinear;texture.wrapMode=TextureWrapMode.Clamp;
                var sprite=Sprite.Create(texture,new Rect(0,0,texture.width,texture.height),new Vector2(.5f,.5f),32);
                owned.Add(sprite);sprites.Add(spec.id,sprite);
            }
            float floor=-(room.y+room.entryY);
            float approachWidth=layout.approachWidth;
            if(owner.Data?.passages!=null)
                foreach(var passage in owner.Data.passages)if(passage.to==room.id)approachWidth=passage.w;
            left=room.x-approachWidth;right=room.x+room.w;
            var chamber=new GameObject("Clock room rear art").transform;chamber.SetParent(transform,false);
            var approach=new GameObject("Clock approach rear art").transform;approach.SetParent(transform,false);
            foreach(var layer in layout.room)Place(chamber,layer,room.x,floor,room.w/layout.roomWidth);
            // Fit the approved backdrop positions to the actual compact connection.
            // Existing solid earth masks decoration over shafts/ceilings; this adds no collision surfaces.
            foreach(var layer in layout.approach)Place(approach,layer,left,floor,approachWidth/layout.approachWidth);
            Ready=true;Animate(0);
        }
        void Place(Transform parent,Layer layer,float x,float floor,float scale)
        {
            var go=new GameObject(layer.id);go.transform.SetParent(parent,false);
            var r=go.AddComponent<SpriteRenderer>();r.sprite=sprites[layer.id];r.sharedMaterial=world.arena.spriteMaterial;
            r.sortingOrder=layer.order;r.flipX=layer.flip;
            r.color=new Color(layer.light,layer.light,layer.light,layer.alpha);
            go.transform.position=new Vector3(x+layer.x*scale,floor+layer.y,0);
            float size=layer.height/r.sprite.bounds.size.y;go.transform.localScale=Vector3.one*size;
            if(layer.id=="room-rear"||layer.id=="approach-soil")go.transform.localScale=new Vector3(size*scale,size,1);
            motions.Add(new Motion{transform=go.transform,origin=go.transform.position,layer=layer});LayerCount++;
        }
        void Update()
        {
            if(!Ready||!world||!world.View)return;
            float x=world.View.transform.position.x;if(x<left-25||x>right+25)return;
            if(world.arena.Controls.Current.SimulationPaused)return;
            age+=Time.deltaTime;Animate(age);
        }
        public void Animate(float seconds)
        {
            foreach(var m in motions)
            {
                var l=m.layer;
                m.transform.localRotation=Quaternion.Euler(0,0,(l.rotation+seconds*l.speed)*Mathf.Rad2Deg);
                m.transform.position=m.origin+new Vector3(Mathf.Sin(seconds*.34f+l.phase)*l.bob*.42f,Mathf.Sin(seconds*.72f+l.phase)*l.bob,0);
            }
        }
        void OnDestroy(){foreach(var item in owned)if(item)Destroy(item);owned.Clear();}
    }
}
