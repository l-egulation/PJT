using System.Globalization;
using Deviant.Definitions;
using UnityEngine;

namespace Deviant.Combat
{
    public partial class CombatArena
    {
        sealed class TuneFields
        {
            public string basic="1", special="1", speed="1", cooldown="-1";
        }
        readonly TuneFields[] tuneFields={new TuneFields(),new TuneFields()};
        string tuningError;
        readonly System.Collections.Generic.Dictionary<string,PlaytestEnvironment.BasicSpeedTrial> testBasicSpeeds = new System.Collections.Generic.Dictionary<string,PlaytestEnvironment.BasicSpeedTrial>();
        internal float TestBasicSpeed(string form) => testBasicSpeeds.TryGetValue(form,out var trial) ? trial.scale : 1;
        internal float TestLastBasicRecovery(string form) => testBasicSpeeds.TryGetValue(form,out var trial) ? trial.lastHitExtraRecoverySeconds : 0;
        internal void ConfigureTestBasicSpeeds(PlaytestEnvironment.BasicSpeedTrial[] trials)
        {
            var values=new System.Collections.Generic.Dictionary<string,PlaytestEnvironment.BasicSpeedTrial>();
            if(trials!=null)foreach(var trial in trials)
            {
                if(trial==null || string.IsNullOrEmpty(trial.form) ||
                   System.Array.FindIndex(catalog.forms,f=>f.id==trial.form)<0 ||
                   float.IsNaN(trial.scale) || float.IsInfinity(trial.scale) || trial.scale<.05f || trial.scale>20 ||
                   float.IsNaN(trial.lastHitExtraRecoverySeconds) || float.IsInfinity(trial.lastHitExtraRecoverySeconds) ||
                   trial.lastHitExtraRecoverySeconds<0 || trial.lastHitExtraRecoverySeconds>3600 || values.ContainsKey(trial.form))
                    throw new System.InvalidOperationException("Invalid playtest basic speed trial.");
                values.Add(trial.form,new PlaytestEnvironment.BasicSpeedTrial {
                    form=trial.form,scale=trial.scale,lastHitExtraRecoverySeconds=trial.lastHitExtraRecoverySeconds });
            }
            testBasicSpeeds.Clear();
            foreach(var value in values)testBasicSpeeds.Add(value.Key,value.Value);
            foreach(var weapon in Player.weapons)weapon.TestFormSpeedScale=TestBasicSpeed(catalog.forms[weapon.FormIndex].id);
        }
        static bool HasCharacterFrames(CharacterAnimationSet set)
        {
            foreach(var clip in set.clips)foreach(var path in clip.frames)if(!Resources.Load<Sprite>(path))return false;
            if(set.effects!=null)foreach(var effect in set.effects)foreach(var path in effect.frames)if(!Resources.Load<Sprite>(path))return false;
            return true;
        }
        static string NumberField(string label,string value)
        {
            GUILayout.BeginHorizontal(); GUILayout.Label(label,GUILayout.MinWidth(170));
            value=GUILayout.TextField(value,GUILayout.Width(85)); GUILayout.EndHorizontal(); return value;
        }
        static bool Finite(string text,out float value)
        {
            return float.TryParse(text,NumberStyles.Float,CultureInfo.InvariantCulture,out value) && !float.IsNaN(value) && !float.IsInfinity(value);
        }
        public bool ApplyTestWeaponTuning(int slot,string basic,string special,string speed,string cooldown,out string error)
        {
            error=null;
            if(!Player || !Player.EditingAllowed || slot<0 || slot>=Player.weapons.Length)
            {error="설정창을 열거나 적 AI를 정지한 후 적용하세요.";return false;}
            if(!Finite(basic,out var b)||b<0||b>1000 || !Finite(special,out var s)||s<0||s>1000 ||
               !Finite(speed,out var h)||h<.05f||h>20 || !Finite(cooldown,out var c)||c < -1||c>3600 || c<0 && c!=-1)
            {error="피해 배율 0~1000, 공속 배율 0.05~20, 쿨타임 -1(원본) 또는 0~3600초를 입력하세요.";return false;}
            var w=Player.weapons[slot];
            w.TestBasicDamageScale=b;w.TestSpecialDamageScale=s;w.TestSpeedScale=h;w.TestCooldownSeconds=c;
            Player.Reconfigure(slot,w.FormIndex,w.SpecialIndex);
            var f=tuneFields[slot]; f.basic=basic;f.special=special;f.speed=speed;f.cooldown=cooldown;
            return true;
        }
        public void DrawWeaponTuning(int slot,FormData form,WeaponState weapon)
        {
            var fields=tuneFields[slot];
            GUILayout.Space(8);GUILayout.Label("무기 스펙 직접 설정 / 이 슬롯에 적용");
            fields.basic=NumberField("기본 공격력 배율",fields.basic);
            fields.special=NumberField("특수 공격력 배율",fields.special);
            fields.speed=NumberField("추가 기본 공격속도 배율",fields.speed);
            GUILayout.Label("무기별 시험 공속 ×"+weapon.TestFormSpeedScale.ToString("0.####")+" / 추가 배율 1 = 시험 기본값");
            if(TestLastBasicRecovery(form.id)>0)GUILayout.Label("마지막 타 추가 후딜 +"+TestLastBasicRecovery(form.id).ToString("0.###")+"초");
            fields.cooldown=NumberField("특수 쿨타임 초 (-1=원본)",fields.cooldown);
            GUILayout.BeginHorizontal();
            if(GUILayout.Button("스펙 적용"))ApplyTestWeaponTuning(slot,fields.basic,fields.special,fields.speed,fields.cooldown,out tuningError);
            if(GUILayout.Button("슬롯 배율, 쿨타임 초기화"))ApplyTestWeaponTuning(slot,"1","1","1","-1",out tuningError);
            GUILayout.EndHorizontal();
            if(!string.IsNullOrEmpty(tuningError))GUILayout.Label(tuningError);
            var basics=form.basics;
            if(basics!=null && basics.Length>0)
            {
                var attack=basics[0];
                GUILayout.Label("현재 기본 첫 타 피해 "+(attack.damage*(1+weapon.BasicBonus)).ToString("0.##")+
                    " / 모션 "+(attack.duration/weapon.Speed).ToString("0.###")+"초 / 공속 ×"+weapon.Speed.ToString("0.##"));
            }
            GUILayout.Label("특수 피해 ×"+(1+weapon.SpecialBonus).ToString("0.##")+" / 쿨타임 "+weapon.Cooldown.Period.ToString("0.##")+"초");
            GUILayout.Label("피해, 공속은 강화 잉크에 곱해집니다. 특수 공속, 사거리는 원본 유지.");
            GUILayout.Label("F5 리셋 시 설정 유지 / Play 종료 시 초기화");
            GUILayout.Space(8);
        }
    }
}
