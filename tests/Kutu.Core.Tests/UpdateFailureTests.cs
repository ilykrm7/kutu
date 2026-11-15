using System.Net.Http;
using Kutu.Core.Updates;

namespace Kutu.Core.Tests;

public class UpdateFailureTests
{
    [Fact]
    public void Release_page_uses_the_version_tag()
    {
        Assert.Equal(
            "https://github.com/ilykrm7/kutu/releases/tag/v1.1.6",
            UpdateFailure.ReleasePage("1.1.6"));
        Assert.Equal(
            "https://github.com/ilykrm7/kutu/releases/tag/v1.1.6",
            UpdateFailure.ReleasePage("v1.1.6"));
    }

    [Fact]
    public void Virus_hresults_are_a_blocked_download()
    {
        var infected = new IOException("infected");
        infected.HResult = UpdateFailure.VirusInfected;
        var deleted = new UnauthorizedAccessException("deleted");
        deleted.HResult = UpdateFailure.VirusDeleted;

        Assert.Equal(UpdateFailureKind.Blocked, UpdateFailure.Download(infected, fileStillThere: true));
        Assert.Equal(UpdateFailureKind.Blocked, UpdateFailure.Download(deleted, fileStillThere: false));
        Assert.Equal(UpdateFailureKind.Blocked, UpdateFailure.Replace(infected));
        Assert.Equal(UpdateFailureKind.Blocked, UpdateFailure.Launch(deleted));
    }

    [Fact]
    public void A_missing_file_after_an_io_error_is_blocked_and_a_network_error_is_http()
    {
        Assert.Equal(UpdateFailureKind.Blocked, UpdateFailure.Download(new IOException("gone"), fileStillThere: false));
        Assert.Equal(UpdateFailureKind.Http, UpdateFailure.Download(new HttpRequestException("no route"), fileStillThere: false));
        Assert.Equal(UpdateFailureKind.Http, UpdateFailure.Download(new IOException("disk"), fileStillThere: true));
    }

    [Fact]
    public void Hash_mismatch_stays_distinct_from_a_deleted_file()
    {
        Assert.Equal(UpdateFailureKind.Hash, UpdateFailure.Hash(null, fileStillThere: true, matches: false));
        Assert.Equal(UpdateFailureKind.Blocked, UpdateFailure.Hash(null, fileStillThere: false, matches: false));
        Assert.Equal(UpdateFailureKind.Http, UpdateFailure.Hash(new HttpRequestException("sums"), fileStillThere: true, matches: true));
        Assert.Equal(UpdateFailureKind.Replace, UpdateFailure.Replace(new IOException("in use")));
        Assert.Equal(UpdateFailureKind.Relaunch, UpdateFailure.Launch(new InvalidOperationException("start")));
    }

    [Fact]
    public void Newer_1_1_6_is_visible_to_older_releases()
    {
        Assert.True(ReleaseVersion.IsNewer("v1.1.6", new Version(1, 1, 4)));
        Assert.True(ReleaseVersion.IsNewer("v1.1.6", new Version(1, 1, 5)));
        Assert.False(ReleaseVersion.IsNewer("v1.1.6", new Version(1, 1, 6)));
    }
}
