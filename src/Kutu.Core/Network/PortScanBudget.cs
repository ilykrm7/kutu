namespace Kutu.Core.Network;

public enum PortAnswer
{
    Open,
    Refused,
    Unanswered
}

public static class PortScanBudget
{
    public const int MaxConcurrency = 64;

    public static int Timeout(int requested)
    {
        if (requested <= 0)
            requested = 400;
        if (requested < 250)
            return 250;
        if (requested > 4000)
            return 4000;
        return requested;
    }

    public static int Concurrency(int requested)
    {
        if (requested <= 0)
            requested = 48;
        if (requested < 1)
            return 1;
        if (requested > MaxConcurrency)
            return MaxConcurrency;
        return requested;
    }

    public static int RetryTimeout(int timeoutMs)
    {
        var doubled = Timeout(timeoutMs) * 2;
        if (doubled < 800)
            return 800;
        if (doubled > 4000)
            return 4000;
        return doubled;
    }

    public static bool ShouldRetry(PortAnswer answer, int attempt) =>
        answer == PortAnswer.Unanswered && attempt < 1;
}
