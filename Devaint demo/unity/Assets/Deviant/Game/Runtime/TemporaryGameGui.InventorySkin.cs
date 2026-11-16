using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Deviant.Game
{
    // The supplied 800x464 inventory-journal-kit; coordinates follow its runtime/layout.json.
    public sealed partial class TemporaryGameGui
    {
        readonly GameObject[] bagEquipmentPages=new GameObject[2];
        readonly Text[] bagEquipmentLabels=new Text[14];
        RectTransform bagDetailContent;
        string bagLastDetailText;
        Text bagDetailHint;
        static Sprite BagArt(string name)
        {
            string key="inventory-kit/"+name;
            if(JournalSprites.TryGetValue(key,out var cached)&&cached)return cached;
            var texture=Resources.Load<Texture2D>("InventoryJournalUI/"+name);
            if(!texture)throw new InvalidOperationException("Inventory journal kit missing: "+name);
            var sprite=UnityEngine.Sprite.Create(texture,new Rect(0,0,texture.width,texture.height),new Vector2(.5f,.5f),100);
            sprite.name=name;return JournalSprites[key]=sprite;
        }
        Image BagFace(string art,Transform parent,float x,float y,float w,float h,bool raycast=false)
        {
            var image=Rect(art,parent,x,y,w,h).gameObject.AddComponent<Image>();image.sprite=BagArt(art);image.raycastTarget=raycast;return image;
        }
        // The supplied base sheet still paints four kind tabs and a 5x3 grid. Blank paper cut from the same sheet's
        // empty lower right page covers what the single shared bag no longer uses.
        RawImage BagPaper(Transform page,float x,float y,float w,float h,float fromY)
        {
            var paper=Rect("Blank paper",page,x,y,w,h).gameObject.AddComponent<RawImage>();var sheet=BagArt("inventory-base").texture;
            paper.texture=sheet;paper.uvRect=new Rect(x/sheet.width,1-(fromY+h)/sheet.height,w/sheet.width,h/sheet.height);paper.raycastTarget=false;
            return paper;
        }
        // The bag's cells: five per row, up to the sheet's three rows (15, the largest bag unlock). Rows past the bag
        // are papered over. Rebuilt when the run's bag grows after the screen was built.
        Transform bagPage;
        readonly List<GameObject> bagCellParts=new List<GameObject>();
        void BuildBagCells()
        {
            foreach(var part in bagCellParts)if(part)Destroy(part);
            bagCellParts.Clear();
            int capacity=host.Inventory.Capacity;
            for(int row=(capacity+4)/5;row<3;row++){var paper=BagPaper(bagPage,414,102+row*68,360,68,330);paper.transform.SetSiblingIndex(1);bagCellParts.Add(paper.gameObject);}
            float[] columns={420,490,561,632,702};
            slots=new Button[capacity];
            for(int i=0;i<slots.Length;i++){int cell=i;slots[i]=BagSlot("bag-slot","Bag cell "+i,bagPage,columns[i%5],108+i/5*68,66,60,()=>SelectBagEntry(cell));Words("+",slots[i].transform,0,0,66,60,18,false,JSlotMark,TextAnchor.MiddleCenter).name="Empty marker";bagCellParts.Add(slots[i].gameObject);}
        }
        Button BagButton(string art,string name,Transform parent,float x,float y,float w,float h,Action action)
        {
            var image=BagFace(art,parent,x,y,w,h,true);image.name=name;
            var button=image.gameObject.AddComponent<Button>();button.transition=Selectable.Transition.None;
            button.onClick.AddListener(()=>{if(!button.interactable)return;action();Refresh();});return button;
        }
        Button BagSlot(string art,string name,Transform parent,float x,float y,float w,float h,Action action)
        {
            var button=BagButton(art,name,parent,x,y,w,h,action);
            var item=Rect("Item",button.transform,0,0,w,h).gameObject.AddComponent<Image>();item.raycastTarget=false;item.preserveAspect=true;
            var outline=button.gameObject.AddComponent<Outline>();outline.effectColor=JDark;outline.effectDistance=new Vector2(1,-1);outline.enabled=false;
            return button;
        }
        void SetBagSlot(Button button,bool selected)
        {
            SelectionRing(button.transform,selected);
            bool bagCell=button.name.StartsWith("Bag cell",StringComparison.Ordinal);
            if(bagCell)button.image.sprite=BagArt(selected?"bag-slot-selected":"bag-slot");
            var outline=button.GetComponent<Outline>();if(outline)outline.enabled=selected&&!bagCell;
        }
        static void SetBagTab(Button button,bool selected,bool weapon)
        {
            button.image.sprite=BagArt((weapon?"weapon-tab":"filter-tab")+(selected?"-active":""));
            button.GetComponentInChildren<Text>().color=selected?JPaperText:JInk;
        }
        void BuildBagKit()
        {
            bag=Rect("가방 장비",stage,80,38,800,464).gameObject;var page=bag.transform;
            BagFace("inventory-base",page,0,0,800,464);
            // The supplied sheet also paints five palette boxes; cover them before drawing the actual 3–5 cells.
            BagPaper(page,28,248,356,76,326).name="Palette slot paper";
            BagPaper(page,506,54,268,42,330);
            Words("가방 / 장비",page,48,12,180,28,24,true,JPaperText,TextAnchor.MiddleCenter);
            var close=BagButton("close-button","Close inventory",page,756,28,36,36,()=>Presenter.Close());
            BagFace("close-icon",close.transform,10,10,16,16);
            equipment=new Button[14];bagPage=page;
            for(int w=0;w<2;w++)
            {
                int weapon=w;weapons[w]=BagButton("weapon-tab","Weapon tab "+w,page,32+w*176,58,172,36,()=>Presenter.SelectWeapon(weapon));
                weaponNames[w]=Words("",weapons[w].transform,0,0,172,36,16,true,JInk,TextAnchor.MiddleCenter);
                var root=Rect("Equipped weapon "+w,page,0,0,400,464);bagEquipmentPages[w]=root.gameObject;
                bagPaletteButtons[w]=PaletteInfoButton(root,32,220,348,()=>SelectBagPalette(weapon),out bagPaletteCounts[w]);
                for(int s=0;s<7;s++)
                {
                    int slot=s;float x=s==0?32:s==6?210:32+(s-1)*72,y=s==0||s==6?138:252,width=s==0||s==6?170:60;
                    var b=equipment[w*7+s]=BagSlot(s==0||s==6?"equipment-slot":"stat-slot","Equipment "+w+" "+s,root,x,y,width,64,()=>SelectBagEquipment(weapon,slot));
                    bagEquipmentLabels[w*7+s]=Words("",b.transform,s==0||s==6?56:0,0,s==0||s==6?width-64:width,s==0||s==6?64:46,s==0||s==6?14:11,false,JMuted,TextAnchor.MiddleCenter);
                }
            }
            Words("형태",page,32,108,170,24,18,true);Words("특수",page,210,108,170,24,18,true);
            // One shared bag: a single 전체 tab, no kind filters and no pages.
            Words("전체",BagFace("filter-tab-active",page,420,58,84,36).transform,0,0,84,36,16,true,JPaperText,TextAnchor.MiddleCenter);
            bagCapacity=Words("",page,610,30,136,22,16,true,JMuted,TextAnchor.MiddleRight);
            BuildBagCells();
            detailDrop=Drop(page,null,null,420,332,40,56);detailName=Words("",page,472,332,296,24,18,true);
            bagDetailContent=JournalScroll("Bag ink effect",page,472,365,296,54);detail=Paragraph(bagDetailContent,0,0,296,14,20,JInk);
            detailPreview=Words("",page,472,422,296,18,11,true,JInk,TextAnchor.MiddleLeft);
            equip=Action("장착",page,32,336,110,32,EquipBagSelected,true);unequip=Action("해제",page,32,336,110,32,UnequipBagSelected);
            discard=Action("버리기",page,151,336,110,32,()=>Presenter.DiscardSelected());exchange=Action("바닥 잉크 교환",page,151,336,110,32,()=>host.ExchangeSelected());destroy=Action("파괴",page,270,336,110,32,DestroySelectedInk);
            foreach(var b in new[]{equip,unequip,discard,exchange,destroy})b.GetComponentInChildren<Text>().fontSize=13;
            bagMessage=Words("",page,32,412,348,40,11,false,JMuted,TextAnchor.UpperLeft);bagMessage.horizontalOverflow=HorizontalWrapMode.Wrap;
            BindPrompt(Words("",page,420,428,348,MenuHintHeight,MenuHintFontSize,false,JMuted,TextAnchor.MiddleRight), "{Inventory} / {Cancel} 닫기");
            bagDetailHint=Words("",page,420,428,180,18,11,false,JMuted);
            RegisterScreen(bag,close);
        }
        void UpdateBagDetailLayout()
        {
            float height=Mathf.Max(1,LineCount(detail))*20;Place(detail,0,0,296,height);bagDetailContent.sizeDelta=new Vector2(296,Mathf.Max(54,height));
            bagDetailHint.text=height>54?"효과 설명 스크롤":"";
            if(detail.text!=bagLastDetailText){bagLastDetailText=detail.text;bagDetailContent.parent.GetComponent<ScrollRect>().verticalNormalizedPosition=1;}
        }
    }
}
