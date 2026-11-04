using System;
using System.Threading;
using System.Threading.Tasks;
using HB_NLP_Research_Lab.Core.Hardware;

namespace HB_NLP_Research_Lab.Core.Control
{
    /// <summary>
    /// Waits for one <see cref="IActuator.SetPositionAsync"/> call. A command that
    /// is still running when the deadline elapses is not accepted. This wait does
    /// not issue a second command: the pass that started it has to return, and a
    /// later pass or the stop path decides whether to try again.
    /// </summary>
    internal static class BoundedActuatorCommand
    {
        internal static readonly TimeSpan DefaultDeadline = TimeSpan.FromSeconds(1);

        internal readonly struct Wait
        {
            private Wait(bool completed, bool accepted)
            {
                Completed = completed;
                Accepted = accepted;
            }

            /// <summary>
            /// True when the actuator call finished. False when the deadline
            /// elapsed and the call was still running.
            /// </summary>
            public bool Completed { get; }

            /// <summary>
            /// The actuator result. Meaningful only when <see cref="Completed"/> is true.
            /// </summary>
            public bool Accepted { get; }

            public static Wait Finished(bool accepted) => new(true, accepted);

            public static Wait DeadlineElapsed() => new(false, false);
        }

        internal static async Task<Wait> TrySetPositionAsync(
            IActuator actuator,
            double position,
            TimeSpan deadline,
            CancellationToken cancellationToken)
        {
            var command = actuator.SetPositionAsync(position, cancellationToken);
            if (command.IsCompleted)
                return Wait.Finished(await command.ConfigureAwait(false));

            if (cancellationToken.IsCancellationRequested)
            {
                Observe(command);
                throw new OperationCanceledException(cancellationToken);
            }

            if (deadline <= TimeSpan.Zero)
            {
                Observe(command);
                return Wait.DeadlineElapsed();
            }

            using var timeout = new CancellationTokenSource();
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
            var signal = Task.Delay(BoundedDelay(deadline), linked.Token);
            try
            {
                await Task.WhenAny(command, signal).ConfigureAwait(false);
            }
            finally
            {
                // Release the delay when the command wins the race, and observe
                // that delay so a cancelled wait is not an unobserved task.
                timeout.Cancel();
                Observe(signal);
            }

            if (command.IsCompleted)
                return Wait.Finished(await command.ConfigureAwait(false));

            Observe(command);
            cancellationToken.ThrowIfCancellationRequested();
            return Wait.DeadlineElapsed();
        }

        private static TimeSpan BoundedDelay(TimeSpan deadline)
        {
            var max = TimeSpan.FromMilliseconds(int.MaxValue);
            return deadline < max ? deadline : max;
        }

        private static void Observe(Task task)
        {
            if (task.IsCompleted)
            {
                if (task.IsFaulted)
                    _ = task.Exception;
                return;
            }

            _ = task.ContinueWith(
                static completed =>
                {
                    if (completed.IsFaulted)
                        _ = completed.Exception;
                },
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }
    }
}
