namespace SignalLost.Enemies
{
    /// <summary>Any enemy that follows the fighting-radius rule. Lets spawners and encounters share one zone.</summary>
    public interface ITerritorial
    {
        Territory Territory { get; }
    }
}
