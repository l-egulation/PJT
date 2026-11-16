using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Deviant.Combat;

namespace Deviant.Definitions
{
    // Numeric/source version only. The existing cast, projectile, bomb, missile,
    // recoil and return executions retain all activation and lifetime authority.
    public sealed class RangedActionDefinition
    {
        readonly SkillData source;
        readonly BasicData basicSource;
        readonly SkillData start;
        public string Id { get; }
        public string FormId { get; }
        public int Selection { get; }
        public bool IsBasic { get; }
        public RangedActionPolicy Policy { get; }
        public string ContentHash { get; }
        public string SourceVersion { get; }
        public SkillExecutionDefinition Execution { get; }
        public bool IsStartSnapshot { get; }
        public string RuntimeId => start.id;
        internal RangedActionDefinition(string id, string form, int selection, BasicData basic, SkillData special, float inputSpeed)
        {
            if (basic == null && special == null) throw new ArgumentException("A complete canonical ranged payload is required.");
            Id = id; FormId = form; Selection = selection; IsBasic = basic != null; Policy = PolicyFor(id);
            basicSource = Copy(basic); source = Copy(special);
            if (IsBasic)
            {
                start = BasicStart(basicSource, form, selection, inputSpeed, 1);
                source = Copy(start); // Complete policy metadata for one hash path.
            }
            else start = Copy(source);
            Validate(); Execution = new SkillExecutionDefinition(start);
            SourceVersion = Hash(false); ContentHash = Hash(true);
        }
        RangedActionDefinition(RangedActionDefinition owner, SkillData actualStart)
        {
            Id = owner.Id; FormId = owner.FormId; Selection = owner.Selection; IsBasic = owner.IsBasic; Policy = owner.Policy;
            basicSource = owner.basicSource; source = owner.source; start = Copy(actualStart); IsStartSnapshot = true;
            SourceVersion = owner.SourceVersion; ContentHash = owner.ContentHash; Execution = new SkillExecutionDefinition(start);
        }
        public RangedActionDefinition CaptureBasicStart(SkillData actualStart)
        {
            if (!IsBasic || actualStart == null || actualStart.id != FormId + "_basic_" + (FormId == "launcher" ? 0 : Selection))
                throw new ArgumentException("A matching basic start payload is required.");
            // The pure start resolver supplies its already-computed float payload;
            // do not divide/round it a second time at the Native create boundary.
            return new RangedActionDefinition(this, actualStart);
        }
        public BasicData ToBasicData() => Copy(basicSource);
        public SkillData ToData() => Copy(start);
        public RangedActionSnapshotData ToSnapshot()
        {
            if (IsStartSnapshot) throw new InvalidOperationException("Persist the canonical action, not a rate-scaled in-flight start snapshot.");
            return new RangedActionSnapshotData { id=Id,formId=FormId,selection=Selection,isBasic=IsBasic,policy=Policy.ToString(),inputSpeed=source.inputSpeed,
                basic=Copy(basicSource),special=IsBasic?null:Copy(source),contentHash=ContentHash,sourceVersion=SourceVersion };
        }
        public static RangedActionDefinition FromSnapshot(RangedActionSnapshotData snapshot)
        {
            if(snapshot==null||string.IsNullOrWhiteSpace(snapshot.id))throw new ArgumentException("A complete ranged snapshot is required.");
            var policy=PolicyFor(snapshot.id);string form=snapshot.id.StartsWith("launcher_",StringComparison.Ordinal)?"launcher":snapshot.id.StartsWith("bomb_",StringComparison.Ordinal)?"bomb":"shuriken";
            bool basic=snapshot.id.Contains("_basic_")&&!snapshot.id.EndsWith("_special",StringComparison.Ordinal);
            int selection=basic?(snapshot.id.EndsWith("_1",StringComparison.Ordinal)?1:0):
                snapshot.id.Contains("_basic_")?0:snapshot.id.Contains("_range_")?1:snapshot.id.Contains("_move_")?2:3;
            if(snapshot.formId!=form||snapshot.isBasic!=basic||snapshot.selection!=selection||snapshot.policy!=policy.ToString())throw new ArgumentException("Ranged snapshot closed identity/policy mismatch.");
            if (basic && snapshot.basic == null || !basic && snapshot.special == null) throw new ArgumentException("A complete matching basic or special payload is required.");
            var result=new RangedActionDefinition(snapshot.id,form,selection,basic?snapshot.basic:null,basic?null:snapshot.special,snapshot.inputSpeed);
            if(result.ContentHash!=snapshot.contentHash||result.SourceVersion!=snapshot.sourceVersion)throw new ArgumentException("Ranged snapshot content or policy witness differs from its complete payload.");
            return result;
        }
        public RangedActionNumericData ToNumericData() => new RangedActionNumericData {
            activation = source.activation, end = source.end,
            releaseTimes = source.hits.Select(hit => hit.time).ToArray(), damages = source.hits.Select(hit => hit.damage).ToArray(),
            ranged = Empty(source.ranged) ? null : Copy(source.ranged), bomb = Empty(source.bomb) ? null : Copy(source.bomb), missile = Empty(source.missile) ? null : Copy(source.missile)
        };
        public RangedActionDefinition Recompile(RangedActionNumericData numeric)
        {
            if (numeric == null) throw new ArgumentNullException(nameof(numeric));
            var data = Copy(source); data.activation = numeric.activation; data.end = numeric.end;
            if (Policy == RangedActionPolicy.CasterMissile && numeric.missile != null &&
                (numeric.activation != source.activation || numeric.missile.speed != source.missile.speed || numeric.missile.distance != source.missile.distance || numeric.missile.recovery != source.missile.recovery))
                data.end = (float)((double)numeric.activation + (double)numeric.missile.distance / numeric.missile.speed + numeric.missile.recovery);
            if (numeric.releaseTimes == null || numeric.damages == null || numeric.releaseTimes.Length != data.hits.Length || numeric.damages.Length != data.hits.Length)
                throw new ArgumentException("The existing release count and damage slots must be retained.");
            for (int i = 0; i < data.hits.Length; i++) { data.hits[i].time = numeric.releaseTimes[i]; data.hits[i].damage = numeric.damages[i]; }
            // A charged shot's minimum hold and damage are its first release; keep the charge floor on that one record.
            if (Policy == RangedActionPolicy.ChargedStraightShot) { data.chargeMin = data.activation; data.chargeMinDamage = data.hits[0].damage; }
            if (!Empty(source.ranged) && numeric.ranged == null || !Empty(source.bomb) && numeric.bomb == null || !Empty(source.missile) && numeric.missile == null)
                throw new ArgumentException("The complete existing launch payload is required.");
            if (!Empty(source.ranged) && numeric.ranged != null && numeric.ranged.recoilDuration != source.ranged.recoilDuration)
                throw new ArgumentException("The existing hop policy derives recoil duration; its configured transport witness is read-only.");
            if (Empty(source.ranged) && !Empty(numeric.ranged) || Empty(source.bomb) && !Empty(numeric.bomb) || Empty(source.missile) && !Empty(numeric.missile))
                throw new ArgumentException("Inactive launch payloads may be absent or empty Unity inline objects, never nonempty ignored numeric data.");
            data.ranged = numeric.ranged == null ? Copy(source.ranged) : Copy(numeric.ranged);
            data.bomb = numeric.bomb == null ? Copy(source.bomb) : Copy(numeric.bomb);
            data.missile = numeric.missile == null ? Copy(source.missile) : Copy(numeric.missile);
            RangedActionDefinition result;
            if (IsBasic)
            {
                var basic = Copy(basicSource); basic.damage = data.hits[0].damage; basic.impact = data.activation; basic.duration = data.end;
                basic.ranged = data.ranged; basic.bomb = data.bomb;
                if (data.hits[0].time != data.activation) throw new ArgumentException("A basic release must equal its activation.");
                result = new RangedActionDefinition(Id, FormId, Selection, basic, null, source.inputSpeed);
            }
            else result = new RangedActionDefinition(Id, FormId, Selection, null, data, source.inputSpeed);
            if (result.SourceVersion != SourceVersion) throw new ArgumentException("Ranged numeric edits cannot change identity, resource or closed execution policy.");
            return result;
        }
        internal static SkillData BasicStart(BasicData basic, string form, int index, float inputSpeed, float rate)
            => new SkillData { id = form + "_basic_" + (form == "launcher" ? 0 : index), kind = basic.kind, name = basic.name,
                activation = basic.impact / rate, end = basic.duration / rate, inputSpeed = inputSpeed, vertical = "normal", facing = "windup_start",
                invulStart = Array.Empty<float>(), invulEnd = Array.Empty<float>(), ranged = Copy(basic.ranged), bomb = Copy(basic.bomb),
                basicLungeStart = Math.Max(0, basic.impact - .04f) / rate, basicLungeDuration = .08f / rate,
                hits = new[] { new HitData { time = basic.impact / rate, damage = basic.damage, active = basic.active / rate, box = Copy(basic.box), anchor = "ranged_projectile", targets = "all" } } };
        void Validate()
        {
            if (source == null || source.hits == null || source.hits.Length != (Policy == RangedActionPolicy.ContactBombBurst ? 3 : 1) || source.hits.Any(hit => hit == null)) Fail("Existing release slots are required.");
            Nonnegative(source.activation); Positive(source.end);
            if (source.activation > source.end || source.hits[0].time != source.activation) Fail("Activation must match the first release and not exceed the body end.");
            float previous = -1;
            foreach (var hit in source.hits)
            {
                Nonnegative(hit.time); Nonnegative(hit.damage);
                if (hit.time < previous || hit.time > source.end || hit.active != .001f || !Empty(hit.box) || hit.targets != "all" || hit.root != 0 || hit.stun != 0 || hit.push != 0 || hit.pushWithCaster)
                    Fail("Ordered existing projectile releases without new hit/control policy are required.");
                previous = hit.time;
            }
            if (Policy == RangedActionPolicy.ContactBombBurst && source.hits.Any(hit => hit.damage != source.hits[0].damage)) Fail("Approved burst bombs share their damage.");
            bool charged = Policy == RangedActionPolicy.ChargedStraightShot;
            if (charged && (!(source.chargeMax > source.chargeMin) || source.chargeMin != source.activation || source.chargeMaxDamage < source.chargeMinDamage || source.hits[0].damage != source.chargeMinDamage || source.chargeSteps < 0 || source.chargeSteps == 1))
                Fail("A charged shot releases at its minimum hold with its minimum damage and grows to a larger maximum.");
            if (source.vertical != "normal" || !Empty(source.movement) || !Empty(source.effect) || source.unavailable || !charged && (source.chargeMax != 0 || source.chargeMin != 0 || source.chargeMinDamage != 0 || source.chargeMaxDamage != 0 || source.chargeSteps != 0) ||
                source.invulStart != null && source.invulStart.Length != 0 || source.invulEnd != null && source.invulEnd.Length != 0 || source.verticalFreeze != null && source.verticalFreeze.Length != 0 || !ActionTrackDocument.IsAbsent(source.actionTracks))
                Fail("Only the existing ranged policy is supported; no undeclared charge, defense, extra movement or action graph.");
            if (IsBasic && source.facing != "windup_start" || !IsBasic && source.facing != "windup_input_else_facing") Fail("Existing start-facing policy is required.");
            if (!IsBasic) { Positive(source.cooldown); if (source.capacity != 1) Fail("Existing single-charge resources are required."); }
            if (source.inputSpeed != (FormId == "shuriken" ? 5 : 0)) Fail("Existing ground input policy is immutable.");
            if (!Empty(source.ranged)) ValidateRanged(source.ranged);
            if (!Empty(source.bomb)) ValidateBomb(source.bomb);
            if (!Empty(source.missile))
            {
                if (Policy != RangedActionPolicy.CasterMissile || source.kind != "missile" || !Empty(source.ranged) || !Empty(source.bomb)) Fail("Missile launch policy mismatch.");
                var missile = source.missile; Positive(missile.speed); Positive(missile.distance); Positive(missile.blastRadius); Nonnegative(missile.recovery);
                Nonnegative(missile.manualDelay); Nonnegative(missile.recoilHeight); Nonnegative(missile.recoilDistance);
                Nonnegative(missile.distanceBonusMaxDamage); Nonnegative(missile.distanceBonusFullDistance);
                if (missile.distanceBonusMaxDamage > 0 && missile.distanceBonusFullDistance <= 0) Fail("Missile distance bonus requires a positive full-bonus distance.");
            }
            if (Empty(source.ranged) && Empty(source.bomb) && Empty(source.missile)) Fail("A real launch payload is required.");
        }
        void ValidateRanged(RangedData value)
        {
            if (source.kind != "ranged" || !Empty(source.bomb) || !Empty(source.missile)) Fail("Straight/laser launch policy mismatch.");
            string type = Policy == RangedActionPolicy.LaserAllOnce ? "laser" : Policy == RangedActionPolicy.PiercingPellets ? "shotgun" : Policy == RangedActionPolicy.ChargedStraightShot ? "charge_ball" : FormId == "launcher" ? "ball" :
                IsBasic ? "shuriken" : Policy == RangedActionPolicy.PiercingOnce ? "shuriken_special" : Policy == RangedActionPolicy.FirstContactBlast ? "shuriken_range" :
                Policy == RangedActionPolicy.PiercingVisitReturn ? "shuriken_move" : "shuriken_followup";
            if (value.type != type || value.pierce != (Policy == RangedActionPolicy.PiercingOnce || Policy == RangedActionPolicy.PeriodicPiercing || Policy == RangedActionPolicy.PiercingPellets || Policy == RangedActionPolicy.PiercingVisitReturn) || value.noBasicStagger != !IsBasic) Fail("Ranged collision/stagger policy is immutable.");
            if (IsBasic && value.ignoreTerrain) Fail("Basic attacks must keep terrain collision.");
            if (value.unlimitedRange) { if (Policy != RangedActionPolicy.LaserAllOnce || value.distance != 0) Fail("Unlimited range requires a laser with no finite distance."); }
            else Positive(value.distance);
            if (value.impactBoxSize != 0) { if (Policy != RangedActionPolicy.ChargedStraightShot) Fail("Only charged shots have an enemy-contact square burst."); Positive(value.impactBoxSize); }
            Positive(value.radius); Nonnegative(value.spawnHeight); Nonnegative(value.spawnGap); Nonnegative(value.spawnX);
            if (Policy == RangedActionPolicy.PiercingPellets && value.spawnX != 0 || Policy != RangedActionPolicy.PiercingPellets && Policy != RangedActionPolicy.LaserAllOnce && value.spawnGap != 0)
                Fail("Use the existing absolute spawn-X or body-front-gap mode, not an ignored competing muzzle value.");
            if (Policy == RangedActionPolicy.LaserAllOnce) { Positive(value.thickness); if (value.radius != value.thickness / 2 || value.speed != 0) Fail("Laser radius follows thickness; no unused flight speed."); }
            else Positive(value.speed);
            if (Policy != RangedActionPolicy.LaserAllOnce && value.thickness != 0) Fail("Only the laser consumes thickness.");
            if (Policy == RangedActionPolicy.LaserAllOnce)
            {
                if (value.angles != null && value.angles.Length != 0 || value.spawnX != 0) Fail("Laser has no pellet list or absolute muzzle X.");
            }
            else
            {
                if (value.angles == null || value.angles.Length == 0 || value.angles.Length > 64 || value.angles.Any(angle => !Finite(angle))) Fail("A bounded real pellet angle list is required.");
                if (Policy != RangedActionPolicy.PiercingPellets && (value.angles.Length != 1 || value.angles[0] != 0)) Fail("Only shotgun has a spread policy.");
            }
            if (Policy == RangedActionPolicy.PiercingPellets) { Positive(value.recoilHopHeight); Nonnegative(value.recoilDistance); Positive(value.recoilDuration); }
            else if (value.recoilDistance != 0 || value.recoilDuration != 0 || value.recoilHopHeight != 0) Fail("This policy has no recoil.");
            if (Policy == RangedActionPolicy.FirstContactBlast) Positive(value.blastRadius); else if (value.blastRadius != 0) Fail("This projectile has no blast.");
            if (Policy == RangedActionPolicy.PiercingVisitReturn) { Positive(value.returnDelay); Nonnegative(value.behindGap); }
            else if (value.returnDelay != 0 || value.behindGap != 0) Fail("This projectile has no visit/return.");
            if (Policy == RangedActionPolicy.PeriodicPiercing) Positive(value.hitInterval); else if (value.hitInterval != 0) Fail("This projectile has no periodic hit clock.");
            if (Policy == RangedActionPolicy.ChargedStraightShot) { if (!Finite(value.chargeRadius) || value.chargeRadius < value.radius) Fail("A charged shot grows from its radius to a finite larger full-charge radius."); }
            else if (value.chargeRadius != 0) Fail("Only a charged shot has a full-charge radius.");
            if (Policy == RangedActionPolicy.StraightFirstHit && FormId == "shuriken")
            {
                Nonnegative(value.homingTurnRate); Nonnegative(value.homingCone); Nonnegative(value.homingMaxAngle);
                if (value.homingTurnRate > 0 && (value.homingCone <= 0 || value.homingCone > 90 || value.homingMaxAngle <= 0 || value.homingMaxAngle > 90))
                    Fail("Shuriken homing needs a cone and deflection cap within (0, 90] degrees.");
            }
            else if (value.homingTurnRate != 0 || value.homingCone != 0 || value.homingMaxAngle != 0) Fail("Only the basic shuriken throws home.");
        }
        void ValidateBomb(BombData value)
        {
            if (source.kind != "bomb" || !Empty(source.ranged) || !Empty(source.missile)) Fail("Bomb launch policy mismatch.");
            if (!Finite(value.angle)) Fail("Finite throw angle required."); Positive(value.speed); Positive(value.gravity); Positive(value.radius); Positive(value.blastRadius); Positive(value.lifetime);
            Nonnegative(value.spawnHeight); Nonnegative(value.spawnGap);
            if (Policy == RangedActionPolicy.LandingFuseBomb) { Positive(value.fuseSeconds); if (!value.explodeOnExpiry) Fail("Landing fuse retains airborne timeout explosion."); }
            else if (value.fuseSeconds != 0 || value.explodeOnExpiry) Fail("Contact bombs do not explode on noncollision expiry.");
            int count = value.fragmentAngles?.Length ?? 0;
            if (Policy == RangedActionPolicy.ContactBombWithFragments)
            {
                if (count == 0 || count > 64 || value.fragmentAngles.Any(angle => !Finite(angle))) Fail("A bounded fragment angle list is required.");
                Nonnegative(value.fragmentDamage); Positive(value.fragmentSpeed); Positive(value.fragmentGravity); Positive(value.fragmentRadius); Positive(value.fragmentBlastRadius); Positive(value.fragmentLifetime); Nonnegative(value.fragmentSpawnOffset);
            }
            else if (count != 0 || value.fragmentDamage != 0 || value.fragmentSpeed != 0 || value.fragmentGravity != 0 || value.fragmentRadius != 0 || value.fragmentBlastRadius != 0 || value.fragmentLifetime != 0 || value.fragmentSpawnOffset != 0) Fail("This bomb has no fragment policy.");
        }
        string Hash(bool numeric)
        {
            using (var bytes = new MemoryStream())
            {
                using (var writer = new BinaryWriter(bytes, System.Text.Encoding.UTF8, true))
                {
                    writer.Write("ranged-action-v1"); writer.Write(Id); writer.Write(FormId); writer.Write(Selection); writer.Write(IsBasic); writer.Write((int)Policy);
                    writer.Write(source.id ?? ""); writer.Write(source.name ?? ""); writer.Write(source.slot ?? ""); writer.Write(source.description ?? ""); writer.Write(source.inputSpeed); writer.Write(source.cooldown); writer.Write(source.capacity);
                    writer.Write(source.facing ?? ""); writer.Write(source.vertical ?? ""); writer.Write(source.kind ?? ""); writer.Write(source.hits.Length);
                    foreach (var hit in source.hits) { writer.Write(hit.anchor ?? ""); writer.Write(hit.active); writer.Write(hit.targets ?? ""); writer.Write(hit.pushSeconds); if (numeric) { writer.Write(hit.time); writer.Write(hit.damage); } }
                    if (numeric && Policy == RangedActionPolicy.ChargedStraightShot) { foreach (float v in new[] { source.chargeMin, source.chargeMax, source.chargeMinDamage, source.chargeMaxDamage }) writer.Write(v); if (source.chargeSteps > 0) writer.Write(source.chargeSteps); }
                    if (source.ranged?.ignoreTerrain == true) writer.Write("ignoreTerrain");
                    if (numeric) { writer.Write(source.activation); writer.Write(source.end); Write(writer, Empty(source.ranged) ? null : source.ranged); Write(writer, Empty(source.bomb) ? null : source.bomb); Write(writer, Empty(source.missile) ? null : source.missile); }
                    else { writer.Write(source.ranged?.type ?? ""); writer.Write(source.ranged?.pierce ?? false); writer.Write(source.ranged?.noBasicStagger ?? false); writer.Write(source.bomb?.explodeOnExpiry ?? false); if (Policy == RangedActionPolicy.PiercingPellets) writer.Write(source.ranged.recoilDuration); }
                }
                using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes.ToArray())).Replace("-", "").ToLowerInvariant();
            }
        }
        static void Write(BinaryWriter w, RangedData r) { w.Write(r != null); if (r == null) return; w.Write(r.pierce); foreach (float v in new[] { r.speed,r.distance,r.radius,r.spawnHeight,r.spawnX,r.spawnGap,r.thickness,r.recoilDistance,r.recoilDuration,r.recoilHopHeight,r.blastRadius,r.returnDelay,r.behindGap,r.hitInterval }) w.Write(v); w.Write(r.angles?.Length ?? 0); if (r.angles != null) foreach (float v in r.angles) w.Write(v); if (r.chargeRadius != 0) { w.Write("chargeRadius"); w.Write(r.chargeRadius); } if (r.unlimitedRange) w.Write("unlimitedRange"); if (r.impactBoxSize != 0) { w.Write("impactBoxSize"); w.Write(r.impactBoxSize); } if (r.homingTurnRate != 0 || r.homingCone != 0 || r.homingMaxAngle != 0) { w.Write("homing"); w.Write(r.homingTurnRate); w.Write(r.homingCone); w.Write(r.homingMaxAngle); } }
        static void Write(BinaryWriter w, BombData b) { w.Write(b != null); if (b == null) return; foreach (float v in new[] { b.angle,b.speed,b.gravity,b.radius,b.blastRadius,b.lifetime,b.spawnHeight,b.spawnGap,b.fuseSeconds,b.fragmentDamage,b.fragmentSpeed,b.fragmentGravity,b.fragmentRadius,b.fragmentBlastRadius,b.fragmentLifetime,b.fragmentSpawnOffset }) w.Write(v); w.Write(b.fragmentAngles?.Length ?? 0); if (b.fragmentAngles != null) foreach (float v in b.fragmentAngles) w.Write(v); }
        static void Write(BinaryWriter w, MissileData m) { w.Write(m != null); if (m != null) { foreach (float v in new[] { m.speed,m.distance,m.blastRadius,m.recovery,m.manualDelay,m.recoilHeight,m.recoilDistance }) w.Write(v); if (m.distanceBonusMaxDamage != 0 || m.distanceBonusFullDistance != 0) { w.Write("distanceBonus"); w.Write(m.distanceBonusMaxDamage); w.Write(m.distanceBonusFullDistance); } } }
        internal static RangedActionPolicy PolicyFor(string id)
        {
            switch (id)
            {
                case "launcher_basic_0": case "shuriken_basic_0": case "shuriken_basic_1": return RangedActionPolicy.StraightFirstHit;
                case "launcher_basic_special": return RangedActionPolicy.ChargedStraightShot;
                case "launcher_range_special": return RangedActionPolicy.LaserAllOnce;
                case "launcher_move_special": return RangedActionPolicy.PiercingPellets;
                case "launcher_followup_special": return RangedActionPolicy.ContactBombWithFragments;
                case "bomb_basic_0": case "bomb_range_special": return RangedActionPolicy.ContactBomb;
                case "bomb_basic_special": return RangedActionPolicy.ContactBombBurst;
                case "bomb_move_special": return RangedActionPolicy.CasterMissile;
                case "bomb_followup_special": return RangedActionPolicy.LandingFuseBomb;
                case "shuriken_basic_special": return RangedActionPolicy.PiercingOnce;
                case "shuriken_range_special": return RangedActionPolicy.FirstContactBlast;
                case "shuriken_move_special": return RangedActionPolicy.PiercingVisitReturn;
                case "shuriken_followup_special": return RangedActionPolicy.PeriodicPiercing;
                default: throw new ArgumentException("Unsupported ranged action ID: " + id);
            }
        }
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        static void Positive(float value) { if (!Finite(value) || value <= 0) Fail("Positive finite numeric value required."); }
        static void Nonnegative(float value) { if (!Finite(value) || value < 0) Fail("Nonnegative finite numeric value required."); }
        static void Fail(string error) => throw new ArgumentException(error);
        internal static BasicData Copy(BasicData b) => b == null ? null : new BasicData { kind=b.kind,name=b.name,damage=b.damage,duration=b.duration,impact=b.impact,active=b.active,ranged=Copy(b.ranged),bomb=Copy(b.bomb),box=Copy(b.box) };
        internal static RangedData Copy(RangedData r) => r == null ? null : new RangedData { type=r.type,ignoreTerrain=r.ignoreTerrain,pierce=r.pierce,noBasicStagger=r.noBasicStagger,speed=r.speed,distance=r.distance,radius=r.radius,spawnHeight=r.spawnHeight,spawnX=r.spawnX,spawnGap=r.spawnGap,thickness=r.thickness,recoilDistance=r.recoilDistance,recoilDuration=r.recoilDuration,recoilHopHeight=r.recoilHopHeight,blastRadius=r.blastRadius,returnDelay=r.returnDelay,behindGap=r.behindGap,hitInterval=r.hitInterval,chargeRadius=r.chargeRadius,unlimitedRange=r.unlimitedRange,impactBoxSize=r.impactBoxSize,homingTurnRate=r.homingTurnRate,homingCone=r.homingCone,homingMaxAngle=r.homingMaxAngle,angles=r.angles == null ? null : (float[])r.angles.Clone() };
        internal static BombData Copy(BombData b) => b == null ? null : new BombData { angle=b.angle,speed=b.speed,gravity=b.gravity,radius=b.radius,blastRadius=b.blastRadius,lifetime=b.lifetime,spawnHeight=b.spawnHeight,spawnGap=b.spawnGap,fuseSeconds=b.fuseSeconds,explodeOnExpiry=b.explodeOnExpiry,fragmentDamage=b.fragmentDamage,fragmentSpeed=b.fragmentSpeed,fragmentGravity=b.fragmentGravity,fragmentRadius=b.fragmentRadius,fragmentBlastRadius=b.fragmentBlastRadius,fragmentLifetime=b.fragmentLifetime,fragmentSpawnOffset=b.fragmentSpawnOffset,fragmentAngles=b.fragmentAngles == null ? null : (float[])b.fragmentAngles.Clone() };
        internal static MissileData Copy(MissileData m) => m == null ? null : new MissileData { speed=m.speed,distance=m.distance,blastRadius=m.blastRadius,recovery=m.recovery,manualDelay=m.manualDelay,recoilHeight=m.recoilHeight,recoilDistance=m.recoilDistance,distanceBonusMaxDamage=m.distanceBonusMaxDamage,distanceBonusFullDistance=m.distanceBonusFullDistance };
        static SkillData Copy(SkillData s) => s == null ? null : new SkillData { id=s.id,name=s.name,slot=s.slot,kind=s.kind,description=s.description,unavailable=s.unavailable,activation=s.activation,end=s.end,inputSpeed=s.inputSpeed,cooldown=s.cooldown,capacity=s.capacity,vertical=s.vertical,facing=s.facing,chargeMin=s.chargeMin,chargeMax=s.chargeMax,chargeMinDamage=s.chargeMinDamage,chargeMaxDamage=s.chargeMaxDamage,chargeSteps=s.chargeSteps,basicLungeDistance=s.basicLungeDistance,basicLungeStart=s.basicLungeStart,basicLungeDuration=s.basicLungeDuration,invulStart=s.invulStart == null ? null : (float[])s.invulStart.Clone(),invulEnd=s.invulEnd == null ? null : (float[])s.invulEnd.Clone(),verticalFreeze=s.verticalFreeze == null ? null : (float[])s.verticalFreeze.Clone(),movement=Copy(s.movement),effect=Copy(s.effect),actionTracks=s.actionTracks,ranged=Copy(s.ranged),bomb=Copy(s.bomb),missile=Copy(s.missile),hits=s.hits?.Select(h=>h == null ? null : new HitData { time=h.time,damage=h.damage,active=h.active,anchor=h.anchor,targets=h.targets,box=Copy(h.box),root=h.root,stun=h.stun,push=h.push,pushSeconds=h.pushSeconds,pushWithCaster=h.pushWithCaster }).ToArray() };
        static BoxData Copy(BoxData b) => b == null ? null : new BoxData { width=b.width,height=b.height,x=b.x,y=b.y };
        static MoveData Copy(MoveData m) => m == null ? null : new MoveData { type=m.type,wallPolicy=m.wallPolicy,start=m.start,duration=m.duration,distance=m.distance,speed=m.speed,wallRecovery=m.wallRecovery,gap=m.gap,recastWindow=m.recastWindow,targetRadius=m.targetRadius,primaryPush=m.primaryPush,endDamage=m.endDamage,targetBox=Copy(m.targetBox) };
        static EffectData Copy(EffectData e) => e == null ? null : new EffectData { type=e.type,duration=e.duration,hp=e.hp,reduction=e.reduction,speed=e.speed,distance=e.distance,radius=e.radius,x=e.x,y=e.y,parryWindow=e.parryWindow,parryDamage=e.parryDamage,box=Copy(e.box) };
        static bool Empty(BoxData b) => b == null || b.width == 0 && b.height == 0 && b.x == 0 && b.y == 0;
        static bool Empty(MoveData m) => m == null || string.IsNullOrEmpty(m.type) && string.IsNullOrEmpty(m.wallPolicy) && m.start == 0 && m.duration == 0 && m.distance == 0 && m.speed == 0 && m.wallRecovery == 0 && m.gap == 0 && m.recastWindow == 0 && m.targetRadius == 0 && !m.primaryPush && m.endDamage == 0 && Empty(m.targetBox);
        static bool Empty(EffectData e) => e == null || string.IsNullOrEmpty(e.type) && e.duration == 0 && e.hp == 0 && e.reduction == 0 && e.speed == 0 && e.distance == 0 && e.radius == 0 && e.x == 0 && e.y == 0 && e.parryWindow == 0 && e.parryDamage == 0 && Empty(e.box);
        static bool Empty(RangedData r) => r == null || string.IsNullOrEmpty(r.type) && !r.pierce && !r.noBasicStagger && !r.ignoreTerrain && r.speed == 0 && r.distance == 0 && r.radius == 0 && r.spawnHeight == 0 && r.spawnX == 0 && r.spawnGap == 0 && r.thickness == 0 && r.recoilDistance == 0 && r.recoilDuration == 0 && r.recoilHopHeight == 0 && r.blastRadius == 0 && r.returnDelay == 0 && r.behindGap == 0 && r.hitInterval == 0 && r.chargeRadius == 0 && !r.unlimitedRange && r.impactBoxSize == 0 && r.homingTurnRate == 0 && r.homingCone == 0 && r.homingMaxAngle == 0 && (r.angles == null || r.angles.Length == 0);
        static bool Empty(BombData b) => b == null || b.angle == 0 && b.speed == 0 && b.gravity == 0 && b.radius == 0 && b.blastRadius == 0 && b.lifetime == 0 && b.spawnHeight == 0 && b.spawnGap == 0 && b.fuseSeconds == 0 && !b.explodeOnExpiry && b.fragmentDamage == 0 && b.fragmentSpeed == 0 && b.fragmentGravity == 0 && b.fragmentRadius == 0 && b.fragmentBlastRadius == 0 && b.fragmentLifetime == 0 && b.fragmentSpawnOffset == 0 && (b.fragmentAngles == null || b.fragmentAngles.Length == 0);
        static bool Empty(MissileData m) => m == null || m.speed == 0 && m.distance == 0 && m.blastRadius == 0 && m.recovery == 0 && m.manualDelay == 0 && m.recoilHeight == 0 && m.recoilDistance == 0 && m.distanceBonusMaxDamage == 0 && m.distanceBonusFullDistance == 0;
    }
}
