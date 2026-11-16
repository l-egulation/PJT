using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;

namespace Deviant.ThemePreview
{
    [Serializable] public sealed class PreviewContentItem
    {
        public string id, kind, name, art, skill, attack, description, cost, note, group;
        public bool available; public int maxStacks;
    }
    [Serializable] public sealed class PreviewOffer
    {
        public string id,node; public string[] colors; public int price; public bool sold,empty;
    }
    [Serializable] public sealed class PreviewOfferScreen { public string key; public PreviewOffer[] offers; }
    [Serializable] public sealed class PreviewCommerceFixtures
    {
        public PreviewContentItem[] items; public PreviewOfferScreen[] screens;
        public int priceStart,priceStep,augmentLimit;
    }
    public sealed class PreviewOwnedAugment
    {
        public PreviewContentItem Item; public int Stacks;
    }
    public sealed partial class ThemePreviewScreen
    {
        PreviewCommerceFixtures commerce;
        readonly Dictionary<string,PreviewContentItem> commerceItems=new Dictionary<string,PreviewContentItem>();
        readonly List<PreviewOwnedAugment> ownedAugments=new List<PreviewOwnedAugment>();
        PreviewOffer[] maintenanceOffer,sealOffer;
        CorePreviewState commerceInventory;
        PreviewOffer receiptOffer;
        string receiptOrigin;
        int receiptWeaponId=-1, receiptInkIndex=-1;
        int augmentPurchases,earnedSketches,earnedInks,extendedCoreCandidate,commerceSequence=1000;
        public IReadOnlyList<PreviewOwnedAugment> OwnedAugments=>ownedAugments;
        public CorePreviewState CommerceInventory=>commerceInventory;
        public string ReceiptItemId=>receiptOffer?.id;
        public int AugmentPrice=>commerce.priceStart+commerce.priceStep*augmentPurchases;
        public IReadOnlyList<PreviewOffer> CurrentOffers=>CommerceOffers();

        void ResetCommerce()
        {
            commerce=null;commerceItems.Clear();ownedAugments.Clear();boughtSlots.Clear();maintenanceOffer=sealOffer=null;
            commerceInventory=null;receiptOffer=null;receiptOrigin=null;augmentPurchases=earnedSketches=earnedInks=0;commerceSequence=1000;
            PreviewGold=1200;PreviewRewardCount=2;AugmentPending=false;rewardReturn=null;LoadCommerce();
        }
        void LoadCommerce()
        {
            if(commerce!=null)return;
            commerce=JsonUtility.FromJson<PreviewCommerceFixtures>(File.ReadAllText(Path.Combine(Application.dataPath,"Deviant/UI/ThemePreview/Layout/commerce-fixtures.json")));
            foreach(var item in commerce.items)commerceItems.Add(item.id,item);
            foreach(var ink in art.Package.inks)commerceItems.Add(ink.id,new PreviewContentItem{id=ink.id,kind="ink",name=ink.name,description=CoreInkText.Description(ink.description,false),art="ink:"+ink.id});
            // Sample starting inventory, separate from the core layout fixtures and the game save.
            commerceInventory=new CorePreviewState(5);
            foreach(string id in new[]{"S01","S03","S04"})ownedAugments.Add(new PreviewOwnedAugment{Item=commerceItems[id],Stacks=1});
            ownedAugments.Add(new PreviewOwnedAugment{Item=commerceItems["attackSpeed"],Stacks=2});
        }
        PreviewOffer[] CommerceOffers()
        {
            LoadCommerce();
            if(ExtendedKey=="augment-choose")return maintenanceOffer??(maintenanceOffer=NextAugmentOffer(false));
            if(ExtendedKey=="augment-seal")return sealOffer??(sealOffer=NextAugmentOffer(true));
            var offers=commerce.screens.FirstOrDefault(s=>s.key==ExtendedKey)?.offers??Array.Empty<PreviewOffer>();
            if(IsShop()&&ExtendedKey!="shop-no-inks")
            {
                foreach(var offer in offers.Where(o=>!o.empty&&commerceItems[o.id].kind=="ink"))
                {
                    int slot=Array.IndexOf(offers,offer);if(offer.sold||boughtSlots.Contains(ShopSlotKey(slot)))continue;
                    int index=Array.FindIndex(art.Package.inks,i=>i.id==offer.id);if(!commerceInventory.OwnedInkIndices.Contains(index))continue;
                    var replacement=art.Package.inks.FirstOrDefault(i=>!commerceInventory.OwnedInkIndices.Contains(Array.IndexOf(art.Package.inks,i))&&!offers.Any(o=>!o.empty&&o.id==i.id));
                    if(replacement==null)offer.empty=true;else offer.id=replacement.id;
                }
            }
            return offers;
        }
        PreviewOffer[] NextAugmentOffer(bool seal)
        {
            var source=commerce.screens.First(s=>s.key==(seal?"augment-seal":"augment-choose")).offers;
            Func<PreviewContentItem,bool> eligible=i=>seal?i.kind=="seal"&&OwnedStacks(i.id)<i.maxStacks:i.kind=="maintenance"&&i.available&&OwnedStacks(i.id)==0;
            var result=new List<PreviewOffer>();
            for(int slot=0;slot<3;slot++)
            {
                var preferred=commerceItems[source[slot].id];
                Func<PreviewContentItem,bool> fits=i=>eligible(i)&&!result.Any(o=>o.id==i.id)&&(seal||(slot==0?i.group=="safe":i.group!="safe"))&&(seal||slot<2||i.id[0]!=result[1].id[0]);
                var item=fits(preferred)?preferred:commerce.items.FirstOrDefault(fits);
                if(item==null)return Array.Empty<PreviewOffer>();
                result.Add(new PreviewOffer{id=item.id,node=source[slot].node,colors=Array.Empty<string>(),price=0});
            }
            return result.ToArray();
        }
        int OwnedStacks(string id)=>ownedAugments.FirstOrDefault(o=>o.Item.id==id)?.Stacks??0;
        string ShopSlotKey(int slot)=>ExtendedKey+":"+slot;
        bool ShopSold(int slot)=>boughtSlots.Contains(ShopSlotKey(slot))||(CommerceOffers().ElementAtOrDefault(slot)?.sold??false);
        bool CommerceCanBuy(PreviewOffer offer)=>offer!=null&&!offer.empty&&offer.price>=0&&!ShopSold(selectedCandidate)&&PreviewGold>=offer.price;
        void GrantOffer(PreviewOffer offer)
        {
            var item=commerceItems[offer.id];
            receiptWeaponId=receiptInkIndex=-1;
            if(item.kind=="ink"){receiptInkIndex=Array.FindIndex(art.Package.inks,i=>i.id==item.id);commerceInventory.AddInk(receiptInkIndex);}
            else if(item.kind=="sketch")
            {
                var colors=offer.colors?.Length>0?offer.colors:new[]{"neutral"};
                commerceInventory.Sketches.Add(new PreviewWeapon{Id=receiptWeaponId=++commerceSequence,Name=item.name,Art=item.art,Attack=item.attack,Skill=item.skill,SkillDescription=item.description,Colors=(string[])colors.Clone(),Applied=Enumerable.Repeat(-1,colors.Length).ToArray()});
            }
            else
            {
                var owned=ownedAugments.FirstOrDefault(o=>o.Item.id==item.id);
                if(owned==null)ownedAugments.Add(new PreviewOwnedAugment{Item=item,Stacks=1});
                else if(item.kind=="seal")owned.Stacks=Math.Min(item.maxStacks,owned.Stacks+1);
            }
        }
        void OpenCommerceBag()
        {
            extendedCoreReturn=ExtendedKey;extendedCoreCandidate=selectedCandidate;ExtendedMode=false;CoreMode=true;Inventory=commerceInventory;
            CoreTab=0;CoreStage="list";CorePick=CoreSlot=CoreInk=bagCategory=target=action=0;
            bagTabs=coreClosed=false;detailScroll=0;Render();
        }
        bool ActivateCommerce(string caption)
        {
            LoadCommerce();string k=ExtendedKey;
            if(caption.Contains("가방 확인")){OpenCommerceBag();return true;}
            if((k=="reward-receipt"||k=="boss-ink"))
            {
                if(caption.Contains("지금")){OpenCommerceBag();extendedCoreReturn="commerce-finish";OpenAcquiredConfiguration(receiptWeaponId,receiptInkIndex);return true;}
                FinishCommerceReceipt();return true;
            }
            if(k=="augment"||k=="augment-pending")
            {
                if(caption.Contains("보유 증강"))return false;
                if(!AugmentPending)
                {
                    var next=NextAugmentOffer(false);if(augmentPurchases>=commerce.augmentLimit||PreviewGold<AugmentPrice||next.Length!=3)return true;
                    maintenanceOffer=next;PreviewGold-=AugmentPrice;AugmentPending=true;
                }
                OpenExtended("augment-choose",false);return true;
            }
            if(k=="augment-choose"||k=="augment-seal")
            {
                if(caption.Contains("나중에")){OpenExtended(k=="augment-choose"?"augment-pending":"seal-complete",false);return true;}
                var offers=CommerceOffers();if(selectedCandidate>=offers.Length)return true;var selected=offers[selectedCandidate];
                if(k=="augment-choose"&&!AugmentPending)
                {if(PreviewGold<AugmentPrice||augmentPurchases>=commerce.augmentLimit)return true;PreviewGold-=AugmentPrice;}
                GrantOffer(selected);
                if(k=="augment-choose"){augmentPurchases++;AugmentPending=false;maintenanceOffer=null;}else sealOffer=null;
                // Back from the owned list returns to the purchase/complete page, never to a consumed offer.
                OpenExtended(k=="augment-choose"?"augment":"seal-complete",false);OpenExtended("augment-owned");return true;
            }
            if(IsShop())
            {
                if(k=="shop-poor"||k=="shop-world-poor"||k=="shop-ch3")return true;
                var offers=CommerceOffers();var offer=offers.ElementAtOrDefault(selectedCandidate);
                if(CommerceCanBuy(offer)){PreviewGold-=offer.price;boughtSlots.Add(ShopSlotKey(selectedCandidate));GrantOffer(offer);receiptOffer=offer;receiptOrigin=k;OpenExtended("reward-receipt",false);}return true;
            }
            if(new[]{"reward","mixed","quota","reward-seven","reward-all-inks","reward-guarantee","boss","boss-last","boss-all-inks"}.Contains(k))
            {
                var offer=CommerceOffers().ElementAtOrDefault(selectedCandidate);if(offer==null)return true;
                if(!k.StartsWith("boss")&&rewardReturn==null&&PreviewRewardCount<=0)return true;
                GrantOffer(offer);receiptOffer=offer;receiptOrigin=k;
                if(!k.StartsWith("boss")&&rewardReturn==null){PreviewRewardCount--;if(commerceItems[offer.id].kind=="ink")earnedInks++;else earnedSketches++;}
                OpenExtended("reward-receipt",false);return true;
            }
            return false;
        }
        bool IsMandatoryReward()=>new[]{"reward","mixed","quota","reward-seven","reward-all-inks","reward-guarantee","boss","boss-last","boss-all-inks"}.Contains(ExtendedKey);
        void FinishCommerceReceipt()
        {
            string origin=receiptOrigin;receiptOffer=null;receiptOrigin=null;
            if(origin!=null&&origin.StartsWith("shop")){OpenExtended(origin,false);return;}
            if(rewardReturn!=null){var back=rewardReturn;rewardReturn=null;OpenExtended(back,false);return;}
            extendedHistory.Clear();ExtendedKey="";Render();
        }
        static string ProductLabel(string name)
        {
            if(name.Length<=10)return name;
            var spaces=Enumerable.Range(0,name.Length).Where(i=>name[i]==' ').ToArray();
            if(spaces.Length==0)return name;int split=spaces.OrderBy(i=>Math.Abs(i-name.Length/2)).First();
            return name.Substring(0,split)+"\n"+name.Substring(split+1);
        }
        NativeNode CopyNative(NativeNode source)
        {
            var n=JsonUtility.FromJson<NativeNode>(JsonUtility.ToJson(source));n.text=source.text;n.artId=source.artId;n.ch=(source.ch??Array.Empty<NativeNode>()).Select(CopyNative).ToArray();return n;
        }
        NativeNode CNode(string id,string name,float x,float y,float w,float h,params NativeNode[] children)=>new NativeNode{id="commerce:"+id,n=name,x=x,y=y,w=w,h=h,ch=children};
        NativeNode CText(string id,string text,float x,float y,float width,float size=28,Color? color=null)
        {
            var t=Text(page,"Measure commerce",text,0,0,width,1,size,color??paper);t.textWrappingMode=TextWrappingModes.Normal;
            float height=Mathf.Ceil(t.GetPreferredValues(text,width,float.PositiveInfinity).y)+6;DestroyImmediate(t.gameObject);
            var c=color??paper;var n=CNode(id,"Bound text",x,y,width,height);n.text=text;n.size=size;n.wrap=true;n.p=new[]{new NativePaint{t="s",c=new[]{c.r,c.g,c.b,c.a}}};return n;
        }
        NativeNode CArt(string id,PreviewContentItem item,float x,float y,float size)
        {
            var n=CNode(id,"svg",x,y,size,size);n.artId=item.kind=="sketch"?"ui-assets-v2/"+item.art:item.art;return n;
        }
        NativeNode[] ItemBody(PreviewOffer offer,string prefix,float width,bool identity=true,bool showAttack=true)
        {
            var item=commerceItems[offer.id];var nodes=new List<NativeNode>();float y=0;
            if(identity){nodes.Add(CArt(prefix+"art",item,0,0,140));var name=CText(prefix+"name",item.name,175,8,width-175,34);nodes.Add(name);if(item.kind=="sketch")nodes.Add(CText(prefix+"skill",item.skill,175,70,width-175,23,muted));y=180;}
            if(item.kind=="sketch")
            {
                foreach(var pair in (showAttack?new[]{("기본 공격",item.attack),("스킬 · "+item.skill,item.description)}:new[]{("스킬 · "+item.skill,item.description)}))
                {var title=CText(prefix+"title"+y,pair.Item1,0,y,width,28);nodes.Add(title);y+=title.h+18;var desc=CText(prefix+"desc"+y,pair.Item2,0,y,width,27);nodes.Add(desc);y+=desc.h+32;}
                nodes.Add(CText(prefix+"palette","팔레트",0,y,width,27));y+=50;
                int i=0;foreach(string color in offer.colors??Array.Empty<string>()){var artNode=CNode(prefix+"palette"+i,"Palette",i*94,y,82,58);artNode.artId="palette/bases/"+color;nodes.Add(artNode);i++;}
            }
            else
            {
                if(!identity&&(item.kind=="maintenance"||item.kind=="seal")){nodes.Add(CText(prefix+"effect-heading","효과",0,y,width,27));y+=50;}
                var desc=CText(prefix+"description",item.description,0,y,width,28);nodes.Add(desc);y+=desc.h+24;
                if(!string.IsNullOrEmpty(item.cost)){if(!identity){nodes.Add(CText(prefix+"cost-heading","대가",0,y,width,27));y+=50;}var cost=CText(prefix+"cost",item.cost,0,y,width,27,new Color32(204,155,146,255));nodes.Add(cost);y+=cost.h+24;}
                if(!string.IsNullOrEmpty(item.note))nodes.Add(CText(prefix+"note",item.note,0,y,width,24,muted));
            }
            return nodes.ToArray();
        }
        NativeNode[] ReceiptBody(PreviewOffer offer)
        {
            var item=commerceItems[offer.id];const float width=970;var body=new List<NativeNode>{CArt("receipt-art",item,0,0,140),CText("receipt-name",item.name,190,18,width-190,34),CText("receipt-stored",item.kind=="ink"?"도감에 등록했습니다.":"가방에 보관했습니다.",190,70,width-190,23,muted)};
            float y=180;body.Add(CommerceRule("receipt-divider",0,y,width));y+=32;
            if(item.kind=="sketch")
            {
                var title=CText("receipt-skill",item.skill,0,y,width,28,muted);body.Add(title);y+=title.h+18;
                var desc=CText("receipt-description",item.description,0,y,width,27);body.Add(desc);y=Math.Max(353,y+desc.h+32);
                body.Add(CommerceRule("receipt-palette-divider",0,y,width));y+=32;body.Add(CText("receipt-palette-heading","팔레트",0,y,width,27));y+=54;
                for(int i=0;i<offer.colors.Length;i++){var a=CNode("receipt-palette"+i,"Palette",i*86,y,64,45);a.artId="palette/bases/"+offer.colors[i];body.Add(a);}
            }
            else body.Add(CText("receipt-description",item.description,0,y,width,28));
            return body.ToArray();
        }
        NativeNode CommerceRule(string id,float x,float y,float w)
        {var n=CNode(id,"Border Top",x,y,w,1);n.p=new[]{new NativePaint{t="s",c=new[]{.6f,.63f,.64f,.22f}}};return n;}
        void SetScrollBody(NativeNode container,NativeNode[] body)
        {
            container.ch=body;container.clip=true;float extent=body.Select(n=>n.y+n.h).DefaultIfEmpty(0).Max();
            if(extent>container.h)
            {var track=CNode(container.id+"track","Scroll track",container.w-10,0,6,container.h);track.p=new[]{new NativePaint{t="s",c=new[]{.21f,.24f,.26f,1}}};var thumb=CNode(container.id+"thumb","Scroll thumb",container.w-10,0,6,Mathf.Max(36,container.h*container.h/extent));thumb.p=new[]{new NativePaint{t="s",c=new[]{.6f,.68f,.7f,1}}};container.ch=body.Concat(new[]{track,thumb}).ToArray();}
        }
        void SetActionLabel(NativeNode actionNode,string value,bool disabled=false)
        {
            var label=Descendants(actionNode).FirstOrDefault(n=>n.text!=null&&n.text!="F"&&n.text!="G");if(label==null)return;
            label.text=value;label.w=Math.Max(label.w,value.Length*label.size);actionNode.w=Math.Max(actionNode.w,label.x+label.w+24);
            if(disabled&&!actionNode.n.Contains("disabled"))actionNode.n+=" disabled";
        }
        NativeNode BindCommerce(NativeNode original)
        {
            LoadCommerce();var root=CopyNative(original);var nodes=Descendants(root).ToArray();var offers=CommerceOffers();
            if((ExtendedKey=="reward-receipt"||ExtendedKey=="boss-ink")&&receiptOffer==null)
            {receiptOffer=new PreviewOffer{id=ExtendedKey=="boss-ink"?"green_basic_attack_shield":"sketch/rapier",colors=new[]{"red"}};receiptOrigin=null;receiptWeaponId=receiptInkIndex=-1;
                if(ExtendedKey=="boss-ink"){receiptInkIndex=Array.FindIndex(art.Package.inks,i=>i.id==receiptOffer.id);commerceInventory.AddInk(receiptInkIndex);}
                else {var sample=commerceInventory.Sketches.FirstOrDefault(w=>w.Art=="rapier"&&w.Colors.Length==1);if(sample!=null){receiptWeaponId=sample.Id;receiptOffer.colors=(string[])sample.Colors.Clone();}}}
            if((ExtendedKey=="reward-receipt"||ExtendedKey=="boss-ink")&&receiptOffer!=null)
            {
                var center=nodes.First(n=>n.n=="center rewardReceipt");var actions=center.ch.First(n=>n.n=="actions");
                var body=CNode("receipt-body","Receipt scroll",0,63,1000,513);SetScrollBody(body,ReceiptBody(receiptOffer));
                center.ch=new[]{center.ch.First(n=>n.n=="gain"),body,actions};
                var buttons=actions.ch.Where(n=>n.n.StartsWith("action")).ToArray();
                bool ink=commerceItems[receiptOffer.id].kind=="ink";
                if(buttons.Length>0){SetActionLabel(buttons[0],ink?"계속":"가방에 넣고 계속");buttons[0].x=0;buttons[0].w=390;}
                if(buttons.Length>1){SetActionLabel(buttons[1],ink?"지금 부여하기":"지금 구성하기",ink&&commerceInventory.Sketches.Count==0);buttons[1].x=440;buttons[1].w=350;}
            }
            if(IsShop()&&offers.Length>0)
            {
                var grid=nodes.FirstOrDefault(n=>n.n=="worldShopGoods")??nodes.First(n=>n.n=="grid");
                bool world=grid.n=="worldShopGoods";grid.h=world?212:612;
                var products=new List<NativeNode>();
                for(int i=0;i<offers.Length;i++)
                {
                    var listing=offers[i];var product=CNode("product-"+i,(world?"worldGood item ":"item ")+(i==selectedCandidate?"selected":""),world?i*270:i%3*228,world?0:i/3*306,world?244:192,world?212:282);
                    if(listing.empty){product.ch=new[]{CText("empty-"+i,"진열할 잉크 없음",0,world?166:198,product.w,22,muted)};products.Add(product);continue;}
                    var info=commerceItems[listing.id];float size=(world?132:140)*(i==selectedCandidate?(world?1.13f:1.08f):(world?1:.82f));var icon=CArt("product-art-"+i,info,(product.w-size)/2,world?4:10,size);
                    var label=CText("product-name-"+i,ProductLabel(info.name),0,166,product.w,i==selectedCandidate?27:24);label.align="CENTER";label.n="name";
                    var price=CText("product-price-"+i,ShopSold(i)?"품절":listing.price<0?"가격 미정":listing.price+" G",0,world?166:238,product.w,23,muted);price.align="CENTER";price.n="price";
                    product.ch=world?new[]{icon,price}:new[]{icon,label,price};products.Add(product);
                }
                grid.ch=products.ToArray();
                var offer=offers[Math.Min(selectedCandidate,offers.Length-1)];var item=commerceItems[offer.id];
                string blocked=offer.empty?"진열할 잉크 없음":ShopSold(selectedCandidate)?"품절":offer.price<0?"가격 미정":PreviewGold<offer.price||ExtendedKey.Contains("poor")?"골드 부족":null;
                var bubble=nodes.FirstOrDefault(n=>n.n=="worldShopBubble");
                if(bubble!=null)
                {
                    bubble.x=Mathf.Clamp(33+selectedCandidate*270+(244-bubble.w)/2,0,1660-bubble.w);
                    var skin=bubble.ch.First(n=>n.n=="speechSkin");var actions=bubble.ch.First(n=>n.n=="actions");
                    var name=CText("shop-bubble-name",item.name,40,60,500,32);
                    var description=CText("shop-bubble-desc",item.kind=="sketch"?item.skill+"\n"+item.description:item.description,40,112,500,25);
                    var body=CNode("shop-bubble-body","Bubble description scroll",40,112,500,100);description.x=description.y=0;SetScrollBody(body,new[]{description});
                    actions.y=216;bubble.ch=new[]{skin,name,body,actions};
                    foreach(var a in actions.ch.Where(n=>n.n.StartsWith("action")))SetActionLabel(a,blocked??"구매 · "+offer.price+" G",blocked!=null);
                }
                else
                {
                    var regions=nodes.Where(n=>n.n=="region active").ToArray();var region=regions.LastOrDefault();
                    var scroll=region?.ch.FirstOrDefault(n=>n.n.Contains("scroll"));if(scroll!=null)SetScrollBody(scroll,offer.empty?new[]{CText("empty-detail","진열할 잉크가 없습니다.",0,20,scroll.w-28)}:ItemBody(offer,"shop-",scroll.w-28));
                    var actionNode=region==null?null:Descendants(region).FirstOrDefault(n=>n.n.Split(' ')[0]=="action");if(actionNode!=null)SetActionLabel(actionNode,blocked??"구매 · "+offer.price+" G",blocked!=null);
                }
            }
            if(ExtendedKey=="augment-choose"||ExtendedKey=="augment-seal")
            {
                var cards=nodes.Where(n=>n.n.StartsWith("card ")).ToArray();
                for(int i=0;i<cards.Length;i++)
                {
                    var card=cards[i];if(i>=offers.Length){card.ch=new[]{CText("no-offer"+i,"제안할 증강 없음",30,50,card.w-60)};card.n+=" disabled";continue;}
                    var item=commerceItems[offers[i].id];var visual=card.ch.First(n=>n.n=="svg");var title=card.ch.First(n=>n.n=="H2");
                    var icon=CArt("augment-art-"+i+"-"+item.id,item,visual.x,visual.y,visual.w);
                    var name=CText("augment-name"+i,item.name,0,0,title.w,34);name.n="name";title.ch=new[]{name};
                    var body=ItemBody(offers[i],"augment-body"+i,card.w-60,false);float offset=title.y+title.h+38;foreach(var b in body){b.x+=30;b.y+=offset;}
                    card.ch=card.ch.Where(n=>n.n.StartsWith("Border")).Concat(new[]{icon,title}).Concat(body).ToArray();
                    if(item.kind=="seal"){var list=card.ch.ToList();float y=list.Where(n=>!n.n.StartsWith("Border")).Max(n=>n.y+n.h)+25;list.Add(CText("stack"+i,OwnedStacks(item.id)+" / "+item.maxStacks+"중첩",30,y,card.w-60,24,muted));card.ch=list.ToArray();}
                    SetScrollBody(card,card.ch);
                }
            }
            if(ExtendedKey=="augment-owned")
            {
                var scroll=nodes.First(n=>n.n=="ownedAugments scroll");var rows=new List<NativeNode>();float y=0;
                foreach(var owned in ownedAugments)
                {
                    var item=owned.Item;string id="owned-"+item.id;rows.Add(CArt(id,item,0,y+4,88));
                    var title=CText(id+"name",item.name+(item.kind=="seal"?" · "+owned.Stacks+"중첩":""),128,y,scroll.w-165,32);rows.Add(title);
                    string copy=item.description+(item.kind=="seal"?" (1중첩당)":"");var desc=CText(id+"desc",copy,128,y+title.h+25,scroll.w-165,27);rows.Add(desc);float end=desc.y+desc.h;
                    if(!string.IsNullOrEmpty(item.cost)){var cost=CText(id+"cost",item.cost,128,end+20,scroll.w-165,26,new Color32(204,155,146,255));rows.Add(cost);end=cost.y+cost.h;}
                    y=Mathf.Max(y+150,end+36);var line=CNode(id+"line","Separator",0,y,scroll.w-32,1);line.p=new[]{new NativePaint{t="s",c=new[]{.32f,.35f,.38f,.5f}}};rows.Add(line);y+=36;
                }
                if(rows.Count==0)rows.Add(CText("owned-empty","획득한 증강이 없습니다.",0,20,scroll.w-30));SetScrollBody(scroll,rows.ToArray());
            }
            nodes=Descendants(root).ToArray();
            foreach(var n in nodes)
            {
                if(n.text==null)continue;
                if((IsShop()||ExtendedKey=="augment"||ExtendedKey=="augment-pending")&&!ExtendedKey.Contains("poor")&&Regex.IsMatch(n.text,@"^\d[\d,]* G$")&&!offers.Any(o=>o.node!=null&&ContainsNode(nodes.FirstOrDefault(x=>x.id==o.node)??root,n.id))){n.text=PreviewGold+" G";n.w=Math.Max(n.w,145);}
                if(n.text.StartsWith("이번 챕터 획득"))n.text=$"이번 챕터 획득 · 스케치 {earnedSketches}　잉크 {earnedInks}";
                if(n.text.StartsWith("이번 런 "))n.text=$"이번 런 {augmentPurchases} / {commerce.augmentLimit}회 · 안정형 1개와 도박형 2개 중 선택";
                if(n.text.StartsWith("증강 구매 ·"))n.text="증강 구매 · "+AugmentPrice+" G";
            }
            if(ExtendedKey=="augment"&&!AugmentPending)
            {
                string reason=augmentPurchases>=commerce.augmentLimit?"최대 구매 횟수":NextAugmentOffer(false).Length!=3?"제안할 후보 부족":PreviewGold<AugmentPrice?"골드 부족":null;
                if(reason!=null){var button=Descendants(root).First(n=>n.n.Split(' ')[0]=="action"&&n.Caption.Contains("증강 구매"));SetActionLabel(button,reason,true);}
            }
            var status=Descendants(root).FirstOrDefault(n=>n.n=="status rewardStatus");if(status!=null){var count=Descendants(status).FirstOrDefault(n=>n.text!=null&&Regex.IsMatch(n.text,@"^\d+$"));if(count!=null)count.text=PreviewRewardCount.ToString();}
            return root;
        }
    }
}
