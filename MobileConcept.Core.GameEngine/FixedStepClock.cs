namespace MobileConcept.Core.GameEngine;

/// <summary>Pas de simulation borné, sans rattrapage illimité après une suspension.</summary>
public sealed class FixedStepClock
{
    private double accumulator;
    public double StepSeconds { get; }
    public int MaxSteps { get; }
    public float Interpolation => (float)(accumulator / StepSeconds);
    public FixedStepClock(double stepSeconds = 1.0 / 60, int maxSteps = 6)
    {
        if (!double.IsFinite(stepSeconds) || stepSeconds <= 0) throw new ArgumentOutOfRangeException(nameof(stepSeconds));
        if (maxSteps < 1) throw new ArgumentOutOfRangeException(nameof(maxSteps));
        StepSeconds = stepSeconds;
        MaxSteps = maxSteps;
    }
    public int Advance(double elapsedSeconds, Action<float> update)
    {
        ArgumentNullException.ThrowIfNull(update);
        if (!double.IsFinite(elapsedSeconds) || elapsedSeconds < 0) throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
        accumulator += Math.Min(elapsedSeconds, StepSeconds * MaxSteps);
        int steps = 0;
        while (accumulator + 1e-12 >= StepSeconds && steps < MaxSteps)
        {
            update((float)StepSeconds);
            accumulator = Math.Max(0, accumulator - StepSeconds);
            steps++;
        }
        if (accumulator >= StepSeconds) accumulator %= StepSeconds;
        return steps;
    }
    public void Reset() => accumulator = 0;
}
