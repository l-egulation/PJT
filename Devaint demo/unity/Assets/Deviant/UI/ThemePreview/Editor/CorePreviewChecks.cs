using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEditor;
namespace Deviant.ThemePreview
{
    public static partial class ThemePreviewWorkbench
    {
        [MenuItem("Deviant/UI Theme Preview/Run bag crafting equipment checks")]
        public static void VerifyCore(){Directory.CreateDirectory(Output);File.WriteAllText(Path.Combine(Output,"request.txt"),"verify-core");}
        static void CheckLabelBrushes()
        {
            foreach(var t in UnityEngine.Object.FindObjectsByType<TMPro.TextMeshProUGUI>(FindObjectsSortMode.None))
            {
                var rt=t.rectTransform;int index=rt.GetSiblingIndex();if(index==0)continue;
                var previous=rt.parent.GetChild(index-1) as RectTransform;
                if(previous==null||previous.name!="common/maintenance-palette-v1__assets__selection-wash-v1")continue;
                float textWidth=t.name=="Label"?t.GetPreferredValues(t.text).x:Mathf.Min(rt.rect.width,t.GetPreferredValues(t.text,rt.rect.width,Mathf.Infinity).x);
                Expect(previous.rect.width<=textWidth+(t.name=="Label"?101:t.name.StartsWith("CoreTab")?41:29),"Brush follows text: "+t.name);
            }
        }
        static void CheckModel()
        {
            var s=new CorePreviewState(36);s.BeginCraft(0);s.Apply(0,3);s.Apply(1,3);
            Expect(s.InkCount==36&&s.Draft.Applied[0]==s.Draft.Applied[1],"Ink can be reused in multiple slots");
            s.BeginCraft(1);s.BeginCraft(0);Expect(s.Draft.Applied[0]==3,"Draft by sketch ID");
            var w=s.FinalizeCraft();Expect(s.FinalizeCraft()==null,"Finalize cannot run twice");
            Expect(s.Equip(0,w.Id)&&!s.Unequip(0),"Equip and unequip share wait");
            int other=s.Equipped[1];Expect(s.Equip(1,w.Id)&&s.Equipped[0]==other&&s.Cooldown==10,"Swap bypasses and preserves wait");
            s.AdvanceGameplay(10);Expect(s.Unequip(1),"Unequip after gameplay time");
            Expect(s.Rename(w.Id,"새 무기 이름")&&w.Name=="새 무기 이름","Rename updates weapon by ID");
            Expect(!s.Rename(w.Id,"   ")&&!s.Rename(w.Id,new string('가',21)),"Rename rejects empty and over-limit values");
            var empty=new CorePreviewState(0);empty.BeginCraft(0);empty.Apply(0,0);
            Expect(empty.Draft.Applied.All(i=>i==-1),"No ink cannot apply");
            while(empty.Sketches.Count>0){empty.BeginCraft(0);Expect(empty.FinalizeCraft()!=null,"Empty sketch may be crafted");}
            Expect(empty.FinalizeCraft()==null,"Exhausted sketches cannot craft");
        }
        static void BeginCoreChecks()
        {
            File.Delete(Path.Combine(Output,"failure.txt"));File.Delete(Path.Combine(Output,"core-checks.json"));passed.Clear();Screen.ResetCore();
            steps=new Queue<Action>();keyboard=InputSystem.AddDevice<Keyboard>("CorePreviewVerification");nextFrame=Screen.Frames+8;
            steps.Enqueue(()=>Expect(CoreInkText.Value("적을 1초(강화 1.5초) 동안 기절",true)=="1.5초","Duration boosted value"));
            steps.Enqueue(CheckModel);
            steps.Enqueue(()=>Expect(Screen.Inventory.InkCount==36,"All 36 approved inks loaded"));
            steps.Enqueue(CheckLabelBrushes);
            steps.Enqueue(()=>{Expect(UnityEngine.Object.FindObjectsByType<UnityEngine.UI.RawImage>(FindObjectsSortMode.None).Any(x=>x.name=="ApprovedVillage"),"Approved village background loaded");Expect(GameObject.Find("Keycap-Q")!=null&&GameObject.Find("Keycap-E")!=null,"Q E keycaps visible");Expect(GameObject.Find("ColumnDivider")!=null,"Column separator visible");});
            steps.Enqueue(()=>Capture("bag-sketch-1920.png"));
            Press(Key.UpArrow);Press(Key.RightArrow);Press(Key.F);
            for(int i=0;i<8;i++)Press(Key.DownArrow);for(int i=0;i<3;i++)Press(Key.RightArrow);
            steps.Enqueue(()=>Expect(Screen.CorePick==35,"Bag last ink reachable in four columns"));
            steps.Enqueue(()=>Capture("bag-ink-1920.png"));
            Press(Key.E);Press(Key.F);
            steps.Enqueue(()=>Expect(Screen.CoreStage=="slots"&&Screen.Inventory.Draft.Colors.Length==6,"Craft six-slot sketch"));
            for(int i=0;i<5;i++)Press(Key.RightArrow);
            steps.Enqueue(()=>Expect(Screen.CoreSlot==5,"Sixth palette scrolls into view"));steps.Enqueue(()=>Capture("craft-sixth-slot.png"));
            Press(Key.F);Press(Key.RightArrow);Press(Key.F);
            steps.Enqueue(()=>Expect(Screen.Inventory.Draft.Applied[5]==1&&Screen.CoreStage=="slots","Ink applied to selected slot"));
            Press(Key.DownArrow);Press(Key.RightArrow);Press(Key.F);
            steps.Enqueue(()=>Expect(Screen.Inventory.Draft.Applied[5]==-1,"Temporary remove reachable"));
            Press(Key.F);Press(Key.F);Press(Key.Escape);Press(Key.F);
            steps.Enqueue(()=>Expect(Screen.Inventory.Draft.Applied[5]==1,"Draft survives leaving sketch"));
            steps.Enqueue(()=>Capture("craft-slots-1920.png"));
            Press(Key.F);steps.Enqueue(()=>Capture("craft-ink-1920.png"));Press(Key.Escape);
            Press(Key.DownArrow);Press(Key.DownArrow);Press(Key.UpArrow);
            steps.Enqueue(()=>Expect(Screen.CoreStage=="slots","Review button up returns to slots"));
            Press(Key.DownArrow);Press(Key.DownArrow);Press(Key.F);steps.Enqueue(CheckLabelBrushes);steps.Enqueue(()=>Capture("craft-review-1920.png"));Press(Key.F);steps.Enqueue(()=>Capture("craft-done-1920.png"));
            steps.Enqueue(()=>Expect(Screen.Inventory.Weapons.Count==5&&Screen.Inventory.Sketches.Count==8,"Craft consumes sketch once"));
            Press(Key.RightArrow);Press(Key.F);
            steps.Enqueue(()=>Expect(Screen.CoreTab==1&&Screen.CoreStage=="list","Store returns to crafting"));
            Press(Key.E);Press(Key.F);Press(Key.RightArrow);Press(Key.F);
            steps.Enqueue(()=>Expect(Screen.Inventory.Equipped[0]==101,"Equipped weapon swaps slots"));
            Press(Key.RightArrow);Press(Key.F);
            steps.Enqueue(()=>Expect(Screen.Inventory.Cooldown==10&&Screen.Inventory.Equipped[0]==102,"New equip begins wait"));
            Press(Key.LeftArrow);Press(Key.F);
            steps.Enqueue(()=>Expect(Screen.Inventory.Equipped[0]==102,"Cooldown rejects replacement"));
            steps.Enqueue(()=>Capture("equipment-1920.png"));
            Press(Key.S);steps.Enqueue(()=>Expect(Screen.DetailOffset>0,"W S scroll equipment effects"));
            steps.Enqueue(()=>SetSize(960,540));steps.Enqueue(()=>Capture("equipment-960.png"));
            steps.Enqueue(()=>Screen.SwitchCore(1));Press(Key.F);Press(Key.F);steps.Enqueue(()=>Capture("craft-960.png"));
            steps.Enqueue(()=>Screen.SwitchCore(0));Press(Key.UpArrow);Press(Key.RightArrow);Press(Key.F);steps.Enqueue(()=>Capture("bag-960.png"));
            steps.Enqueue(()=>Expect(Screen.Inventory.Cooldown==10,"Menu time does not tick cooldown"));
            steps.Enqueue(()=>{Screen.Inventory.AdvanceGameplay(10);Expect(Screen.Inventory.Cooldown==0,"Only simulated gameplay advances cooldown");});
            Press(Key.Escape);Press(Key.F);steps.Enqueue(()=>Expect(!Screen.CoreClosed,"Root close reopen"));
            steps.Enqueue(()=>SetSize(1920,1080));
            steps.Enqueue(()=>{Screen.ResetCore();Screen.SwitchCore(2);});
            steps.Enqueue(()=>{
                foreach(string name in new[]{"Target0","Target1","EquippedName0","EquippedName1","WeaponCell0","WeaponCell1"})
                    Expect(GameObject.Find(name).GetComponent<TMPro.TextMeshProUGUI>().alignment==TMPro.TextAlignmentOptions.Top,"Centered equipment label: "+name);
                Capture("equipment-target-1920.png");});
            Press(Key.F);steps.Enqueue(()=>Capture("equipment-pick-1920.png"));
            steps.Enqueue(()=>{Screen.ResetCore();Screen.SwitchCore(1);});
            steps.Enqueue(()=>Capture("craft-list-1920.png"));
            Press(Key.F);Press(Key.DownArrow);Press(Key.DownArrow);Press(Key.F);Press(Key.F);
            steps.Enqueue(()=>Capture("craft-empty-done-1920.png"));
            Press(Key.F);steps.Enqueue(()=>Capture("craft-destination-1920.png"));
            steps.Enqueue(()=>{Screen.ResetCore();Screen.SwitchCore(1);});
            Press(Key.F);Press(Key.DownArrow);Press(Key.DownArrow);Press(Key.F);Press(Key.F);Press(Key.F);
            steps.Enqueue(()=>{var b=UnityEngine.Object.FindObjectsByType<UnityEngine.UI.Button>(FindObjectsSortMode.None).First(x=>x.name=="선택한 자리에 장착");b.onClick.Invoke();Expect(Screen.CoreStage=="list","Mouse equip action completes in one click");});
            steps.Enqueue(()=>{Screen.ResetCore(0);Screen.SwitchCore(1);});Press(Key.F);Press(Key.F);
            steps.Enqueue(()=>Expect(Screen.CoreStage=="slots","No ink F stays on slots"));
            steps.Enqueue(()=>Capture("craft-no-ink-1920.png"));
            Press(Key.DownArrow);Press(Key.DownArrow);Press(Key.F);Press(Key.F);Press(Key.RightArrow);Press(Key.F);
            steps.Enqueue(()=>Expect(Screen.CoreStage=="list","Empty craft store returns to list"));
            steps.Enqueue(()=>{Screen.Inventory.Weapons.Clear();Screen.Inventory.Equipped[0]=Screen.Inventory.Equipped[1]=-1;Screen.SwitchCore(2);});
            Press(Key.F);Press(Key.Escape);Press(Key.F);
            steps.Enqueue(()=>Expect(Screen.CoreStage=="target","Empty equipment remains navigable"));
            steps.Enqueue(()=>Capture("equipment-empty-1920.png"));
            steps.Enqueue(()=>{Screen.ResetCore();Screen.SwitchCore(2);});
            Press(Key.F);Press(Key.DownArrow);Press(Key.DownArrow);Press(Key.DownArrow);Press(Key.F);
            steps.Enqueue(()=>Expect(Screen.RenameOpen,"Rename reachable by arrows and F"));
            steps.Enqueue(()=>Capture("rename-1920.png"));
            steps.Enqueue(()=>SetSize(960,540));steps.Enqueue(()=>Capture("rename-960.png"));
            Press(Key.Escape);steps.Enqueue(()=>Expect(!Screen.RenameOpen&&Screen.CoreStage=="renameButton","Rename Esc returns to action"));
            Press(Key.F);steps.Enqueue(()=>UnityEngine.Object.FindFirstObjectByType<TMPro.TMP_InputField>().text="새 무기 이름");
            Press(Key.Enter);steps.Enqueue(()=>Expect(!Screen.RenameOpen&&Screen.Inventory.Weapons[Screen.CorePick].Name=="새 무기 이름","Rename Enter saves"));
            steps.Enqueue(()=>SetSize(1920,1080));
            steps.Enqueue(()=>Screen.ResetCore());
            steps.Enqueue(()=>Expect(GameObject.Find("ReviewOnly")==null,"No review-only footer copy"));
            steps.Enqueue(()=>File.WriteAllText(Path.Combine(Output,"core-checks.json"),"{\"passed\":true,\"checks\":["+string.Join(",",passed.Select(x=>"\""+x.Replace("\"","'")+"\""))+"],\"frame\":"+Screen.Frames+"}"));
        }
    }
}
