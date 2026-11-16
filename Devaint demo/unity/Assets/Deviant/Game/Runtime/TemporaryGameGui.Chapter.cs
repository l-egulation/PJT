using System;
using System.Linq;
using Deviant.Run;
using ForestCombatV2;
using UnityEngine;
using UnityEngine.UI;

namespace Deviant.Game
{
    public sealed partial class TemporaryGameGui
    {
        // Maintenance book (journal design, faithful.css shop/growth/stamps): 696 px wide, centred, tabs 팔레트 · 성장 ·
        // 스탬프 · 잉크, then the palette management spread (TemporaryGameGui.Growth). Gold shows on a HUD chip over the veil.
        // 잉크 is the ink stall that replaced the random draw (user decision 2026-11-09).
        const float ShopW = 696, ShopX = (StageW - ShopW) / 2;
        Text shopGold, shopMessage, menuTitle, menuStats;
        GameObject shopGoldChip;
        Button[] paletteButtons, tradeButtons;
        Text[] paletteNames, tradePrices, tradeNames, tradeNotes;
        GameObject[] shopPages;
        RectTransform[] offerChips;
        string[] offerKeys;
        Button[] shopTabs;
        int shopTab;
        Button menuPrimary, menuSecondary;
        string transactionMessage = "";
        delegate bool Trade(out string error);

        void BuildShop()
        {
            if(host.Growth!=null||host.Arena.SketchEquipment){BuildNativeBook(true);return;}
            shopPanel=Book("상점",true,ShopX,ShopW,out _,out var close);
            string[] tabs={"팔레트","성장","스탬프","잉크"};
            shopPages=new GameObject[tabs.Length];shopTabs=new Button[tabs.Length];
            for(int i=0;i<tabs.Length;i++)
            {
                int tab=i;shopPages[i]=Rect(tabs[i],shopPanel.transform,0,0,ShopW,BookH).gameObject;
                shopTabs[i]=Tab(tabs[i],shopPanel.transform,36+i*157,40,153,48,20,()=>SelectShopTab(tab));
            }
            // 팔레트: one card per weapon (무기 N badge, name, portrait, departure palette drops) over its 색 변경 button, which
            // opens the colour-change spread for that weapon (user decision 2026-11-09). Full management opens from 슬롯 강화.
            paletteButtons=new Button[2];paletteNames=new Text[2];offerChips=new RectTransform[2];offerKeys=new string[2];
            for(int i=0;i<2;i++)
            {
                int weapon=i;float x=36+i*338;var card=Card(shopPages[0].transform,x,108,286,200);
                var badge=Face("primary",card.transform,12,12,84,28,14,5,true);
                Words($"무기 {i+1}",badge.transform,0,0,84,28,18,true,JPaperText,TextAnchor.MiddleCenter);
                paletteNames[i]=Words("",card.transform,112,14,170,24,18,true);
                Face("slot",card.transform,12,55,88,116,10,3).name="Portrait";
                Words("팔레트",card.transform,108,71,166,22,16,true,JInk,TextAnchor.MiddleCenter);
                offerChips[i]=Rect("팔레트",card.transform,108,99,166,72);
                paletteButtons[i]=Action("색 변경",shopPages[0].transform,x+22,324,242,52,()=>TradeAt(()=>OpenRecolor(weapon)),true);
                paletteButtons[i].GetComponentInChildren<Text>().fontSize=22;
            }
            // 성장: two service cards (icon, name and note, coin price, action).
            tradeButtons=new Button[2];tradePrices=new Text[2];tradeNames=new Text[2];tradeNotes=new Text[2];
            for(int i=0;i<2;i++)
            {
                int action=i;var row=Card(shopPages[1].transform,36,108+i*98,624,90);
                Face(i==0?"sword":"slots",row.transform,16,19,52,52);
                tradeNames[i]=Words("",row.transform,80,18,294,26,22,true);
                tradeNotes[i]=Words("",row.transform,80,50,294,20,14,true);
                tradePrices[i]=Price(row.transform,386,31,28,20,"");
                // Every row's button names its own action (playtest 2026-11-04); 슬롯 강화 opens the palette to pick the slot.
                tradeButtons[i]=Action("강화",row.transform,488,17,120,56,()=>TradeAt(()=>
                {
                    if(action==0)Report(shop.TryUpgradeMvpAttack,"공격 강화 완료");
                    else OpenManagement();
                }),true);
                tradeButtons[i].GetComponentInChildren<Text>().fontSize=20;
            }
            BuildStampPage(shopPages[2].transform);
            BuildInkStall(shopPages[3].transform);
            shopMessage=Words("",shopPanel.transform,36,404,624,20,14,true,JInk,TextAnchor.MiddleCenter);
            // Gold on the HUD chip spot while the maintenance screens cover the HUD.
            var chip=Rounded("Shop gold",stage,16,14,72,30,3,0,new Color32(245,237,219,191),default);shopGoldChip=chip.gameObject;
            Glyph(CombatSprite("gold-coin"),chip.transform,7,3,24,24,Color.white);
            shopGold=Words("",chip.transform,37,0,80,30,16,true,JDark);
            var shadow=shopGold.gameObject.AddComponent<Shadow>();shadow.effectColor=new Color32(255,247,229,255);shadow.effectDistance=new Vector2(0,-1);
            shopGoldChip.SetActive(false);
            BuildGrowthPanel();
            SelectShopTab(0);RegisterScreen(shopPanel,close);
        }
        void SelectShopTab(int tab)
        {
            shopTab=tab;
            for(int i=0;i<shopPages.Length;i++){shopPages[i].SetActive(i==tab);SetTab(shopTabs[i],i==tab);}
        }
        // 잉크: the facility's 2~3 grade 3~4 inks as cards (art with grade emblem, name, colour · grade, effect, buy with the
        // fixed price). A bought card stays as 판매 완료; short gold or a full bag disables 구매.
        RectTransform inkStall;
        Text inkStallNote;
        readonly System.Collections.Generic.List<Button> stallButtons = new System.Collections.Generic.List<Button>();
        string inkStallState;
        const float StallCardW = 200, StallCardH = 268, StallGap = 12;
        void BuildInkStall(Transform page)
        {
            inkStall=Rect("잉크 판매대",page,36,108,624,StallCardH);
            inkStallNote=Words("",page,36,384,624,18,13,false,JMuted,TextAnchor.MiddleCenter);
        }
        void RefreshInkStall()
        {
            var stock=shop.InkStock;var growth=shop.Growth;var economy=shop.Service.Economy;
            bool room=growth!=null&&growth.HasInkRoom;
            string state=string.Join("/",stock.Select(o=>o.Part.id+":"+o.Price+":"+o.Sold))+":"+economy.Gold+":"+room;
            if(state==inkStallState)return;inkStallState=state;
            ClearGrowth(inkStall);stallButtons.Clear();
            inkStallNote.text=stock.Count>0&&!room?"가방이 가득 차 살 수 없습니다.":"";
            if(stock.Count==0){Words("진열된 잉크가 없습니다",inkStall,0,0,624,StallCardH,16,true,JMuted,TextAnchor.MiddleCenter);return;}
            float x0=Mathf.Round((624-(stock.Count*StallCardW+(stock.Count-1)*StallGap))/2);
            for(int i=0;i<stock.Count;i++)
            {
                int index=i;var offer=stock[i];var part=offer.Part;
                var card=Card(inkStall,x0+i*(StallCardW+StallGap),0,StallCardW,StallCardH);card.name="Ink offer "+part.id;
                var icon=Drop(card.transform,part,null,(StallCardW-64)/2,14,64,72);
                SetInkGrade(icon,part.grade);
                if(offer.Sold)icon.color=new Color(1,1,1,.4f);
                Words(part.name,card.transform,8,94,StallCardW-16,24,18,true,JInk,TextAnchor.MiddleCenter);
                Words($"{ForestCombatV2.Chapter1MaintenanceShop.ColorName(part.color)} / {part.grade}등급",card.transform,8,120,StallCardW-16,18,13,true,JMuted,TextAnchor.MiddleCenter);
                var clip=Rect("Effect",card.transform,14,144,StallCardW-28,70);clip.gameObject.AddComponent<RectMask2D>();
                var effect=Paragraph(clip,0,0,StallCardW-28,13,17,JInk);
                effect.text=Deviant.UI.Theme.ApprovedInkText.Display(part.description);
                Place(effect,0,0,StallCardW-28,Mathf.Max(1,LineCount(effect))*17);
                var buy=Action(offer.Sold?"판매 완료":"구매",card.transform,16,StallCardH-56,StallCardW-32,44,()=>TradeAt(()=>
                {
                    if(shop.TryBuyInk(index,out var message))growthDirty=true;
                    transactionMessage=message;
                }),true);
                buy.name="Buy ink "+index;
                buy.interactable=!offer.Sold&&room&&economy.Gold>=offer.Price;Paint(buy,true);
                SetActionGold(buy,offer.Sold?(int?)null:offer.Price,true);
                stallButtons.Add(buy);
            }
        }
        void TradeAt(Action transaction) { if(shop && shop.IsOpen && !(run && run.ShowingResult))transaction(); }
        void Report(Trade action,string success) { bool ok=action(out var error);transactionMessage=ok?success:error; }
        void RefreshShop()
        {
            if(nativeShop!=null){RefreshNativeBook(nativeShop);return;}
            if(!shop || !shop.IsOpen || shop.Service==null)return;
            var service=shop.Service;var economy=service.Economy;var stash=service.Stash;
            if(shopGold.text!=economy.Gold.ToString()){shopGold.text=economy.Gold.ToString();((RectTransform)shopGoldChip.transform).sizeDelta=new Vector2(44+Width(shopGold),30);}
            for(int i=0;i<paletteButtons.Length;i++)
            {
                var palette=shop.Growth?.Equipped(shop.GrowthWeapon(i));
                string kind=shop.SlotBlueprintKind(i);
                var state=host.Arena.Player.weapons[i];
                string key=(palette==null?"none":string.Join("/",palette.Slots.Select(s=>s.Color+":"+s.Part?.id)))+kind+":"+state.FormIndex+":"+state.SpecialIndex;
                paletteNames[i].text=host.WeaponName(i);
                if(key!=offerKeys[i])
                {
                    offerKeys[i]=key;ClearGrowth(offerChips[i]);
                    var portrait=paletteNames[i].transform.parent.Find("Portrait");ClearGrowth(portrait);
                    portrait.GetComponent<Image>().enabled=false;
                    if(kind!=null)
                    {
                        var binding=CombatWeapon(i);var skill=CombatSkill(i,binding);
                        var sprite=CombatSprite(!string.IsNullOrEmpty(skill?.presentationIcon)?skill.presentationIcon:binding?.icon);
                        Glyph(CombatSprite("weapon-disc"),portrait,4,18,80,80,Color.white);
                        Glyph(sprite,portrait,12,26,64,64,Color.white).name="Weapon icon";
                    }
                    if(palette==null)Words("팔레트 없음",offerChips[i],0,0,166,56,14,true,JMuted,TextAnchor.MiddleCenter);
                    else PaletteDrops(offerChips[i],palette,166,54,72);
                }
                paletteButtons[i].interactable=palette!=null;Paint(paletteButtons[i],true);
            }
            RefreshStampPage();
            string[] tracks={PriceTracks.StatUpgrade,PriceTracks.SlotUpgrade};
            int attackLevel=shop.Growth?.AttackLevel??stash.StatLevel;
            bool attackMax=shop.Growth!=null&&shop.Growth.AttackLevel>=shop.Growth.Data.attackMaxLevel;
            float attackStep=shop.Growth!=null?shop.Growth.Data.attackPerLevel*100:0;
            string[] labels={"공격 강화","슬롯 강화"};
            string[] notes={attackMax?$"현재 Lv.{attackLevel} / 최대":$"공격력 +{attackStep:0.#}%   현재 Lv.{attackLevel}","선택 슬롯 강화 +1"};
            for(int i=0;i<2;i++)
            {
                int price=i==0?economy.PriceOf(tracks[i]):0;tradeNames[i].text=labels[i];tradeNotes[i].text=notes[i];tradePrices[i].text=i==1?"슬롯별":price.ToString();
                tradeButtons[i].interactable=i==1||economy.Gold>=price&&!attackMax;Paint(tradeButtons[i],true);
            }
            tradePrices[0].transform.parent.Find("Coin").gameObject.SetActive(!attackMax);tradePrices[0].enabled=!attackMax;
            RefreshInkStall();
            shopMessage.gameObject.SetActive(shopTab!=2);
            shopMessage.text=Deviant.UI.Theme.ApprovedInkText.Display(transactionMessage);
            RefreshGrowthPanel();
        }
        // A palette's slots as HTML drops: the mounted part, or the slot colour faintly; colourless reads 무색.
        void PaletteDrops(Transform parent,GrowthPalette palette,float width,float w,float h,float step=0)
        {
            int n=palette.Slots.Count;w=Mathf.Min(w,(width-(n-1)*4)/n);
            if(step==0)step=w+4;float x0=Mathf.Round((width-((n-1)*step+w))/2);
            for(int i=0;i<n;i++)
            {
                var slot=palette.Slots[i];
                if(slot.Part==null&&slot.Color=="colorless")Words("무색",parent,x0+i*step,0,w,h,12,true,JMuted,TextAnchor.MiddleCenter);
                else Drop(parent,slot.Part,slot.Color,x0+i*step,0,w,h);
            }
        }

        // Pause, results and the title share one menu (journal design, faithful.css pause / death / clear). menuTitle and
        // menuStats stay the text the screens and tests read ("label   value" lines); LayoutMenu draws them: results as
        // the 580 px book (art, ruled rows, buttons), pause and title as the centred button column over the veil.
        const float ResultW = 580, ResultX = (StageW - ResultW) / 2;
        GameObject resultBook;
        RectTransform resultRows;
        Text resultHeading, menuCaption, menuSubcaption;
        Image resultArt;
        Button menuShortcuts, menuGuide;
        static readonly Color PauseVeil = Deviant.UI.Theme.ApprovedTheme.PauseBackdrop;
        void BuildMenu()
        {
            menu=Rect("메뉴 / 결과",MenuStage,0,0,960,540).gameObject;
            menuTitle=Words("",menu.transform,0,0,1,1,12);menuTitle.enabled=false;
            menuStats=Words("",menu.transform,0,0,1,1,12);menuStats.enabled=false;
            resultBook=Book("결과",false,ResultX,BookY,ResultW,BookH,out resultHeading,out var resultClose);
            resultBook.transform.SetParent(menu.transform,false);resultClose.gameObject.SetActive(false); // a result cannot be dismissed
            resultArt=Face("death",resultBook.transform,32,38,516,164);
            resultRows=Rect("기록",resultBook.transform,72,206,436,147);
            if(!oldTitle){Place(resultBook.transform,65,0,830,480);Place(resultRows,0,185,830,210);resultArt.gameObject.SetActive(false);Place(resultHeading.transform.parent,0,40,830,70);Place(resultHeading,0,0,830,70);resultHeading.alignment=TextAnchor.MiddleCenter;resultHeading.fontSize=58;}
            menuCaption=Words("",menu.transform,0,96,960,48,36,true,JPaperText,TextAnchor.MiddleCenter);
            menuSubcaption=Words("",menu.transform,0,144,960,24,16,false,JPaperText,TextAnchor.MiddleCenter);
            menuPrimary=Action("계속하기",menu.transform,360,184,240,48,MenuPrimary,true);
            menuShortcuts=Action("설정",menu.transform,360,246,240,48,OpenApprovedSettings);
            menuGuide=Action("조작 안내",menu.transform,360,0,240,48,ShowKeyGuide);
            menuSecondary=Action("타이틀",menu.transform,360,308,240,48,MenuSecondary);
            foreach(var b in new[]{menuPrimary,menuShortcuts,menuGuide,menuSecondary})b.GetComponentInChildren<Text>().fontSize=20;
            BuildVolume();
            RegisterScreen(menu,menuPrimary);
            BuildDemoCompletion();
        }
        // Pause volume (playtest 2026-11-04): a paper card under the menu column with 전체 and 음악, each − value +.
        RectTransform volumeCard;
        Text masterValue, musicValue;
        void BuildVolume()
        {
            volumeCard=(RectTransform)Card(menu.transform,360,0,240,96).transform;volumeCard.name="음량";
            masterValue=VolumeRow("전체",12,()=>Deviant.Music.GameAudioSettings.SetMaster(Deviant.Music.GameAudioSettings.Master-Deviant.Music.GameAudioSettings.Step),
                ()=>Deviant.Music.GameAudioSettings.SetMaster(Deviant.Music.GameAudioSettings.Master+Deviant.Music.GameAudioSettings.Step));
            musicValue=VolumeRow("음악",52,()=>Deviant.Music.GameAudioSettings.SetMusic(Deviant.Music.GameAudioSettings.Music-Deviant.Music.GameAudioSettings.Step),
                ()=>Deviant.Music.GameAudioSettings.SetMusic(Deviant.Music.GameAudioSettings.Music+Deviant.Music.GameAudioSettings.Step));
        }
        Text VolumeRow(string label,float y,Action down,Action up)
        {
            Words(label,volumeCard,16,y,60,32,16,true,JInk);
            var minus=Action("−",volumeCard,84,y,40,32,()=>{down();RefreshVolume();});minus.name=label+" −";
            var value=Words("",volumeCard,124,y,60,32,16,true,JInk,TextAnchor.MiddleCenter);
            var plus=Action("+",volumeCard,184,y,40,32,()=>{up();RefreshVolume();});plus.name=label+" +";
            return value;
        }
        void RefreshVolume()
        {
            masterValue.text=Deviant.Music.GameAudioSettings.Percent(Deviant.Music.GameAudioSettings.Master);
            musicValue.text=Deviant.Music.GameAudioSettings.Percent(Deviant.Music.GameAudioSettings.Music);
        }
        void ShowKeyGuide()
        {
            KeyHelpShown = true;
            if (keyHelp) keyHelp.SetActive(true);
            if (host && Presenter.Paused) Presenter.TogglePause();
        }
        void LayoutMenu()
        {
            bool result=!oldTitle&&(chapter2&&chapter2.ShowingResult||run&&run.ShowingResult);
            resultBook.SetActive(result);
            menuCaption.text=oldTitle?menuTitle.text:"";menuSubcaption.text=oldTitle?menuStats.text:"";
            if(oldTitle){menuCaption.color=menuTitle.color;menuCaption.fontSize=menuTitle.fontSize;menuCaption.fontStyle=FontStyle.Bold;}
            menuSubcaption.gameObject.SetActive(oldTitle&&!string.IsNullOrEmpty(menuStats.text));
            menuShortcuts.gameObject.SetActive(!result&&!oldTitle);
            menuGuide.gameObject.SetActive(KeyHelpAvailable&&!result&&!oldTitle&&host);
            if(result&&!oldTitle){LayoutApprovedResult();SetApprovedPauseVisible(false);volumeCard.gameObject.SetActive(false);return;}
            if(!oldTitle){LayoutApprovedPause();return;}
            if(result)
            {
                if(resultHeading.text!=menuTitle.text){resultHeading.text=menuTitle.text;FitTitle(resultHeading);}
                resultArt.sprite=Art(menuTitle.text.Contains("클리어")?"clear":"death");
                if(resultRows.name!=menuStats.text)
                {
                    resultRows.name=menuStats.text;ClearGrowth(resultRows);
                    var lines=menuStats.text.Split('\n').Where(l=>l.Trim().Length>0).ToArray();
                    float rowH=lines.Length<=3?49:Mathf.Floor(147f/lines.Length);int size=rowH<40?18:20;
                    for(int i=0;i<lines.Length;i++)
                    {
                        int split=lines[i].IndexOf("   ");string label=split>0?lines[i].Substring(0,split):lines[i],value=split>0?lines[i].Substring(split+3).Trim():"";
                        Words(label,resultRows,0,i*rowH,280,rowH,size,false);
                        Words(value,resultRows,0,i*rowH,436,rowH,size,true,JInk,TextAnchor.MiddleRight);
                        Fill(resultRows,0,(i+1)*rowH-1,436,1,JRule);
                    }
                }
                bool both=menuSecondary.gameObject.activeSelf;
                // Buttons share the menu parent; use the current paper position to keep them inside its margins.
                float resultLeft=((RectTransform)resultBook.transform).anchoredPosition.x;
                if(both){Place(menuSecondary,resultLeft+32,441,254,48);Place(menuPrimary,resultLeft+294,441,254,48);}
                else Place(menuPrimary,resultLeft+32,441,516,48);
            }
            else
            {
                float y=oldTitle?196:menuGuide.gameObject.activeSelf?81:112;
                Place(menuPrimary,360,y,240,48);y+=62;
                if(menuShortcuts.gameObject.activeSelf){Place(menuShortcuts,360,y,240,48);y+=62;}
                if(menuGuide.gameObject.activeSelf){Place(menuGuide,360,y,240,48);y+=62;}
                Place(menuSecondary,360,y,240,48);y+=62;
                // The title has no volume; the pause menus (combat and village) do.
                Place(volumeCard,360,y,240,96);
            }
            volumeCard.gameObject.SetActive(!result&&!oldTitle);
            if(volumeCard.gameObject.activeSelf)RefreshVolume();
            foreach(var b in new[]{menuPrimary,menuSecondary,menuShortcuts,menuGuide})
            {
                Paint(b,b==menuPrimary);
                var label=b.GetComponentInChildren<Text>();label.rectTransform.sizeDelta=((RectTransform)b.transform).sizeDelta;
            }
        }
        WellTitlePresentation wellTitle;
        void BuildTitleBackdrop()
        {
            var root = new GameObject("우물 타이틀", typeof(RectTransform));
            root.transform.SetParent(transform, false);
            root.transform.SetSiblingIndex(dim.transform.GetSiblingIndex());
            wellTitle = root.AddComponent<WellTitlePresentation>();
            wellTitle.Build(font, MenuPrimary, MenuSecondary);
            FitToWorld();
        }
        void RefreshTitle()
        {
            if(!oldTitle)return;
            var session=Chapter1RunHost.Ensure();bool pending=session.ChapterTwoPending;
            wellTitle.SetPending(pending);
            dim.SetActive(pending);menu.SetActive(pending);oldTitle.ExternalPresentation=true;
            if (!pending) return;
            menuTitle.text=pending?"2챕터":"deviant";menuTitle.color=JInk;
            menuTitle.fontSize=pending?24:48;
            menuStats.text=pending?"준비 중입니다.":"";
            menuPrimary.GetComponentInChildren<Text>().text=pending?"타이틀로":"시작하기";
            menuSecondary.GetComponentInChildren<Text>().text="종료";menuSecondary.gameObject.SetActive(!pending);LayoutMenu();
        }
        void RefreshMenu()
        {
            if (chapter2 && chapter2.ShowingResult && chapter2.Complete && demoCompletion)
            {
                var settled = chapter2.SourceRun?.Result;
                double elapsed = settled?.ElapsedSeconds ?? chapter2.Elapsed;
                int kills = settled?.Kills ?? chapter2.Kills;
                int pigment = settled.HasValue ? chapter2.SourceRun.Pigment : chapter2.PigmentEarned;
                demoCompletion.SetSummary("플레이 시간 " + Chapter1RunBinding.FormatTime(elapsed)
                    + " / 처치 " + kills + " / 마법안료 +" + pigment);
                return;
            }
            if(chapter2&&chapter2.ShowingResult)
            {
                bool clear2=chapter2.Complete;menuTitle.text=clear2?"최종 클리어":"사망";menuTitle.color=clear2?Cream:Red;
                // The same lines as the chapter-1 result (run-structure.md 결과 화면), counted over the whole run.
                var source=chapter2.SourceRun;resultReach??=Chapter2Reach(clear2);
                if(source!=null&&source.Result.HasValue)
                {
                    var value=source.Result.Value;bool profileRun=source.Plan!=null&&!source.Plan.Training;
                    menuStats.text="플레이 시간   "+Chapter1RunBinding.FormatTime(value.ElapsedSeconds)+"\n처치 수   "+value.Kills+resultReach+(profileRun?"\n마법안료   +"+source.Pigment+"\n"+Chapter1RunBinding.PaletteLine.Replace("  ","   "):"");
                }
                else menuStats.text="플레이 시간   "+Chapter1RunBinding.FormatTime(chapter2.Elapsed)+"\n처치 수   "+chapter2.Kills+resultReach;
                menuPrimary.GetComponentInChildren<Text>().text="마을로";menuSecondary.gameObject.SetActive(false);LayoutMenu();return;
            }
            bool result=run&&run.ShowingResult&&run.Run.Result.HasValue;
            if(!result&&!(chapter2&&chapter2.ShowingResult))resultReach=null;
            if(!result&&!Presenter.Paused)return;
            if(result)
            {
                var value=run.Run.Result.Value;bool clear=value.Outcome==RunOutcome.ChapterCleared;
                menuTitle.text=clear?"1챕터 클리어":"사망";menuTitle.color=clear?Cream:Red;
                // How far the run got, read once when it ends (playtest 2026-11-04): a death says what is left to beat.
                resultReach??=RunReach(clear);
                menuStats.text="플레이 시간   "+Chapter1RunBinding.FormatTime(value.ElapsedSeconds)+"\n처치 수   "+value.Kills+resultReach+(run.ProfileRun?"\n마법안료   +"+run.PigmentEarned+"\n"+Chapter1RunBinding.PaletteLine.Replace("  ","   "):"");
                // Both endings go back to the starting village (user decision 2026-10-31); the next run leaves from there.

                menuPrimary.GetComponentInChildren<Text>().text="마을로";
                // A clear that ended the run (a village palette piece run) has no chapter 2 to go on to.
                bool onward=clear&&!run.Run.Ended;
                menuSecondary.gameObject.SetActive(onward);if(onward)menuSecondary.GetComponentInChildren<Text>().text="2챕터로";
            }
            else
            {
                menuTitle.text="일시정지";menuTitle.color=Cream;menuStats.text="";
                menuPrimary.GetComponentInChildren<Text>().text="계속하기";menuSecondary.GetComponentInChildren<Text>().text="마을로";menuSecondary.gameObject.SetActive(true);
            }
            LayoutMenu();
        }
        string resultReach;
        // 탐험 (discovered rooms of the chapter) on both endings; on a death, the boss's remaining health or 만나지 못함.
        string RunReach(bool clear)
        {
            string text="";
            var rooms=map?map.MapData?.rooms:null;
            if(rooms!=null&&rooms.Length>0)text+=$"\n탐험   {rooms.Count(r=>map.IsRoomDiscovered(r.id))} / {rooms.Length} 구역";
            if(clear)return text;
            var actor=clockRoom&&clockRoom.Boss?clockRoom.Boss:boss?boss.Boss:null;
            if(!actor)return text;
            bool met=clockRoom&&clockRoom.Boss?clockRoom.Boss.Started:actor.health<actor.maxHealth;
            float left=Mathf.Clamp01(actor.health/Mathf.Max(1f,actor.maxHealth));
            return text+(met?$"\n보스 체력   {Mathf.Max(1,Mathf.CeilToInt(left*100))}% 남음":"\n보스   만나지 못함");
        }
        // Chapter 2: discovered rooms, and on a death the boss's remaining health (its current health bar) or 만나지 못함.
        string Chapter2Reach(bool clear)
        {
            string text="";
            var rooms=map?map.MapData?.rooms:null;
            if(rooms!=null&&rooms.Length>0)text+=$"\n탐험   {rooms.Count(r=>map.IsRoomDiscovered(r.id))} / {rooms.Length} 구역";
            if(clear||!chapter2.Boss)return text;
            if(!chapter2.Boss.Started)return text+"\n보스   만나지 못함";
            float left=Mathf.Clamp01(chapter2.Boss.health/Mathf.Max(1f,chapter2.Boss.maxHealth));
            return text+$"\n보스 체력   {Mathf.Max(1,Mathf.CeilToInt(left*100))}% 남음";
        }
        void MenuPrimary()
        {
            if(village) { VillageMenuPrimary(); return; }
            if(chapter2&&chapter2.ShowingResult){Chapter1RunHost.Ensure().GoToVillage();return;}
            if(oldTitle) {var session=Chapter1RunHost.Ensure();if(session.ChapterTwoPending)session.LeaveChapterTwoPending();else { session.GoToVillage(false); }return;}
            if(run&&run.ShowingResult) run.Host.GoToVillage();
            else if(host&&Presenter.Paused)Presenter.TogglePause();
        }
        void MenuSecondary()
        {
            if(village) { VillageMenuSecondary(); return; }
            if(run&&run.ShowingResult&&run.Run.Result?.Outcome==RunOutcome.ChapterCleared){Deviant.Chapter2.Chapter2EntryBridge.TryEnterFromChapterOne();return;}
            if(oldTitle)
            {
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying=false;
#else
                Application.Quit();
#endif
            }
            else if(host&&Presenter.Paused&&!(run&&run.ShowingResult)&&!(chapter2&&chapter2.ShowingResult))
                OpenAbandonRunQuestion(()=>Chapter1RunHost.Ensure().GoToVillage());
            else Chapter1RunHost.Ensure().GoToVillage();
        }
    }
}
