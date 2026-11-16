using Deviant.Run;
using UnityEngine;
using UnityEngine.UI;

namespace Deviant.Game
{
    public sealed partial class TemporaryGameGui
    {
        void PaintInkDetailGrade(Text title,GrowthPart part)
        {
            var badge=title.transform.Find("Grade")?.GetComponent<Image>();
            if(part==null){if(badge)badge.gameObject.SetActive(false);return;}
            if(!badge)
            {
                badge=Rect("Grade",title.transform,0,-2,22,26).gameObject.AddComponent<Image>();
                badge.raycastTarget=false;badge.preserveAspect=true;
            }
            badge.sprite=Art($"parts/ink-grade-{part.grade}");
            badge.rectTransform.anchoredPosition=new Vector2(Mathf.Min(Width(title),title.rectTransform.sizeDelta.x-28)+6,2);
            badge.gameObject.SetActive(badge.sprite);
        }

        void SetActionGold(Button button,int? amount,bool inline=false,bool gain=false)
        {
            var caption=button.GetComponentInChildren<Text>();
            var size=((RectTransform)button.transform).sizeDelta;
            var root=button.transform.Find("Gold price") as RectTransform;
            if(!amount.HasValue)
            {
                if(root)root.gameObject.SetActive(false);
                Place(caption,0,0,size.x,size.y);return;
            }
            if(!root)
            {
                root=Rect("Gold price",button.transform,0,0,1,22);
                Glyph(CombatSprite("gold-coin"),root,0,2,18,18,Color.white).name="Coin";
                Words("",root,22,0,1,22,13,true).name="Value";
            }
            root.gameObject.SetActive(true);
            var number=root.Find("Value").GetComponent<Text>();
            number.text=(gain?"+":"")+amount.Value;number.color=caption.color;
            float priceWidth=22+Width(number);
            var coin=root.Find("Coin").GetComponent<Image>();
            coin.color=button.interactable?Color.white:new Color(1,1,1,.5f);
            if(inline)
            {
                float captionWidth=Width(caption),left=(size.x-captionWidth-priceWidth-8)/2;
                Place(caption,left,0,captionWidth,size.y);
                Place(root,left+captionWidth+8,0,priceWidth,size.y);
                Place(coin,0,(size.y-18)/2,18,18);
                Place(number,22,0,priceWidth-22,size.y);
            }
            else
            {
                Place(caption,0,0,size.x,22);
                Place(root,(size.x-priceWidth)/2,22,priceWidth,22);
                Place(coin,0,2,18,18);Place(number,22,0,priceWidth-22,22);
            }
        }
    }
}
