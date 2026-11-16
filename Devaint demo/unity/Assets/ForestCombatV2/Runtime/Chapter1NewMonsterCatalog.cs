using System;
using System.Collections.Generic;
using Deviant.Combat;
using UnityEngine;

namespace ForestCombatV2
{
    [Serializable] public sealed class Chapter1NewMonsterSpec
    {
        public string id,name,resource,sourceSha256;
        public bool ranged;
        public float firstDelayMin,firstDelayMax;
        public float windupSeconds,warningSeconds,ascentSeconds,offscreenSeconds,fallSeconds,stopBeforeChargeSeconds;
        // Red over the body through the telegraph, its alpha rising to windupTintMax (0 for none).
        public int[] windupTint;
        public float windupTintMax;
        // The player a melee hit lands on flies: playerLaunchSpeed units/s along the hit for playerLaunchSeconds, starting
        // upward at playerLaunchUp units/s (0 seconds for none).
        public float playerLaunchSpeed, playerLaunchSeconds, playerLaunchUp;
        public float[] blastSize;
        public FieldMouseDefinition definition;
    }
    [Serializable] public sealed class Chapter1NewMonsterCatalog
    {
        public string schema,status;
        public Chapter1NewMonsterSpec[] monsters;
        public static Chapter1NewMonsterCatalog Load()
        {
            var text=Resources.Load<TextAsset>("Chapter1NewMonsters");
            if(!text)throw new InvalidOperationException("Export Chapter1NewMonsters before running Chapter1.");
            var data=JsonUtility.FromJson<Chapter1NewMonsterCatalog>(text.text);
            if(data.schema!="chapter1-new-monsters-v1"||data.monsters.Length!=2)throw new InvalidOperationException("Invalid new monster catalog.");
            return data;
        }
        public static void ExtendSource(ForestData source)
        {
            var kinds=new List<MonsterKind>(source.monsters);
            foreach(var spec in Load().monsters)
                if(!kinds.Exists(m=>m.id==spec.id))kinds.Add(new MonsterKind{id=spec.id,name=spec.name,width=256,height=160,
                    baseline=152,bodyWidth=Mathf.RoundToInt(spec.definition.bodySize[0]*32),
                    bodyHeight=Mathf.RoundToInt(spec.definition.bodySize[1]*32),bodyOffset=spec.definition.bodyOffset[0],ranged=spec.ranged});
            source.monsters=kinds.ToArray();
        }
    }
}
