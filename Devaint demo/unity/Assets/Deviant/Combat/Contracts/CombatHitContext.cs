using System;

namespace Deviant.Combat.Contracts
{
    public enum HitAnchorKind { Missing, SweepContact, OverlapRepresentative, BeamRepresentative, RadialRepresentative, TargetFallback }
    public readonly struct CombatAttackStamp
    {
        public Guid WeaponId { get; }
        public Deviant.Equipment.Contracts.InkPaletteSnapshot InkPalette { get; }
        public string FormId { get; }
        public string ActionId { get; }
        public string DefinitionVersion { get; }
        public bool Basic { get; }
        public int Conversion { get; }
        public bool IsSpecified => !string.IsNullOrEmpty(ActionId) && !string.IsNullOrEmpty(FormId);
        public CombatAttackStamp(Guid weaponId, string formId, string actionId, string version, bool basic, int conversion = -1)
            : this(weaponId, formId, actionId, version, basic, conversion, null) { }
        public CombatAttackStamp(Guid weaponId, string formId, string actionId, string version, bool basic, int conversion, Deviant.Equipment.Contracts.InkPaletteSnapshot inkPalette)
        { InkPalette = inkPalette; WeaponId = weaponId; FormId = formId; ActionId = actionId; DefinitionVersion = version; Basic = basic; Conversion = conversion; }
    }
    public readonly struct CombatHitContact
    {
        public float X { get; }
        public float Y { get; }
        public float IncomingX { get; }
        public float IncomingY { get; }
        public HitAnchorKind Kind { get; }
        public bool HasPoint => Kind != HitAnchorKind.Missing;
        public bool HasDirection => IncomingX != 0 || IncomingY != 0;
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        public CombatHitContact(float x, float y, float incomingX, float incomingY, HitAnchorKind kind)
        {
            bool valid = Finite(x) && Finite(y) && kind != HitAnchorKind.Missing;
            X = valid ? x : 0; Y = valid ? y : 0; Kind = valid ? kind : HitAnchorKind.Missing;
            double length = Math.Sqrt((double)incomingX * incomingX + (double)incomingY * incomingY);
            bool directional = Finite(incomingX) && Finite(incomingY) && length > .000001;
            IncomingX = directional ? (float)(incomingX / length) : 0;
            IncomingY = directional ? (float)(incomingY / length) : 0;
        }
    }
    public readonly struct CombatCompletedStep
    {
        public CombatInputSession Session { get; }
        public long Tick { get; }
        public float Now { get; }
        public float Delta { get; }
        public CombatCompletedStep(CombatInputSession session, long tick, float now, float delta)
        { Session = session; Tick = tick; Now = now; Delta = delta; }
    }
    public readonly struct CombatFootSweep
    {
        public float FromX { get; }
        public float FromY { get; }
        public float ToX { get; }
        public float ToY { get; }
        public float Width { get; }
        public CombatFootSweep(float x, float y, float toX, float toY, float width)
        { FromX = x; FromY = y; ToX = toX; ToY = toY; Width = width; }
    }
}
