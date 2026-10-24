using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace SignalLost.Story
{
    /// <summary>
    /// Playtest metric: how long the tutorial takes (lead's request, 10-23). It runs from the opening to the end of
    /// Cavendish's wrap-up, in game time, so the pause menu doesn't count. It also notes when each stage began and how
    /// many times the player went down. Each finished run adds one row to <see cref="FileName"/> in
    /// <c>Application.persistentDataPath</c> and is logged to the Console (and Player.log in builds).
    /// </summary>
    public sealed class TutorialRunTimer
    {
        public const string FileName = "tutorial-times.csv";

        private readonly Dictionary<TutorialStory.Stage, float> stageStarts = new Dictionary<TutorialStory.Stage, float>();
        private float started = -1f;
        private float finished = -1f;

        public bool Running => started >= 0f && finished < 0f;
        public int Deaths { get; private set; }
        /// <summary>Seconds so far, or the final time once finished.</summary>
        public float Elapsed => started < 0f ? 0f : (finished >= 0f ? finished : Time.time) - started;
        public static string FilePath => Path.Combine(Application.persistentDataPath, FileName);

        public void Begin()
        {
            started = Time.time;
            finished = -1f;
            Deaths = 0;
            stageStarts.Clear();
        }

        public void Mark(TutorialStory.Stage stage)
        {
            if (Running && !stageStarts.ContainsKey(stage)) stageStarts[stage] = Elapsed;
        }

        public void CountDeath()
        {
            if (Running) Deaths++;
        }

        /// <summary>Stops the clock and saves the run. Returns the final time in seconds.</summary>
        public float Finish()
        {
            if (!Running) return Elapsed;
            finished = Time.time;
            Save();
            return Elapsed;
        }

        /// <summary>12:34 (or 1:02:03 past an hour).</summary>
        public static string Format(float seconds)
        {
            TimeSpan time = TimeSpan.FromSeconds(Mathf.Max(0f, seconds));
            return time.TotalHours >= 1 ? $"{(int)time.TotalHours}:{time.Minutes:00}:{time.Seconds:00}" : $"{time.Minutes}:{time.Seconds:00}";
        }

        private void Save()
        {
            var stages = (TutorialStory.Stage[])Enum.GetValues(typeof(TutorialStory.Stage));
            var header = new StringBuilder("finished_utc,version,total_seconds,total,deaths");
            var row = new StringBuilder();
            row.Append(DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)).Append(',')
               .Append(Application.version).Append(',')
               .Append(Elapsed.ToString("0.0", CultureInfo.InvariantCulture)).Append(',')
               .Append(Format(Elapsed)).Append(',')
               .Append(Deaths);
            foreach (TutorialStory.Stage stage in stages)
            {
                header.Append(',').Append(stage).Append("_at");
                row.Append(',');
                if (stageStarts.TryGetValue(stage, out float at)) row.Append(at.ToString("0.0", CultureInfo.InvariantCulture));
            }
            Debug.Log($"TUTORIAL_COMPLETE {Format(Elapsed)} ({Elapsed:0.0} s, {Deaths} death(s)). Saved to {FilePath}");
            try
            {
                string path = FilePath;
                // If the stages ever change, the old file is set aside so old and new rows never mix columns.
                if (File.Exists(path) && !File.ReadAllText(path).StartsWith(header.ToString(), StringComparison.Ordinal))
                    File.Move(path, Path.ChangeExtension(path, DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + ".csv"));
                if (!File.Exists(path)) File.WriteAllText(path, header + Environment.NewLine);
                File.AppendAllText(path, row + Environment.NewLine);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Couldn't save the tutorial time: " + exception.Message);
            }
        }
    }
}
