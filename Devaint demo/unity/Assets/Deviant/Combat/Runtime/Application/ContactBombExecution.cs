using Deviant.Combat.Contracts;

namespace Deviant.Combat.Execution
{
    // Embed privately and invoke on the same field. Native owns the clock,
    // velocity, position and the shared timed/contact terminal claim.
    public struct ContactBombExecution
    {
        float expires;
        public void Initialize(float now, float lifetime) => expires = now + lifetime;

        public void Step<T>(float dt, ref T host) where T : struct, IContactBombHost
        {
            if (host.Now >= expires)
            {
                if (host.ExplodeOnExpiry) host.Explode();
                else host.Remove();
                return;
            }
            float remaining = expires - host.Now;
            float step = dt < remaining ? dt : remaining;
            // The original Vector2 multiplication snapshots both velocity
            // components before reading gravity. Keep its scalar float order,
            // including the horizontal zero product for nonfinite gravity.
            float velocityX = host.VelocityX, velocityY = host.VelocityY;
            float horizontal = velocityX * step, vertical = velocityY * step;
            float fall = .5f * host.Gravity * step * step;
            float dx = horizontal + 0f * fall, dy = vertical + -1f * fall;
            host.SetVelocityY(host.VelocityY - host.Gravity * step);
            host.SweepTo(host.PositionX + dx, host.PositionY + dy);
            if (!host.Stopped && host.Now + dt >= expires && host.ExplodeOnExpiry) host.Explode();
        }
    }
}
