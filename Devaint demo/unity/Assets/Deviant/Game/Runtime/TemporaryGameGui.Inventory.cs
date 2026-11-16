using Deviant.Input.Contracts;
using Deviant.Inventory;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace Deviant.Game
{
    // Combat HUD and the bag book (journal design, faithful.css compact HUD / [data-screen="bag"]).
    public sealed partial class TemporaryGameGui
    {
        Image healthFill, bossFill;
        Text healthText, healthMax, goldText, bagCount, toast, hint, bossText, moveHelp, combatHelp;
        GameObject bossHud, rightTop, keyHelp, goldChip;
        // Hidden key lines stay hidden for the rest of the session, across runs and scene loads.
        static bool KeyHelpShown = false;
        static bool KeyHelpAvailable => Application.isEditor || Debug.isDebugBuild;
        Button bagHud;
        readonly Text[] weaponHud = new Text[2], weaponNames = new Text[2];
        readonly Image[] weaponIcons = new Image[2], weaponCards = new Image[2];
        readonly RectTransform[] weaponDots = new RectTransform[2];
        readonly Image[] weaponShades = new Image[2];
        readonly Text[] weaponSeconds = new Text[2];
        readonly Image[] choiceCards = new Image[3];
        readonly Text[] choiceText = new Text[3];
        Text choiceHint, detail, detailName, detailPreview, bagMessage, bagCapacity;
        Image detailDrop;
        GameObject choices;
        GameObject toastPanel, hintPanel;
        Image maintenanceKey;
        Text maintenanceKeyLabel;
        MaintenanceRoomPresentation maintenancePromptSource;
        Button[] slots, equipment;
        readonly Button[] weapons = new Button[2];
        Button equip, unequip, exchange, discard, destroy;
        static readonly Color HudChip = new Color32(245, 237, 219, 191), HudControl = new Color32(245, 237, 219, 217), HudControlEdge = new Color32(130, 119, 97, 255),
            BarEdge = new Color32(41, 39, 31, 255), BarTrack = new Color32(200, 189, 166, 255), HealthRed = new Color32(169, 56, 48, 255);

        // faithful.css .hud-control: a 30 px soft paper square with a 1 px edge, a glyph inside.
        Button HudButton(Transform parent, float x, string name, System.Action action)
        {
            var button = Rounded(name, parent, x, 14, 30, 30, 3, 1, HudControl, HudControlEdge, true).gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None; button.onClick.AddListener(() => { action(); Refresh(); }); return button;
        }
        // HTML .ink-health: a 2 px ink border around the track and its red (or phase-coloured) fill.
        Image InkBar(Transform parent, float x, float y, float w, float h, Color fill)
        {
            Fill(parent, x, y, w, h, BarEdge); Fill(parent, x + 2, y + 2, w - 4, h - 4, BarTrack);
            return Fill(parent, x + 2, y + 2, w - 4, h - 4, fill);
        }
        // The bag control's glyph: a handle arc over a rounded body, as the HTML's svg.
        static Sprite BagGlyph => Drawn("bag", 16, 16, (x, y) =>
        {
            var p = new Vector2(x, y);
            bool body = BoxDistance(x - 1.5f, y - 5, 13, 10, 2) <= 0 && BoxDistance(x - 3.5f, y - 7, 9, 6, 1) > 0;
            bool handle = p.y <= 6 && Mathf.Abs((p - new Vector2(8, 6)).magnitude - 3.5f) <= 1;
            return body || handle ? Color.white : Color.clear;
        });

        void BuildHud()
        {
            hud = Rect("Gameplay HUD",stage,0,0,960,540).gameObject;
            BuildApprovedHud();
            // The stage key (stage-gimmicks.md) right of the health bar while it is held; gone once the chest takes it.
            var keySprite = ForestCombatV2.Chapter1KeyChest.LoadSprite("key-item", new Vector2(.5f,.5f));
            if (keySprite)
            {
                keyHud = Rect("Key held",hud.transform,0,0,24,24).gameObject.AddComponent<Image>();
                keyHud.rectTransform.pivot = new Vector2(.5f,.5f); keyHud.rectTransform.anchoredPosition = new Vector2(306+12,-(72+12));
                keyHud.sprite = keySprite; keyHud.preserveAspect = true; keyHud.raycastTarget = false; keyHud.gameObject.SetActive(false);
            }
            keyHelp = Card(hud.transform,16,98,340,72).gameObject; keyHelp.name = "Key help"; keyHelp.SetActive(KeyHelpShown && KeyHelpAvailable);
            moveHelp = BindPrompt(Words("",keyHelp.transform,12,6,316,20,12,false), "{Jump} 점프  {Dash} 대시  {Move.Up} 벽타기  {Map} 지도");
            combatHelp = BindPrompt(Words("",keyHelp.transform,12,26,316,20,12,false), "{Weapon1Basic}/{Weapon2Basic} 기본1/2  {Weapon1Special}/{Weapon2Special} 스킬1/2  {Interact} 상호작용");
            BindPrompt(Words("",keyHelp.transform,12,46,316,20,12,false), "{SwapWeaponOrder} 무기 1, 2 순서 바꾸기");
            moveHelp.resizeTextForBestFit = combatHelp.resizeTextForBestFit = true;
            moveHelp.resizeTextMinSize = combatHelp.resizeTextMinSize = 9;
            moveHelp.resizeTextMaxSize = combatHelp.resizeTextMaxSize = 12;
            // Pickups use only text at the top centre, without a card or background.
            toastPanel = Rect("Pickup notification",hud.transform,180,18,600,36).gameObject;
            toast = Words("",toastPanel.transform,0,0,600,36,18,true,JInk,TextAnchor.MiddleCenter);
            toast.raycastTarget=false;
            var toastShadow=toast.gameObject.AddComponent<Shadow>();toastShadow.effectColor=JPaperText;toastShadow.effectDistance=new Vector2(1,-1);
            maintenanceKey=WorldInteractionKey(hud.transform,0,0,KeyLabel(GameAction.Interact),out maintenanceKeyLabel);
            maintenanceKey.name="Merchant interact key";
            maintenanceKey.gameObject.SetActive(false);
            BuildWorldShop();
            hintPanel = Card(hud.transform,280,66,400,40).gameObject;
            hint = Words("",hintPanel.transform,0,0,400,40,14,true,JInk,TextAnchor.MiddleCenter);
            bossHud = Rect("Boss",hud.transform,290,18,380,41).gameObject;
            bossText = Words("",bossHud.transform,0,0,380,21,16,true,JInk,TextAnchor.MiddleCenter);
            bossFill = InkBar(bossHud.transform,0,28,380,13,HealthRed);
            BuildBuffSlots();
            choices = Rect("Starting blueprints",hud.transform,0,0,960,540).gameObject;
            Card(choices.transform,300,85,360,56);
            choiceHint = Words("",choices.transform,300,85,360,56,14,true,JInk,TextAnchor.MiddleCenter);
            for(int i=0;i<3;i++)
            {
                choiceCards[i] = Card(choices.transform,0,0,116,66);
                Face(BlueprintArt[i],choiceCards[i].transform,6,19,28,28);
                choiceText[i] = Words("",choiceCards[i].transform,38,5,74,56,12,true);
            }
        }
        void RefreshHud()
        {
            RefreshEnemyHealth();
            var player = host.Arena.Player;
            RefreshPromptLabels();
            RefreshApprovedHud();
            RefreshKeyHud();
            toast.text = Presenter.ToastVisible ? Presenter.ToastText : "";
            toastPanel.SetActive(Presenter.ToastVisible);
            // The clock encounter's own room test (its horizontal span, as its fight uses) decides first; the run's
            // Chapter1BossEncounter rectangle is the fallback. The bar stays hidden until the boss has dropped in.
            bool inClockRoom = clockRoom && clockRoom.Boss && clockRoom.Boss.PresentationVisible &&
                player.Position.x >= clockRoom.Room.x && player.Position.x <= clockRoom.Room.x + clockRoom.Room.w;
            var bossActor = chapter2&&chapter2.CurrentRoom=="B"&&chapter2.Boss.Started ? chapter2.Boss.HudActor : inClockRoom ? clockRoom.Boss : boss ? boss.Boss : null;
            bool inBossRoom = chapter2 ? chapter2.CurrentRoom=="B" : inClockRoom || boss && boss.PlayerInBossRoom;
            // The Rose/Blue cutscenes (entrance, phase 1 to 2) show no boss bar; its bubbles and bars come from RefreshBossCutscene.
            bool hasBoss = chapter2 ? chapter2.Boss && chapter2.Boss.Started && inBossRoom && !chapter2.Boss.Dead && !chapter2.Boss.InBossCutscene : bossActor && inBossRoom && !bossActor.Dead;
            RefreshBossCutscene();
            bossHud.SetActive(hasBoss); rightTop.SetActive(!hasBoss);
            RefreshBuffSlots(player, bossActor, hasBoss);
            float bossShift = hasBoss ? BossHudBottomShift : 0;
            Place(toastPanel.transform as RectTransform,180,hasBoss?66+bossShift:18,600,36);
            RefreshMaintenanceKey();
            RefreshWorldShop();
            if (clockRoom) clockRoom.ExternalBossBar = true;
            if(hasBoss)
            {
                // Phase one red, phase two purple (clock-executioner-sprites.md).
                bossText.text = chapter2 ? chapter2.Boss.HudText : boss ? boss.DisplayName : "시계탑 집행자";
                bossFill.color = bossActor.bossBarColor;
                bossFill.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,376*Mathf.Clamp01(chapter2 ? chapter2.Boss.HudFraction : bossActor.health/Mathf.Max(1,bossActor.maxHealth)));
            }
            hint.text = Altar && Altar.Available && !Altar.Finished && Altar.PlayerNear ? "잉크 제단 / {Interact} 색을 골라 잉크 1개 얻기" :
                "";
            if(chapter2)hint.text=Altar&&Altar.Available&&!Altar.Finished&&Altar.PlayerNear?"잉크 제단 / {Interact} 색을 골라 잉크 1개 얻기":chapter2.CurrentRoom=="B"&&chapter2.Boss.Dead?"출구로 이동 / {Interact} 2챕터 클리어":"";
            hint.text = Prompt(hint.text);
            hintPanel.SetActive(hint.text.Length>0);
            if (hint.text.Length > 0) { float width = Width(hint)+40; Place(hintPanel.transform as RectTransform,Mathf.Round(480-width/2),hasBoss&&Presenter.ToastVisible?106+bossShift:hasBoss?66+bossShift:58,width,40); hint.rectTransform.sizeDelta = new Vector2(width,40); }
            choices.SetActive(blueprints && !blueprints.Complete);
            if(!blueprints || blueprints.Complete || !world || !world.View) return;
            choiceHint.text = $"시작 도면 선택   {blueprints.SelectedCount} / 2\n{blueprints.ExitGuide}";
            for(int i=0;i<3;i++)
            {
                bool offered = blueprints.IsOffered(i); var point = Deviant.Presentation.Unity.CameraDisplayProjection.WorldToScreenPoint(world.View,blueprints.OfferPosition(i)+Vector2.up*2.5f);
                bool visible = offered && point.z>0 && point.x>=0 && point.x<=Screen.width && point.y>=0 && point.y<=Screen.height;
                choiceCards[i].gameObject.SetActive(visible); if(!visible) continue;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(stage,point,null,out var local);
                choiceCards[i].rectTransform.anchoredPosition = new Vector2(Mathf.Clamp(local.x+480-58,8,836),Mathf.Clamp(local.y-270+80,-350,-145));
                bool near = blueprints.NearestAvailableOffer==i;
                SetCard(choiceCards[i],near);
                choiceText[i].text = BlueprintNames[i]+"\n"+(near?KeyLabel(GameAction.Interact)+" 선택":"가까이 이동");
            }
        }
        void RefreshMaintenanceKey()
        {
            if(!maintenancePromptSource&&shop)maintenancePromptSource=shop.GetComponent<MaintenanceRoomPresentation>();
            Vector3 head=default;
            bool show=maintenancePromptSource&&maintenancePromptSource.TryGetInteractionAnchor(out head);
            maintenanceKey.gameObject.SetActive(show);
            if(!show)return;
            var camera=chapter2?chapter2.sceneCamera:world?world.View:null;
            if(!camera){maintenanceKey.gameObject.SetActive(false);return;}
            var point=camera.WorldToViewportPoint(head);
            maintenanceKey.gameObject.SetActive(point.z>0&&point.x>0&&point.x<1&&point.y>0&&point.y<1);
            SetWorldInteractionKey(maintenanceKey,maintenanceKeyLabel,KeyLabel(GameAction.Interact));
            float keyWidth=maintenanceKey.rectTransform.sizeDelta.x;
            Place(maintenanceKey,Mathf.Round(point.x*960-keyWidth/2),Mathf.Round((1-point.y)*540-15),keyWidth,30);
        }
        // Shown while keyChest.HasKey; it pops in (small, past full size, settles) when the key is picked up.
        public const float KeyHudPopSeconds = .25f;
        Image keyHud;
        float keyHudAt = -1;
        public bool KeyHudShown => keyHud && keyHud.gameObject.activeSelf;
        void RefreshKeyHud()
        {
            if (!keyHud) return;
            bool holding = keyChest && keyChest.HasKey;
            if (!holding) keyHudAt = -1; else if (keyHudAt < 0) keyHudAt = Time.unscaledTime;
            keyHud.gameObject.SetActive(holding);
            float pop = holding ? Mathf.Clamp01((Time.unscaledTime - keyHudAt) / KeyHudPopSeconds) : 1;
            float scale = pop < .6f ? Mathf.Lerp(.4f, 1.2f, pop / .6f) : Mathf.Lerp(1.2f, 1f, (pop - .6f) / .4f);
            keyHud.rectTransform.localScale = new Vector3(scale, scale, 1);
        }
        // Whole seconds rounded up, tenths under one second, nothing once ready.
        static string CooldownSeconds(float left) => left <= 0 ? "" : left >= 1 ? Mathf.CeilToInt(left).ToString() : (Mathf.Ceil(left*10)/10).ToString("0.0",System.Globalization.CultureInfo.InvariantCulture);
        // The cooldown veil: a 78x58 rounded face inset 2 px into the 82x62 card, drawn at its own size so the radial
        // fill never stretches its corners.
        static Sprite CardVeil => Drawn("card veil", 78, 58, (x, y) => BoxDistance(x, y, 78, 58, 4) <= 0 ? Color.white : Color.clear);
        string WeaponIcon(int w)
        {
            var form = host.Arena.catalog.forms[host.Arena.Player.weapons[w].FormIndex].id;
            return form.Contains("hammer")?"hammer":form.Contains("launcher")?"launcher":"sword";
        }

        // Bag book: two weapon cards (form, five stat slots, special) on the left; the shared bag slots, the
        // selected ink and its actions on the right. Book x 140, y 62, 800x448; content from (28, 44).
        void BuildBag() { if(host.Growth!=null||host.Arena.SketchEquipment)BuildNativeBook(false);else BuildBagKit(); }
        Button MakeSlot(Transform parent,float x,float y,float w,float h,System.Action action)
        {
            var button=SlotButton("",parent,x,y,w,h,action);
            var item=Rect("Item",button.transform,0,0,w,h).gameObject.AddComponent<Image>();item.raycastTarget=false;item.preserveAspect=true;
            return button;
        }
        // Approved stamp items share the same art in the bag, equipped slot and detail view.
        void PaintSlot(Button button,InkInstance item,bool selected,bool inactive,bool formSlot=false,string emptyWeapon=null)
        {
            SetSlotLevel(button.transform, null);
            SetBagSlot(button,selected);
            var rect=(RectTransform)button.transform; var icon=button.transform.Find("Item").GetComponent<Image>(); icon.enabled=item!=null;
            SetInkGrade(icon, null);
            button.image.color=item==null&&!formSlot?new Color(1,1,1,.6f):Color.white;
            // An empty form slot shows the weapon the blueprint gave, as in the HTML.
            if(item==null&&formSlot&&emptyWeapon!=null){icon.enabled=true;icon.sprite=Art(emptyWeapon);icon.color=Color.white;Place(icon,8,Mathf.Round((rect.sizeDelta.y-40)/2),40,40);}
            if(item==null)return;
            string id=item.Definition.Id;
            string weapon=formSlot?(id.Contains("hammer")?"hammer":id.Contains("launcher")?"launcher":id.Contains("sword")?"sword":null):null;
            var drop=InkDrop(id);
            if (LegacyStampIcon(id)) weapon=null;
            var art=host.Content.Find(id);
            icon.sprite=weapon!=null?Art(weapon):drop?drop:art!=null&&art.icon?art.icon:Sprite(item.Definition.Kind==InkKind.Stat?"icon-ink":"icon-stamp");
            float w=rect.sizeDelta.x,h=rect.sizeDelta.y,iw=weapon!=null?Mathf.Min(40,w-8):Mathf.Min(40,w-4),ih=weapon!=null?iw:Mathf.Min(56,h-8)*(w<40?.8f:1);
            Place(icon,w>=150?8:Mathf.Round((w-iw)/2),Mathf.Round((h-ih)/2),iw,ih);
            icon.color=inactive?new Color(.55f,.55f,.55f,1):drop||weapon!=null?Color.white:art!=null?art.tint:Color.white;
        }
        static Sprite InkDrop(string id)=>LegacyStampIcon(id) ?? (id switch
        {
            "stat.basic"=>Art("parts/ink2_red_04"),"stat.special"=>Art("parts/ink2_red_10"),
            "stat.speed"=>Art("parts/ink2_yellow_04"),"stat.cooldown"=>Art("parts/ink2_blue_08"),
            "form.base_sword"=>Art("parts/ink2_red_01"),"form.greatsword"=>Art("parts/ink2_red_04"),
            "form.whip"=>Art("parts/ink2_red_01"),"form.dagger"=>Art("parts/ink2_red_01"),
            "form.shuriken"=>Art("parts/ink2_blue_04"),"form.bomb"=>Art("parts/ink2_purple_04"),
            "form.rapier"=>Art("parts/ink2_blue_04"),"form.shield"=>Art("parts/ink2_green_04"),
            "special.range"=>Art("parts/ink2_blue_04"),"special.move"=>Art("parts/ink2_yellow_01"),
            "special.follow"=>Art("parts/ink2_purple_04"),_=>null
        });
        void RefreshBag()
        {
            if(nativeBag!=null){RefreshNativeBook(nativeBag);return;}
            if(slots.Length!=host.Inventory.Capacity)BuildBagCells();
            RefreshInkGuideBag();
            if(!Presenter.IsOpen)return;
            RebuildBagEntries();
            bagCapacity.text=$"{host.TotalStored} / {host.Inventory.Capacity}";
            for(int i=0;i<slots.Length;i++)
            {
                var entry=i<bagEntries.Count?bagEntries[i]:null;
                if(entry?.Color!=null)PaintBagColor(slots[i],entry.Color.Part,null,null,entry.Color.Id==bagColorSelected);
                else {int legacy=entry?.Legacy??-1;PaintSlot(slots[i],legacy<0?null:host.Inventory.BagAt(legacy),legacy>=0&&Presenter.SelectedBag==legacy,false);}
                slots[i].interactable=entry!=null;slots[i].transform.Find("Empty marker").GetComponent<Text>().text=entry==null?"+":"";
            }
            for(int w=0;w<2;w++)
            {
                weaponNames[w].text=$"무기 {w+1} {host.WeaponName(w)}";SetBagTab(weapons[w],Presenter.SelectedWeapon==w,true);bagEquipmentPages[w].SetActive(Presenter.SelectedWeapon==w);
                var palette=host.Growth?.Equipped(host.Arena.Player.weapons[w].InstanceId);
                bagPaletteButtons[w].interactable=palette!=null;
                bagPaletteCounts[w].text=palette!=null?$"{palette.Slots.Count}칸 / 정보 보기":"";
                for(int slot=0;slot<7;slot++)
                {
                    var button=equipment[w*7+slot];
                    bool paletteCell=host.Growth!=null&&slot>=1&&slot<=5;
                    bool exists=!paletteCell||palette!=null&&slot<=palette.Slots.Count;
                    button.gameObject.SetActive(exists);if(!exists)continue;
                    button.interactable=true;
                    if(paletteCell)
                    {
                        var mounted=palette.Slots[slot-1];
                        Place(button,32+(5-palette.Slots.Count)*36+(slot-1)*72,252,60,64);
                        PaintBagColor(button,mounted?.Part,mounted?.Color,mounted?.Level,Presenter.SelectedWeapon==w&&bagPaletteSlot==slot-1);
                        button.interactable = mounted != null;
                        var label = bagEquipmentLabels[w * 7 + slot];
                        bool wildcard = mounted?.Color == "colorless";
                        Place(label, wildcard ? 4 : 0, wildcard ? 3 : 0, wildcard ? 26 : 60, wildcard ? 14 : 46);
                        label.text = mounted == null ? "잠김" : wildcard ? "무색" : "";
                    }
                    else {var item=host.Inventory.EquippedAt(w,slot);PaintSlot(button,item,Presenter.SelectedWeapon==w&&Presenter.SelectedEquipmentSlot==slot,host.Inactive(item,w),slot==0,slot==0&&host.Arena.Player.IsWeaponSlotReady(w)?WeaponIcon(w):null);bagEquipmentLabels[w*7+slot].text=item?.Definition.Name??(slot==0&&host.Arena.Player.IsWeaponSlotReady(w)?host.WeaponName(w):"미장착");
                        if(slot==6)PaintBagSkillSlot(button,bagEquipmentLabels[w*7+slot],w,item==null);}
                }
            }
            var picked=Presenter.Selected;var color=SelectedBagColor;
            var selectedPalette=host.Growth?.Equipped(host.Arena.Player.weapons[Presenter.SelectedWeapon].InstanceId);
            if(selectedPalette==null||bagPaletteSlot>=selectedPalette.Slots.Count)bagPaletteSlot=-1;
            var mountedSlot=bagPaletteSlot>=0&&selectedPalette!=null&&bagPaletteSlot<selectedPalette.Slots.Count?selectedPalette.Slots[bagPaletteSlot]:null;
            // A mounted ink pressed last is described even while a bag ink stays picked for 교체.
            bool showMounted=mountedSlot?.Part!=null&&(color==null||bagDetailMounted);
            var colorPart=showMounted?mountedSlot.Part:color?.Part??mountedSlot?.Part;
            bool colorView=color!=null||bagPaletteSlot>=0;
            HideBagSkill();
            if(bagInspectPalette&&selectedPalette!=null)
            {
                ShowPaletteInfo(detailName,detail,detailDrop,selectedPalette,Presenter.SelectedWeapon);
                detailPreview.text="";bagMessage.text="";UpdateBagDetailLayout();
                foreach(var action in new[]{equip,unequip,discard,exchange,destroy})action.gameObject.SetActive(false);
                return;
            }
            if(colorView)
            {
                detailName.text=colorPart?.name??"빈 팔레트 칸";detail.text=Deviant.UI.Theme.ApprovedInkText.Display(colorPart?.description??"보유 색상 잉크를 고른 뒤 장착을 누르세요.");
                PaintInkDetailGrade(detailName,colorPart);
                detailDrop.sprite=DropArt(colorPart,mountedSlot?.Color);detailDrop.enabled=detailDrop.sprite;detailDrop.color=Color.white;detailPreview.text="";
            }
            else if(picked!=null||!ShowBagSkill(Presenter.SelectedWeapon))
            {
                detailName.text=picked?.Definition.Name??"";
                PaintInkDetailGrade(detailName,null);
                string preview=picked==null?"":host.Inactive(picked,Presenter.SelectedWeapon)?"비활성":host.SelectedPreview();
                detail.text=Deviant.UI.Theme.ApprovedInkText.Display(picked==null?"":picked.Definition.Description+(string.IsNullOrEmpty(preview)?"":"\n"+preview));
                var drop=picked==null?null:InkDrop(picked.Definition.Id);var art=picked==null?null:host.Content.Find(picked.Definition.Id);
                detailDrop.sprite=drop?drop:art!=null&&art.icon?art.icon:host.FallbackIcon;detailDrop.enabled=picked!=null&&detailDrop.sprite;detailDrop.color=drop||art==null?Color.white:art.tint;
                detailPreview.text="";
            }
            UpdateBagDetailLayout();
            // Colour inks are equipped, replaced and destroyed here at any time; the legacy bag keeps the facility rule.
            bool editable=colorView?!host.Arena.Player.Dead:host.Arena.Player.EditingAllowed;
            equip.gameObject.SetActive(colorView?color!=null:Presenter.SelectedEquipmentSlot<0);
            // A mounted palette slot picked with no bag ink shows 해제 in the equip button's place.
            bool mountedPick=colorView&&color==null&&mountedSlot?.Part!=null;
            unequip.gameObject.SetActive(mountedPick);unequip.interactable=mountedPick&&editable&&host.Growth.HasInkRoom;Paint(unequip,true);
            equip.interactable=editable&&(colorView?CanEquipBagColor():picked!=null&&Presenter.SelectedBag>=0);Paint(equip,true);
            equip.GetComponentInChildren<Text>().text=colorView&&BagColorTarget()?.Part!=null?"교체":"장착";
            bool bagPick=!colorView&&picked!=null&&Presenter.SelectedBag>=0;
            exchange.gameObject.SetActive(!colorView&&host.PendingExchange);exchange.interactable=bagPick;Paint(exchange,false);
            discard.gameObject.SetActive(!colorView&&!host.PendingExchange);discard.interactable=bagPick;Paint(discard,false);
            int bonus=host.Growth?.DestroyedInkGoldBonus??0;
            destroy.gameObject.SetActive(!colorView||color!=null);destroy.interactable=bagPick||color!=null&&editable;Paint(destroy,false);
            destroy.GetComponentInChildren<Text>().text=(colorView?bagColorDestroyArmed:Presenter.DestroyArmed)?"한 번 더: 파괴":colorView&&bonus>0?$"파괴 +{bonus} G":"파괴";
            // The run-long lock belongs to the legacy (form·stat·special) inks only: its line shows only while one of them is
            // picked. Colour inks are equipped, swapped and unequipped freely (weapons-and-ink.md 정비·재고·교환).
            bagMessage.text=colorView&&color!=null&&SuggestedBagColorSlot()<0?PickSlotFirst:colorView&&mountedPick&&!host.Growth.HasInkRoom?"가방이 가득 차 해제할 수 없습니다. 잉크를 비운 뒤 해제하세요.":
                !colorView&&picked!=null?LegacyInkRule:host.Growth!=null?ColorInkRule:"";
            if(!string.IsNullOrEmpty(Presenter.Message)&&Presenter.Message!="잉크를 선택하고 장착할 슬롯을 누르세요."&&!Presenter.Message.EndsWith(" 획득",StringComparison.Ordinal)
                &&!(colorView&&LegacyMessage(Presenter.Message)))bagMessage.text=Deviant.UI.Theme.ApprovedInkText.Display(Presenter.Message);
            if(!colorView&&picked!=null&&host.Inactive(picked,Presenter.SelectedWeapon))bagMessage.text=host.InactiveReason(picked,Presenter.SelectedWeapon);
        }

        public const string ColorInkRule = "색상 잉크 장착, 교체, 해제는 언제든 무료 / 판매, 강화, 색 변경은 정비소에서";
        public const string LegacyInkRule = "형태, 능력치, 특수 잉크는 정비소에서만 장착하며, 장착하면 런이 끝날 때까지 빠지지 않습니다.";
        // Replies of the legacy bag (its run-long lock) are not shown over a colour ink.
        static bool LegacyMessage(string message) => message.Contains(InkInventory.LockedMessage) || message.Contains("런이 끝날 때까지 빠지지 않습니다");

        // The destruction effect plays over the destroyed ink's slot only after the second press really destroyed it.
        void DestroySelectedInk()
        {
            if (bagColorSelected != Guid.Empty) { DestroyBagColor(); return; }
            int visibleIndex = bagEntries.FindIndex(entry => entry.Legacy == Presenter.SelectedBag);
            var at = visibleIndex >= 0 && visibleIndex < slots.Length ? (RectTransform)slots[visibleIndex].transform : null;
            if (Presenter.DestroySelected()) { PlayUiEffect("TemporaryGUI/Effects/ink-destruction", at); ForestCombatV2.PresentationSfx.Play(ForestCombatV2.PresentationSfx.InkDestroy); }
        }
    }
}
