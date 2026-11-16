using UnityEngine;

namespace Deviant.Stage1Monsters
{
    public sealed class Stage1MonsterShowcase : MonoBehaviour
    {
        public Stage1MonsterPlayer[] players;
        public Camera previewCamera;
        public bool automatic=true;
        public bool paused;
        readonly string[] sequence={"idle","walk","attack","hit","death"};
        int[] phases;
        float[] ages;
        void Start()
        {
            phases=new int[players.Length];ages=new float[players.Length];
            foreach(var p in players) { p.Initialize();p.autoReturnToIdle=false; }
            ResetAll();
        }
        void Update()
        {
            // Integer display scale; no sprite filtering or animated Transform scale.
            int k=Mathf.Max(1,Mathf.FloorToInt(Screen.width/(36f*32)));
            previewCamera.orthographicSize=Screen.height/(64f*k);
            if(paused || !automatic)return;
            for(int i=0;i<players.Length;i++)
            {
                var p=players[i];ages[i]+=Time.deltaTime;
                if(ages[i]>=p.Clip.DurationMs/1000f+(p.Motion=="death"?.7f:p.Motion=="hit"?.35f:.25f))Next(i);
            }
        }
        void Next(int i)
        {
            var p=players[i];ages[i]=0;
            if(p.Definition.Find("idle")==null) { p.Play(p.Motion=="base"?"hit":"base",true);return; }
            phases[i]=(phases[i]+1)%sequence.Length;p.Play(sequence[phases[i]],true);
        }
        void ResetAll()
        {
            for(int i=0;i<players.Length;i++) { phases[i]=0;ages[i]=0;players[i].paused=paused;players[i].Play(players[i].Definition.Find("idle")==null?"base":"idle",true); }
        }
        void OnGUI()
        {
            GUI.Box(new Rect(12,12,Screen.width-24,110),"");
            GUI.Label(new Rect(24,18,950,24),"STAGE 1 / Approved native sprites / PPU 32 / Six colors / Ground-aligned pivots");
            float x=24;
            if(GUI.Button(new Rect(x,48,100,28),automatic?"Auto cycle":"Manual")) { automatic=!automatic;ResetAll(); }x+=108;
            foreach(string state in sequence)
            {
                if(GUI.Button(new Rect(x,48,80,28),state)) { automatic=false;foreach(var p in players)p.Play(state,true); }x+=88;
            }
            if(GUI.Button(new Rect(x,48,80,28),paused?"Resume":"Pause")) { paused=!paused;foreach(var p in players)p.paused=paused; }x+=88;
            if(GUI.Button(new Rect(x,48,80,28),"Flip"))foreach(var p in players)p.faceRight=!p.faceRight;
            GUI.Label(new Rect(24,84,1000,24),"Boss: base + hit only. Missing motions are not simulated. Hit preview duration is not gameplay hitstun.");
            string[] names={"Thorn Root","Ink Broom","Gutter Spitter","Well Warden","Clock Boss"};
            for(int i=0;i<players.Length;i++)
            {
                Vector3 pos=previewCamera.WorldToScreenPoint(players[i].transform.position+Vector3.down*.35f);
                GUI.Label(new Rect(pos.x-80,Screen.height-pos.y,190,50),names[i]+"\n"+players[i].Motion+" / "+(players[i].Frame+1));
            }
        }
    }
}
