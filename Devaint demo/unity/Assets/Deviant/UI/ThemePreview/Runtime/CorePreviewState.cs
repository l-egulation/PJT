using System;
using System.Collections.Generic;
using System.Linq;
namespace Deviant.ThemePreview
{
    // Editor preview fixtures only. Never reads or writes a game inventory/save.
    public sealed class PreviewWeapon
    {
        public int Id;
        public string Name, Art, Attack, Skill, SkillDescription;
        public string[] Colors;
        public int[] Applied;
        public float Durability=100;
        public PreviewWeapon Copy() => new PreviewWeapon { Id=Id,Name=Name,Art=Art,Attack=Attack,Skill=Skill,
            SkillDescription=SkillDescription,Colors=(string[])Colors.Clone(),Applied=(int[])Applied.Clone(),Durability=Durability };
    }
    public sealed class CorePreviewState
    {
        public readonly List<PreviewWeapon> Sketches=new List<PreviewWeapon>(), Weapons=new List<PreviewWeapon>();
        readonly Dictionary<int,PreviewWeapon> drafts=new Dictionary<int,PreviewWeapon>();
        public readonly int[] Equipped={-1,-1};
        public readonly List<int> OwnedInkIndices=new List<int>();
        public int InkCount=>OwnedInkIndices.Count;
        public int InkAt(int position)=>OwnedInkIndices[position];
        public void AddInk(int index){if(index>=0&&!OwnedInkIndices.Contains(index))OwnedInkIndices.Add(index);}
        public float Cooldown {get;private set;}
        public PreviewWeapon Draft {get;private set;}
        public CorePreviewState(int inkCount)
        {
            OwnedInkIndices.AddRange(Enumerable.Range(0,inkCount));
            string[] names={"레이피어","발사기","기본검"},arts={"rapier","launcher","base_sword"};
            string[] attacks={"좁은 전방을 빠르게 찌릅니다.","직선으로 탄환을 발사합니다.","전방을 베어 공격합니다."};
            string[] skills={"범위 · 장침","기본 · 모아쏘기","이동 · 돌진 찌르기"};
            string[] desc={"긴 찌르기로 전방의 적을 공격합니다.","힘을 모아 강한 탄환을 발사합니다.","앞으로 이동하며 적을 공격합니다."};
            string[] colors={"red","neutral","blue","yellow","green","purple"};
            for(int i=0;i<9;i++)
            {
                int m=i%3, n=i==0?6:1+i%6;
                Sketches.Add(new PreviewWeapon{Id=i,Name=names[m],Art=arts[m],Attack=attacks[m],Skill=skills[m],SkillDescription=desc[m],
                    Colors=Enumerable.Range(0,n).Select(x=>colors[(x+i)%6]).ToArray(),Applied=Enumerable.Repeat(-1,n).ToArray()});
            }
            for(int i=0;i<4;i++)
            {
                var w=Sketches[i].Copy();w.Id=100+i;w.Durability=100-i*21;
                for(int j=0;j<w.Applied.Length;j++)w.Applied[j]=inkCount>0?(i+j)%inkCount:-1;
                Weapons.Add(w);
            }
            Equipped[0]=100;Equipped[1]=101;
        }
        public void BeginCraft(int index)
        {
            if(index<0||index>=Sketches.Count)return;
            BeginCraftById(Sketches[index].Id);
        }
        // Receipt handoff uses the acquired instance ID; list order may have changed since acquisition.
        // Confirmed weapons are absent from Sketches and can never reenter the palette editor.
        public bool BeginCraftById(int id)
        {
            var s=Sketches.FirstOrDefault(w=>w.Id==id);if(s==null)return false;
            if(!drafts.TryGetValue(s.Id,out var draft)){draft=s.Copy();drafts[s.Id]=draft;}
            Draft=draft;return true;
        }
        public void Apply(int slot,int ink)
        {
            if(Draft==null||slot<0||slot>=Draft.Applied.Length||ink<0||!OwnedInkIndices.Contains(ink))return;
            Draft.Applied[slot]=ink;
        }
        public void Remove(int slot){if(Draft!=null&&slot>=0&&slot<Draft.Applied.Length)Draft.Applied[slot]=-1;}
        public PreviewWeapon FinalizeCraft()
        {
            if(Draft==null||!Sketches.Any(x=>x.Id==Draft.Id))return null;
            var w=Draft.Copy();Weapons.Add(w);Sketches.RemoveAll(x=>x.Id==w.Id);drafts.Remove(w.Id);Draft=null;return w;
        }
        public bool Equip(int target,int id)
        {
            if(target<0||target>1||!Weapons.Any(x=>x.Id==id)||Equipped[target]==id)return false;
            int other=1-target;
            if(Equipped[other]==id){Equipped[other]=Equipped[target];Equipped[target]=id;return true;}
            if(Cooldown>0)return false;
            Equipped[target]=id;Cooldown=10;return true;
        }
        public bool CanEquip(int target,int id)=>Weapons.Any(x=>x.Id==id)&&Equipped[target]!=id&&(Cooldown<=0||Equipped[1-target]==id);
        public bool Unequip(int target)
        {
            if(target<0||target>1||Equipped[target]<0||Cooldown>0)return false;
            Equipped[target]=-1;Cooldown=10;return true;
        }
        public bool Rename(int id,string value)
        {
            var weapon=Weapons.FirstOrDefault(w=>w.Id==id);var name=(value??"").Trim();
            if(weapon==null||name.Length==0||name.Length>20)return false;
            weapon.Name=name;return true;
        }
        public void AdvanceGameplay(float seconds){Cooldown=Math.Max(0,Cooldown-Math.Max(0,seconds));}
        public PreviewWeapon EquippedWeapon(int slot)=>Weapons.FirstOrDefault(x=>x.Id==Equipped[slot]);
    }
}
