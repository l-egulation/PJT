using System;
using System.Linq;
using System.Reflection;
namespace Deviant.ThemePreview
{
    // Pure model checks also run without loading Unity; UI checks run in the existing preview.
    public static class CoreAcquisitionChecks
    {
        static void Check(bool ok,string message){if(!ok)throw new InvalidOperationException(message);}
        public static void RunModel()
        {
            var state=new CorePreviewState(2);
            var begin=typeof(CorePreviewState).GetMethod("BeginCraftById",new[]{typeof(int)});
            Check(begin!=null,"Acquired configuration must address a sketch by stable ID, not its list position");
            var open=(Func<int,bool>)Delegate.CreateDelegate(typeof(Func<int,bool>),state,begin);
            var acquired=state.Sketches[0].Copy();acquired.Id=900;state.Sketches.Add(acquired);
            Check(open(900)&&state.Draft.Id==900,"Open the actual acquired instance");
            state.Apply(5,1);state.BeginCraft(1);state.Sketches.Reverse();
            Check(open(900)&&state.Draft.Applied[5]==1,"Reopen the same draft after list reorder without another grant");
            Check(state.Sketches.Count==10&&state.InkCount==2,"Opening and applying do not grant or consume content");
            var confirmed=state.FinalizeCraft();
            Check(confirmed!=null&&confirmed.Applied[0]==-1&&confirmed.Applied[5]==1,"Confirm preserves empty and applied slots");
            Check(!open(confirmed.Id)&&state.Draft==null,"Confirmed weapon cannot reopen for ink placement, even with empty slots");
            state.Apply(0,1);
            Check(confirmed.Applied[0]==-1&&confirmed.Applied[5]==1,"Confirmed palette stays immutable");
            Check(state.FinalizeCraft()==null&&state.Weapons.Count==5&&state.Sketches.Count==9,"Repeated completion cannot duplicate acquisition");
            Check(open(state.Sketches[0].Id),"Another unconfirmed sketch remains configurable");
            var draft=state.Draft;
            Check(!open(-1)&&ReferenceEquals(draft,state.Draft),"Invalid ID cannot change the active draft");
            state.Apply(0,99);Check(state.Draft.Applied[0]==-1,"Unregistered ink is rejected");
        }
#if UNITY_EDITOR
        public static void RunScreen(ThemePreviewScreen screen)
        {
            screen.ResetCore(2);
            var state=screen.Inventory;var acquired=state.Sketches[0].Copy();acquired.Id=900;state.Sketches.Add(acquired);
            screen.OpenAcquiredConfiguration(900,-1);
            Check(screen.CoreTab==1&&screen.CoreStage=="slots"&&state.Draft.Id==900,"Receipt opens exact acquired palette");
            screen.HandleCore("Right");screen.HandleCore("F");screen.HandleCore("Right");screen.HandleCore("F");
            Check(state.Draft.Applied[1]==1,"Native arrows and F apply selected ink");
            screen.HandleCore("Esc");Check(screen.CoreClosed,"Acquired palette Esc closes to caller");
            screen.OpenAcquiredConfiguration(900,-1);
            Check(state.Draft.Applied[1]==1&&state.Sketches.Count==10,"Reentry preserves draft and does not grant again");
            state.AddInk(5);screen.OpenAcquiredConfiguration(-1,5);
            Check(screen.CoreStage=="acquiredTargets","Ink acquisition first selects an unconfirmed sketch");
            screen.HandleCore("F");
            Check(screen.CoreStage=="acquiredSlots"&&state.Draft.Id==0,"Target selection opens owned sketch");
            screen.HandleCore("F");
            Check(state.Draft.Applied[0]==5&&state.InkCount==3,"Acquired ink applies to empty slot without consumption");
            screen.HandleCore("Esc");Check(screen.CoreClosed,"Ink configuration Esc closes to caller");
            screen.OpenAcquiredConfiguration(-1,5);screen.HandleCore("F");
            Check(screen.CoreSlot==1,"Ink flow starts at next empty slot, preserving prior placement");
            screen.HandleCore("Left");screen.HandleCore("F");
            Check(screen.CoreStage=="acquiredSlots"&&state.Draft.Applied[0]==5,"Acquisition shortcut cannot overwrite filled draft slot");
            screen.HandleCore("Down");screen.HandleCore("Right");screen.HandleCore("F");
            Check(screen.CoreStage=="acquiredTargets"&&state.Draft.Applied[0]==5,"Other sketch action preserves the draft");
            screen.HandleCore("F");
            var confirmed=state.FinalizeCraft();
            screen.OpenAcquiredConfiguration(confirmed.Id,-1);
            Check(state.Draft==null&&screen.CoreStage=="list","Confirmed weapon cannot enter palette editor");
            state.Sketches.Clear();screen.OpenAcquiredConfiguration(-1,5);screen.HandleCore("F");
            Check(screen.CoreStage=="acquiredTargets"&&state.Weapons.Count==5,"No sketch never exposes confirmed weapons as ink targets");
            screen.HandleCore("Esc");Check(screen.CoreClosed,"No-target view is dismissible");
        }
#endif
    }
}
