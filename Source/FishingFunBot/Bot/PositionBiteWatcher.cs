using log4net;
using System;
using System.Collections.Generic;
using System.Drawing;

#nullable enable
namespace FishingFun
{
    public class PositionBiteWatcher : IBiteWatcher
    {
        private static ILog logger = LogManager.GetLogger("Fishbot");

        private List<int> yPositions = new List<int>();
        private int strikeValue;
        private int yDiff;
        private TimedAction? timer;

        // sensitivity scales the required movement (strikeValue) to detect a bite.
        // - values < 1.0 make the watcher more sensitive (smaller movement needed to trigger).
        // - values > 1.0 make it less sensitive (larger movement required).
        // The constructor clamps this to a minimum of 0.1 to avoid zero/negative thresholds.
        private double sensitivity = 1.0;

        public Action<FishingEvent> FishingEventHandler { set; get; } = (e)=> { };

        // strikeValue: baseline pixel movement required for a bite.
        // sensitivity: multiplier applied to strikeValue; effectiveThreshold = strikeValue * sensitivity.
        public PositionBiteWatcher(int strikeValue, double sensitivity = 1.0)
        {
            this.strikeValue = strikeValue;
            this.sensitivity = Math.Max(0.1, sensitivity);
        }

        public void RaiseEvent(FishingEvent ev)
        {
            FishingEventHandler?.Invoke(ev);
        }

        public void Reset(Point InitialBobberPosition)
        {
            RaiseEvent(new FishingEvent { Action = FishingAction.Reset });

            yPositions = new List<int>();
            yPositions.Add(InitialBobberPosition.Y);
            // sample more frequently so small/fast movements are captured
            timer = new TimedAction((a) =>
            {
                RaiseEvent(new FishingEvent { Amplitude = yDiff, Action = FishingAction.BobberMove });
            }, 200, 25);
        }

        public bool IsBite(Point currentBobberPosition)
        {
            if (!yPositions.Contains(currentBobberPosition.Y))
            {
                yPositions.Add(currentBobberPosition.Y);
                yPositions.Sort();
            }

            yDiff = yPositions[(int)((((double)yPositions.Count) + 0.5) / 2)] - currentBobberPosition.Y;

            int effectiveThreshold = (int)Math.Max(1, Math.Round(strikeValue * sensitivity));
            bool thresholdReached = yDiff <= -effectiveThreshold;

            if (timer != null)
            {
                timer.ExecuteIfDue();
            }

            if (thresholdReached)
            {
                RaiseEvent(new FishingEvent { Action = FishingAction.Loot });
                if (timer != null)
                {
                    timer.ExecuteNow();
                }
                return true;
            }

            return false;
        }
    }
}