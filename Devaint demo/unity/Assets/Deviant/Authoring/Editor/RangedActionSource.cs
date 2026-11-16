using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Deviant.Combat;
using Deviant.Definitions;
using UnityEngine;

namespace Deviant.Authoring
{
    public sealed class RangedActionSourceSnapshot
    {
        public string Repository { get; }
        public string RangedHash { get; }
        public string LauncherHash { get; }
        public RangedActionCatalog Catalog { get; }
        internal RangedActionSourceSnapshot(string repository, string rangedHash, string launcherHash, RangedActionCatalog catalog)
        { Repository = repository; RangedHash = rangedHash; LauncherHash = launcherHash; Catalog = catalog; }
    }
    // Existing JSON objects remain the sole numeric source. This transports their
    // approved closed policies into the same C# compiler as fresh runtime startup.
    public static class RangedActionSource
    {
        public const string RangedPath = "data/combat/ranged_basic_design.json", LauncherPath = "data/combat/launcher_skills.json";
        public const int MaximumFileBytes = 1024 * 1024;
        public static RangedActionSourceSnapshot Load(string repository)
        {
            var ranged = BasicMeleeSource.Parse(SourceFiles.Read(Path.Combine(repository, RangedPath), out var rangedHash, MaximumFileBytes));
            var launcher = BasicMeleeSource.Parse(SourceFiles.Read(Path.Combine(repository, LauncherPath), out var launcherHash, MaximumFileBytes));
            return new RangedActionSourceSnapshot(repository, rangedHash, launcherHash, Compile(ranged, launcher));
        }
        public static void ValidateGenerated(string repository, Catalog generated)
        {
            var source = Load(repository).Catalog;
            if (!RangedActionCatalog.TryCreate(generated, out var actual, out var error)) throw new ArgumentException(error);
            foreach (var action in source.Actions)
                if (actual.Read(action.Id)?.ContentHash != action.ContentHash)
                    throw new ArgumentException("Generated ranged action differs from canonical source: " + action.Id + ". Export the catalog before building.");
        }
        public static RangedActionSourceSnapshot Save(RangedActionSourceSnapshot loaded, RangedActionCatalog candidate, string selectedId)
        {
            if (loaded == null || candidate == null) throw new ArgumentNullException(nameof(loaded));
            if (!loaded.Catalog.TryReplace(candidate.Actions, out _, out var error)) throw new ArgumentException(error);
            var selected = candidate.Read(selectedId) ?? throw new ArgumentException("Select an existing canonical action.");
            CheckBoth(loaded);
            var ranged = BasicMeleeSource.Parse(SourceFiles.Read(Path.Combine(loaded.Repository, RangedPath), out _, MaximumFileBytes));
            var launcher = BasicMeleeSource.Parse(SourceFiles.Read(Path.Combine(loaded.Repository, LauncherPath), out _, MaximumFileBytes));
            bool conversionFile = selected.FormId == "launcher" && !selected.IsBasic;
            var beforeOther = conversionFile ? loaded.RangedHash : loaded.LauncherHash;
            if (conversionFile) WriteLauncher(launcher, candidate);
            else WriteRanged(ranged, candidate);
            var serialized = Compile(ranged, launcher);
            foreach (var action in candidate.Actions)
                if (serialized.Read(action.Id).ContentHash != action.ContentHash) throw new ArgumentException("Candidate changes cross canonical file ownership. Save one owning source family at a time: " + action.Id);
            CheckBoth(loaded);
            string relative = conversionFile ? LauncherPath : RangedPath, expected = conversionFile ? loaded.LauncherHash : loaded.RangedHash;
            SourceFiles.Save(Path.Combine(loaded.Repository, relative), expected, BasicMeleeSource.Serialize(conversionFile ? launcher : ranged), MaximumFileBytes);
            SourceFiles.Read(Path.Combine(loaded.Repository, conversionFile ? RangedPath : LauncherPath), out var afterOther, MaximumFileBytes);
            if (afterOther != beforeOther) throw new IOException("The other canonical ranged source changed concurrently; reload both sources. The selected file was saved atomically, not as a cross-file transaction.");
            return Load(loaded.Repository);
        }
        static void CheckBoth(RangedActionSourceSnapshot loaded)
        {
            SourceFiles.Read(Path.Combine(loaded.Repository, RangedPath), out var ranged, MaximumFileBytes);
            SourceFiles.Read(Path.Combine(loaded.Repository, LauncherPath), out var launcher, MaximumFileBytes);
            if (ranged != loaded.RangedHash || launcher != loaded.LauncherHash) throw new IOException("Ranged sources changed outside this window. Reload both before saving; nothing was overwritten.");
        }
        static RangedActionCatalog Compile(XElement root, XElement launcherSkills)
        {
            if (Text(launcherSkills, "status") != "user_approved_launcher_ink_skills_20261106" || Text(Member(root, "launcher"), "attack_model") != "S_ball_always_D_skill_selected_by_ink")
                throw new ArgumentException("The approved launcher S ball plus ink-selected D skill policy is required.");
            var l = Member(root, "launcher"); var s = Member(root, "shuriken"); var b = Member(root, "bomb");
            var original = new BasicData { name = "볼 탄환", kind = "ranged", damage = N(l,"damage"),duration=N(l,"base_motion_duration"),impact=N(l,"windup"),active=.001f,
                ranged = new RangedData { type="ball",speed=N(l,"speed"),distance=N(l,"range_units"),radius=N(Member(l,"hit_shape"),"radius"),spawnX=Coordinate(l,0),spawnHeight=Coordinate(l,1),angles=new[]{0f} } };
            var launcherSpecials = new[] { "basic", "laser", "shotgun", "grenade" }.Select(key => LauncherSkill(launcherSkills, key)).ToArray();
            var bomb = new BombData { angle=N(b,"angle_degrees"),speed=N(b,"initial_speed"),gravity=N(b,"gravity"),radius=N(Member(b,"hit_shape"),"radius"),blastRadius=N(b,"explosion_radius"),
                lifetime=N(Member(b,"implementation_defaults"),"cleanup_lifetime_seconds"),spawnHeight=N(Member(b,"implementation_defaults"),"spawn_height"),spawnGap=N(Member(b,"implementation_defaults"),"spawn_gap") };
            var bombSkills = new SkillData[4]; var burst=Member(b,"basic_special_design"); var area=Member(b,"range_special_design"); var move=Member(b,"movement_special_design"); var follow=Member(b,"followup_special_design");
            var burstAngles=Numbers(burst,"angles_degrees");
            if(burstAngles.Length!=3||burstAngles.Any(angle=>angle!=burstAngles[0]))throw new ArgumentException("The current burst has one shared actual throw angle; ignored per-release angles are not accepted.");
            var burstBomb=Clone(bomb); burstBomb.angle=Numbers(burst,"angles_degrees")[0]; burstBomb.blastRadius=N(burst,"explosion_radius");
            bombSkills[0]=Skill("bomb_basic_special","폭탄 3연속 투척","base","bomb",burst,N(burst,"windup"),N(burst,"motion_duration"),Numbers(burst,"local_release_times"),N(burst,"damage_per_bomb"),"bomb_projectile"); bombSkills[0].bomb=burstBomb;
            var areaBomb=Clone(bomb); areaBomb.angle=N(area,"angle_degrees");areaBomb.speed=N(area,"initial_speed");areaBomb.radius=N(area,"projectile_diameter")/2;areaBomb.blastRadius=N(area,"explosion_radius");
            bombSkills[1]=Skill("bomb_range_special","대형 폭탄","range","bomb",area,N(area,"windup"),N(area,"motion_duration"),new[]{N(area,"windup")},N(area,"damage"),"bomb_projectile");bombSkills[1].bomb=areaBomb;
            var missile=new MissileData { speed=N(move,"speed"),distance=N(move,"max_distance"),blastRadius=N(move,"explosion_radius"),recovery=N(move,"recovery"),manualDelay=N(move,"manual_detonation_min_flight_age"),recoilHeight=N(move,"recoil_height"),recoilDistance=N(move,"recoil_distance"),distanceBonusMaxDamage=OptionalNumber(move,"distance_bonus_max_total_damage",0),distanceBonusFullDistance=OptionalNumber(move,"distance_bonus_full_units",0) };
            bombSkills[2]=Skill("bomb_move_special","미사일 변신","move","missile",move,N(move,"windup"),(float)(D(move,"windup")+D(move,"max_distance")/D(move,"speed")+D(move,"recovery")),new[]{N(move,"windup")},N(move,"explosion_damage"),"missile_explosion");bombSkills[2].missile=missile;
            var timed=Clone(bomb);timed.angle=N(follow,"angle_degrees");timed.speed=N(follow,"initial_speed");timed.radius=N(follow,"projectile_diameter")/2;timed.blastRadius=N(follow,"explosion_radius");timed.gravity=N(follow,"gravity");timed.fuseSeconds=N(follow,"fuse_seconds");timed.lifetime=N(follow,"unlanded_timeout_seconds");timed.explodeOnExpiry=true;
            bombSkills[3]=Skill("bomb_followup_special","착지 시한폭탄","followup","bomb",follow,N(follow,"windup"),N(follow,"motion_duration"),new[]{N(follow,"windup")},N(follow,"damage"),"bomb_projectile");bombSkills[3].bomb=timed;
            var shot=new RangedData { type="shuriken",speed=N(s,"speed"),distance=N(s,"range_units"),radius=N(Member(s,"hit_shape"),"radius"),spawnX=Coordinate(s,0),spawnHeight=Coordinate(s,1),angles=new[]{0f} };
            var homing=BasicMeleeSource.Optional(s,"homing");
            if(homing!=null)
            {
                shot.homingTurnRate=N(homing,"turn_rate_degrees_per_second");
                shot.homingCone=N(homing,"acquire_cone_half_angle_degrees");
                shot.homingMaxAngle=N(homing,"max_deflection_degrees");
            }
            var damage=Numbers(s,"damages");var ends=Numbers(s,"motion_durations");var releases=Numbers(s,"local_release_times");
            if(damage.Length!=2||ends.Length!=2||releases.Length!=2)throw new ArgumentException("Two existing shuriken basic records are required.");
            var shurikenSkills=new SkillData[4];
            for(int i=0;i<4;i++)
            {
                string key=new[]{"basic","range","movement","followup"}[i]+"_special_design"; var node=Member(s,key);
                var r=new RangedData { type=new[]{"shuriken_special","shuriken_range","shuriken_move","shuriken_followup"}[i],pierce=i==0||i==3||i==2&&bool.Parse(Member(node,"pierce").Value),noBasicStagger=true,speed=N(node,"speed"),distance=N(node,"range_units"),radius=N(node,i==0?"diameter":"projectile_diameter")/2,spawnX=Coordinate(node,0),spawnHeight=Coordinate(node,1),angles=new[]{0f} };
                if(i==1)r.blastRadius=N(node,"explosion_radius");if(i==2){r.returnDelay=N(node,"return_delay_after_arrival_seconds");r.behindGap=N(node,"behind_target_gap");}if(i==3)r.hitInterval=N(node,"per_target_hit_interval");
                float end=i==3?(float)(D(node,"windup")+D(node,"recovery")):N(node,"motion_duration");
                shurikenSkills[i]=Skill("shuriken_"+new[]{"basic","range","move","followup"}[i]+"_special",new[]{"큰 관통 수리검","폭발 수리검","수리검 뒤이동·복귀","전진 회전 수리검"}[i],new[]{"base","range","move","followup"}[i],"ranged",node,N(node,"windup"),end,new[]{N(node,"windup")},N(node,i==1?"explosion_damage":i==3?"tick_damage":"damage"),"ranged_projectile");shurikenSkills[i].inputSpeed=5;shurikenSkills[i].ranged=r;
            }
            var forms=new[] {
                new FormData {id="launcher",name="발사기",inputSpeed=0,basics=new[]{original},specials=launcherSpecials},
                new FormData {id="bomb",name="폭탄",inputSpeed=0,basics=new[]{new BasicData{kind="bomb",damage=N(b,"damage"),duration=N(b,"motion_duration"),impact=N(b,"windup"),active=.001f,bomb=bomb}},specials=bombSkills},
                new FormData {id="shuriken",name="수리검",inputSpeed=N(s,"ground_input_speed"),basics=Enumerable.Range(0,2).Select(i=>new BasicData{kind="ranged",damage=damage[i],duration=ends[i],impact=releases[i],active=.001f,ranged=shot}).ToArray(),specials=shurikenSkills}
            };
            if(!RangedActionCatalog.TryCreate(new Catalog{forms=forms},out var result,out var error))throw new ArgumentException(error);return result;
        }
        static void WriteLauncher(XElement root, RangedActionCatalog candidate)
        {
            for (int i=0;i<4;i++)
            {
                var key=new[]{"basic","laser","shotgun","grenade"}[i];var node=Member(root,key);
                var numeric=candidate.Read(Text(node,"id")).ToNumericData();
                var charge=BasicMeleeSource.Optional(node,"charge");
                if(charge!=null){Set(Member(charge,"min_damage"),numeric.damages[0]);Set(Member(charge,"min_hold"),numeric.activation);}
                else Set(Member(node,"damage"),numeric.damages[0]);
                Set(Member(node,"windup"),numeric.activation);Set(Member(node,"duration"),numeric.end);Set(Member(node,"recovery"),numeric.end-numeric.activation);
                var media=Member(node,i==3?"bomb":"ranged");
                if(i==3)WriteNumericObject(media,numeric.bomb);else WriteNumericObject(media,numeric.ranged);
            }
        }
        static void WriteRanged(XElement root,RangedActionCatalog candidate)
        {
            var l=Member(root,"launcher");var launcher=candidate.Read("launcher_basic_0").ToNumericData();var r=launcher.ranged;
            Set(Member(l,"damage"),launcher.damages[0]);Set(Member(l,"windup"),launcher.activation);Set(Member(l,"base_motion_duration"),launcher.end);Set(Member(l,"recovery"),launcher.end-launcher.activation);
            Set(Member(l,"speed"),r.speed);Set(Member(l,"range_units"),r.distance);Set(Member(l,"base_flight_seconds"),(double)r.distance/r.speed);Shape(Member(l,"hit_shape"),r.radius);Spawn(l,r.spawnX,r.spawnHeight);
            var s=Member(root,"shuriken");var basics=new[]{candidate.Read("shuriken_basic_0").ToNumericData(),candidate.Read("shuriken_basic_1").ToNumericData()};var shot=basics[0].ranged;
            SetArray(Member(s,"damages"),basics.Select(n=>n.damages[0]).ToArray());SetArray(Member(s,"motion_durations"),basics.Select(n=>n.end).ToArray());SetArray(Member(s,"local_release_times"),basics.Select(n=>n.activation).ToArray());
            SetArray(Member(s,"fastest_chain_release_times"),new[]{basics[0].activation,basics[0].end+basics[1].activation});Set(Member(s,"fastest_cycle_seconds"),basics[0].end+basics[1].end);
            Set(Member(s,"speed"),shot.speed);Set(Member(s,"range_units"),shot.distance);Set(Member(s,"base_flight_seconds"),(double)shot.distance/shot.speed);Shape(Member(s,"hit_shape"),shot.radius);Spawn(s,shot.spawnX,shot.spawnHeight);
            for(int i=0;i<4;i++)
            {
                var action=candidate.Read("shuriken_"+new[]{"basic","range","move","followup"}[i]+"_special");var n=action.ToNumericData();r=n.ranged;var node=Member(s,new[]{"basic","range","movement","followup"}[i]+"_special_design");
                Set(Member(node,i==1?"explosion_damage":i==3?"tick_damage":"damage"),n.damages[0]);Set(Member(node,"windup"),n.activation);Set(Member(node,"recovery"),n.end-n.activation);
                var end=BasicMeleeSource.Optional(node,"motion_duration");if(end!=null)Set(end,n.end);
                Set(Member(node,"speed"),r.speed);Set(Member(node,"range_units"),r.distance);Set(Member(node,i==0?"diameter":"projectile_diameter"),r.radius*2);Spawn(node,r.spawnX,r.spawnHeight);
                if(i==1)Set(Member(node,"explosion_radius"),r.blastRadius);if(i==2){Set(Member(node,"return_delay_after_arrival_seconds"),r.returnDelay);Set(Member(node,"behind_target_gap"),r.behindGap);}if(i==3){Set(Member(node,"per_target_hit_interval"),r.hitInterval);Set(Member(node,"bottom_height"),r.spawnHeight-r.radius);}
            }
            var b=Member(root,"bomb");var basic=candidate.Read("bomb_basic_0").ToNumericData();var bomb=basic.bomb;
            Set(Member(b,"damage"),basic.damages[0]);Set(Member(b,"windup"),basic.activation);Set(Member(b,"motion_duration"),basic.end);Set(Member(b,"recovery"),basic.end-basic.activation);
            Set(Member(b,"angle_degrees"),bomb.angle);Set(Member(b,"initial_speed"),bomb.speed);Set(Member(b,"gravity"),bomb.gravity);Set(Member(b,"explosion_radius"),bomb.blastRadius);Shape(Member(b,"hit_shape"),bomb.radius);
            var defaults=Member(b,"implementation_defaults");Set(Member(defaults,"cleanup_lifetime_seconds"),bomb.lifetime);Set(Member(defaults,"spawn_height"),bomb.spawnHeight);Set(Member(defaults,"spawn_gap"),bomb.spawnGap);
            for(int i=0;i<4;i++)
            {
                var n=candidate.Read("bomb_"+new[]{"basic","range","move","followup"}[i]+"_special").ToNumericData();var node=Member(b,new[]{"basic","range","movement","followup"}[i]+"_special_design");
                Set(Member(node,"windup"),n.activation);
                if(i==2)
                {
                    var m=n.missile;Set(Member(node,"explosion_damage"),n.damages[0]);Set(Member(node,"speed"),m.speed);Set(Member(node,"max_distance"),m.distance);Set(Member(node,"explosion_radius"),m.blastRadius);Set(Member(node,"recovery"),m.recovery);
                    Set(Member(node,"normal_max_flight_seconds"),(double)m.distance/m.speed);Set(Member(node,"manual_detonation_min_flight_age"),m.manualDelay);Set(Member(node,"recoil_height"),m.recoilHeight);Set(Member(node,"recoil_distance"),m.recoilDistance);continue;
                }
                bomb=n.bomb;Set(Member(node,i==0?"damage_per_bomb":"damage"),n.damages[0]);Set(Member(node,"motion_duration"),n.end);Set(Member(node,"recovery"),n.end-n.releaseTimes[n.releaseTimes.Length-1]);Set(Member(node,"explosion_radius"),bomb.blastRadius);
                if(i==0)
                {
                    SetArray(Member(node,"local_release_times"),n.releaseTimes);Set(Member(node,"release_interval"),n.releaseTimes[1]-n.releaseTimes[0]);SetArray(Member(node,"angles_degrees"),Enumerable.Repeat(bomb.angle,3).ToArray());
                }
                else
                {
                    Set(Member(node,"angle_degrees"),bomb.angle);Set(Member(node,"initial_speed"),bomb.speed);Set(Member(node,"projectile_diameter"),bomb.radius*2);Set(Member(node,"gravity"),bomb.gravity);
                    if(i==3){Set(Member(node,"fuse_seconds"),bomb.fuseSeconds);Set(Member(node,"unlanded_timeout_seconds"),bomb.lifetime);Set(Member(node,"user_damage_override"),n.damages[0]);Set(Member(node,"user_radius_units"),bomb.blastRadius);}
                }
            }
        }
        static void WriteNumericObject(XElement node,object value)
        {
            var source=BasicMeleeSource.Parse(JsonUtility.ToJson(value));
            foreach(var child in node.Elements())
            {
                var candidate=source.Element(child.Name);if(candidate==null)continue;
                if((string)child.Attribute("type")=="number")Set(child,BasicMeleeSource.Number(candidate));
                else if((string)child.Attribute("type")=="array")SetArray(child,BasicMeleeSource.Items(candidate).Select(BasicMeleeSource.Number).ToArray());
            }
        }
        static void Shape(XElement node,float radius){Set(Member(node,"radius"),radius);Set(Member(node,"diameter"),radius*2);}
        static void Spawn(XElement node,float x,float y)
        {
            var center=Member(node,"spawn_center");SetArray(Member(center,"right"),new[]{x,y});SetArray(Member(center,"left"),new[]{-x,y});
            var gap=BasicMeleeSource.Optional(center,"distance_ahead_of_front_body_edge")??BasicMeleeSource.Optional(center,"front_body_edge_gap");if(gap!=null)Set(gap,x-.65625f);
        }
        static void Set(XElement node,float value){if(!BasicMeleeSource.Number(node).Equals(value))node.Value=value.ToString("R",CultureInfo.InvariantCulture);}
        static void Set(XElement node,double value){if(double.Parse(node.Value,CultureInfo.InvariantCulture)!=value)node.Value=value.ToString("R",CultureInfo.InvariantCulture);}
        static void SetArray(XElement node,float[] values)
        {
            var items=BasicMeleeSource.Items(node);
            if(items.Length==values.Length){for(int i=0;i<values.Length;i++)Set(items[i],values[i]);return;}
            node.ReplaceNodes(values.Select(value=>new XElement("item",new XAttribute("type","number"),value.ToString("R",CultureInfo.InvariantCulture))));
        }
        // Mirrors export_unity_catalog.py: one D skill per ink slot, built from launcher_skills.json.
        static SkillData LauncherSkill(XElement root,string key)
        {
            var node=Member(root,key);bool bomb=key=="grenade";var charge=BasicMeleeSource.Optional(node,"charge");
            var skill=Skill(Text(node,"id"),Text(node,"name"),Text(node,"slot"),bomb?"bomb":"ranged",node,N(node,"windup"),N(node,"duration"),new[]{N(node,"windup")},
                charge!=null?N(charge,"min_damage"):N(node,"damage"),bomb?"bomb_projectile":"ranged_projectile");
            var transport=new XElement(Member(node,bomb?"bomb":"ranged"));transport.Name="root";
            if(bomb)skill.bomb=JsonUtility.FromJson<BombData>(BasicMeleeSource.Serialize(transport));
            else
            {
                skill.ranged=JsonUtility.FromJson<RangedData>(BasicMeleeSource.Serialize(transport));skill.ranged.noBasicStagger=true;
                if(skill.ranged.type!="laser"&&(skill.ranged.angles==null||skill.ranged.angles.Length==0))skill.ranged.angles=new[]{0f};
            }
            if(charge!=null){skill.chargeMin=N(charge,"min_hold");skill.chargeMax=N(charge,"max_hold");skill.chargeMinDamage=N(charge,"min_damage");skill.chargeMaxDamage=N(charge,"max_damage");skill.chargeSteps=(int)OptionalNumber(charge,"steps",0);}
            return skill;
        }
        static SkillData Skill(string id,string name,string slot,string kind,XElement node,float activation,float end,float[] releases,float damage,string anchor)
            =>new SkillData{id=id,name=name,slot=slot,kind=kind,capacity=1,cooldown=N(node,"cooldown"),activation=activation,end=end,inputSpeed=OptionalNumber(node,"ground_input_speed",0),vertical="normal",facing="windup_input_else_facing",invulStart=Array.Empty<float>(),invulEnd=Array.Empty<float>(),hits=releases.Select(time=>new HitData{time=time,damage=damage,active=.001f,anchor=anchor,targets="all"}).ToArray()};
        static BombData Clone(BombData value)=>new BombData{angle=value.angle,speed=value.speed,gravity=value.gravity,radius=value.radius,blastRadius=value.blastRadius,lifetime=value.lifetime,spawnHeight=value.spawnHeight,spawnGap=value.spawnGap};
        static XElement Member(XElement node,string name)=>BasicMeleeSource.Member(node,name);
        static string Text(XElement node,string name)=>BasicMeleeSource.String(Member(node,name));
        static float N(XElement node,string name)=>BasicMeleeSource.Number(Member(node,name));
        static double D(XElement node,string name)=>double.Parse(Member(node,name).Value,CultureInfo.InvariantCulture);
        static float OptionalNumber(XElement node,string name,float fallback){var value=BasicMeleeSource.Optional(node,name);return value==null?fallback:BasicMeleeSource.Number(value);}
        static float[] Numbers(XElement node,string name)=>BasicMeleeSource.Items(Member(node,name)).Select(BasicMeleeSource.Number).ToArray();
        static float Coordinate(XElement node,int index)=>BasicMeleeSource.Number(BasicMeleeSource.Items(Member(Member(node,"spawn_center"),"right"))[index]);
    }
}
