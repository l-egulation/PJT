using UnityEngine;

namespace Deviant.Combat
{
    // The fixed-step owner and colliders stay untouched. Only an empty parent of
    // the native Sprite is offset between consecutive simulation positions.
    public struct ActorRenderPoseHistory
    {
        Vector2 previous,current;
        double lastFixed;
        bool ready;
        public Vector2 Latest=>current;
        public void Push(Vector2 position,double fixedTime)
        {
            if(!ready||fixedTime<=lastFixed||Vector2.Distance(position,current)>4)previous=position;
            else previous=current;
            current=position;lastFixed=fixedTime;ready=true;
        }
        public Vector2 Sample(double renderTime,float step)
            =>Vector2.Lerp(previous,current,Mathf.Clamp01((float)((renderTime-lastFixed)/Mathf.Max(.0001f,step))));
    }
    [DefaultExecutionOrder(800)]
    public sealed class ActorRenderInterpolation : MonoBehaviour
    {
        CombatActor actor;
        Transform offset;
        SpriteRenderer drawing;
        ActorRenderPoseHistory history;
        bool wasLedgeClimbing;
        public void Bind(CombatActor target)
        {
            Unbind();actor=target;history=default;wasLedgeClimbing=false;
            if(!actor||!actor.visual||actor.visual.transform==actor.transform)return;
            drawing=actor.visual;
            offset=new GameObject("Render interpolation only").transform;
            offset.SetParent(drawing.transform.parent,false);
            drawing.transform.SetParent(offset,false);
            history.Push(actor.Position,Time.fixedTimeAsDouble);
        }
        bool LedgeClimbing=>actor is CombatPlayer player&&player.WallLedgeClimbing;
        // The ledge art already animates the ascent. Its final short stand is a snap, not ordinary travel:
        // never blend the first standing pose back down toward the held body from the previous tick.
        bool CanInterpolate=>!LedgeClimbing&&!wasLedgeClimbing&&isActiveAndEnabled&&actor&&actor.arena&&!actor.arena.ManualSimulationActive&&
            !actor.arena.Controls.Current.SimulationPaused&&Time.timeScale>0;
        void FixedUpdate(){if(actor)history.Push(actor.Position,Time.fixedTimeAsDouble);}
        public Vector2 RenderPosition()
        {
            if(!actor)return Vector2.zero;
            // Warp/respawn can happen between fixed ticks; never display or
            // follow the previous room for one frame after an explicit snap.
            if(Vector2.Distance(actor.Position,history.Latest)>4)history.Push(actor.Position,Time.fixedTimeAsDouble);
            return CanInterpolate?history.Sample(Time.timeAsDouble,Time.fixedDeltaTime):actor.Position;
        }
        void Update()
        {
            if(!offset||!actor)return;
            if(!CanInterpolate)history.Push(actor.Position,Time.fixedTimeAsDouble);
            Vector2 delta=RenderPosition()-actor.Position;
            offset.localPosition=offset.parent.InverseTransformVector(new Vector3(delta.x,delta.y,0));
            wasLedgeClimbing=LedgeClimbing;
        }
        void OnDisable(){if(offset)offset.localPosition=Vector3.zero;}
        void OnDestroy()
        {
            // Destroying the actor/scene owns this child hierarchy. Reparenting
            // into that dying actor produces a Unity shutdown error. Live art
            // removal explicitly calls Unbind before removing this component.
            actor=null;offset=null;drawing=null;
        }
        public void Unbind()
        {
            if(!offset){actor=null;return;}
            if(drawing&&drawing.transform.parent==offset)drawing.transform.SetParent(offset.parent,false);
            if(Application.isPlaying)Destroy(offset.gameObject);else DestroyImmediate(offset.gameObject);
            offset=null;drawing=null;actor=null;
        }
    }
}
