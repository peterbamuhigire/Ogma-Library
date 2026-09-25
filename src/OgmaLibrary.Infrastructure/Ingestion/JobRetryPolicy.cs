namespace OgmaLibrary.Infrastructure.Ingestion;

/// <summary>
/// Exponential retry schedule for background jobs (Sept-23 Phase 06, T06.3, K25): 5 s, 30 s,
/// 2 min, then 10 min for every later attempt, each with ±20 % jitter so a batch of failures
/// does not retry in lock-step. At most <see cref="DefaultMaxAttempts"/> real attempts run.
/// </summary>
public static class JobRetryPolicy
{
    /// <summary>The default number of real attempts before a job fails terminally.</summary>
    public const int DefaultMaxAttempts = 3;

    /// <summary>The relative jitter applied to each delay.</summary>
    public const double JitterFraction = 0.2;

    private static readonly TimeSpan[] Schedule =
    [
        TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(30),
        TimeSpan.FromMinutes(2),
        TimeSpan.FromMinutes(10),
    ];

    /// <summary>
    /// A requeue that is not a real attempt (crash recovery) stops being refunded after this
    /// many interruptions, so a job that kills its process every time still reaches a
    /// terminal state (poison-message guard).
    /// </summary>
    public const int MaxFreeRequeues = 5;

    /// <summary>Returns the nominal (unjittered) delay before the next attempt.</summary>
    /// <param name="completedAttempts">The number of real attempts already made (1 after the first failure).</param>
    /// <returns>The nominal delay.</returns>
    public static TimeSpan NominalDelay(int completedAttempts)
    {
        int index = Math.Clamp(completedAttempts - 1, 0, Schedule.Length - 1);
        return Schedule[index];
    }

    /// <summary>Returns the jittered delay before the next attempt.</summary>
    /// <param name="completedAttempts">The number of real attempts already made.</param>
    /// <param name="unitSample">A sample in [0, 1); null uses <see cref="Random.Shared"/>.</param>
    /// <returns>The delay, within ±<see cref="JitterFraction"/> of the nominal delay.</returns>
    public static TimeSpan Delay(int completedAttempts, double? unitSample = null)
    {
        double sample = Math.Clamp(unitSample ?? Random.Shared.NextDouble(), 0d, 1d);
        double factor = 1d + (((sample * 2d) - 1d) * JitterFraction);
        return TimeSpan.FromMilliseconds(NominalDelay(completedAttempts).TotalMilliseconds * factor);
    }
}
