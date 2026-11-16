using System;
using System.Globalization;
using Deviant.Definitions;
using UnityEngine;

namespace Deviant.Authoring
{
    [Serializable]
    public sealed class BasicMeleeEdit
    {
        public string formId, formName;
        public int hitIndex;
        public bool rootSymmetric;
        public BasicMeleeHitData hit;
        public string Id => formId + "_basic_" + hitIndex.ToString(CultureInfo.InvariantCulture);
    }

    public sealed class BasicMeleeDraft : ScriptableObject
    {
        public BasicMeleeEdit[] hits;
    }
}
