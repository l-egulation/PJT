using System;
using Deviant.Definitions;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Deviant.Authoring
{
    // Pure analytic flight sample, not a second collision/fuse/return/missile
    // runner. It owns no arena, action handle, clock, input or file-write port.
    public sealed class RangedActionGeometryPreview : VisualElement
    {
        readonly Slider time;
        readonly Toggle left;
        readonly Label diagnostic;
        readonly IMGUIContainer canvas;
        RangedActionDefinition action;
        RangedActionNumericData numeric;
        readonly Vector3[] line = new Vector3[2];
        public float SampleTime => time.value;
        public RangedActionGeometryPreview()
        {
            name="ranged-geometry";
            time=new Slider("Read-only seconds",0,1){name="geometry-time",showInputField=true};time.RegisterValueChangedCallback(_=>canvas.MarkDirtyRepaint());Add(time);
            left=new Toggle("Left-facing mirror"){name="geometry-left"};left.RegisterValueChangedCallback(_=>canvas.MarkDirtyRepaint());Add(left);
            diagnostic=new Label {name="geometry-diagnostic"};diagnostic.style.whiteSpace=WhiteSpace.Normal;Add(diagnostic);
            canvas=new IMGUIContainer(Draw){name="geometry-canvas"};canvas.style.height=280;canvas.style.flexShrink=0;Add(canvas);
        }
        public void SetCandidate(RangedActionDefinition candidate)
        {
            action=candidate;numeric=candidate?.ToNumericData();
            if(candidate==null){diagnostic.text="Invalid candidate; no stale geometry is displayed.";canvas.MarkDirtyRepaint();return;}
            float flight=0;
            if(numeric.ranged!=null&&numeric.ranged.speed>0)flight=numeric.ranged.distance/numeric.ranged.speed;
            else if(numeric.bomb!=null)flight=numeric.bomb.lifetime;
            else if(numeric.missile!=null)flight=numeric.missile.distance/numeric.missile.speed;
            time.highValue=Mathf.Max(numeric.end,numeric.activation+flight);time.SetValueWithoutNotify(Mathf.Min(time.value,time.highValue));
            diagnostic.text=candidate.Id+" · "+candidate.Policy+" · collision-free analytic sample only. No terrain/contact/landing/fuse/return destination is invented; actual runtime remains authoritative. No Sprite asset is inferred from a projectile shape.";
            if (numeric.ranged?.unlimitedRange == true) diagnostic.text += " Unlimited horizontal beam; preview ends at the canvas edge only.";
            canvas.MarkDirtyRepaint();
        }
        void Draw()
        {
            var rect=new Rect(0,0,canvas.contentRect.width,canvas.contentRect.height);EditorGUI.DrawRect(rect,new Color(.07f,.09f,.12f));if(action==null)return;
            float direction=left.value?-1:1;float span=numeric.ranged?.distance??numeric.missile?.distance??numeric.bomb.speed*numeric.bomb.lifetime;
            float scale=Mathf.Min(32,(rect.width-40)/Mathf.Max(8,span+4));Vector2 origin=new Vector2(left.value?rect.width-35:35,rect.height*.65f);
            Vector2 Pixel(Vector2 point)=>origin+new Vector2(point.x*scale,-point.y*scale);
            Handles.BeginGUI();
            try
            {
                Handles.color=new Color(.25f,.3f,.35f);for(int i=-2;i<32;i++){line[0]=Pixel(new Vector2(i,-8));line[1]=Pixel(new Vector2(i,8));Handles.DrawAAPolyLine(1,line);}
                Handles.color=Color.cyan;Vector2 center=new Vector2(direction*.1875f,1);DrawBox(Pixel(center),new Vector2(.9375f,2)*scale);
                float age=Mathf.Max(0,time.value-numeric.activation);bool released=time.value>=numeric.activation;Handles.color=released?new Color(1,.55f,.1f):Color.gray;
                if(numeric.ranged!=null)
                {
                    var r=numeric.ranged;float x=r.spawnX>0?r.spawnX:.65625f+r.radius+r.spawnGap;Vector2 muzzle=new Vector2(direction*x,r.spawnHeight);
                    if(action.Policy==RangedActionPolicy.LaserAllOnce){Vector2 a=Pixel(muzzle),b=r.unlimitedRange?new Vector2(left.value?0:rect.width,a.y):Pixel(muzzle+Vector2.right*direction*r.distance);Handles.DrawAAPolyLine(Mathf.Max(1,r.thickness*scale),new Vector3[]{a,b});}
                    else if(action.Policy==RangedActionPolicy.PiercingPellets){foreach(float angle in r.angles)Projectile(muzzle,new Vector2(direction*Mathf.Cos(angle*Mathf.Deg2Rad),Mathf.Sin(angle*Mathf.Deg2Rad))*Mathf.Min(r.distance,r.speed*age),r.radius,Pixel,scale);}
                    else Projectile(muzzle,Vector2.right*direction*Mathf.Min(r.distance,r.speed*age),r.radius,Pixel,scale);
                }
                else if(numeric.bomb!=null)
                {
                    var b=numeric.bomb;Vector2 muzzle=new Vector2(direction*(.65625f+b.radius+b.spawnGap),b.spawnHeight);float radians=b.angle*Mathf.Deg2Rad;
                    Vector2 delta=new Vector2(direction*Mathf.Cos(radians)*b.speed*age,Mathf.Sin(radians)*b.speed*age-.5f*b.gravity*age*age);Projectile(muzzle,delta,b.radius,Pixel,scale);
                    Handles.color=new Color(1,.25f,.1f);Handles.DrawWireDisc(Pixel(muzzle+delta),Vector3.forward,b.blastRadius*scale);
                }
                else if(numeric.missile!=null){var m=numeric.missile;Vector2 end=new Vector2(direction*Mathf.Min(m.distance,m.speed*age),1);DrawBox(Pixel(end),new Vector2(.9375f,2)*scale);}
            }
            finally{Handles.EndGUI();}
        }
        void DrawBox(Vector2 center,Vector2 size){Handles.DrawWireCube(center,new Vector3(size.x,size.y,0));}
        void Projectile(Vector2 muzzle,Vector2 delta,float radius,Func<Vector2,Vector2> pixel,float scale){line[0]=pixel(muzzle);line[1]=pixel(muzzle+delta);Handles.DrawAAPolyLine(2,line);Handles.DrawWireDisc(pixel(muzzle+delta),Vector3.forward,radius*scale);}
    }
}
