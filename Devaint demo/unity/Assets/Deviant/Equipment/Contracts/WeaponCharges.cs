namespace Deviant.Equipment.Contracts
{
    public readonly struct ChargeState
    {
        public int Capacity { get; }
        public int Available { get; }
        public float Period { get; }
        public float NextReady { get; }
        public ChargeState(int capacity, int available, float period, float nextReady)
        { Capacity = capacity; Available = available; Period = period; NextReady = nextReady; }
    }
    public interface IWeaponCharges
    {
        ChargeState Read();
        void Reset(int capacity, float period);
        void Tick(float now);
        bool Spend(float now);
        void Refund(float seconds, float now);
        float Debt(float now);
        void Reconfigure(int capacity, float period, float now);
    }
    public interface IWeaponChargeClock { void Rebase(float previousNow, float nextNow); }
    // A successful defensive action may replace recovery with a fixed rearm
    // delay. Refunds and period changes cannot bypass that delay.
    public interface IWeaponChargeRearm { void Rearm(float seconds, float now); }
}
