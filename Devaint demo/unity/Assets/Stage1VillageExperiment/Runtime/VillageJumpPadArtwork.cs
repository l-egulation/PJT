using System;
using System.Collections.Generic;
using UnityEngine;

namespace Stage1VillageExperiment
{
    // Complete native artwork; only the opted-in pad receives environmental motion.
    public static class VillageJumpPadArtwork
    {
        [Serializable] public sealed class Placement
        { public string id; public int zone,sortingOrder; public float x,y; }
        [Serializable] public sealed class Layout
        {
            public string status,candidate;
            public bool gameplayEnabled;
            public int ppu,canvasWidth,canvasHeight;
            public float anchorX,anchorY,topWidthPixels;
            public Placement[] placements;
        }
        const string ResourcePath="Village/JumpPadArt/";
        public static GameObject Build(Transform parent,ICollection<UnityEngine.Object> owned)
        {
            var json=Resources.Load<TextAsset>(ResourcePath+"layout");
            if(!json)throw new InvalidOperationException("Jump-pad artwork layout is missing.");
            var layout=JsonUtility.FromJson<Layout>(json.text);
            if(layout.ppu!=32||layout.placements==null)
                throw new InvalidOperationException("Jump-pad artwork requires its registered 32 PPU layout.");
            var color=Resources.Load<Texture2D>(ResourcePath+"color");
            var sketch=Resources.Load<Texture2D>(ResourcePath+"sketch");
            if(!color||!sketch||color.width!=layout.canvasWidth||color.height!=layout.canvasHeight||sketch.width!=color.width||sketch.height!=color.height)
                throw new InvalidOperationException("Jump-pad sprites must share their complete registered canvas.");
            var shader=Resources.Load<Shader>("Village/VillageRegion");
            if(!shader)throw new InvalidOperationException("Village region shader is missing.");
            var material=new Material(shader){name="Jump pad A - native alpha color and pencil"};
            material.mainTexture=color;material.SetTexture("_SketchTex",sketch);material.SetFloat("_HasSketch",1);
            owned.Add(material);
            var pivot=new Vector2(layout.anchorX/color.width,1-layout.anchorY/color.height);
            var sprite=Sprite.Create(color,new Rect(0,0,color.width,color.height),pivot,layout.ppu,0,SpriteMeshType.FullRect);
            sprite.name="Stone spring pad A - complete artwork";owned.Add(sprite);
            var root=new GameObject("Jump pad A");root.transform.SetParent(parent,false);
            foreach(var entry in layout.placements)
            {
                var node=new GameObject(entry.id);node.transform.SetParent(root.transform,false);
                node.transform.localPosition=new Vector3(entry.x/layout.ppu,-entry.y/layout.ppu,0);
                if(layout.gameplayEnabled)
                {
                    var world=parent.GetComponent<Stage1VillageWorld>();
                    var motion=Resources.Load<TextAsset>(ResourcePath+"motion");
                    if(!world||!motion)throw new InvalidOperationException("Jump-pad motion requires the existing village world and local tuning.");
                    node.AddComponent<VillageSpringPad>().Initialize(world.arena,layout,JsonUtility.FromJson<VillageSpringPad.Tuning>(motion.text),material,entry.sortingOrder,owned);
                }
                else
                {
                    var renderer=node.AddComponent<SpriteRenderer>();renderer.sprite=sprite;renderer.sharedMaterial=material;renderer.sortingOrder=entry.sortingOrder;
                }
            }
            return root;
        }
    }
}
