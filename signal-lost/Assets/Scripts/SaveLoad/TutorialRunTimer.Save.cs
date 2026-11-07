using UnityEngine;

namespace SignalLost.Story
{
    // Save and load (Christian, 10-27): the save-only part of TutorialRunTimer. The rest is in Story/TutorialRunTimer.cs.
    public sealed partial class TutorialRunTimer
    {
        /// <summary>
        /// Loading a save: the clock carries on from the saved time and deaths. Splits before the load are not known,
        /// so a loaded run's CSV row only has the stages reached after it.
        /// </summary>
        public void Resume(float elapsed, int deaths)
        {
            started = Time.time - Mathf.Max(0f, elapsed);
            finished = -1f;
            Deaths = Mathf.Max(0, deaths);
            stageStarts.Clear();
        }
    }
}
