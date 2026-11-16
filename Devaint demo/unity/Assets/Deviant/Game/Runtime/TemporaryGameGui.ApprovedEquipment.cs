using System;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Deviant.Run;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

namespace Deviant.Game
{
    public sealed partial class TemporaryGameGui
    {
        // equipment-effects / equipment-swap: the HTML's 1920x1080 coordinates at half scale.
        float liveEquipmentScroll;
        Guid liveEquipmentDetailId;
        RectTransform liveRenamePanel;
        InputField liveRenameInput;
        Text liveRenameCount, liveRenameError;
        Button liveRenameSave, liveRenameCancel;
        Guid liveRenameId;
        Keyboard liveRenameKeyboard;
        bool liveRenameComposing;
        int liveRenameFocus;
        string liveRenameReturnStage;

        Text EquipmentText(string name,string text,Transform parent,float x,float y,float w,float h,int size,Color? color=null,TextAnchor align=TextAnchor.UpperLeft)
        {var t=LiveWords(text,parent,x,y,w,h,size,color,align);t.name=name;return t;}

        string EquipmentSkill(RunInkWeapon weapon)=>LiveSkill(weapon);

        RunInkWeapon SelectedInventoryEquipment()=>liveStage=="target"?Sketch.Equipped(liveTarget):liveStage=="mountTarget"?LiveWeapon():LiveWeapons().ElementAtOrDefault(livePick);
        void DrawApprovedEquipment(EquipmentBook b)
        {
            DrawInventoryHeader(b,"장비");
            Fill(b.Body,502,104,.6f,361,new Color32(210,216,222,40));
            bool target=liveStage=="target",mount=liveStage=="mountTarget";
            // Equip and replace share one cooldown that starts when the bag closes after a change; the X order
            // swap and slot selection stay available while it runs.
            bool cooldownLocked=Sketch.State.Arsenal.EquipLockedByCooldown;
            var slotsPane=Rect("Equipment combat lock",b.Body,0,0,500,256);
            LiveWords(mount?"장착할 자리 선택":"장착 중인 무기",b.Body,65,104,410,22,15,CoreMuted);
            for(int i=0;i<2;i++)
            {
                int slot=i;float x=65+i*226;var weapon=Sketch.Equipped(i);bool selected=(target||mount)&&i==liveTarget;
                var button=LiveButton("Equip target "+i,"",slotsPane,x,144,187,99,()=>{liveTarget=slot;if(!mount){liveStage="target";inventoryTargetLocked=false;}});
                InventoryLabel(button,"",CoreMuted);
                Color edge=selected?CoreGold:new Color32(99,110,117,255);
                Fill(button.transform,0,0,187,.7f,edge);Fill(button.transform,0,98.3f,187,.7f,edge);
                Fill(button.transform,0,0,.7f,99,edge);Fill(button.transform,186.3f,0,.7f,99,edge);
                if(weapon==null)LiveWords("맨손",button.transform,15,34,157,30,20,selected?CoreGold:CoreMuted);
                else
                {
                    LiveArt(LiveWeaponArt(weapon),button.transform,12,23,48,48,selected?1:.55f);
                    var name=LiveWords(LiveWeaponName(weapon),button.transform,67,24,107,25,18,selected?CoreGold:CoreMuted);FitEquipmentName(name,107);
                    DrawEquipmentDurability(button.transform,weapon,67,61,67,true);
                }
            }
            LiveWords("↔",slotsPane,258,164,29,24,18,CoreMuted,TextAnchor.MiddleCenter);
            InventoryAction("Swap equipped weapons","","X",slotsPane,259,196,31,()=>LiveTrade(b,Sketch.SwapOrder),false,Sketch.State.Arsenal.CanSwapOrder);
            LiveWords(inventoryTargetLocked?"무기 "+(liveTarget+1)+"에 장착할 무기":"보관 중인 무기",b.Body,65,268,413,23,15,CoreMuted);
            var weapons=LiveWeapons();livePick=Mathf.Clamp(livePick,0,Math.Max(0,weapons.Length-1));
            var content=ApprovedScroll("Stored equipment list",b.Body,65,306,413,118);
            for(int i=0;i<weapons.Length;i++)
            {
                int index=i;var weapon=weapons[i];bool selected=!target&&!mount&&livePick==i;
                var button=LiveButton("Core item "+i,"",content,i%3*130,i/3*125,121,117,()=>{livePick=index;liveStage="weapons";});
                InventoryLabel(button,"",CoreMuted);
                LiveArt(LiveWeaponArt(weapon),button.transform,32,0,57,57,selected?1:.3f);
                var name=LiveWords(LiveWeaponName(weapon),button.transform,0,67,121,23,16,selected?CoreGold:CoreMuted,TextAnchor.UpperCenter);FitEquipmentName(name,121);
                DrawEquipmentDurability(button.transform,weapon,5,99,75,true);
                if(!selected)button.gameObject.AddComponent<CanvasGroup>().alpha=.5f;
            }
            if(weapons.Length==0)LiveWords("보관 중인 무기가 없습니다.",content,0,15,390,25,14,CoreMuted);
            RestoreApprovedScroll(content,"equipment/stored",413,Mathf.CeilToInt(weapons.Length/3f)*125);
            string selection="equipment/"+livePick;
            if(approvedListSelection!=selection){RevealApprovedItem(content,livePick/3*125,117);approvedListSelection=selection;}
            if(cooldownLocked)LiveWords($"무기 변경 대기 {Mathf.CeilToInt(Sketch.ChangeCooldownRemaining)}초 · X 순서 교환은 가능",b.Body,65,244,413,22,14,CoreGold);
            var detail=SelectedInventoryEquipment();DrawEquipmentDetail(b,detail);
            LiveRule(b.Body,535,435,360);
            bool same=mount&&detail!=null&&Sketch.Equipped(liveTarget)==detail;
            string action=target?(Sketch.Equipped(liveTarget)==null?"장착하기":"교체하기"):mount?(same?"장착 중":Sketch.Equipped(liveTarget)==null?"장착하기":"교체하기"):"장착하기";
            bool executing=mount||inventoryTargetLocked;
            // Picking the other equipped weapon for this slot is an order swap and ignores the cooldown.
            bool orderSwap=executing&&detail!=null&&Sketch.Equipped(1-liveTarget)==detail;
            bool can=target?!cooldownLocked&&weapons.Length>0:detail!=null&&!same&&(orderSwap||!cooldownLocked&&(!executing||Sketch.State.Arsenal.CanEquip(detail.Id,liveTarget,out _)));
            if(cooldownLocked&&!orderSwap&&!same)action="대기 "+Mathf.CeilToInt(Sketch.ChangeCooldownRemaining)+"초";
            InventoryAction("Equipment primary",action,"F",b.Body,535,447,170,ActivateInventoryEquipment,true,can);
        }

        void EquipmentWash(Transform parent,float x,float y,float width,float height)
        {
            // The HTML stretches the brush across its full button, unlike contained weapon art.
            var wash=LiveArt(Deviant.UI.Theme.ApprovedTheme.SelectionWash,parent,x,y,width,height,.48f);
            Place(wash,x,y,width,height);wash.transform.SetAsFirstSibling();
        }

        void FitEquipmentName(Text label,float width)
        {
            label.supportRichText=false;
            string text=label.text;
            label.horizontalOverflow=HorizontalWrapMode.Overflow;
            var settings=label.GetGenerationSettings(Vector2.zero);
            var indices=StringInfo.ParseCombiningCharacters(text);
            while(label.cachedTextGeneratorForLayout.GetPreferredWidth(label.text,settings)/label.pixelsPerUnit>width&&indices.Length>0)
            {
                text=text.Substring(0,indices[indices.Length-1]);indices=StringInfo.ParseCombiningCharacters(text);label.text=text+"…";
            }
        }

        void DrawEquipmentDurability(Transform parent,RunInkWeapon weapon,float x,float y,float width,bool compact=false)
        {
            float ratio=Mathf.Clamp01(weapon.Durability/Mathf.Max(1,weapon.Maximum)),height=compact?3.5f:5;
            Fill(parent,x,y+(compact?5.9f:5.5f),width,height,new Color32(174,182,188,38));
            Fill(parent,x,y+(compact?5.9f:5.5f),width*ratio,height,new Color32(199,214,193,255));
            EquipmentText("Equipment durability",Mathf.CeilToInt(ratio*100)+"%",parent,x+width+(compact?6:11),y,35,20,compact?12:14,CorePaper);
        }

        void DrawEquipmentDetail(EquipmentBook b,RunInkWeapon weapon)
        {
            liveDetail=ApprovedScroll("Approved item description",b.Body,535,104,360,319);
            if(weapon==null){liveEquipmentDetailId=Guid.Empty;liveEquipmentScroll=0;LiveWords("맨손",liveDetail,0,0,345,35,25,CoreMuted);return;}
            if(liveEquipmentDetailId!=weapon.Id){liveEquipmentDetailId=weapon.Id;liveEquipmentScroll=0;}
            sketchSelectedWeapon=weapon.Id;
            float y=DrawApprovedWeaponDescription(liveDetail,weapon,345);
            var title=liveDetail.GetComponentsInChildren<Text>().FirstOrDefault(t=>t.name=="Inventory weapon title");
            if(title){title.text=LiveWeaponName(weapon);title.rectTransform.sizeDelta=new Vector2(157,34);FitEquipmentName(title,157);}
            InventoryAction("Rename equipped weapon","이름 변경","N",liveDetail,195,3,150,()=>OpenLiveRename(weapon),false).GetComponentInChildren<Text>().fontSize=11;
            RestoreApprovedScroll(liveDetail,"equipment/detail/"+weapon.Id,360,y+12);
        }
        float EquipmentDetailHeading(string label,float y)
        {LiveRule(liveDetail,0,y,345);LiveWords(label,liveDetail,0,y+16,345,20,13,CoreMuted);return y+45;}
        void DrawEquipmentFooter(Transform parent)
        {
            string[] keys={MenuKey("← → ↑ ↓",Deviant.Input.Contracts.GameAction.Navigate),InventoryKey("F"),InventoryKey("Esc"),MenuKey("W / S",Deviant.Input.Contracts.GameAction.MapPan)};
            string[] labels={"선택","실행","돌아가기","설명"};
            if(Controls&&Controls.UsingGamepad)
            {
                keys=keys.Concat(new[]{MenuKey("Q / E",Deviant.Input.Contracts.GameAction.Inventory)}).ToArray();
                labels=labels.Concat(new[]{"탭"}).ToArray();
            }
            float x=65;
            for(int i=0;i<keys.Length;i++)
            {
                float width=i==0?43:i==2?30:i==3?37:21;
                width=ApprovedKeycap(parent,keys[i],x,503,width,MenuHintHeight,MenuHintFontSize).rectTransform.sizeDelta.x;
                LiveWords(labels[i],parent,x+width+8,503,65,MenuHintHeight,MenuHintFontSize,CoreMuted,TextAnchor.MiddleLeft);
                x+=width+(i==2?84:60);
            }
        }
        void ActivateInventoryEquipment()
        {
            bool cooldownLocked=Sketch.State.Arsenal.EquipLockedByCooldown;
            if(cooldownLocked&&liveStage!="mountTarget"&&!inventoryTargetLocked)return;
            if(liveStage=="target")
            {
                if(LiveWeapons().Length==0)return;
                inventoryTargetLocked=true;inventoryMountFromBag=false;liveStage="weapons";livePick=0;return;
            }
            if(liveStage=="mountTarget"||inventoryTargetLocked){EquipLiveSelection();return;}
            var chosen=LiveWeapons().ElementAtOrDefault(livePick);if(chosen==null)return;
            liveWeapon=chosen.Id;liveStage="mountTarget";inventoryMountFromBag=false;
            liveTarget=Sketch.Equipped(0)==null?0:Sketch.Equipped(1)==null?1:liveTarget;
        }
        void EquipLiveSelection()
        {
            var chosen=liveStage=="mountTarget"?LiveWeapon():LiveWeapons().ElementAtOrDefault(livePick);
            if(chosen==null||Sketch.Equipped(liveTarget)==chosen)return;
            LiveTrade(nativeBag,()=>{if(Sketch.Equipped(1-liveTarget)==chosen)Sketch.SwapOrder();else Sketch.Equip(chosen.Id,liveTarget);});
            if(Sketch.Equipped(liveTarget)==chosen){liveStage="target";inventoryTargetLocked=inventoryMountFromBag=false;livePick=0;liveAction=0;}
        }
        void TickLiveEquipmentInput()
        {
            if(LiveBackPressed)
            {
                if(liveStage=="target"){Presenter.Close();return;}
                if(liveStage=="mountTarget"&&inventoryMountFromBag){SetLiveTab(0);return;}
                liveStage=liveStage=="mountTarget"?"weapons":"target";inventoryTargetLocked=false;return;
            }
            if(inventoryRenamePressed){OpenLiveRename(SelectedInventoryEquipment());return;}
            if(inventorySwapPressed&&Sketch.State.Arsenal.CanSwapOrder){LiveTrade(nativeBag,Sketch.SwapOrder);return;}
            if(LiveSubmitPressed){ActivateInventoryEquipment();return;}
            int move=TakeLiveNavigation();
            if(liveStage=="target"||liveStage=="mountTarget")
            {
                if(Math.Abs(move)==1)liveTarget=move<0?0:1;
                else if(move==2&&liveStage=="target"&&LiveWeapons().Length>0){liveStage="weapons";inventoryTargetLocked=false;}
            }
            else if(move==-2&&livePick<3){liveStage="target";inventoryTargetLocked=false;}
            else if(move!=0)livePick=Mathf.Clamp(livePick+(Math.Abs(move)==1?move:move/2*3),0,Math.Max(0,LiveWeapons().Length-1));
            ScrollLiveDetail();
        }
        void FocusLiveRename(){OpenLiveRename(SelectedInventoryEquipment());}

        void OpenLiveRename(RunInkWeapon weapon)
        {
            if(liveRenamePanel||weapon==null)return;
            liveRenameId=weapon.Id;liveRenameReturnStage=liveStage;liveRenameFocus=0;
            liveRenamePanel=Rect("Equipment rename modal",nativeBag.Root.transform,0,0,960,540);
            Fill(liveRenamePanel,0,0,960,540,new Color32(16,19,23,181)).raycastTarget=true;
            var skin=Deviant.UI.Theme.ApprovedTheme.CreateFrame(liveRenamePanel,368,177,224,187);
            skin.name="Rename themed frame";skin.raycastTarget=true;
            EquipmentText("Rename title","무기 이름 변경",liveRenamePanel,392,199.68f,176,22,15);
            LiveWords((host.Arena.catalog.forms.FirstOrDefault(f=>f.id==weapon.Form)?.name??weapon.Form)+" / 최대 20자",liveRenamePanel,392,228.225f,176,18,11,new Color32(183,190,198,255));
            var field=Rect("Equipment name input",liveRenamePanel,392,253.625f,176,26.25f);
            var image=field.gameObject.AddComponent<Image>();image.color=new Color32(25,32,37,135);
            Fill(field,0,25.585f,176,.665f,new Color32(183,214,215,255));
            var value=LiveWords("",field,5,3,166,22,14);value.supportRichText=false;value.verticalOverflow=VerticalWrapMode.Truncate;
            liveRenameInput=field.gameObject.AddComponent<InputField>();liveRenameInput.textComponent=value;liveRenameInput.targetGraphic=image;
            liveRenameInput.lineType=InputField.LineType.SingleLine;liveRenameInput.text=LiveWeaponName(weapon);
            liveRenameCount=LiveWords("",liveRenamePanel,513,284.75f,55,16,11,CoreMuted,TextAnchor.UpperRight);
            liveRenameError=LiveWords("",liveRenamePanel,392,302,176,17,11,CoreGold);
            Fill(liveRenamePanel,392,321,176,.5f,new Color32(171,180,188,48));
            liveRenameSave=LiveButton("Equipment rename save","저장",liveRenamePanel,392,329,66,MenuHintHeight,()=>CloseLiveRename(true),size:MenuHintFontSize);
            liveRenameCancel=LiveButton("Equipment rename cancel","취소",liveRenamePanel,474,329,88,MenuHintHeight,()=>CloseLiveRename(false),size:MenuHintFontSize);
            Place(liveRenameSave,370,329,100,MenuHintHeight);Place(liveRenameSave.GetComponentInChildren<Text>(),43,0,45,MenuHintHeight);BindMenuKey(ApprovedKeycap(liveRenameSave.transform,"F",0,0,36,MenuHintHeight,MenuHintFontSize).GetComponentInChildren<Text>(),"F",Deviant.Input.Contracts.GameAction.Submit);
            Place(liveRenameCancel,490,329,110,MenuHintHeight);Place(liveRenameCancel.GetComponentInChildren<Text>(),49,0,50,MenuHintHeight);BindMenuKey(ApprovedKeycap(liveRenameCancel.transform,"Esc",0,0,42,MenuHintHeight,MenuHintFontSize).GetComponentInChildren<Text>(),"Esc",Deviant.Input.Contracts.GameAction.Cancel);
            liveRenameInput.onValueChanged.AddListener(_=>UpdateLiveRenameCount());UpdateLiveRenameCount();
            liveRenameKeyboard=Keyboard.current;
            if(liveRenameKeyboard!=null){liveRenameKeyboard.onIMECompositionChange+=LiveRenameComposition;liveRenameKeyboard.SetIMEEnabled(true);}
            liveRenameInput.Select();liveRenameInput.ActivateInputField();
        }

        void LiveRenameComposition(IMECompositionString value){liveRenameComposing=value.Count>0;}
        void UpdateLiveRenameCount()
        {
            int length=StringInfo.ParseCombiningCharacters(liveRenameInput.text).Length;
            liveRenameCount.text=length+" / 20";
            liveRenameSave.interactable=length>0&&length<=20&&!string.IsNullOrWhiteSpace(liveRenameInput.text);
            if(liveRenameSave.interactable)liveRenameError.text="";
        }
        void TickLiveRename()
        {
            if(LiveBackPressed){CloseLiveRename(false);return;}
            if(liveRenameComposing)return;
            // F remains text input while editing (including the Korean ㄹ key).
            // Select the action row with Down, then F executes the selected button.
            if(Gamepad.current?.buttonSouth.wasPressedThisFrame==true||liveRenameFocus>0&&LiveSubmitPressed)
            {CloseLiveRename(liveRenameFocus!=2);return;}
            int direction=TakeLiveNavigation();
            if(direction==2)liveRenameFocus=1;
            else if(direction==-2)liveRenameFocus=0;
            else if(liveRenameFocus>0&&Math.Abs(direction)==1)liveRenameFocus=direction<0?1:2;
            if(direction!=0)
            {
                if(liveRenameFocus==0){liveRenameInput.Select();liveRenameInput.ActivateInputField();}
                else EventSystem.current?.SetSelectedGameObject(liveRenameFocus==1?liveRenameSave.gameObject:liveRenameCancel.gameObject);
                liveRenameSave.GetComponent<ApprovedActionVisual>().PersistentSelection=liveRenameFocus==1;
                liveRenameCancel.GetComponent<ApprovedActionVisual>().PersistentSelection=liveRenameFocus==2;
            }
        }
        void CloseLiveRename(bool save)
        {
            if(!liveRenamePanel)return;
            if(save)
            {
                try{Sketch.Rename(liveRenameId,liveRenameInput.text);}
                catch(InvalidOperationException error){liveRenameError.text=Deviant.UI.Theme.ApprovedInkText.Display(error.Message);return;}
            }
            liveStage=liveRenameReturnStage;DisposeLiveRename();nativeBag.Key=null;
            // Do not let F used to save activate an equipment action in the same frame.
            liveInputFrame=Time.frameCount;Refresh();
        }
        void DisposeLiveRename()
        {
            if(liveRenameKeyboard!=null){liveRenameKeyboard.onIMECompositionChange-=LiveRenameComposition;liveRenameKeyboard.SetIMEEnabled(false);liveRenameKeyboard=null;}
            if(liveRenamePanel){liveRenamePanel.gameObject.SetActive(false);Destroy(liveRenamePanel.gameObject);}
            liveRenamePanel=null;liveRenameInput=null;liveRenameComposing=false;
        }
    }
}
