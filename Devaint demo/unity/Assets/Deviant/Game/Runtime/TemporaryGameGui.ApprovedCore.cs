using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Deviant.Input.Contracts;
using Deviant.Run;
using Deviant.UI.Theme;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Deviant.Game
{
    // Approved 1920 design coordinates at half scale on the game's existing 960 safe canvas.
    // Views address the real SketchEquipment IDs; no preview inventory or grant path is used here.
    public sealed partial class TemporaryGameGui
    {
        static readonly Color CorePaper=new Color32(243,239,229,255),CoreMuted=new Color32(174,182,192,255),CoreBlue=new Color32(166,207,220,255),CoreGold=new Color32(217,203,132,255);
        int liveTab,liveFilter,livePick,liveSlot,liveTarget,liveAction;
        string liveStage="list";
        Guid liveWeapon;bool liveReceiptConfiguration;
        bool inventoryTargetLocked, inventoryMountFromBag;
        string liveInk;
        RectTransform liveDetail;
        Image liveCoreBackdrop;
        readonly List<Button> liveActions=new List<Button>();
        float liveNextNavigation;int liveDirection;int liveInputFrame=-1;
        int liveNavigation = -1;

        void ActivateKeyboardNavigation(int selected)
        {
            SetLiveTab(selected == 2 ? 2 : 0);
        }

        void CraftSelectedBagItem()
        {
            if(liveFilter==1)
            {
                var draft=Sketch.Weapons.FirstOrDefault(w=>!w.Destroyed&&!w.Confirmed);
                if(draft==null){nativeBag.Message="제작할 스케치를 먼저 획득하세요.";return;}
                liveFilter=0;livePick=Array.FindIndex(LiveWeapons(),w=>w.Id==draft.Id);
                approvedBagCategory=false;return;
            }
            var selected=LiveWeapons().ElementAtOrDefault(livePick);if(selected==null)return;
            liveWeapon=selected.Id;sketchSelectedWeapon=selected.Id;
            if(selected.Confirmed)
            {
                SetLiveTab(2);liveWeapon=selected.Id;liveStage="mountTarget";
                inventoryMountFromBag=true;liveTarget=Sketch.Equipped(0)==null?0:Sketch.Equipped(1)==null?1:0;
            }
            else {liveTab=1;liveStage="palette";liveSlot=liveAction=0;approvedBagCategory=false;inventoryTargetLocked=inventoryMountFromBag=false;}
        }

        bool TickKeyboardNavigation()
        {
            if(liveStage=="review"||liveStage=="done"||liveStage=="craftTarget")return false;
            if(liveNavigation<0)
            {
                bool top=liveTab==0?approvedBagCategory:liveTab==2?liveStage=="target":liveStage=="palette"&&liveAction==0;
                if(top&&LiveNavigationDirection()==-2&&TakeLiveNavigation()!=0){liveNavigation=liveTab==2?2:0;return true;}
                return false;
            }
            if(LiveBackPressed){liveNavigation=-1;return true;}
            int move=TakeLiveNavigation();
            if(Math.Abs(move)==1)
            {
                int next=liveNavigation==0?2:0;SetLiveTab(next);liveNavigation=next;
            }
            else if(move==2||LiveSubmitPressed)liveNavigation=-1;
            return true;
        }

        void DrawKeyboardNavigation(EquipmentBook b)
        {
            // The Figma tabs and footer own navigation guidance. Show only real errors/receipts here.
            if (!string.IsNullOrEmpty(b.Message))
                LiveWords(ApprovedInkText.Display(b.Message), b.Body, 65, 89, 830, 15, 10, CoreGold);
        }

        Text LiveWords(string text,Transform parent,float x,float y,float w,float h,int size=15,Color? color=null,TextAnchor align=TextAnchor.UpperLeft)
        {
            var t=Words(text,parent,x,y,w,h,size,false,color??CorePaper,align);
            if(ApprovedTheme.Font)t.font=ApprovedTheme.Font;
            t.material=null;t.horizontalOverflow=HorizontalWrapMode.Wrap;t.verticalOverflow=VerticalWrapMode.Overflow;return t;
        }
        RawImage LiveArt(Texture texture,Transform parent,float x,float y,float w,float h,float alpha=1)
        {
            var r=Rect("Approved art",parent,x,y,w,h);var im=r.gameObject.AddComponent<RawImage>();im.raycastTarget=false;im.color=new Color(1,1,1,alpha);
            if(texture){im.texture=texture;float ratio=Mathf.Min(w/texture.width,h/texture.height);r.sizeDelta=new Vector2(texture.width*ratio,texture.height*ratio);r.anchoredPosition+=new Vector2((w-r.sizeDelta.x)/2,-(h-r.sizeDelta.y)/2);}else im.color=Color.clear;return im;
        }
        Button LiveButton(string name,string text,Transform parent,float x,float y,float w,float h,Action action,bool selected=false,bool enabled=true,int size=16)
        {
            var r=Rect(name,parent,x,y,w,h);var im=r.gameObject.AddComponent<Image>();im.color=Color.clear;var button=r.gameObject.AddComponent<Button>();button.targetGraphic=im;button.transition=Selectable.Transition.None;button.interactable=enabled;button.navigation=new Navigation{mode=Navigation.Mode.None};
            int separator=text.IndexOf("  ",StringComparison.Ordinal);
            bool keyPrefix=separator>0;
            string key=keyPrefix?InventoryKey(text.Substring(0,separator)):"";
            float keyWidth=Mathf.Max(27,key.Length*8+10), labelLeft=keyPrefix?keyWidth+9:0;
            LiveWords(keyPrefix?text.Substring(separator+2):text,r,labelLeft,0,Mathf.Max(0,w-labelLeft),h,size,enabled?selected?CoreGold:CoreMuted:new Color(CoreMuted.r,CoreMuted.g,CoreMuted.b,.35f),TextAnchor.MiddleLeft);
            if(keyPrefix)
            {
                var cap=ApprovedKeycap(r,key,0,(h-26)/2,keyWidth,26,14);
                if(enabled&&selected){cap.color=CoreGold;cap.GetComponentInChildren<Text>().color=CoreGold;}
                if(!enabled){cap.color=new Color(1,1,1,.35f);cap.GetComponentInChildren<UnityEngine.UI.Text>().color=new Color(CoreMuted.r,CoreMuted.g,CoreMuted.b,.35f);}
            }
            var visual=r.gameObject.AddComponent<ApprovedActionVisual>();visual.PersistentSelection=selected;
            if(nativeBag!=null&&r.IsChildOf(nativeBag.Body)){visual.DrawWash=false;visual.SelectedColor=CoreGold;}
            button.onClick.AddListener(()=>{if(!button.interactable)return;liveNavigation=-1;action();if(nativeBag!=null)nativeBag.Key=null;if(nativeShop!=null)nativeShop.Key=null;Refresh();});return button;
        }
        Button LiveItemButton(string name,string text,Texture art,Transform parent,float x,float y,float width,float height,float iconWidth,float iconHeight,float labelTop,Action action,bool selected,int fontSize)
        {
            // One hit rectangle owns both the art and its centered caption, including their gap.
            var button=LiveButton(name,"",parent,x,y,width,height,action,selected,size:fontSize);
            var label=button.GetComponentInChildren<Text>();label.text=ApprovedInkText.Display(text);label.alignment=TextAnchor.UpperCenter;
            label.rectTransform.anchoredPosition=new Vector2(0,-labelTop);
            label.rectTransform.sizeDelta=new Vector2(width,height-labelTop);
            LiveArt(art,button.transform,(width-iconWidth)/2,0,iconWidth,iconHeight,selected?1:.6f);
            return button;
        }
        void RefreshLiveTab(Button button,bool selected)
        {
            var visual=button.GetComponent<ApprovedActionVisual>();
            if(visual){visual.PersistentSelection=selected;return;}
            var text=button.GetComponentInChildren<Text>();if(text)text.color=selected?CoreGold:CoreMuted;
        }
        void LiveRule(Transform p,float x,float y,float w)=>Fill(p,x,y,w,.6f,new Color(.48f,.52f,.55f,.3f));
        void BuildApprovedSketchBook()
        {
            var b=new EquipmentBook();b.Root=Rect("Approved bag crafting equipment",MenuStage,0,0,960,540).gameObject;
            liveCoreBackdrop=RegisterScreenBackdrop(Fill(b.Root.transform,0,0,960,540,ApprovedTheme.MenuBackdrop));liveCoreBackdrop.raycastTarget=true;b.Body=Rect("Approved core content",b.Root.transform,0,0,960,540);
            b.Tabs=Array.Empty<Button>();b.Gold=LiveWords("",b.Root.transform,780,32,110,22,12,CoreMuted,TextAnchor.MiddleRight);
            nativeBag=b;bag=b.Root;
            var close=LiveButton("Close approved bag","",b.Root.transform,0,0,1,1,()=>Presenter.Close());close.gameObject.SetActive(false);RegisterScreen(b.Root,close);
        }
        RunInkWeapon[] LiveWeapons()=>Sketch.Weapons.Where(w=>!w.Destroyed&&(liveTab==1?!w.Confirmed:liveTab==2?w.Confirmed&&Sketch.Equipped(0)!=w&&Sketch.Equipped(1)!=w:true)).ToArray();
        RunInkDefinition[] LiveInks()=>Sketch.Catalog.inks.Where(i=>Sketch.State.Arsenal.Registered(i.id)).ToArray();
        RunInkWeapon LiveWeapon()=>Sketch.Find(liveWeapon);
        string LiveWeaponName(RunInkWeapon w)=>w.CustomName??host.Arena.catalog.forms.FirstOrDefault(f=>f.id==w.Form)?.name??w.Form;
        string LiveSkill(RunInkWeapon w)=>host.Arena.catalog.forms.FirstOrDefault(f=>f.id==w.Form)?.specials.ElementAtOrDefault(w.Skill)?.name??"";
        string LiveSkillDescription(RunInkWeapon w)=>host.Arena.catalog.forms.FirstOrDefault(f=>f.id==w.Form)?.specials.ElementAtOrDefault(w.Skill)?.summary??"";
        Texture LiveWeaponArt(RunInkWeapon w)=>ApprovedTheme.Texture("ui-assets-v2/"+w.Form)??Resources.Load<Texture2D>("SketchRewardArt/Weapons/"+w.Form);
        void SetLiveTab(int tab){liveNavigation=-1;if(nativeBag!=null)nativeBag.Message="";liveTab=(tab+3)%3;liveStage=liveTab==2?"target":"list";livePick=liveSlot=liveAction=0;liveDetail=null;liveEquipmentScroll=0;approvedBagCategory=liveTab==0;inventoryTargetLocked=inventoryMountFromBag=false;}
        void SelectLiveReceipt(Guid weapon,string ink)
        {
            liveReceiptConfiguration=true;
            if(weapon!=Guid.Empty){sketchSelectedWeapon=liveWeapon=weapon;SetLiveTab(1);var w=Sketch?.Find(weapon);if(w!=null&&!w.Confirmed)liveStage="palette";}
            if(ink!=null)
            {
                sketchSelectedInk=liveInk=ink;
                var draft=Sketch.Weapons.FirstOrDefault(w=>!w.Destroyed&&!w.Confirmed);
                if(draft!=null)
                {
                    SetLiveTab(1);sketchSelectedWeapon=liveWeapon=draft.Id;liveStage="palette";
                    liveSlot=Math.Max(0,Array.FindIndex(draft.Slots.ToArray(),slot=>slot.Ink==null));
                }
                else {SetLiveTab(0);liveFilter=1;}
            }
            if(nativeBag!=null)nativeBag.Key=null;
        }
        void RefreshApprovedSketchBook(EquipmentBook b)
        {
            if(!Presenter.IsOpen){DisposeLiveRename();b.Key=null;return;}
            var model=Sketch;if(model==null)return;
            var key=$"approved/{InventoryPromptSignature}/{model.Revision}/{liveTab}/{liveFilter}/{livePick}/{liveSlot}/{liveTarget}/{liveStage}/{liveAction}/{liveWeapon}/{liveInk}/{approvedBagCategory}/{liveNavigation}/{inventoryTargetLocked}/{inventoryMountFromBag}/{Mathf.CeilToInt(model.ChangeCooldownRemaining)}/{b.Message}";
            if(b.Key==key)return;
            if(liveTab==2&&liveDetail)liveEquipmentScroll=liveDetail.anchoredPosition.y;
            SaveApprovedScrollPositions();
            b.Key=key;ClearGrowth(b.Body);liveActions.Clear();liveDetail=null;
            if(liveTab==2)DrawApprovedEquipment(b);else DrawApprovedBagCraft(b);
            DrawKeyboardNavigation(b);
            ApplyInventoryNavigationFocus(b);
        }
        void ApplyInventoryNavigationFocus(EquipmentBook b)
        {
            if(liveNavigation<0)return;
            // Keep the top-level header and footer readable; everything between them belongs to the body.
            var children=new List<RectTransform>();
            foreach(Transform child in b.Body)
                if(child is RectTransform rect && (rect.name=="Equipment combat lock" || rect.anchoredPosition.y<=-100 && rect.anchoredPosition.y>-489))children.Add(rect);
            var body=Rect("Inventory inactive body",b.Body,0,0,960,540);
            foreach(var child in children)child.SetParent(body,false);
            var fade=body.gameObject.AddComponent<CanvasGroup>();
            fade.alpha=.3f;fade.interactable=false;fade.blocksRaycasts=false;
        }
        void AddLiveAction(EquipmentBook b,string label,float x,float y,float width,Action action,bool enabled=true)
        {int index=liveActions.Count;var button=LiveButton("Core action "+index,(liveAction==index+1?"F  ":"")+label,b.Body,x,y,width,34,action,liveAction==index+1,enabled);liveActions.Add(button);}
        float LiveParagraph(Transform p,string text,float y,int size=15,Color? color=null,float width=388)
        {
            text=ApprovedInkText.Display(text);
            var t=LiveWords(text,p,0,y,width,10,size,color);
            var unbounded=t.GetGenerationSettings(Vector2.zero);
            text=ForestCombatV2.SketchRewardChoiceScreen.WrapWords(text,width,
                value=>t.cachedTextGeneratorForLayout.GetPreferredWidth(value,unbounded)/t.pixelsPerUnit);
            t.text=text;
            // Measure against the actual wrapping width before positioning the next section.
            // The zero-height generation extent measures every line, even in a masked viewport.
            var settings=t.GetGenerationSettings(new Vector2(width,0));
            float measured=t.cachedTextGeneratorForLayout.GetPreferredHeight(text,settings)/t.pixelsPerUnit;
            float height=Mathf.Ceil(Mathf.Max(size*1.6f,measured+5));
            t.rectTransform.sizeDelta=new Vector2(width,height);return y+height;
        }
        string LiveInkText(RunInkDefinition ink,bool enhanced=false)
        {
            const string values=@"(\d+(?:\.\d+)?(?:%|초)?)\(강화 (\d+(?:\.\d+)?(?:%|초)?)\)";
            string text=Regex.Replace(ink.description??"",values,m=>m.Groups[enhanced?2:1].Value),prefix="";
            int end=text.IndexOf(" 시",StringComparison.Ordinal);
            if(end>=0){string trigger=text.Substring(0,end+2);text=text.Substring(end+2);if(trigger.StartsWith("이 무기로 ")){prefix="이 무기로 ";trigger=trigger.Substring(6);}trigger=trigger.Replace("적에게","</color>적에게<color=#ACD4E0>");prefix+="<color=#ACD4E0>"+trigger+"</color>";}
            text=Regex.Replace(text,@"(이동속도가 \d+(?:\.\d+)?% 증가|방어막을 \d+|체력을 \d+ 회복|공격 피해가 \d+(?:\.\d+)?% 증가|스킬 쿨타임 \d+(?:\.\d+)?%|스킬 피해가 \d+(?:\.\d+)?% 증가|\d+(?:\.\d+)?초 동안 (?:기절|속박)|\d+(?:\.\d+)?초|\d+(?:\.\d+)?% 감소)","<color=#B4D38D>$1</color>");
            return prefix+text;
        }
        string LiveAppliedValue(RunInkDefinition ink,bool match)
        {var values=Regex.Matches(ink.description??"",@"(\d+(?:\.\d+)?(?:%|초)?)\(강화 (\d+(?:\.\d+)?(?:%|초)?)\)").Cast<Match>().Select(m=>m.Groups[match?2:1].Value);return string.Join(" / ",values);}
        void ActivateLiveItem()
        {
            if(liveTab==0)return;
            var list=LiveWeapons();if(livePick<0||livePick>=list.Length)return;liveWeapon=list[livePick].Id;
            if(liveTab==1){liveStage="palette";liveSlot=liveInk==null?0:Math.Max(0,Array.FindIndex(list[livePick].Slots.ToArray(),slot=>slot.Ink==null));liveAction=0;}else liveAction=1;
        }
        void OpenLiveBankedReward(){Presenter.Close();if(SketchBinding)SketchBinding.TryOpenBankedReward();else Sketch.OpenReward();}
        bool CanChooseLiveInk
        {
            get
            {
                var weapon=LiveWeapon();if(weapon==null||weapon.Slots.Count==0)return false;
                var mounted=weapon.Slots[Mathf.Clamp(liveSlot,0,weapon.Slots.Count-1)].Ink;
                return LiveInks().Any(ink=>Sketch.AvailableInkUses(ink.id)>0||mounted?.id==ink.id);
            }
        }
        void OpenLiveInkPicker()
        {
            if(!CanChooseLiveInk)return;
            liveStage="ink";livePick=Math.Max(0,Array.FindIndex(LiveInks(),ink=>ink.id==liveInk));
        }
        void LiveTrade(EquipmentBook b,Action action){try{action();host.Arena.SyncSketchLoadout();b.Message="";}catch(InvalidOperationException e){b.Message=e.Message;}b.Key=null;}
        void LiveBack()
        {
            if(liveReceiptConfiguration&&(liveStage=="list"||liveStage=="palette"||liveStage=="done")){liveReceiptConfiguration=false;Presenter.Close();return;}
            if(liveStage=="ink"||liveStage=="review"){liveStage="palette";liveAction=0;}
            else if(liveStage!="list"){liveStage="list";liveAction=0;}
            else Presenter.Close();
        }
        static bool LiveBackPressed => Keyboard.current?.escapeKey.wasPressedThisFrame==true || Gamepad.current?.buttonEast.wasPressedThisFrame==true || Gamepad.current?.startButton.wasPressedThisFrame==true;
        static bool LiveSubmitPressed => Keyboard.current?.fKey.wasPressedThisFrame==true || Gamepad.current?.buttonSouth.wasPressedThisFrame==true;
        static bool LivePreviousTabPressed => Keyboard.current?.qKey.wasPressedThisFrame==true || Gamepad.current?.leftShoulder.wasPressedThisFrame==true;
        static bool LiveNextTabPressed => Keyboard.current?.eKey.wasPressedThisFrame==true || Gamepad.current?.rightShoulder.wasPressedThisFrame==true;
        int LiveNavigationDirection()
        {
            var keyboard=Keyboard.current;var pad=Gamepad.current;
            int direction=keyboard?.leftArrowKey.isPressed==true?-1:keyboard?.rightArrowKey.isPressed==true?1:keyboard?.upArrowKey.isPressed==true?-2:keyboard?.downArrowKey.isPressed==true?2:0;
            if(direction==0&&pad!=null)
            {
                var axis=pad.dpad.ReadValue();if(axis.sqrMagnitude<.25f)axis=pad.leftStick.ReadValue();
                if(Mathf.Max(Mathf.Abs(axis.x),Mathf.Abs(axis.y))>.5f)direction=Mathf.Abs(axis.x)>Mathf.Abs(axis.y)?(axis.x<0?-1:1):(axis.y>0?-2:2);
            }
            return direction;
        }
        int TakeLiveNavigation()
        {
            int direction=LiveNavigationDirection();
            bool moved=direction!=0&&(direction!=liveDirection||Time.unscaledTime>=liveNextNavigation);
            if(moved)liveNextNavigation=Time.unscaledTime+(direction!=liveDirection?.3f:.13f);
            liveDirection=direction;return moved?direction:0;
        }
        void ScrollLiveDetail()
        {
            if(!liveDetail)return;var keyboard=Keyboard.current;
            float direction=keyboard?.wKey.isPressed==true?1:keyboard?.sKey.isPressed==true?-1:Gamepad.current?.rightStick.ReadValue().y??0;
            if(Mathf.Abs(direction)<.2f)return;
            var scroll=liveDetail.parent.GetComponent<ScrollRect>();
            float range=Mathf.Max(1,liveDetail.rect.height-scroll.viewport.rect.height);
            scroll.verticalNormalizedPosition=Mathf.Clamp01(scroll.verticalNormalizedPosition+direction*230*Time.unscaledDeltaTime/range);
        }
        bool TickApprovedCoreInput()
        {
            if(Sketch==null||nativeBag==null||host.Arena.Player.Dead)return false;
            if(!Presenter.IsOpen){liveReceiptConfiguration=false;return TickApprovedShopInput();}
            if(liveInputFrame==Time.frameCount)return true;liveInputFrame=Time.frameCount;
            TickCraftTransferVisual();
            var input=Controls;
            inventoryRemovePressed=input&&input.Take(GameAction.InventoryRemove);
            inventoryRenamePressed=input&&input.Take(GameAction.InventoryRename);
            inventorySwapPressed=input&&input.Take(GameAction.SwapWeaponOrder);
            if(input){input.SuppressGameplay();input.Take(GameAction.Submit);input.Take(GameAction.Interact);input.Take(GameAction.Cancel);input.Take(GameAction.Pause);input.Take(GameAction.Equip);}
            if(liveRenamePanel){input?.Take(GameAction.Inventory);TickLiveRename();return true;}
            if(LiveNavigationDirection()!=0)nativeBag.Message="";
            bool modal=liveStage=="review"||liveStage=="done"||liveStage=="craftTarget";
            if(modal)input?.Take(GameAction.Inventory);
            if(!modal&&(Gamepad.current?.leftShoulder.wasPressedThisFrame==true||Gamepad.current?.rightShoulder.wasPressedThisFrame==true))
            {
                input?.Take(GameAction.Inventory);SetLiveTab(liveTab==2?0:2);
            }
            else if(TickKeyboardNavigation())return true;
            else if(liveTab==2)TickLiveEquipmentInput();
            else TickApprovedBagCraftInput();
            return true;
        }
    }
}
