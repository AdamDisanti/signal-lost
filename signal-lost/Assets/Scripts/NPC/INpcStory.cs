using SignalLost.Player;

namespace SignalLost.NPC
{
    /// <summary>
    /// A scene's story (its quests) decides what NPCs say when the player talks to them. Implement this on the
    /// scene's quest flow; <see cref="NpcActor"/> asks it first and falls back to the NPC's own small talk or shop.
    /// </summary>
    public interface INpcStory
    {
        /// <summary>Handles E on an NPC. Return false to let the NPC do its default thing.</summary>
        bool TalkTo(NpcActor npc, PlayerInteractor player);
    }
}
