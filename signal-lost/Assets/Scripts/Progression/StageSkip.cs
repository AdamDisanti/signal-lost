using SignalLost.Enemies.Desert;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SignalLost.Progression
{
    /// <summary>
    /// TESTING TOOL (Lawson, 11-08): jump to the next stage (right arrow, the "stage_next" key binding) or the one
    /// before (left arrow, "stage_previous") in the order the islands are sailed: Crash Meadow (the tutorial), then the
    /// Desert Dunes, then the Wasteland (the finale, always last; Adam, 11-09). Arriving this way counts every stage before the one you land in as finished, the way playing it
    /// through would have left things:
    /// <list type="bullet">
    /// <item>the stage is cleared, unlocking the next (so its boat sails);</item>
    /// <item>its bosses' ship parts are recovered, and its main ship part (the desert's Engine Core);</item>
    /// <item>its bosses' trophies are on the camp's stands (the camp save remembers these, like real ones).</item>
    /// </list>
    /// The stage you land in starts fresh, with its own quests from the beginning. Going back to the tutorial has nothing
    /// before it to finish. Past the last stage (the ending isn't built) gives a message instead. The upgrades and
    /// pockets don't carry between scenes yet for anyone (the save system's job), so the tool doesn't add them either.
    /// </summary>
    public static class StageSkip
    {
        private sealed class StageInfo
        {
            public string Id, Scene, Name, Unlocks, MainPart;
            public (string part, string stage)[] Parts;
            public string[] Trophies;
            /// <summary>Stands to prefer for the trophies (the desert's Chop Shop rack is stands 4-6).</summary>
            public int[] Stands;
        }

        private static readonly StageInfo[] Stages =
        {
            new StageInfo
            {
                Id = StageProgress.Tutorial, Scene = Core.SceneNames.FirstLevel, Name = "Crash Meadow", Unlocks = StageProgress.Desert,
                Parts = new[] { ("Cooling system", StageProgress.Tutorial) },
                Trophies = new[] { "chief_toad_crown" }, Stands = new[] { 0 }
            },
            new StageInfo
            {
                Id = StageProgress.Desert, Scene = "DesertDunes", Name = "the Desert Dunes", Unlocks = "NextStage", MainPart = "Engine Core",
                Parts = new[] { ("Wing panels", StageProgress.Desert), ("Rocket thruster", StageProgress.Desert), ("Fuel tank", StageProgress.Desert) },
                Trophies = new[] { "old_buzzard_feathers", "burnout_thorns", "sludge_stinger", "core_worm_fang" }, Stands = new[] { 3, 4, 5, 1 }
            },
            // The finale stays last; the islands in between (Mudlands, Fog Forest, Crystal Caves) go above it once
            // their order is decided (Adam, 11-09).
            new StageInfo
            {
                Id = StageProgress.Finale, Scene = "Wasteland", Name = "the Wasteland (finale)",
                Parts = new (string, string)[0], Trophies = new string[0], Stands = new int[0]
            },
        };

        private const string NotBuilt = "the ending";
        private static bool loading;

        /// <summary>The stage the open scene belongs to, or -1 (the menu, a test scene).</summary>
        public static int CurrentIndex
        {
            get
            {
                string scene = SceneManager.GetActiveScene().name;
                for (int i = 0; i < Stages.Length; i++) if (Stages[i].Scene == scene) return i;
                return -1;
            }
        }

        /// <summary>Goes to the next stage (+1) or the previous one (-1).</summary>
        public static void Go(int step)
        {
            if (loading) return;
            int current = CurrentIndex;
            if (current < 0)
            {
                MiniBossHud.Message("TESTING TOOL: stage skip only works in a stage scene (Crash Meadow, the Desert Dunes, the Wasteland).", new Color(1f, 0.85f, 0.3f), 3f);
                return;
            }
            int target = current + step;
            if (target < 0)
            {
                MiniBossHud.Message("TESTING TOOL: this is the first stage.", new Color(1f, 0.85f, 0.3f), 3f);
                return;
            }
            if (target >= Stages.Length)
            {
                FinishUpTo(Stages.Length); // the stage you're leaving counts as done all the same
                MiniBossHud.Message($"TESTING TOOL: {NotBuilt} isn't built yet. {Stages[current].Name} counts as finished.", new Color(1f, 0.85f, 0.3f), 4f);
                return;
            }
            FinishUpTo(target);
            if (!Application.CanStreamedLevelBeLoaded(Stages[target].Scene))
            {
                MiniBossHud.Message($"TESTING TOOL: {Stages[target].Scene} isn't in the build's scene list.", new Color(1f, 0.5f, 0.4f), 4f);
                return;
            }
            loading = true;
            arriving = target;
            Time.timeScale = 1f;
            SceneManager.sceneLoaded += Loaded;
            SceneManager.LoadScene(Stages[target].Scene);
        }

        private static int arriving;

        private static void Loaded(Scene scene, LoadSceneMode mode)
        {
            SceneManager.sceneLoaded -= Loaded;
            loading = false;
            MiniBossHud.Message(arriving == 0
                    ? $"TESTING TOOL: back to {Stages[0].Name}, from the start."
                    : $"TESTING TOOL: skipped to {Stages[arriving].Name}. Earlier stages count as finished.",
                new Color(1f, 0.85f, 0.3f), 4f);
        }

        /// <summary>Counts every stage before <paramref name="target"/> as finished (see the class summary).</summary>
        public static void FinishUpTo(int target)
        {
            for (int i = 0; i < target && i < Stages.Length; i++)
            {
                StageInfo stage = Stages[i];
                foreach ((string part, string partStage) in stage.Parts) ShipPartDrop.Record(part, partStage);
                if (!string.IsNullOrEmpty(stage.MainPart)) StageProgress.RecoverMainPart(stage.MainPart);
                StageProgress.ClearStage(stage.Id, stage.Unlocks);
                for (int t = 0; t < stage.Trophies.Length; t++) PlaceTrophy(stage.Trophies[t], t < stage.Stands.Length ? stage.Stands[t] : 0);
            }
        }

        // On its own stand if that's free, otherwise the first free one of the six.
        private static void PlaceTrophy(string id, int preferred)
        {
            if (CampSave.IsPlaced(id)) return;
            if (CampSave.TrophyOn(preferred) == null)
            {
                CampSave.Place(id, preferred);
                return;
            }
            for (int stand = 0; stand < 6; stand++)
                if (CampSave.TrophyOn(stand) == null)
                {
                    CampSave.Place(id, stand);
                    return;
                }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => loading = false;
    }
}
