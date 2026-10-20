namespace SignalLost.Combat
{
    /// <summary>
    /// Something the mining laser's combo finisher can stun. The desert fodder implement it; crabs, frogs and
    /// bosses do not, so only small enemies are stunned (meeting notes 2026-10-20).
    /// </summary>
    public interface IStunnable
    {
        void Stun(float seconds);
    }
}
