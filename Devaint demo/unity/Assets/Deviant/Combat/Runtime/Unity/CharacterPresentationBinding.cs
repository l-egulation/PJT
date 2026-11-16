using System;
using Deviant.Combat.Contracts;
using Deviant.Definitions;
using UnityEngine;

namespace Deviant.Combat
{
    // Native media/output boundary only. Gameplay is read through the explicitly
    // bound public session; a draw never resolves a replacement actor or Cast.
    internal sealed class CharacterPresentationBinding : IDisposable
    {
        readonly CombatArena arena;
        readonly ICombatReadModel reads;
        CombatInputSession session;
        SpriteRenderer output;
        string launcherForm;
        int launcherHit;
        float launcherEnd=-10;
        int thrownHit;
        float thrownEnd=-10;
        CharacterClip recovery;
        public bool TryRecovery(string form,float now,string state,out Sprite sprite,out string name)
        {
            sprite=null;name=null;
            if(recovery==null)return false;
            if(form!="shuriken" || state!="idle" || now<thrownEnd || now>=thrownEnd+recovery.Duration){recovery=null;return false;}
            sprite=recovery.At(now-thrownEnd,false);name=recovery.name;return true;
        }
        // Shuriken move special (2026-11-08): right after appearing behind the target the body plays its arrival pose,
        // over the throw's own recovery. Only the dash pose draws above it.
        public bool TryShurikenArrival(string form,float now,out Sprite sprite,out string name)
        {
            sprite=null;name=null;
            var player=arena?arena.Player:null;
            if(form!="shuriken" || !player)return false;
            float age=now-player.ShurikenArrivedAt;
            var clip=age>=0?FindSet("shuriken")?.Find("arrive_behind"):null;
            if(clip==null || clip.sprites==null || clip.sprites.Length==0 || age>=clip.Duration)return false;
            sprite=clip.At(age,false);name=clip.name;return sprite;
        }
        // Bomb move special (2026-11-10): out of the missile's blast the body plays its own emerge pose (blown back, curled fall,
        // one-hand landing) over the recoil and recovery, instead of the throw's last frame.
        public bool TryBombMissileEmerge(string form,float now,out Sprite sprite,out string name)
        {
            sprite=null;name=null;
            var player=arena?arena.Player:null;
            if(form!="bomb" || !player)return false;
            float age=now-player.MissileEmergedAt;
            var clip=age>=0?FindSet("bomb")?.Find("missile_emerge"):null;
            if(clip==null || clip.sprites==null || clip.sprites.Length==0 || age>=clip.Duration)return false;
            sprite=clip.At(age,false);name=clip.name;return sprite;
        }
        // 채찍 스킬 땡기기(2026-11-07 확정): 시전이 끝나도 그 시전이 건 채찍 속박이 남아 있고 대기 상태면 땡기는 장(전용 attack_* 의 마지막 장)을
        // 붙잡고 1화소(1/32유닛)씩 떤다. 속박이 끝나면 release_* 를 한 번 재생한다. 이동·공격·점프·대시로 대기가 아니게 되면 바로 놓는다.
        static readonly int[] WhipJitter = { 0, 1, 0, -1 };
        CharacterClip whipRelease;
        Sprite whipHold;
        Guid whipExecution;
        float whipEnd = -10;
        public bool TryWhipHold(string form, float now, string state, out Sprite sprite, out string name, out float offsetX)
        {
            sprite = null; name = null; offsetX = 0;
            if (whipRelease == null) return false;
            if (form != "whip" || state != "idle") { CancelWhipHold(); return false; }
            if (now < whipEnd) return false;
            float rootUntil = arena.WhipRootUntil(whipExecution);
            // 매듭 접근은 다시 D 로 끌려갈 수 있는 동안(재입력 창) 줄을 팽팽히 당긴 채 버틴다. 그래야 당기던 자세에서 바로 끌려간다.
            bool recastReady = whipRecastCast && whipRecastCast.RecastRemaining > 0;
            if (recastReady) rootUntil = Mathf.Max(rootUntil, now + Time.fixedDeltaTime);
            if (now < rootUntil && whipHold)
            {
                sprite = whipHold; name = "whip_hold"; whipHeldAt = now;
                offsetX = WhipJitter[Mathf.FloorToInt((now - whipEnd) * 25) & 3] / 32f;
                return true;
            }
            // 놓기는 마지막으로 버틴 순간부터(재입력 창이 끝나 버티기를 멈춘 경우도 같은 자리에서 이어진다).
            if (float.IsNaN(whipReleaseAt)) whipReleaseAt = Mathf.Max(whipEnd, Mathf.Min(rootUntil, now), whipHeldAt);
            float start = whipReleaseAt;
            if (now < start + whipRelease.Duration) { sprite = whipRelease.At(now - start, false); name = whipRelease.name; return sprite; }
            CancelWhipHold(); return false;
        }
        CombatCast whipRecastCast;
        // WhipSkillVisuals 가 읽는다: 땡기는 중인 시전, 매듭 접근 재입력이 남았는지.
        internal Guid WhipHoldExecution => whipRelease != null ? whipExecution : Guid.Empty;
        internal CombatCast WhipRecastCast => whipRecastCast;
        float whipHeldAt = -10, whipReleaseAt = float.NaN;
        public void CancelWhipHold() { whipRelease = null; whipHold = null; whipExecution = Guid.Empty; whipEnd = -10; whipRecastCast = null; whipHeldAt = -10; whipReleaseAt = float.NaN; }
        public Guid CapturedExecution { get; private set; }
        public string CapturedState { get; private set; }
        public string CapturedForm { get; private set; }
        public AttackMotionSnapshot Attack { get; private set; }
        public string Error { get; private set; }
        public CharacterPresentationBinding(CombatArena arena)
        {
            this.arena = arena; reads = arena.ReadModel;
            arena.Initialized += BindSession; arena.ResetCompleted += BindSession;
            BindSession();
        }
        void BindSession()
        {
            session = default; reads.TryGetSession(out session);
            output = arena && arena.Player ? arena.Player.visual : null;
            if (output && arena.loadCharacterArt) LoadAirJumpVisuals();
        }
        public bool TryRead(out ActorReadSnapshot actor) => reads.TryReadPlayer(session, out actor);
        public SpriteRenderer Output => output;
        public void Footstep()
        {
            var player = arena.Player;
            if (!player || !player.grounded || Mathf.Abs(player.VelocityX) < .01f) return;
            var feet = player.Position;
            var material = FootstepMaterial.Dirt;
            foreach (var hit in Physics2D.RaycastAll(feet + Vector2.up * .05f, Vector2.down, .17f))
            {
                if (!hit.collider || hit.collider.isTrigger || !hit.collider.TryGetComponent(out ArenaSurface surface)
                    || hit.normal.y <= .5f) continue;
                material = surface.footstepMaterial;
                break;
            }
            string id = material switch
            {
                FootstepMaterial.Grass => "sfx_player_step_grass",
                FootstepMaterial.Stone => "sfx_player_step_stone",
                FootstepMaterial.Wood => "sfx_player_step_wood",
                _ => "sfx_player_step_dirt"
            };
            arena.Audio?.EmitSelected(id, feet);
        }
        public void ApplyNativeSprite(Sprite sprite)
        {
            if (!output) return;
            output.sprite = sprite;
            if (!sprite) return;
            // Imported character media already encodes world-unit scale and its foot pivot.
            output.transform.localPosition = Vector3.zero;
            output.transform.localScale = Vector3.one;
        }
        CharacterAnimationCatalog airJumpVisuals;
        bool airJumpLoaded;
        void LoadAirJumpVisuals()
        {
            if (airJumpLoaded) return;
            airJumpLoaded = true;
            var source = Resources.Load<TextAsset>("ApprovedTraversal/AirJump");
            if (!source) return;
            airJumpVisuals = JsonUtility.FromJson<CharacterAnimationCatalog>(source.text);
            foreach (var set in airJumpVisuals.sets) foreach (var clip in set.clips) clip.Load();
        }
        public bool TryAirJump(string form, float now, out Sprite sprite)
        {
            sprite = null;
            var player = arena.Player;
            if (!player || player.grounded || player.jumps.Used != 2 || player.Dead || player.CheckpointBusy ||
                player.Dashing || player.currentCast || player.WallClimbing || player.WallJumpInFlight) return false;
            // Media is loaded at session binding, before interactive movement.
            // A draw must not load every form's sprites on the first double jump.
            var selected = airJumpVisuals?.sets == null ? null : Array.Find(airJumpVisuals.sets, set => set.form == form);
            var motion = selected?.Find("air_jump");
            float age = now - player.AirJumpStartedAt;
            if (motion == null || age < 0 || age >= motion.Duration) return false;
            sprite = motion.At(age, false);
            return sprite;
        }
        public bool HasCatalog => arena && arena.characterAnimations != null;
        public Material Material => arena.spriteMaterial;
        public bool UseFeelSwordTrail => arena && arena.FeelFeedback && arena.FeelFeedback.UseInkSwordTrail;
        public bool UseContinuousSwordTrail => arena && arena.FeelFeedback && arena.FeelFeedback.UseContinuousSwordTrail;
        public CombatFeelFeedback FeelFeedback => arena ? arena.FeelFeedback : null;
        public CharacterAnimationSet FindSet(string form)
        {
            CharacterAnimationSet set = null;
            if (arena.characterAnimations?.sets == null) return null;
            foreach (var candidate in arena.characterAnimations.sets) if (candidate.form == form) set = candidate;
            if (set == null) foreach (var candidate in arena.characterAnimations.sets) if (candidate.form == "base_sword") set = candidate;
            return set;
        }
        // Called exactly at the existing Native Create boundary. Both the first
        // complete media and first failure stay pinned until reset/new execution.
        public void Capture(CombatCast cast)
        {
            if (cast.owner != arena.Player || CapturedExecution == cast.ExecutionId) return;
            CapturedExecution = cast.ExecutionId; CapturedState = CapturedForm = null; Attack = null; Error = null;
            try
            {
                var key = cast.CaptureMotionActionKey();
                CapturedForm = cast.MotionRequestedForm;
                CapturedState = cast.isBasic ? AttackMotionSnapshot.StateFor(key.ActionId, out _) : "attack1";
                string bodyClip=null;
                recovery=null;
                if(cast.isBasic && CapturedForm=="shuriken")
                {
                    int hit=CapturedState=="attack2"?2:1;
                    if(hit==1 && thrownHit==2 && cast.StartedAt<=thrownEnd+.03f)bodyClip="attack1_chain";
                    thrownHit=hit;thrownEnd=cast.EndTime;
                    var source=FindSet("shuriken")?.Find("recover"+hit);
                    if(source!=null)recovery=new CharacterClip{name=source.name,frames=(string[])source.frames.Clone(),seconds=(float[])source.seconds.Clone(),sprites=source.sprites==null?null:(Sprite[])source.sprites.Clone()};
                }
                else {thrownHit=0;thrownEnd=-10;}
                CancelWhipHold();
                if(!cast.isBasic && CapturedForm=="whip")
                {
                    string suffix=cast.Definition.Id switch{"whip_default"=>"wrap","whip_range"=>"net","whip_movement"=>"knot","whip_follow_up"=>"tighten",_=>null};
                    var set=FindSet("whip");
                    var attack=suffix==null?null:set?.Find("attack_"+suffix);
                    var release=suffix==null?null:set?.Find("release_"+suffix);
                    if(attack?.sprites!=null && attack.sprites.Length>0 && release?.sprites!=null && release.sprites.Length>0)
                    {
                        whipHold=attack.sprites[attack.sprites.Length-1];
                        whipRelease=new CharacterClip{name=release.name,frames=(string[])release.frames.Clone(),seconds=(float[])release.seconds.Clone(),sprites=(Sprite[])release.sprites.Clone()};
                        whipExecution=cast.ExecutionId;whipEnd=cast.EndTime;
                        if(cast.Definition.Id=="whip_movement")whipRecastCast=cast;
                    }
                }
                if(key.Kind==MotionActionKind.Conversion)
                {
                    string form=MotionSourceRegistry.ConversionForm(cast.MotionRequestedForm,key.Variant);
                    var set=FindSet(form);
                    if(set?.form==form && set.Find("attack_single")!=null)
                    {
                        launcherHit=form=="launcher_laser" || launcherForm!=form || cast.StartedAt>launcherEnd+.30f ? 1 : launcherHit%3+1;
                        launcherForm=form;launcherEnd=cast.EndTime;
                        bodyClip=launcherHit==1?"attack_single":"attack"+launcherHit;
                    }
                }
                Attack = new AttackMotionSnapshot(arena.characterAnimations, cast, key,bodyClip);
            }
            catch (Exception exception) { Error = exception.Message; }
        }
        public void ResetCapture() { CapturedExecution = Guid.Empty; CapturedState = CapturedForm = null; Attack = null; Error = null;launcherForm=null;launcherHit=0;launcherEnd=-10;thrownHit=0;thrownEnd=-10;recovery=null;CancelWhipHold(); }
        public void Dispose()
        {
            if (arena) { arena.Initialized -= BindSession; arena.ResetCompleted -= BindSession; }
            session = default; output = null; ResetCapture();
        }
    }
}
