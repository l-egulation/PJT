using System;
using UnityEngine;

namespace Deviant.Chapter2
{
    [Serializable] public sealed class Chapter2EliteSettings
    {
        public int schema,fragmentCount;
        public bool automaticHitReaction;
        public float hp,bodyWidth,bodyHeight,bodyArtScale,teleportCast,punchPrepare,punchDamage,punchWidth,attackHeight,
            knockDistance,knockSeconds,wallDamage,wallStun,arrivalGap,comboSpeed,comboTell,comboStep,comboPush,comboWidth,
            rockSize,rockAhead,rockHeight,rockForm,rockFire,fanDegrees,fragmentSpeed,fragmentDamage,fragmentVisualSize,
            rest,activeSeconds,comboStepSeconds,fragmentColliderSize;
        public float[] comboTimes,comboDamage;
        public static Chapter2EliteSettings Load()
        {
            var asset=Resources.Load<TextAsset>("Chapter2Elite");
            if(!asset)throw new InvalidOperationException("Chapter2Elite data missing");
            var s=JsonUtility.FromJson<Chapter2EliteSettings>(asset.text);
            if(s.schema!=1||s.hp<=0||s.comboSpeed<=0||s.bodyArtScale<=0||s.knockSeconds<=0||s.activeSeconds<=0||s.comboStepSeconds<=0||
                s.rockHeight<=0||s.rockSize<=0||s.rockAhead<=0||s.fragmentCount!=5||s.comboTimes?.Length!=3||s.comboDamage?.Length!=3||s.rockFire<s.rockForm)
                throw new InvalidOperationException("Invalid Chapter2Elite data");
            return s;
        }
    }
}
