using System;
using System.Collections.Generic;
using UnityEngine;

namespace ForestCombatV2
{
    // Chain slash: 5-7 ink lines are drawn one after another, every third through a point near the player and the
    // rest elsewhere in the room, alternating sideways (from the left or right) and upright (from above or below),
    // first one sideways. Ink boils where the chain will come out, and 0.65 s after a line is finished a heavy chain slams along it to the far edge.
    public sealed partial class ClockExecutionerBoss
    {
        public const float ChainLead=.2f,ChainInterval=.25f,ChainDraw=.25f,ChainDelay=.65f,ChainSweep=.15f,ChainRetract=.2f;
        public const int ChainAimEvery=3;
        public const float ChainMissGap=5;
        public const float ChainRadius=.6f,ChainDamage=20,ChainLinkScale=2.5f,ChainInkScale=LongFistHeight*32/192;
        // Side chains dip at most 40 degrees below level; top and bottom chains lean at most 35 degrees off vertical.
        const float ChainSideTilt=40,ChainUprightTilt=35;
        public const float ChainStrike=ChainDraw+ChainDelay; // line age when its chain sets off
        public const float ChainImpact=ChainStrike+ChainSweep;
        public float ChainHold { get; private set; }
        [Serializable] sealed class ChainTiming
        {
            public int schemaVersion;
            public float chainHoldSeconds,maxHealth,enrageDamage,finalPhaseHealth,finalPhaseSpeedMultiplier;
            public float finalPhaseRecoverySeconds,finalPhasePatternGapSeconds;
        }
        ChainTiming combatTiming;
        static bool Positive(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value)&&value>0;
        void LoadChainTiming()
        {
            var json=Resources.Load<TextAsset>("ClockExecutioner/Timing");
            if(!json)throw new InvalidOperationException("Export clock boss timing before starting the encounter.");
            var timing=JsonUtility.FromJson<ChainTiming>(json.text);
            if(timing==null||timing.schemaVersion!=2||!Positive(timing.chainHoldSeconds)||!Positive(timing.maxHealth)
                ||!Positive(timing.enrageDamage)||timing.enrageDamage>=timing.maxHealth||!Positive(timing.finalPhaseHealth)
                ||timing.finalPhaseHealth>=timing.maxHealth-timing.enrageDamage||!Positive(timing.finalPhaseSpeedMultiplier)
                ||timing.finalPhaseSpeedMultiplier<1||timing.finalPhaseRecoverySeconds!=0||timing.finalPhasePatternGapSeconds!=0)
                throw new InvalidOperationException("Invalid clock boss health phases or timing. Export schema 2 combat data.");
            combatTiming=timing;
            ChainHold=timing.chainHoldSeconds;
        }
        // Embedded chains hold for the configured combat seconds, independent of phase/enrage speed.
        float ChainHoldAge=>ChainHold*PatternSpeed;
        public float ChainLineLife=>ChainImpact+ChainHoldAge+ChainRetract;
        struct ChainLine { public Vector2 from,to; public bool impacted; public float holdAge; }
        readonly List<ChainLine> chainLines=new List<ChainLine>();
        int chainCount;
        public int ChainLineCount=>chainCount;
        // Verification only: every line passes through this point instead of one near the player.
        public Vector2? ForcedChainAnchor {get;set;}
        public int AnnouncedChainLines=>chainLines.Count;
        public Vector2 ChainLineFrom(int i)=>chainLines[i].from;
        public Vector2 ChainLineTo(int i)=>chainLines[i].to;
        float LineHoldAge(int index)=>index>=0&&index<chainLines.Count?chainLines[index].holdAge:ChainHoldAge;
        public float ChainSlashDuration=>ChainLead+(chainCount-1)*ChainInterval+ChainImpact+LineHoldAge(chainCount-1)+ChainRetract+RecoverySeconds;
        public static float ChainLineAge(int i,float age)=>age-ChainLead-i*ChainInterval;
        public static float ChainImpactAt(int i)=>ChainLead+i*ChainInterval+ChainImpact;
        // Visual extension only. Damage is a single event when the tip hits the far edge.
        public float ChainReach(float lineAge,int index=-1)
        {
            float a=lineAge-ChainStrike,hold=LineHoldAge(index);
            if(a<0)return 0;
            if(a<ChainSweep)return a/ChainSweep;
            if(a<ChainSweep+hold)return 1;
            return Mathf.Clamp01(1-(a-ChainSweep-hold)/ChainRetract);
        }
        void RetimeChainHolds(float age,float oldSpeed,float newSpeed)
        {
            for(int i=0;i<chainLines.Count;i++)
            {
                var line=chainLines[i];float elapsed=ChainLineAge(i,age)-ChainImpact;
                if(elapsed<0)line.holdAge=ChainHold*newSpeed;
                else if(elapsed<line.holdAge)line.holdAge=elapsed+(line.holdAge-elapsed)*newSpeed/oldSpeed;
                chainLines[i]=line;
            }
        }

        void StartChainSlash(){chainLines.Clear();chainCount=random.Next(5,8);}
        void AnnounceChainLine()
        {
            // Only every third line (1st, 4th, 7th) aims a little around the player, 1-2 units above the feet;
            // the others strike a random spot at least ChainMissGap to the side, 1-8 units up (user: less player-focused).
            var p=arena.Player.Position;Vector2 aim;
            if(ForcedChainAnchor.HasValue)aim=ForcedChainAnchor.Value;
            else if(chainLines.Count%ChainAimEvery==0)aim=new Vector2(Mathf.Clamp(p.x+(float)(random.NextDouble()*4-2),RoomLeft+1,RoomRight-1),Floor+1+(float)random.NextDouble());
            else
            {
                float x=RoomLeft+1+(float)random.NextDouble()*(RoomWidth-2);
                for(int tries=0;tries<8&&Mathf.Abs(x-p.x)<ChainMissGap;tries++)x=RoomLeft+1+(float)random.NextDouble()*(RoomWidth-2);
                aim=new Vector2(x,Floor+1+(float)random.NextDouble()*7);
            }
            // Pick the direction it flies in, then trace back to the edge it comes from.
            float u=(float)random.NextDouble()*2-1,degrees;
            switch(chainLines.Count%2*2+random.Next(2))
            {
                case 0:degrees=-Mathf.Abs(u)*ChainSideTilt;break;        // from the left, level or diving
                case 1:degrees=180+Mathf.Abs(u)*ChainSideTilt;break;     // from the right
                case 2:degrees=-90+u*ChainUprightTilt;break;             // from above
                default:degrees=90+u*ChainUprightTilt;break;             // bursting up from the floor
            }
            var d=new Vector2(Mathf.Cos(degrees*Mathf.Deg2Rad),Mathf.Sin(degrees*Mathf.Deg2Rad));
            float Exit(Vector2 dir)
            {
                float t=float.PositiveInfinity;
                if(Mathf.Abs(dir.x)>1e-5f)t=Mathf.Min(t,((dir.x>0?RoomRight:RoomLeft)-aim.x)/dir.x);
                if(Mathf.Abs(dir.y)>1e-5f)t=Mathf.Min(t,((dir.y>0?Floor+ArenaTop:Floor)-aim.y)/dir.y);
                return t;
            }
            chainLines.Add(new ChainLine{from=aim-d*Exit(-d),to=aim+d*Exit(d),holdAge=ChainHoldAge});
        }
        // Which edge a chain end lies on: 0 left, 1 right, 2 top, 3 floor.
        int ChainEdge(Vector2 at)
        {
            float left=Mathf.Abs(at.x-RoomLeft),right=Mathf.Abs(at.x-RoomRight),top=Mathf.Abs(at.y-Floor-ArenaTop),floor=Mathf.Abs(at.y-Floor);
            float m=Mathf.Min(Mathf.Min(left,right),Mathf.Min(top,floor));
            return m==left?0:m==right?1:m==top?2:3;
        }
        // The wall ink lens boils at the chain's starting edge, half the long fist size, facing into the room.
        void ChainInk(ref int k,float age)
        {
            float offset=96*ChainInkScale/32;
            for(int i=0;i<chainLines.Count;i++)
            {
                float a=ChainLineAge(i,age);if(a<0||a>=ChainStrike+ChainSweep)continue;
                var at=chainLines[i].from;
                switch(ChainEdge(at))
                {
                    case 0:Ink(ref k,"wallInk",a,new Vector2(at.x,at.y-offset),false,3,0,ChainInkScale);break;
                    case 1:Ink(ref k,"wallInk",a,new Vector2(at.x,at.y-offset),true,3,0,ChainInkScale);break;
                    case 2:Ink(ref k,"wallInk",a,new Vector2(at.x-offset,at.y),false,3,-90,ChainInkScale);break;
                    default:Ink(ref k,"wallInk",a,new Vector2(at.x+offset,at.y),false,3,90,ChainInkScale);break;
                }
            }
        }
        // The chain slams the far edge: dust and debris burst away from it, and the view jolts.
        void ChainImpacts(float before,float age)
        {
            for(int i=0;i<chainLines.Count;i++)
            {
                float at=ChainImpactAt(i);
                if(!(before<at&&age>=at))continue;
                var to=chainLines[i].to;float x=ToPxX(to.x),y=ToPxY(to.y);
                float angle=ChainEdge(to) switch{0=>Mathf.PI/2,1=>-Mathf.PI/2,2=>Mathf.PI,_=>0};
                SpawnPreviewEffect("dust",x,y,1.5f,false,angle);SpawnPreviewEffect("needleImpact",x,y,1.2f,false,angle);
                Shake(.25f,.2f);
            }
        }
        void StepChainSlash(float age)
        {
            while(chainLines.Count<chainCount&&ChainLineAge(chainLines.Count,age)>=0)AnnounceChainLine();
            var p=arena.Player;
            for(int i=0;i<chainLines.Count;i++)
            {
                var line=chainLines[i];
                if(line.impacted||age<ChainImpactAt(i))continue;
                // Consume the impact even on a miss or invulnerable target: lingering chains never retry.
                line.impacted=true;chainLines[i]=line;
                
                activeBoxes.Add(new Bounds((line.from+line.to)/2,new Vector3(Mathf.Abs(line.to.x-line.from.x)+ChainRadius*2,Mathf.Abs(line.to.y-line.from.y)+ChainRadius*2,1)));
                if(p.Dead||DashSafe(p)||!SegmentTouches(p.hurtCollider.bounds,line.from,line.to,ChainRadius))continue;
                // Parry: face the side the chain is thrown from; it deals no counter.
                p.ReceiveWithParry(ChainDamage,this,p.Position.x>=(line.from.x+line.to.x)/2?1:-1,line.from,false,out _);
            }
            if(age>=ChainSlashDuration)Finish();
        }
        static bool SegmentTouches(Bounds box,Vector2 a,Vector2 b,float radius)
        {
            var grown=box;grown.Expand(radius*2);
            int steps=Mathf.Max(1,Mathf.CeilToInt(Vector2.Distance(a,b)/.1f));
            for(int s=0;s<=steps;s++){var q=Vector2.Lerp(a,b,s/(float)steps);if(grown.Contains(new Vector3(q.x,q.y,box.center.z)))return true;}
            return false;
        }

        // Preview art: an ink stroke drawn along the line, then rigid chain links up to the chain head.
        void DrawChainSlashArt(float age)
        {
            if(CurrentAttack!=ClockBossAttack.ChainSlash)return;
            for(int i=0;i<chainLines.Count;i++)
            {
                float hold=LineHoldAge(i),a=ChainLineAge(i,age);if(a>=ChainImpact+hold+ChainRetract)continue;
                var line=chainLines[i];
                Vector2 f=new Vector2(ToPxX(line.from.x),ToPxY(line.from.y)),t=new Vector2(ToPxX(line.to.x),ToPxY(line.to.y));
                float fade=a<ChainImpact+hold?1:Mathf.Clamp01(1-(a-ChainImpact-hold)/ChainRetract);
                var tip=Vector2.Lerp(f,t,Mathf.Clamp01(a/ChainDraw));
                if(a<ChainStrike)
                {
                    // Warning: dashed and blinking 8 times a second, so it never reads as a real chain or a pen line.
                    // A paper casing under each ink dash keeps it readable on the dark stone room as well as on paper.
                    float blink=Mathf.Repeat(a*8,1)<.5f?1:.55f;var warning=Hex("#171716",blink);var casing=Hex("#F4ECDE",blink);
                    float drawn=Vector2.Distance(f,tip);var along=drawn>0?(tip-f)/drawn:Vector2.zero;
                    for(float s=0;s<drawn;s+=22)
                    {
                        var d0=f+along*s;var d1=f+along*Mathf.Min(s+14,drawn);
                        PreviewLine(d0.x,d0.y,d1.x,d1.y,11,casing,OrderFront-1,4);PreviewLine(d0.x,d0.y,d1.x,d1.y,7,warning,OrderFront,4);
                    }
                }
                else PreviewLine(f.x,f.y,tip.x,tip.y,7,Hex("#171716",fade),OrderFront,4);
                float reach=ChainReach(a,i);if(reach<=0)continue;
                var u=(t-f).normalized;float distance=Vector2.Distance(f,t)*reach,angle=Mathf.Atan2(-u.x,u.y);
                float pitch=13*ChainLinkScale;
                for(int k=0;k*pitch<=distance;k++){var at=f+u*(k*pitch);Paint(k%2==0?"chainLink_wide":"chainLink_narrow",0,at.x,at.y,OrderFront,5+k*.001f,angle,ChainLinkScale);}
            }
        }
        // Test-room shapes when the preview art is missing.
        void DrawChainSlashFallback(ref int n,float age)
        {
            for(int i=0;i<chainLines.Count;i++)
            {
                float a=ChainLineAge(i,age);if(a>=ChainImpact+LineHoldAge(i)+ChainRetract)continue;
                var line=chainLines[i];
                Line(ref n,line.from,Vector2.Lerp(line.from,line.to,Mathf.Clamp01(a/ChainDraw)),.1f,new Color(.09f,.09f,.09f));
                float reach=ChainReach(a,i);
                if(reach>0)Line(ref n,line.from,Vector2.Lerp(line.from,line.to,reach),ChainRadius*2,new Color(.45f,.42f,.38f));
            }
        }
    }
}
