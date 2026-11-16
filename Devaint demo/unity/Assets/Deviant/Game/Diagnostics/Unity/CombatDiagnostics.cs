using Deviant.Combat;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Deviant.Game.Diagnostics
{
    // Existing technical HUD/equipment tools, explicitly selected by both scene roots.
    // This component supplies no terrain, actors, content or capture command line.
    public sealed class CombatDiagnostics : MonoBehaviour
    {
        public CombatArena arena;
        public Font uiFont;
        public bool showDebugUI = true;
        public bool showHud = true;
        public bool showPanel;
        public bool CompactHud => arena&&arena.FeelFeedback&&arena.FeelFeedback.CompactHud;
        public static Rect CompactStatusBounds(float width,float height)=>new Rect(10,height-54,Mathf.Min(590,width-116),44);
        string runSpeedText, runSpeedMessage;
        [System.Serializable] public sealed class InkPartOption
        { public string id, name, color, description, limit, requiresStamp, unityNote; public float cooldown; public bool duplicate; }
        [System.Serializable] sealed class InkPartOptions { public InkPartOption[] parts; }
        InkPartOption[] inkPartOptions;
        int inkPaletteSlot, inkColor;
        string inkMessage;
        Deviant.Control.Contracts.IControlScope inkExperimentScope;
        public bool InkExperimentOpen => inkExperimentScope != null;
        public bool SetInkExperimentOpen(bool open)
        {
            if (!open)
            {
                if (inkExperimentScope == null) return true;
                inkExperimentScope.Dispose(); inkExperimentScope = null;
                if (arena) arena.TestConfigurationOpen = false;
                return true;
            }
            if (!arena || !arena.Player || arena.Player.Dead || arena.ExternalTestPanel ||
                (!InkExperimentOpen && (arena.Controls.Current.SimulationPaused || arena.Player.InputBlocked))) return false;
            if (InkExperimentOpen) return true;
            inkExperimentScope = arena.Controls.OpenScope("ink part experiment");
            inkExperimentScope.BlockInput(Deviant.Control.Contracts.InputChannels.Combat | Deviant.Control.Contracts.InputChannels.Interaction);
            inkExperimentScope.PauseSimulation(); arena.TestConfigurationOpen = true; showPanel = true;
            return true;
        }
        void OnDisable() => SetInkExperimentOpen(false);
        public bool SelectNativeSpecial(int slot, int special, out string error)
        {
            error=null;
            if (!arena || !arena.Player || slot<0 || slot>=arena.Player.weapons.Count) return false;
            if (!arena.Player.TryReconfigure(slot,arena.Player.weapons[slot].FormIndex,special,out error)) return false;
            return arena.Player.TrySetChargeStamp(slot,false,out error);
        }
        public System.Action<int, FormData, WeaponState> EquipmentTuning;
        public System.Func<Vector3,Vector3> WorldPointToScreen;
        readonly Dictionary<Sprite,float> opaqueTops=new Dictionary<Sprite,float>();
        public Vector3 ProjectWorldPoint(Vector3 world)
            =>WorldPointToScreen!=null?WorldPointToScreen(world):Deviant.Presentation.Unity.CameraDisplayProjection.WorldToScreenPoint(arena.ViewCamera,world);
        public static Vector3 ProjectViewportPoint(Rect display,Vector3 viewport,int screenHeight)
            =>new Vector3(display.x+viewport.x*display.width,screenHeight-display.y-(1-viewport.y)*display.height,viewport.z);
        public Vector3 EnemyHealthAnchor(CombatActor actor)
        {
            var visual=actor.visual;var sprite=visual?visual.sprite:null;
            float top=actor.Center.y+actor.size.y/2;
            if(sprite&&sprite.texture.isReadable)
            {
                if(!opaqueTops.TryGetValue(sprite,out var localTop))
                {
                    var rect=sprite.rect;var pixels=sprite.texture.GetPixels32();int stride=sprite.texture.width;
                    localTop=sprite.bounds.max.y;
                    for(int y=(int)rect.height-1;y>=0;y--)
                    {
                        bool ink=false;
                        for(int x=0;x<(int)rect.width;x++)if(pixels[((int)rect.y+y)*stride+(int)rect.x+x].a>=128){ink=true;break;}
                        if(!ink)continue;
                        localTop=(y+1-sprite.pivot.y)/sprite.pixelsPerUnit;break;
                    }
                    opaqueTops[sprite]=localTop;
                }
                top=visual.transform.TransformPoint(Vector3.up*localTop).y;
            }
            return new Vector3(actor.Center.x,top+.3f,0);
        }
        public static Vector3 EnemyFootHealthAnchor(CombatActor actor) => new Vector3(actor.Center.x,actor.Position.y-.18f,0);
        public static Color EnemyHealthColor(float fraction) => fraction<=.25f?new Color(.96f,.22f,.19f):
            fraction<=.6f?new Color(1f,.61f,.12f):new Color(.23f,.83f,.42f);
        int editingSlot;
        Vector2 panelScroll;
        public enum HitboxKind { EnemyPrepare, EnemyAttack, EnemyProjectile, PlayerProjectile, PlayerExplosion }
        public readonly struct AttackHitbox
        {
            public readonly Object Source;
            public readonly Bounds Bounds;
            public readonly float Radius;
            public readonly HitboxKind Kind;
            public AttackHitbox(Object source,Bounds bounds,float radius,HitboxKind kind)
            {Source=source;Bounds=bounds;Radius=radius;Kind=kind;}
        }
        readonly List<AttackHitbox> attackHitboxes=new List<AttackHitbox>();
        public void CollectAttackHitboxes(List<AttackHitbox> output)
        {
            output.Clear();
            if(!arena||!showDebugUI||!arena.showBoxes)return;
            foreach(var enemy in arena.enemies)
            {
                if(!(enemy is FieldMouseEnemy species)||species.Dead||species.Brain==null||!species.Brain.Executing)continue;
                double activeAt=species.Brain.StartedAt+species.Definition.prepare;
                if(arena.Now>=activeAt+species.Definition.active)continue;
                output.Add(new AttackHitbox(species,species.AttackBounds,0,
                    arena.Now<activeAt?HitboxKind.EnemyPrepare:HitboxKind.EnemyAttack));
            }
            foreach(var shot in arena.projectiles)if(shot)
                output.Add(new AttackHitbox(shot,new Bounds(shot.transform.position,new Vector2(.25f,.25f)),0,HitboxKind.EnemyProjectile));
            foreach(var shot in arena.playerShots)if(shot)
                output.Add(new AttackHitbox(shot,new Bounds(shot.Position,Vector2.one*shot.Definition.Radius*2),shot.Definition.Radius,HitboxKind.PlayerProjectile));
            foreach(var bomb in arena.bombs)if(bomb)
                output.Add(new AttackHitbox(bomb,new Bounds(bomb.Position,Vector2.one*bomb.Definition.Radius*2),bomb.Definition.Radius,HitboxKind.PlayerProjectile));
            // The burst keeps the actual blast radius during its existing short visual lifetime.
            foreach(var burst in arena.bombBursts)if(burst)
                output.Add(new AttackHitbox(burst,new Bounds(burst.transform.position,Vector2.one*burst.radius*2),burst.radius,HitboxKind.PlayerExplosion));
        }
        void Update()
        {
            if (!arena || !arena.Player || arena.ManualSimulationActive || arena.ExternalTestPanel) return;
            var player = arena.Player;
            var keyboard = Keyboard.current;
            if (InkExperimentOpen)
            {
                if (!showPanel || !showDebugUI || player.Dead || (keyboard != null && keyboard.f1Key.wasPressedThisFrame))
                { SetInkExperimentOpen(false); showPanel = false; }
                return;
            }
            if (player.InputBlocked || arena.Controls.Current.SimulationPaused || Time.timeScale == 0) return;
            if (!DevFeatures.Enabled || keyboard == null || CombatKeyboardBindings.WasPressed(keyboard, CombatKeyboardBindings.For(player).DevelopmentReset)) return;
            if (keyboard.f1Key.wasPressedThisFrame) showPanel = !showPanel;
            if (keyboard.f2Key.wasPressedThisFrame) arena.showBoxes = !arena.showBoxes;
        }
        public void DrawWorldBox(Bounds b, Color color)
        {
            if (!arena.ViewCamera) return;
            Vector3 lo = Deviant.Presentation.Unity.CameraDisplayProjection.WorldToScreenPoint(arena.ViewCamera, b.min), hi = Deviant.Presentation.Unity.CameraDisplayProjection.WorldToScreenPoint(arena.ViewCamera, b.max);
            if(lo.z<=0||hi.z<=0)return;
            var rect=new Rect(lo.x,Screen.height-hi.y,hi.x-lo.x,hi.y-lo.y);
            Color old = GUI.color;
            try
            {
                GUI.color=color;GUI.DrawTexture(rect,Texture2D.whiteTexture);color.a=1;GUI.color=color;
                GUI.DrawTexture(new Rect(rect.x,rect.y,rect.width,2),Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(rect.x,rect.yMax-2,rect.width,2),Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(rect.x,rect.y,2,rect.height),Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(rect.xMax-2,rect.y,2,rect.height),Texture2D.whiteTexture);
            }
            finally{GUI.color=old;}
        }
        void DrawWorldCircle(Vector2 center,float radius,Color color)
        {
            if(!arena.ViewCamera)return;
            var origin=Deviant.Presentation.Unity.CameraDisplayProjection.WorldToScreenPoint(arena.ViewCamera, center);if(origin.z<=0)return;
            var matrix=GUI.matrix;var old=GUI.color;color.a=1;
            try
            {
                GUI.color=color;
                Vector2 previous=Vector2.zero;
                for(int i=0;i<=32;i++)
                {
                    float angle=i*Mathf.PI/16;
                    var screen=Deviant.Presentation.Unity.CameraDisplayProjection.WorldToScreenPoint(arena.ViewCamera, center+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*radius);
                    var point=new Vector2(screen.x,Screen.height-screen.y);
                    if(i>0)
                    {
                        var delta=point-previous;
                        GUI.matrix=matrix;GUIUtility.RotateAroundPivot(Mathf.Atan2(delta.y,delta.x)*Mathf.Rad2Deg,previous);
                        GUI.DrawTexture(new Rect(previous.x,previous.y-1,delta.magnitude,2),Texture2D.whiteTexture);
                    }
                    previous=point;
                }
            }
            finally{GUI.matrix=matrix;GUI.color=old;}
        }
        void Bar(Rect rect,float fraction,Color fill)
        {
            var old=GUI.color; GUI.color=new Color(.06f,.07f,.10f,.95f); GUI.DrawTexture(rect,Texture2D.whiteTexture);
            GUI.color=fill; GUI.DrawTexture(new Rect(rect.x+2,rect.y+2,(rect.width-4)*Mathf.Clamp01(fraction),rect.height-4),Texture2D.whiteTexture); GUI.color=old;
        }
        void Panel(Rect rect)
        {
            var old=GUI.color; GUI.color=new Color(.10f,.12f,.16f,.98f);
            GUI.DrawTexture(rect,Texture2D.whiteTexture); GUI.color=old;
        }
        public Sprite GetWeaponIcon(string form)
        {
            var sets = arena ? arena.characterAnimations?.sets : null;
            var set = sets == null ? null : System.Array.Find(sets, value => value != null && value.form == form);
            return set?.Find("idle")?.At(0, false);
        }
        public void DrawWeaponIcon(string form, Rect bounds)
        {
            var sprite = GetWeaponIcon(form);
            if (!sprite) return;
            var set = System.Array.Find(arena.characterAnimations.sets, value => value != null && value.form == form);
            var crop = set.iconUvRect;
            var uv = crop != null && crop.Length == 4 ? new Rect(crop[0], crop[1], crop[2], crop[3])
                : new Rect(sprite.rect.x / sprite.texture.width, sprite.rect.y / sprite.texture.height, sprite.rect.width / sprite.texture.width, sprite.rect.height / sprite.texture.height);
            float ratio = uv.width * sprite.texture.width / (uv.height * sprite.texture.height);
            float width = Mathf.Min(bounds.width, bounds.height * ratio), height = width / ratio;
            GUI.DrawTextureWithTexCoords(new Rect(bounds.center.x - width * .5f, bounds.center.y - height * .5f, width, height), sprite.texture, uv, true);
        }
        void DrawHud()
        {
            var style=new GUIStyle(GUI.skin.label) {fontSize=16,alignment=TextAnchor.MiddleCenter};
            var attackStyle=new GUIStyle(style) {fontSize=28,fontStyle=FontStyle.Bold};
            attackStyle.normal.textColor=new Color(1f,.38f,.12f);
            if(CompactHud)DrawCompactStatus();
            else {
            Panel(new Rect(12,Screen.height-102,320,90));
            GUI.Label(new Rect(24,Screen.height-98,290,26),"PLAYER HP  "+arena.Player.health.ToString("0.#")+" / "+arena.Player.maxHealth,style);
            Bar(new Rect(24,Screen.height-68,290,20),arena.Player.health/arena.Player.maxHealth,new Color(.2f,.85f,.45f));
            GUI.Label(new Rect(24,Screen.height-45,290,25),"Dash: "+(arena.Player.DashRemaining<=0 ? "READY" : arena.Player.DashRemaining.ToString("0.0")+"s"),style);
            for(int i=0;i<2;i++)
            {
                var w=arena.Player.weapons[i]; float left=344+i*260;
                var form=arena.catalog.forms[w.FormIndex]; var skill=form.specials[w.SpecialIndex]; var cd=w.Cooldown;
                float remaining=cd.Available<cd.Capacity ? Mathf.Max(0,cd.NextReady-arena.Now) : 0;
                Panel(new Rect(left,Screen.height-102,250,90));
                if (!arena.Player.IsWeaponSlotReady(i))
                {
                    GUI.Label(new Rect(left+8,Screen.height-94,234,70),"슬롯 "+(i+1)+" / 도면 선택 대기\n시작방에서 도면을 선택하세요",style);
                    continue;
                }
                string attackName=skill.name;
                DrawWeaponIcon(form.id, new Rect(left+5,Screen.height-100,24,24));
                GUI.Label(new Rect(left+32,Screen.height-101,214,24),(i==arena.Player.selected ? "[선택] " : "[Q] ")+form.name+" / "+attackName,new GUIStyle(style) {fontSize=13});
                if(skill.unavailable)
                {
                    GUI.Label(new Rect(left+5,Screen.height-65,240,36),"S: 기본 투척 / 특수 설계 대기",style);
                    continue;
                }
                Bar(new Rect(left+12,Screen.height-69,226,15),1-remaining/cd.Period,i==arena.Player.selected ? new Color(.3f,.7f,1) : Color.gray);
                string label=i==arena.Player.selected && arena.Player.ApproachRemaining>0 ? "W 재입력: 접근 "+arena.Player.ApproachRemaining.ToString("0.0")+"초" : remaining>0 ? "다음 충전 "+remaining.ToString("0.0")+"초" : "사용 가능";
                if(arena.Player.ShurikenReturnPending(w)) label="복귀 예약 "+arena.Player.ShurikenReturnRemaining(w).ToString("0.0")+"초";
                GUI.Label(new Rect(left+5,Screen.height-50,240,27),label+"  "+cd.Available+"/"+cd.Capacity,style);
            }
            }
            if(arena.ViewCamera)
            {
                foreach(var e in arena.enemies)
                {
                    if(!e||!e.isActiveAndEnabled||e.Dead) continue;
                    // Bosses get one long bar along the bottom of the view instead of a small bar at their feet.
                    if(e.isBoss){var boss=new Rect(Mathf.Round(Screen.width*.2f),Screen.height-44,Mathf.Round(Screen.width*.6f),16);Bar(boss,e.health/Mathf.Max(.001f,e.maxHealth),e.bossBarColor);DrawMarkCount(e,boss);continue;}
                    if(arena.FeelFeedback && arena.FeelFeedback.CompactHealthBars) {
                        var foot=ProjectWorldPoint(EnemyHealthAnchor(e));
                        if(foot.z<=0||foot.x<0||foot.x>Screen.width||foot.y<0||foot.y>Screen.height)continue;
                        float fraction=Mathf.Clamp01(e.health/Mathf.Max(.001f,e.maxHealth));
                        float compactWidth=e.isBoss?110:62;
                        var compact=new Rect(Mathf.Round(foot.x-compactWidth/2),Mathf.Round(Screen.height-foot.y),compactWidth,8);
                        Bar(compact,fraction,EnemyHealthColor(fraction));DrawMarkCount(e,compact);
                        continue;
                    }
                    var p=ProjectWorldPoint(EnemyHealthAnchor(e));
                    if(p.z<=0 || p.x<0 || p.x>Screen.width || p.y<0 || p.y>Screen.height) continue;
                    float width=e.isBoss ? 210 : 150; float top=Screen.height-p.y-36;
                    Panel(new Rect(p.x-width/2-3,top-2,width+6,42));
                    GUI.Label(new Rect(p.x-width/2,top,width,24),e.profile==1 ? "원거리 적" : "근접 적",style);
                    var full=new Rect(p.x-width/2,top+25,width,12);
                    Bar(full,e.health/e.maxHealth,e.isBoss ? new Color(1,.3f,.25f) : new Color(.95f,.65f,.1f));DrawMarkCount(e,full);
                    if(e is FieldMouseEnemy species && species.Brain != null && species.Brain.State == FieldMouseState.Prepare)
                    {
                        var old=GUI.color;
                        GUI.color=new Color(1f,.35f,.12f);
                        GUI.DrawTexture(new Rect(p.x-width/2-5,top-4,width+10,3),Texture2D.whiteTexture);
                        GUI.DrawTexture(new Rect(p.x-width/2-5,top+40,width+10,3),Texture2D.whiteTexture);
                        GUI.color=old;
                        GUI.Label(new Rect(p.x+width/2+5,top-2,30,40),"!",attackStyle);
                    }
                }
            }
            if(CompactHud)return;
            var help=new GUIStyle(GUI.skin.label) {fontSize=16}; help.normal.textColor=new Color(.10f,.12f,.16f);
            GUI.Label(new Rect(18,12,850,26),PlayerControls.Prompt(arena.Player,"{Move} 이동   {Jump} 점프   {Dash} 대시   {Weapon1Basic}/{Weapon2Basic} 기본1/2   {Weapon1Special}/{Weapon2Special} 스킬1/2   {DevelopmentReset} 리셋   키보드 F1 장비, F2 판정"),help);
            if(GUI.Button(new Rect(Screen.width-150,12,138,32),showPanel ? "장비 닫기 [F1]" : "장비 선택 [F1]")) showPanel=!showPanel;
            if(arena.Player.LastFallTime>=0) GUI.Label(new Rect(18,40,850,25),"낙사 "+arena.Player.LastFallTime.ToString("0.00")+"초 → 3초 전 기준 "+(arena.Player.LastFallTime-3).ToString("0.00")+"초 / 복귀 접지 "+(arena.Player.LastRestoreTime<0 ? "방 입구" : arena.Player.LastRestoreTime.ToString("0.00")+"초")+" "+arena.Player.LastRestorePosition,help);
        }
        GUIStyle markStyle;
        // 무지개 표식 수를 체력바 오른쪽에 쌓인 순번 색으로 적는다(빨·주·노·초·파·남·보). 표식이 없으면 그리지 않는다
        void DrawMarkCount(CombatActor e,Rect bar)
        {
            int n=Mathf.Min(arena.ReadMvpMarkDisplay(e),CombatArena.MvpMarkColors.Length);
            if(n<=0)return;
            markStyle??=new GUIStyle(GUI.skin.label){fontSize=13,fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleLeft,clipping=TextClipping.Overflow};
            var r=new Rect(bar.xMax+4,bar.center.y-9,24,18);string text=n.ToString();
            markStyle.normal.textColor=new Color(0,0,0,.85f);GUI.Label(new Rect(r.x+1,r.y+1,r.width,r.height),text,markStyle);
            markStyle.normal.textColor=CombatArena.MvpMarkColors[n-1];GUI.Label(r,text,markStyle);
        }
        public void DrawCompactStatus(bool showHints = true)
        {
            var bounds=CompactStatusBounds(Screen.width,Screen.height);
            float unit=(bounds.width-16)/3;
            var label=new GUIStyle(GUI.skin.label){fontSize=11,alignment=TextAnchor.MiddleLeft,clipping=TextClipping.Clip};
            label.normal.textColor=new Color(.96f,.94f,.88f);
            Panel(new Rect(bounds.x,bounds.y,unit,bounds.height));
            GUI.Label(new Rect(bounds.x+7,bounds.y+2,unit-14,18),"HP "+arena.Player.health.ToString("0")+" / "+arena.Player.maxHealth.ToString("0")+(arena.ReadInkShield()>0?"  보호막 "+arena.ReadInkShield().ToString("0.#"):""),label);
            Bar(new Rect(bounds.x+7,bounds.y+24,unit-14,7),arena.Player.health/arena.Player.maxHealth,new Color(.2f,.85f,.45f));
            for(int i=0;i<2;i++) {
                float x=bounds.x+(unit+8)*(i+1);var w=arena.Player.weapons[i];var form=arena.catalog.forms[w.FormIndex];
                Panel(new Rect(x,bounds.y,unit,bounds.height));
                if(!arena.Player.IsWeaponSlotReady(i)){GUI.Label(new Rect(x+7,bounds.y+9,unit-14,24),"도면 선택 대기",label);continue;}
                bool selected=i==arena.Player.selected;var skill=form.specials[w.SpecialIndex];var cd=w.Cooldown;
                float left=cd.Available<cd.Capacity?Mathf.Max(0,cd.NextReady-arena.Now):0;
                bool charging=w.ExperimentalChargeStamp && selected && arena.Player.InkCharging;
                GUI.Label(new Rect(x+7,bounds.y+2,unit-14,18),(i==0?"[Q/E] ":"[W/R] ")+form.name+" / "+(w.ExperimentalChargeStamp?"완충 일격":skill.name),label);
                float fraction=w.ExperimentalChargeStamp?(charging?arena.Player.InkChargeFraction:arena.ReadInkChargeStore(i)):(cd.Period>0?1-left/cd.Period:1);
                Bar(new Rect(x+7,bounds.y+24,unit-60,5),fraction,selected?new Color(.4f,.8f,1):Color.gray);
                GUI.Label(new Rect(x+unit-48,bounds.y+17,43,18),w.ExperimentalChargeStamp?(fraction*100).ToString("0")+"%":left>0?left.ToString("0.0")+"s":cd.Available+"/"+cd.Capacity,label);
            }
            if(!showHints)return;
            var hint=new GUIStyle(label){fontSize=11};
            Panel(new Rect(10,8,Mathf.Min(690,Screen.width-20),23));
            GUI.Label(new Rect(17,9,Screen.width-34,21),PlayerControls.Prompt(arena.Player,"{Move} 이동  {Jump} 점프  {Dash} 대시  {Weapon1Basic}/{Weapon2Basic} 평타1/2  {Weapon1Special}/{Weapon2Special} 스킬1/2  {Interact} 상호작용  {Map} 지도  {DevelopmentReset} 재시작  키보드 F1~F4 개발 도구"),hint);
        }
        public bool ExternalHud { get; set; }
        void OnGUI()
        {
            if (!arena || !showDebugUI) return;
            if (!arena.Player) return;
            if(uiFont) GUI.skin.font=uiFont;
            if(showHud && !ExternalHud)DrawHud();
            if (arena.showBoxes)
            {
                DrawWorldBox(new Bounds(arena.Player.Center,arena.Player.size),new Color(0,1,1,.12f));
                foreach(var inkBox in arena.InkDebugBoxes())DrawWorldBox(inkBox,new Color(.65f,.25f,1,.2f));
                foreach (var e in arena.enemies) if (e && !e.Dead) DrawWorldBox(e.hurtCollider.bounds,new Color(1,.2f,.1f,.15f));
                for (int i = 0; i < arena.DebugFlashCount; i++)
                {
                    var flash = arena.GetDebugFlash(i);
                    DrawWorldBox(flash.box, new Color(flash.color.r,flash.color.g,flash.color.b,.25f));
                }
                foreach (var cast in arena.casts)
                    if (cast) for (int i = 0; i < cast.DebugBoxCount; i++) DrawWorldBox(cast.GetDebugBox(i), new Color(.8f,.4f,1,.18f));
                CollectAttackHitboxes(attackHitboxes);
                foreach(var box in attackHitboxes)
                {
                    var color=box.Kind==HitboxKind.EnemyPrepare?new Color(1,.8f,.15f,.1f):
                        box.Kind==HitboxKind.EnemyAttack||box.Kind==HitboxKind.EnemyProjectile?new Color(1,.2f,.15f,.18f):
                        box.Kind==HitboxKind.PlayerExplosion?new Color(1,.6f,.1f,.16f):new Color(.2f,.85f,1,.18f);
                    if(box.Radius>0)DrawWorldCircle(box.Bounds.center,box.Radius,color);
                    else DrawWorldBox(box.Bounds,color);
                }
            }
            if(!showPanel) return;
            DrawEquipmentPanel(new Rect(Screen.width-392,78,380,Screen.height-190));
        }
        public void DrawEquipmentPanel(Rect rect)
        {
            if (!arena || !arena.Player || !showPanel) return;
            if (uiFont) GUI.skin.font = uiFont;
            Panel(rect);
            GUILayout.BeginArea(rect,GUI.skin.box);
            GUILayout.Label("전투 테스트 / 장비 선택");
            GUILayout.Label(PlayerControls.Prompt(arena.Player,"{Weapon1Basic}/{Weapon2Basic} 기본1/2 / {Weapon1Special}/{Weapon2Special} 스킬1/2 / {DevelopmentReset} 리셋"));
            GUILayout.Label("현재 들고 있는 무기: 슬롯 "+(arena.Player.selected+1)+" / "+arena.Player.Form.name);
            if (arena.Player.Dead) GUILayout.Label("RUN ENDED. F5 starts a fresh test run (selected gear preserved).");
            if (showPanel)
            {
                panelScroll = GUILayout.BeginScrollView(panelScroll);
                arena.DrawFeedbackOptions?.Invoke();
                GUILayout.Label("캐릭터 이동속도: "+arena.Movement.RunSpeed.ToString("0.###")+" 유닛/초");
                if(runSpeedText==null)runSpeedText=arena.Movement.RunSpeed.ToString(System.Globalization.CultureInfo.InvariantCulture);
                GUILayout.BeginHorizontal();
                GUILayout.Label("이동속도 (유닛/초)");
                runSpeedText=GUILayout.TextField(runSpeedText,GUILayout.Width(90));
                GUILayout.EndHorizontal();
                if(GUILayout.Button("이동속도 적용 / 전투 리셋"))
                    runSpeedMessage=arena.TryApplyTestRunSpeed(runSpeedText,out var speedError)
                        ? "이동속도 적용 완료 / 현재 플레이 중 유지" : speedError;
                if(!string.IsNullOrEmpty(runSpeedMessage))GUILayout.Label(runSpeedMessage);
                GUILayout.Label("대시, 점프 수치 유지 / 플레이 재시작 시 기본값 복원");
                bool combat = arena.CombatActive;
                if (GUILayout.Button(combat ? "적 AI 중지 / 장비 변경 허용" : "적 AI 시작 / 전투 중 장비 변경 잠금")) foreach (var e in arena.enemies) { e.aggressive = !combat; if (combat) e.CancelAttack(); }
                if (GUILayout.Button(PlayerControls.Prompt(arena.Player,"테스트 리셋 [{DevelopmentReset}]"))) arena.ResetTest();
                GUILayout.Label(arena.Player.EditingAllowed ? "슬롯과 무기를 선택해 장착하세요. 기본공격, 스킬 입력으로 해당 무기를 사용합니다." : "적 AI를 끄고 행동이 끝나면 장비를 변경할 수 있습니다.");
                editingSlot = GUILayout.Toolbar(editingSlot,new[] { "무기 슬롯 1", "무기 슬롯 2" });
                var w = arena.Player.weapons[editingSlot];
                bool before = GUI.enabled; GUI.enabled = arena.Player.EditingAllowed;
                for(int row=0;row<arena.catalog.forms.Length;row+=3)
                {
                    GUILayout.BeginHorizontal();
                    for(int i=row;i<Mathf.Min(row+3,arena.catalog.forms.Length);i++)
                    {
                        var formId = arena.catalog.forms[i].id;
                        var button = GUILayoutUtility.GetRect(new GUIContent(arena.catalog.forms[i].name),GUI.skin.button,GUILayout.Height(42),GUILayout.ExpandWidth(true));
                        if (GUI.Button(button,"")) arena.Player.Reconfigure(editingSlot,i,w.SpecialIndex);
                        bool hasIcon = GetWeaponIcon(formId) != null;
                        if (hasIcon) DrawWeaponIcon(formId,new Rect(button.x+3,button.y+3,32,36));
                        GUI.Label(new Rect(button.x+(hasIcon ? 38 : 4),button.y+3,button.width-(hasIcon ? 40 : 8),36),(w.FormIndex==i ? "✓ " : "")+arena.catalog.forms[i].name);
                    }
                    GUILayout.EndHorizontal();
                }
                var form=arena.catalog.forms[w.FormIndex];
                GUILayout.Label(PlayerControls.Prompt(arena.Player,"{Weapon1Special}/{Weapon2Special} 스킬 / 장착 잉크"));
                for(int i=0;i<4;i++)
                {
                    bool available=!form.specials[i].unavailable;
                    GUI.enabled=arena.Player.EditingAllowed && available;
                    string label=form.specials[i].name;
                    string[] modes={"미장착","범위","이동","후속타"};
                    if(GUILayout.Button((!w.ExperimentalChargeStamp && w.SpecialIndex==i ? "✓ " : "")+modes[i]+": "+label)) SelectNativeSpecial(editingSlot,i,out inkMessage);
                }
                GUI.enabled=arena.Player.EditingAllowed;
                EquipmentTuning?.Invoke(editingSlot, form, w);
                string[] stats = { "빈 칸", "기본 피해 +20%", "특수 피해 +20%", "기본 공속 +10%", "특수 쿨감 +10%" };
                for (int i = 0; i < 5; i++)
                    if (GUILayout.Button("강화 " + (i+1) + ": " + stats[w.StatSlots[i]])) { w.StatSlots[i] = (w.StatSlots[i]+1)%5; arena.Player.Reconfigure(editingSlot,w.FormIndex,w.SpecialIndex); }
                GUI.enabled = before;
                DrawInkExperiment(w);
                GUILayout.Label("전용 잉크는 설계 보류 / 테스트 장비 선택 화면입니다.");
                GUILayout.Label("수리검: 기본 수치는 준비됨 / 생성, 지형 규칙 확정 대기");
                foreach (var e in arena.enemies) GUILayout.Label(e.name + (e.Telegraph >= 0 ? " ATTACK " + e.Telegraph.ToString("0.00") : ""));
                GUILayout.Label("Recent events:");
                var events = arena.GetRecentEvents();
                while (events.MoveNext()) GUILayout.Label(events.Current);
                GUILayout.EndScrollView();
            }
            GUILayout.EndArea();
        }
        void DrawInkExperiment(WeaponState weapon)
        {
            GUILayout.Space(12);
            GUILayout.Label("색상 파츠 / 공통 전투 실험 / "+InkPartCatalog.ImplementedCount+"종");
            GUILayout.Label("자유 6칸 / 수치/중복은 실험값 / 강철 주입 보류");
            if (!arena.ExternalTestPanel && GUILayout.Button(InkExperimentOpen ? "실험 설정 완료 / 전투 재개" : "파츠 실험 설정 / 전투 일시정지"))
                SetInkExperimentOpen(!InkExperimentOpen);
            bool enabledBefore = GUI.enabled;
            GUI.enabled = enabledBefore && arena.Player.EditingAllowed;
            if (GUILayout.Button("별도 시험 차징 스탬프: "+(weapon.ExperimentalChargeStamp?"ON":"OFF")))
                arena.Player.TrySetChargeStamp(editingSlot,!weapon.ExperimentalChargeStamp,out inkMessage);
            if (inkPartOptions == null)
            {
                var asset = Resources.Load<TextAsset>("TrainingInkParts");
                inkPartOptions = asset ? JsonUtility.FromJson<InkPartOptions>(asset.text).parts : System.Array.Empty<InkPartOption>();
            }
            var slots = weapon.InkPalette.Read().Slots;
            for (int i=0;i<slots.Count;i++)
            {
                var part = System.Array.Find(inkPartOptions,p=>p.id==slots[i]?.PartId);
                if (GUILayout.Button((inkPaletteSlot==i?"✓ ":"")+(i+1)+"칸: "+(part==null?"빈 칸":part.name))) inkPaletteSlot=i;
            }
            if (GUILayout.Button("선택한 칸 비우기")) arena.Player.TrySetInkPart(editingSlot,inkPaletteSlot,null,out inkMessage);
            inkColor = GUILayout.Toolbar(inkColor,new[]{"전체","빨강","노랑","초록","파랑","보라"});
            string[] colors={"","red","yellow","green","blue","purple"};
            if (!string.IsNullOrEmpty(inkMessage)) GUILayout.Label(inkMessage);
            foreach (var part in inkPartOptions)
            {
                if (inkColor!=0 && part.color!=colors[inkColor]) continue;
                if (GUILayout.Button(part.name+(InkPartCatalog.Implemented(part.id)?"":" [보류]")))
                {
                    if (arena.Player.TrySetInkPart(editingSlot,inkPaletteSlot,part.id,out inkMessage))
                        inkMessage=part.name+": "+part.description+"\n"+(part.duplicate?"중복 가능":"무기당 1개")+" / 자체 쿨타임 "+part.cooldown+"초"+
                            (string.IsNullOrEmpty(part.requiresStamp)?"":" / 별도 시험 차징 스탬프 ON 필요");
                }
            }
            GUI.enabled = enabledBefore;
            GUILayout.Label("F1로 닫으면 전투 재개 / 이미 발동한 효과는 당시 수치 유지");
        }
    }
}
