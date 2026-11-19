using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HB_NLP_Research_Lab.Core.Hardware;

namespace HB_NLP_Research_Lab.Core.Control
{
    /// <summary>
    /// Redundant control voting helpers for Triple Modular Redundancy (TMR)
    /// or N-Modular Redundancy (NMR).
    /// Runtime operation is disabled until real per-channel output sources,
    /// timestamps/staleness, quorum rules, bounds, rate limits, and
    /// single-writer actuation are designed. <see cref="StartAsync"/> always
    /// throws; voting helpers are retained for internal/offline testing.
    /// </summary>
    public class RedundantControlSystem : IDisposable
    {
        private readonly List<RealTimeControlLoop> _controlLoops;
        private readonly VotingStrategy _votingStrategy;
        private bool _isRunning;

        public int RedundancyLevel => _controlLoops.Count;
        public VotingStrategy Strategy => _votingStrategy;
        public bool IsRunning => _isRunning;

        public RedundantControlSystem(
            List<RealTimeControlLoop> controlLoops,
            VotingStrategy votingStrategy,
            IActuator primaryActuator,
            List<IActuator>? redundantActuators = null)
        {
            if (controlLoops == null || controlLoops.Count < 2)
                throw new ArgumentException("At least 2 control loops required for redundancy", nameof(controlLoops));

            if (primaryActuator == null)
                throw new ArgumentNullException(nameof(primaryActuator));

            // Retained for API compatibility while runtime actuation is disabled.
            _ = redundantActuators;

            _controlLoops = controlLoops;
            _votingStrategy = votingStrategy;
        }

        /// <summary>
        /// Runtime start is not supported. Throws before any control loop is
        /// started, <see cref="IsRunning"/> is changed, or any actuator is commanded.
        /// </summary>
        /// <exception cref="NotSupportedException">Always thrown; runtime is disabled.</exception>
        public Task StartAsync()
        {
            throw new NotSupportedException(
                "RedundantControlSystem runtime is disabled until real per-channel output sources, " +
                "timestamps/staleness checks, quorum rules, command bounds, rate limits, and " +
                "single-writer actuation are designed. Voting helpers remain available.");
        }

        /// <summary>
        /// Safe no-op when the system was never started or <see cref="StartAsync"/> failed.
        /// Does not command actuators.
        /// </summary>
        public Task StopAsync()
        {
            _isRunning = false;
            return Task.CompletedTask;
        }

        /// <summary>
        /// Selects one finite actuator command, or null to hold.
        /// LINQ Min and Max skip a NaN channel, then Average folds that NaN back in,
        /// so consensus of two healthy channels plus a failed channel commanded NaN.
        /// </summary>
        internal static double? SelectVote(VotingStrategy strategy, IReadOnlyList<double> outputs)
        {
            if (outputs == null || outputs.Count == 0)
                return null;

            return strategy switch
            {
                VotingStrategy.MajorityVote => MajorityVote(outputs),
                VotingStrategy.MedianVote => MedianVote(outputs),
                VotingStrategy.AverageVote => AverageVote(outputs),
                VotingStrategy.MidValueSelect => MidValueSelect(outputs),
                VotingStrategy.Consensus => ConsensusVote(outputs),
                _ => MedianVote(outputs)
            };
        }

        internal static double? MajorityVote(IReadOnlyList<double> outputs)
        {
            if (outputs == null || outputs.Count == 0)
                return null;

            // Group finite values within tolerance. A NaN group used to win
            // because Double.Equals treats NaN as equal to NaN, and the winner
            // was commanded even when it was not a majority of the channels.
            const double tolerance = 0.01;
            var groups = outputs
                .Where(static value => double.IsFinite(value))
                .GroupBy(value => Math.Round(value / tolerance) * tolerance)
                .OrderByDescending(group => group.Count())
                .ToList();

            if (groups.Count == 0)
                return null;

            var winnerCount = groups[0].Count();
            if (winnerCount * 2 <= outputs.Count)
                return null;

            var key = groups[0].Key;
            return double.IsFinite(key) ? key : null;
        }

        internal static double? MedianVote(IReadOnlyList<double> outputs)
        {
            if (outputs == null || outputs.Count == 0)
                return null;

            var sorted = outputs.OrderBy(value => value).ToList();
            int mid = sorted.Count / 2;
            var selected = sorted.Count % 2 == 0
                ? (sorted[mid - 1] + sorted[mid]) / 2.0
                : sorted[mid];

            return double.IsFinite(selected) ? selected : null;
        }

        internal static double? AverageVote(IReadOnlyList<double> outputs)
        {
            if (outputs == null || outputs.Count == 0)
                return null;

            double sum = 0;
            foreach (var value in outputs)
            {
                if (!double.IsFinite(value))
                    return null;

                sum += value;
            }

            var average = sum / outputs.Count;
            return double.IsFinite(average) ? average : null;
        }

        internal static double? MidValueSelect(IReadOnlyList<double> outputs)
        {
            if (outputs == null || outputs.Count == 0)
                return null;

            // Middle value of the sorted channels (TMR). A non-finite middle holds.
            var sorted = outputs.OrderBy(value => value).ToList();
            var selected = sorted[sorted.Count / 2];
            return double.IsFinite(selected) ? selected : null;
        }

        internal static double? ConsensusVote(IReadOnlyList<double> outputs)
        {
            if (outputs == null || outputs.Count == 0)
                return null;

            const double tolerance = 0.01;
            double min = double.PositiveInfinity;
            double max = double.NegativeInfinity;
            double sum = 0;
            foreach (var value in outputs)
            {
                if (!double.IsFinite(value))
                    return null;

                if (value < min)
                    min = value;
                if (value > max)
                    max = value;
                sum += value;
            }

            var span = max - min;
            if (!double.IsFinite(span) || span > tolerance)
                return null;

            var average = sum / outputs.Count;
            return double.IsFinite(average) ? average : null;
        }

        /// <summary>
        /// A non-finite channel is a fault. Comparisons with NaN are false, so the
        /// old deviation check never reported a failed channel, and one NaN made
        /// the mean NaN so healthy outliers were missed too.
        /// </summary>
        internal static List<Fault> DetectChannelFaults(IReadOnlyList<double> outputs)
        {
            var faults = new List<Fault>();
            if (outputs == null || outputs.Count < 2)
                return faults;

            var finite = new List<double>();
            foreach (var value in outputs)
            {
                if (double.IsFinite(value))
                    finite.Add(value);
            }

            double? mean = finite.Count > 0 ? finite.Average() : null;
            var stdDev = 0.0;
            if (finite.Count >= 2 && mean.HasValue)
            {
                var variance = finite.Select(value => Math.Pow(value - mean.Value, 2)).Average();
                stdDev = Math.Sqrt(variance);
            }

            for (int i = 0; i < outputs.Count; i++)
            {
                var value = outputs[i];
                if (!double.IsFinite(value))
                {
                    faults.Add(new Fault
                    {
                        ControllerIndex = i,
                        OutputValue = value,
                        ExpectedValue = mean ?? double.NaN,
                        Deviation = double.PositiveInfinity,
                        Timestamp = DateTime.UtcNow
                    });
                    continue;
                }

                if (!mean.HasValue || !double.IsFinite(stdDev) || stdDev <= 0.001)
                    continue;

                var deviation = Math.Abs(value - mean.Value);
                if (deviation > 3 * stdDev)
                {
                    faults.Add(new Fault
                    {
                        ControllerIndex = i,
                        OutputValue = value,
                        ExpectedValue = mean.Value,
                        Deviation = deviation,
                        Timestamp = DateTime.UtcNow
                    });
                }
            }

            return faults;
        }

        /// <summary>
        /// Safe when never started or after a failed <see cref="StartAsync"/>.
        /// Does not command actuators. Idempotent.
        /// </summary>
        public void Dispose()
        {
            _isRunning = false;
        }
    }

    public enum VotingStrategy
    {
        MajorityVote,    // Strict majority of channels; otherwise hold
        MedianVote,      // Median value
        AverageVote,      // Average of all values
        MidValueSelect,  // Middle value (TMR)
        Consensus        // All must agree
    }

    public class Fault
    {
        public int ControllerIndex { get; set; }
        public double OutputValue { get; set; }
        public double ExpectedValue { get; set; }
        public double Deviation { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
