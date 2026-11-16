using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Deviant.Game
{
    // Pixel-aligned nine-slice frames. Corners stay at native resolution when panels stretch.
    public static class PaperGuiSkin
    {
        public static readonly Color Paper = new Color32(246,237,216,255);
        public static readonly Color Text = new Color32(39,43,37,255);
        public static readonly Color Muted = new Color32(113,108,91,255);
        public static readonly Color Accent = new Color32(17,121,111,255);
        static readonly Dictionary<string,Sprite> Frames = new Dictionary<string,Sprite>();
        public static Sprite Frame(string name)
        {
            Color fill, edge;
            switch(name)
            {
                case "panel": fill=Paper;edge=Text;break;
                case "card": fill=new Color32(239,230,209,255);edge=new Color32(169,160,139,255);break;
                case "button": fill=new Color32(229,220,198,255);edge=new Color32(154,145,124,255);break;
                case "button-selected":fill=Accent;edge=new Color32(9,78,74,255);break;
                case "button-disabled":fill=new Color32(218,210,193,255);edge=Muted;break;
                case "slot":fill=new Color32(241,233,215,255);edge=new Color32(161,153,133,255);break;
                case "slot-selected":fill=new Color32(226,240,219,255);edge=Accent;break;
                case "hud":fill=new Color32(31,41,35,250);edge=new Color32(133,143,123,255);break;
                case "hp-track":fill=new Color32(20,30,25,255);edge=new Color32(169,176,151,255);break;
                default:return null;
            }
            if(Frames.TryGetValue(name,out var existing))return existing;
            const int size=24;
            bool dark=name=="hud"||name=="hp-track";
            bool selected=name=="button-selected"||name=="slot-selected";
            Color outline=dark?new Color32(211,207,182,255):selected?new Color32(18,91,80,255):Text;
            Color inset=dark?new Color32(85,100,85,255):selected?new Color32(126,193,164,255):new Color32(255,248,228,255);
            var texture=new Texture2D(size,size,TextureFormat.RGBA32,false){name="Paper GUI "+name,filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp,hideFlags=HideFlags.HideAndDontSave};
            var pixels=new Color[size*size];
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {
                int dx=Mathf.Min(x,size-1-x),dy=Mathf.Min(y,size-1-y),d=Mathf.Min(dx,dy);
                // Two stepped corner pixels, a dark keyline, then an ivory inner keyline.
                Color pixel=d==0?outline:d==1?edge:d==2?inset:fill;
                if(dx+dy<2)pixel=Color.clear;
                pixels[y*size+x]=pixel;
            }
            texture.SetPixels(pixels);texture.Apply();
            var sprite=UnityEngine.Sprite.Create(texture,new Rect(0,0,size,size),Vector2.one*.5f,100,0,SpriteMeshType.FullRect,new Vector4(5,5,5,5));
            sprite.name=name;sprite.hideFlags=HideFlags.HideAndDontSave;Frames.Add(name,sprite);return sprite;
        }
    }

}
