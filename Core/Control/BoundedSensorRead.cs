using System;
using System.Threading;
using System.Threading.Tasks;

namespace HB_NLP_Research_Lab.Core.Control
{
    /// <summary>
    /// Bounds a sensor read so a call that ignores cancellation cannot hold the
    /// control loop. Stop cancels the loop token; a read that does not observe
    /// that token used to keep the pass from returning, so the safe command in
    /// the loop's stop path never ran.
    /// </summary>
    internal static class BoundedSensorRead
    {
        internal static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(1);

        internal static TimeSpan NormalizeTimeout(TimeSpan? sensorReadTimeout)
        {
            var timeout = sensorReadTimeout ?? DefaultTimeout;
            // Task.Delay treats a zero delay as already complete, and it cannot
            // represent a timeout past int.MaxValue milliseconds.
            if (timeout < TimeSpan.FromMilliseconds(1) || timeout.TotalMilliseconds > int.MaxValue)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(sensorReadTimeout),
                    "Sensor read timeout must be at least 1 millisecond and representable as a delay.");
            }

            return timeout;
        }

        /// <summary>
        /// Returns the read's result. Throws <see cref="OperationCanceledException"/> when
        /// <paramref name="cancellationToken"/> is cancelled, and <see cref="TimeoutException"/>
        /// when the read is still running after <paramref name="timeout"/>. A timeout is not
        /// cancellation of the caller: the pass stays in the loop and can command the safe
        /// position. The abandoned read is observed so a later fault is not unobserved.
        /// </summary>
        internal static async Task<T> ReadAsync<T>(
            Task<T> read,
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(read);

            if (cancellationToken.IsCancellationRequested)
            {
                Observe(read);
                throw new OperationCanceledException(cancellationToken);
            }

            var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var timeoutTask = Task.Delay(timeout, timeoutSource.Token);
            try
            {
                await Task.WhenAny(read, timeoutTask).ConfigureAwait(false);
                if (!read.IsCompleted)
                {
                    Observe(read);
                    if (cancellationToken.IsCancellationRequested)
                        throw new OperationCanceledException(cancellationToken);

                    throw new TimeoutException("Sensor read timed out.");
                }
            }
            finally
            {
                // Cancel releases the timer. Dispose on the delay's completion so the
                // registration is not torn down inside the cancel callback.
                timeoutSource.Cancel();
                _ = timeoutTask.ContinueWith(
                    completed =>
                    {
                        if (completed.IsFaulted)
                            _ = completed.Exception;
                        try
                        {
                            timeoutSource.Dispose();
                        }
                        catch (ObjectDisposedException)
                        {
                            // Cancel already released the timer.
                        }
                    },
                    CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);
            }

            if (cancellationToken.IsCancellationRequested)
            {
                Observe(read);
                throw new OperationCanceledException(cancellationToken);
            }

            return await read.ConfigureAwait(false);
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
                completed =>
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
