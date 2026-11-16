using ForestCombatV2;
using Deviant.Run;
using System.Linq;
using Deviant.UI.Theme;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace Deviant.Game
{
    // Free augment choice of a boss clear (chapters 1 and 2) or the seal reward chest (docs/design/world/stage-gimmicks.md):
    // three cards (one stable, two risk); submitting the focused card takes it. The world layer (AugmentRewardChoice) owns
    // the pause scope and the draw; the taken card joins the run growth. This view only shows and forwards the pick.
    public sealed partial class TemporaryGameGui
    {
        GameObject augmentPanel;
        Button augmentTake;
        readonly Button[] augmentCards = new Button[3];
        int augmentPick = -1;
        AugmentDefinition[] displayedAugmentOffer;
        readonly RectTransform[] approvedAugmentContents=new RectTransform[3];
        readonly RawImage[] approvedAugmentIcons=new RawImage[3];
        readonly RawImage[] approvedAugmentWashes=new RawImage[3];
        AugmentRewardChoice augments;
        // Shared reroll budget of the current run (roguelite-progression.md 공용 리롤), shown on the seal, altar and
        // legacy ink shelf buttons.
        Button altarReroll, sealReroll;
        int RerollsLeft
        {
            get
            {
                var bridge = host && host.Arena ? host.Arena.GetComponent<RunRevivalBridge>() : null;
                return bridge && bridge.Run != null ? bridge.Run.Perks.RerollsRemaining : 0;
            }
        }
        string RerollText => "공용 리롤 / " + RerollsLeft + "회";

        void BuildAugment()
        {
            augmentPanel=Rect("증강 선택",MenuStage,65,0,830,540).gameObject;
            var page=augmentPanel.transform;
            Words("증강 선택",page,0,32,600,30,23,true);
            Words("일시정지",page,779,40,70,20,12,false,JMuted);
            Fill(page,0,491.5f,830,.5f,new Color32(84,90,96,150));
            SettingsHint(page,0,"← →","증강 선택",88,502);
            SettingsHint(page,180,"F","획득",44,502);
            SettingsHint(page,300,"↑ ↓","설명 스크롤",88,502);
            for (int i = 0; i < augmentCards.Length; i++)
            {
                int index = i;
                var rewardCard=SketchRewardChoiceScreen.Card(i);
                float cardWidth=rewardCard.width;
                var face=Card(page,rewardCard.x-65,rewardCard.y,cardWidth,rewardCard.height);face.raycastTarget=true;face.name="Augment "+i;
                if(i>0)Fill(page,rewardCard.x-90,rewardCard.y,.5f,rewardCard.height,new Color32(84,90,96,150));
                augmentCards[i] = face.gameObject.AddComponent<Button>(); augmentCards[i].transition = Selectable.Transition.None;
                augmentCards[i].onClick.AddListener(() => { augmentPick = index; Refresh(); });
                var selection=face.gameObject.AddComponent<ApprovedAugmentCardSelection>();
                selection.Selected=()=>{if(augmentPick==index)return;augmentPick=index;Refresh();};
                selection.Submitted=()=>TakeAugment(index);
                selection.Scrolled=direction=>ScrollAugmentDescription(index,direction);
                Words("",face.transform,12,5,cardWidth-36,20,12,false,JMuted).name="Kind";
                approvedAugmentIcons[i]=Rect("Augment icon",face.transform,(cardWidth-70)/2,46,70,70).gameObject.AddComponent<RawImage>();approvedAugmentIcons[i].raycastTarget=false;
                approvedAugmentWashes[i]=Rect("Name selection",face.transform,5,100,cardWidth-10,38).gameObject.AddComponent<RawImage>();
                approvedAugmentWashes[i].texture=ApprovedTheme.SelectionWash;approvedAugmentWashes[i].raycastTarget=false;approvedAugmentWashes[i].color=new Color(1,1,1,.48f);
                Words("",face.transform,12,129,cardWidth-36,32,20,false,JInk,TextAnchor.UpperLeft).name="Name";
                Fill(face.transform,12,164,cardWidth-24,.5f,new Color32(84,90,96,150));
                approvedAugmentContents[i]=JournalScroll("Augment effect",face.transform,12,176,cardWidth-30,rewardCard.height-214);
                float textWidth=cardWidth-46;
                Words("효과",approvedAugmentContents[i],0,0,textWidth,14,10,false,JMuted).name="Effect heading";
                var text=Paragraph(approvedAugmentContents[i],0,19,textWidth,14,22,JInk);text.name="Text";
                var rule=Fill(approvedAugmentContents[i],0,100,textWidth,.5f,new Color32(84,90,96,150));rule.name="Cost rule";
                Words("대가",approvedAugmentContents[i],0,114,textWidth,22,13,false,JMuted).name="Cost heading";
                var cost=Paragraph(approvedAugmentContents[i],0,149,textWidth,14,22,JWarning);cost.name="Cost";

            }
            augmentTake = ApprovedKeyAction("선택하기","F",page,12,442,206,25,()=>TakeAugment(augmentPick),14);
            augmentTake.GetComponent<ApprovedActionVisual>().NormalColor=new Color32(217,203,132,255);
            sealReroll=SettingsAction("공용 리롤",page,590,62,225,20,()=>{if(augments&&augments.TryReroll())augmentPick=-1;Refresh();},12);
            RegisterScreen(augmentPanel, augmentCards[0]);
        }

        bool CanScrollAugmentDescription(int index,bool up)
        {
            var scroll=approvedAugmentContents[index].GetComponentInParent<ScrollRect>();
            return scroll&&scroll.viewport&&scroll.content.rect.height>scroll.viewport.rect.height&&
                (up?scroll.verticalNormalizedPosition<.999f:scroll.verticalNormalizedPosition>.001f);
        }
        void ScrollAugmentDescription(int index,float direction)
        {
            var content=approvedAugmentContents[index];if(!content)return;
            var scroll=content.GetComponentInParent<ScrollRect>();if(!scroll||!scroll.viewport)return;
            float overflow=content.rect.height-scroll.viewport.rect.height;
            if(overflow>0)scroll.verticalNormalizedPosition=Mathf.Clamp01(scroll.verticalNormalizedPosition+direction*40/overflow);
        }
        void TakeAugment(int index)
        {
            if (augments && augments.TryTake(index)) PresentationSfx.Play(PresentationSfx.AltarAcquire);
            Refresh();
        }

        void RefreshAugment()
        {
            // Chapter 2 adds its boss choice when its world starts, which may be after the GUI found the chapter.
            if (!augments && chapter2) { augments = chapter2.GetComponent<AugmentRewardChoice>(); if (augments) augments.ExternalPresentation = true; }
            if (!augments || !augments.IsOpen || augments.Offer == null) { augmentPick = -1; displayedAugmentOffer=null; return; }
            var offer = augments.Offer;
            bool newOffer=displayedAugmentOffer!=offer;
            if(newOffer){displayedAugmentOffer=offer;augmentPick=0;}
            sealReroll.GetComponentInChildren<Text>().text=RerollText;sealReroll.interactable=RerollsLeft>0;Paint(sealReroll,false);
            for (int i = 0; i < augmentCards.Length; i++)
            {
                var card = augmentCards[i];
                bool shown = i < offer.Length;
                card.gameObject.SetActive(shown);
                if (!shown) continue;
                var definition = offer[i];
                var kind = card.transform.Find("Kind").GetComponent<Text>();
                kind.text = AugmentKind(augments.Augments.Catalog, definition); kind.color = ApprovedTextColor(definition.Stable ? JMuted : JWarning);
                card.transform.Find("Name").GetComponent<Text>().text = definition.name;
                card.GetComponentsInChildren<Text>().First(t=>t.name=="Text").text = definition.gain;
                // Stable cards carry no drawback, so only risk cards show their cost.
                card.GetComponentsInChildren<Text>().First(t=>t.name=="Cost").text = definition.Stable ? "" : definition.cost;
                var gainText=card.GetComponentsInChildren<Text>().First(t=>t.name=="Text");var costText=card.GetComponentsInChildren<Text>().First(t=>t.name=="Cost");
                float cardWidth=SketchRewardChoiceScreen.Card(i).width,textWidth=cardWidth-46;
                float gainHeight=Mathf.Max(22,LineCount(gainText)*22);Place(gainText,0,19,textWidth,gainHeight);
                float ruleY=19+gainHeight+14,costHeight=LineCount(costText)*22;
                var costRule=approvedAugmentContents[i].Find("Cost rule");var costHeading=approvedAugmentContents[i].Find("Cost heading");
                costRule.gameObject.SetActive(!definition.Stable);costHeading.gameObject.SetActive(!definition.Stable);
                Place(costRule,0,ruleY,textWidth,.5f);Place(costHeading,0,ruleY+14,textWidth,14);
                costHeading.GetComponent<Text>().fontSize=10;
                Place(costText,0,ruleY+33,textWidth,costHeight);
                approvedAugmentContents[i].sizeDelta=new Vector2(cardWidth-30,Mathf.Max(136,definition.Stable?19+gainHeight+12:ruleY+33+costHeight+12));
                if(newOffer)approvedAugmentContents[i].GetComponentInParent<ScrollRect>(true).verticalNormalizedPosition=1;
                var icon=ApprovedTheme.Texture("ui-assets-v1/"+definition.id);approvedAugmentIcons[i].texture=icon;approvedAugmentIcons[i].enabled=icon;
                var title=card.transform.Find("Name").GetComponent<Text>();title.color=i==augmentPick?new Color32(217,203,132,255):new Color32(174,182,192,255);
                title.fontSize=20;card.image.color=Color.clear;
                float iconSize=i==augmentPick?76:70;Place(approvedAugmentIcons[i],(cardWidth-iconSize)/2,38+(86-iconSize)/2,iconSize,iconSize);
                approvedAugmentIcons[i].color=new Color(1,1,1,i==augmentPick?1:.6f);
                var wash=approvedAugmentWashes[i];wash.enabled=false;
                float washWidth=Mathf.Min(cardWidth-10,title.preferredWidth+18);Place(wash,5,103,washWidth,32);
            }
            var selectedCard=SketchRewardChoiceScreen.Card(Mathf.Max(0,augmentPick));
            Place(augmentTake,selectedCard.x-65+12,selectedCard.yMax-28,selectedCard.width-24,25);
            augmentTake.interactable = augmentPick >= 0; Paint(augmentTake, true);
            // Submit works on each card; the lower actions remain reachable for mouse/pad users.
            for (int i = 0; i < augmentCards.Length; i++)
                augmentCards[i].navigation = new Navigation
                {
                    mode = Navigation.Mode.Explicit,
                    selectOnLeft = i > 0 && i < offer.Length ? augmentCards[i - 1] : null,
                    selectOnRight = i + 1 < offer.Length ? augmentCards[i + 1] : null,
                    selectOnDown = CanScrollAugmentDescription(i,false)?null:augmentTake,
                    selectOnUp = CanScrollAugmentDescription(i,true)?null:sealReroll.interactable?sealReroll:null
                };
            augmentTake.navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnUp = augmentCards[augmentPick >= 0 ? augmentPick : 0],
                selectOnRight = sealReroll.interactable ? sealReroll : null
            };
            sealReroll.navigation = new Navigation
            {
                mode=Navigation.Mode.Explicit,
                selectOnUp=augmentCards[augmentPick>=0?augmentPick:0],
                selectOnDown=augmentCards[augmentPick>=0?augmentPick:0],
                selectOnLeft=augmentTake.interactable?augmentTake:null
            };
            if(newOffer && augmentPanel.activeInHierarchy && EventSystem.current && !EventSystem.current.alreadySelecting)
                EventSystem.current.SetSelectedGameObject(augmentCards[0].gameObject);
        }
    }

    // Selection follows native keyboard/gamepad focus without requiring an extra submit.
    public sealed class ApprovedAugmentCardSelection : MonoBehaviour, ISelectHandler, ISubmitHandler, IMoveHandler
    {
        public System.Action Selected;
        public System.Action Submitted;
        public System.Action<float> Scrolled;
        public void OnMove(AxisEventData data)
        {
            if(data.moveDir==MoveDirection.Down||data.moveDir==MoveDirection.Up)
            {Scrolled?.Invoke(data.moveDir==MoveDirection.Up?1:-1);data.Use();}
        }
        public void OnSelect(BaseEventData eventData)=>Selected?.Invoke();
        public void OnSubmit(BaseEventData eventData)=>Submitted?.Invoke();
    }
}
