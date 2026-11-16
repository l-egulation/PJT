using System.Linq;
using UnityEngine;
namespace Deviant.Game
{
    public sealed partial class TemporaryGameGui
    {
        void LayoutApprovedResult()
        {
            resultHeading.text=menuTitle.text;resultHeading.color=menuTitle.text.Contains("클리어")?new Color32(184,220,208,255):new Color32(204,155,146,255);
            Place(resultBook.transform,140,0,680,430);
            Place(resultHeading.transform.parent,0,44,680,64.25f);Place(resultHeading,0,0,680,64.25f);resultHeading.fontSize=56;
            Place(resultRows,0,212.95f,680,175);
            LayoutApprovedResultFooter();
            var subtitle=menuCaption;subtitle.gameObject.SetActive(true);
            subtitle.text=menuTitle.text.Contains("클리어")?"챕터를 돌파했습니다.":"이번 도전이 끝났습니다.";
            Place(subtitle,140,117.25f,680,23);subtitle.fontSize=13;subtitle.color=new Color32(174,182,192,255);
            if(resultRows.name!=menuStats.text)
            {
                resultRows.name=menuStats.text;ClearGrowth(resultRows);
                Words("이번 도전",resultRows,0,-32.25f,307.5f,22,16,true);
                Words("획득 기록",resultRows,372.5f,-32.25f,307.5f,22,16,true);
                var lines=menuStats.text.Split('\n').Where(l=>l.Trim().Length>0).ToArray();int left=0,right=0;
                foreach(var line in lines)
                {
                    int split=line.IndexOf("   ");string key=split>0?line.Substring(0,split):line,value=split>0?line.Substring(split+3).Trim():"";
                    bool gain=key.Contains("안료")||key.Contains("팔레트")||key.Contains("조각");float x=gain?372.5f:0,y=(gain?right++:left++)*29.25f;
                    Words(key,resultRows,x,y,200,22,14,false,new Color32(174,182,192,255));
                    Words(value,resultRows,x+200,y,107.5f,22,14,true,null,TextAnchor.MiddleRight);
                }
                if(right==0)Words("획득 기록이 없습니다.",resultRows,372.5f,0,307.5f,22,14,false,new Color32(174,182,192,255));
                Fill(resultRows,0,-51.75f,680,.5f,new Color32(84,90,96,150));
                Fill(resultRows,0,140.65f,680,.5f,new Color32(84,90,96,150));
            }
            var events=UnityEngine.EventSystems.EventSystem.current;
            if(events&&events.currentSelectedGameObject!=menuPrimary.gameObject&&events.currentSelectedGameObject!=menuSecondary.gameObject)
                events.SetSelectedGameObject(menuPrimary.gameObject);
            bool both=menuSecondary.gameObject.activeSelf;
            Place(menuPrimary,both?500:410,366.875f,140,37.45f);
            if(both)Place(menuSecondary,320,366.875f,140,37.45f);
            foreach(var b in new[]{menuPrimary,menuSecondary})
            {
                var pauseCap=b.transform.Find("Pause action key");if(pauseCap)pauseCap.gameObject.SetActive(false);
                var label=b.GetComponentInChildren<UnityEngine.UI.Text>();label.fontSize=17;
                bool selected=UnityEngine.EventSystems.EventSystem.current&&UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject==b.gameObject;
                var cap=b.transform.Find("Keycap F");if(!cap)cap=ApprovedKeycap(b.transform,"F",0,7,28,28,15).transform;
                cap.GetComponentInChildren<UnityEngine.UI.Text>().text=InventoryKey("F");
                float keyWidth=Mathf.Max(28,InventoryKey("F").Length*8+8),gap=keyWidth+12;
                cap.GetComponentInChildren<UnityEngine.UI.Text>().rectTransform.sizeDelta=new Vector2(keyWidth,28);
                cap.gameObject.SetActive(selected);
                float width=((RectTransform)b.transform).rect.width;
                float textWidth=Mathf.Min(width-gap,label.preferredWidth),start=(width-textWidth-(selected?gap:0))/2;
                label.fontSize=17;Place(label,start+(selected?gap:0),0,textWidth,37.45f);
                if(selected)Place(cap,start,7,keyWidth,28);
            }
        }
    }
}
