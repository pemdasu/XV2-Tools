using System;
using System.Diagnostics;

namespace LB_Mod_Installer.Installer
{
    /// <summary>
    /// Estimates the time left for the current progress phase. Timing auto-restarts whenever the phase
    /// changes (the total count changes, or progress resets), so it works across the install's phases
    /// (file processing, then file saving) without any explicit phase tracking.
    ///
    /// File sizes vary a lot, so a raw estimate jumps around when a big file is hit. The shown value is
    /// eased toward the estimate (a time-based moving average) and displayed in coarse units so it stays steady.
    /// </summary>
    public class ProgressEta
    {
        //Wait this long into a phase before showing anything, so the first estimate has settled.
        private const double MinSampleSeconds = 2.0;
        //Larger = steadier and slower to react to a sudden change in speed.
        private const double SmoothingTimeConstant = 6.0;

        private readonly Stopwatch stopwatch = new Stopwatch();
        private int phaseTotal = -1;
        private int phaseStart;
        private double smoothedSeconds = -1;
        private double lastUpdateSeconds;

        public string GetEtaText(int current, int total)
        {
            if (total <= 0 || current < 0) return string.Empty;

            //New phase: start timing from here.
            if (total != phaseTotal || current < phaseStart)
            {
                phaseTotal = total;
                phaseStart = current;
                smoothedSeconds = -1;
                stopwatch.Restart();
                return string.Empty;
            }

            int doneThisPhase = current - phaseStart;
            int remaining = total - current;
            if (doneThisPhase < 1 || remaining <= 0) return string.Empty;

            double elapsed = stopwatch.Elapsed.TotalSeconds;
            if (elapsed < MinSampleSeconds) return string.Empty;

            double rawSeconds = (elapsed / doneThisPhase) * remaining;

            //Ease the shown value toward the raw estimate. The blend is time-based, so it behaves the same
            //whether updates arrive every few milliseconds or every few seconds.
            if (smoothedSeconds < 0)
            {
                smoothedSeconds = rawSeconds;
            }
            else
            {
                double step = elapsed - lastUpdateSeconds;
                double weight = 1 - Math.Exp(-step / SmoothingTimeConstant);
                smoothedSeconds += weight * (rawSeconds - smoothedSeconds);
            }

            lastUpdateSeconds = elapsed;

            return " - " + FormatTime(smoothedSeconds) + " left";
        }

        private static string FormatTime(double seconds)
        {
            if (seconds < 60)
            {
                int rounded = (int)(Math.Ceiling(seconds / 5.0) * 5);
                return rounded >= 60 ? "1m" : $"{rounded}s";
            }

            if (seconds < 3600)
            {
                int minutes = (int)Math.Round(seconds / 60.0);
                return $"{Math.Max(1, minutes)}m";
            }

            int hours = (int)(seconds / 3600);
            int mins = (int)Math.Round((seconds % 3600) / 60.0);
            return $"{hours}h {mins}m";
        }
    }
}
