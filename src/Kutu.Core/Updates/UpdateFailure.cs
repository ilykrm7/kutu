using System.Net.Http;

namespace Kutu.Core.Updates;

public enum UpdateFailureKind
{
    Http,
    Hash,
    Blocked,
    Replace,
    Relaunch
}

public static class UpdateFailure
{
    public const int VirusInfected = unchecked((int)0x800700E1);
    public const int VirusDeleted = unchecked((int)0x800700E2);

    public static string ReleasePage(string? version)
    {
        var token = (version ?? "").Trim().TrimStart('v', 'V');
        return "https://github.com/ilykrm7/kutu/releases/tag/v" + token;
    }

    public static bool IsBlocked(Exception? exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current.HResult == VirusInfected || current.HResult == VirusDeleted)
                return true;
        }

        return false;
    }

    public static UpdateFailureKind Download(Exception exception, bool fileStillThere)
    {
        if (IsBlocked(exception))
            return UpdateFailureKind.Blocked;
        if (!fileStillThere && exception is IOException or UnauthorizedAccessException)
            return UpdateFailureKind.Blocked;
        return UpdateFailureKind.Http;
    }

    public static UpdateFailureKind Hash(Exception? exception, bool fileStillThere, bool matches)
    {
        if (IsBlocked(exception) || !fileStillThere)
            return UpdateFailureKind.Blocked;
        if (exception is HttpRequestException)
            return UpdateFailureKind.Http;
        if (!matches)
            return UpdateFailureKind.Hash;
        return UpdateFailureKind.Http;
    }

    public static UpdateFailureKind Replace(Exception exception) =>
        IsBlocked(exception) ? UpdateFailureKind.Blocked : UpdateFailureKind.Replace;

    public static UpdateFailureKind Launch(Exception exception) =>
        IsBlocked(exception) ? UpdateFailureKind.Blocked : UpdateFailureKind.Relaunch;
}
