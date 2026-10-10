namespace Sek.Engine;

public sealed class ExplorationBudget
{
    private readonly ExplorationOptions options;
    private readonly long started;

    public ExplorationBudget(ExplorationOptions options)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(options.MaxCandidateEvaluations);
        ArgumentOutOfRangeException.ThrowIfNegative(options.MaxNegativeTransitions);
        ArgumentOutOfRangeException.ThrowIfNegative(options.MaxStates);
        ArgumentOutOfRangeException.ThrowIfNegative(options.MaxTransitions);
        ArgumentOutOfRangeException.ThrowIfNegative(options.MaxDepth);
        if (options.MaxElapsedTime <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(options.MaxElapsedTime));
        this.options = options;
        started = options.TimeProvider.GetTimestamp();
    }

    public string? TerminationReason { get; private set; }
    public long CandidateEvaluations { get; private set; }
    public int States { get; private set; }
    public int Transitions { get; private set; }
    public int NegativeTransitions { get; private set; }
    public TimeSpan Elapsed => options.TimeProvider.GetElapsedTime(started);

    public bool Check()
    {
        if (TerminationReason is not null) return false;
        if (options.CancellationToken.IsCancellationRequested) return Stop("cancelled");
        if (Elapsed >= options.MaxElapsedTime) return Stop("deadline");
        return true;
    }

    public bool Stop(string reason)
    {
        TerminationReason ??= reason;
        return false;
    }

    public bool TryCandidate()
    {
        if (!Check()) return false;
        if (CandidateEvaluations >= options.MaxCandidateEvaluations) return Stop("candidate-limit");
        CandidateEvaluations++;
        return true;
    }

    public bool TryState()
    {
        if (!Check()) return false;
        if (States >= options.MaxStates) return Stop("state-limit");
        States++;
        return true;
    }

    public bool TryTransition()
    {
        if (!Check()) return false;
        if (Transitions >= options.MaxTransitions) return Stop("transition-limit");
        Transitions++;
        return true;
    }

    public bool TryNegative()
    {
        if (!Check()) return false;
        if (NegativeTransitions >= options.MaxNegativeTransitions) return Stop("negative-limit");
        NegativeTransitions++;
        return true;
    }
}