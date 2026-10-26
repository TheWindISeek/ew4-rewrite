using Ew4.Platform.Engine;
using Ew4.Service.Play;
using Xunit;

namespace Ew4.Test.Service;

/// <summary>
/// 持久账户的提交点：局内变化只在内存，明确操作（手动存档/离开战役或征服/结算/总部）才写盘。
/// </summary>
public sealed class ProfileStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "ew4-profile-" + Guid.NewGuid().ToString("N"));
    private string Path_ => Path.Combine(_dir, "account.json");

    public ProfileStoreTests() => Directory.CreateDirectory(_dir);

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private ProfileStore Store() => new(Path_, PlaySession.Json);

    private static PlayerAccount Account(long revision, long medals) =>
        new() { Id = "local", ContentRevision = "rev", Revision = revision, Medals = medals };

    private PlayerAccount OnDisk() => Store().Open();

    private ProfileStore Committed(long medals = 100)
    {
        ProfileStore store = Store();
        store.Set(Account(0, medals));
        store.Commit();
        return store;
    }

    [Fact]
    public void Set_changes_stay_in_memory_until_commit()
    {
        ProfileStore store = Committed();
        store.Set(Account(1, 40));

        Assert.True(store.Dirty);
        Assert.Equal(40, store.Current!.Medals);
        Assert.Equal(100, OnDisk().Medals);
    }

    [Fact]
    public void Commit_writes_pending_changes_and_clears_dirty()
    {
        ProfileStore store = Committed();
        store.Set(Account(1, 40));
        store.Commit();

        Assert.False(store.Dirty);
        Assert.Equal(40, OnDisk().Medals);
    }

    [Fact]
    public void Set_with_same_revision_is_not_pending()
    {
        ProfileStore store = Committed();
        store.Set(Account(0, 100));

        Assert.False(store.Dirty);
    }

    [Fact]
    public void Commit_without_changes_does_not_rewrite_existing_file()
    {
        ProfileStore store = Committed();
        DateTime stamp = new(2001, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(Path_, stamp);

        store.Commit();

        Assert.Equal(stamp, File.GetLastWriteTimeUtc(Path_));
    }

    [Fact]
    public void Commit_creates_missing_file_even_when_not_dirty()
    {
        ProfileStore store = Store();
        store.Reset(Account(0, 7));

        store.Commit();

        Assert.True(store.Exists);
        Assert.Equal(7, OnDisk().Medals);
    }

    [Fact]
    public void Discard_drops_pending_changes_and_keeps_disk()
    {
        ProfileStore store = Committed();
        store.Set(Account(1, 40));

        store.Discard();

        Assert.Null(store.Current);
        Assert.False(store.Dirty);
        Assert.Equal(100, OnDisk().Medals);
    }

    [Fact]
    public void Discard_without_pending_changes_keeps_memory_copy()
    {
        ProfileStore store = Committed();

        store.Discard();

        Assert.NotNull(store.Current);
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(2, true)]
    [InlineData(0, false)]
    [InlineData(3, false)]
    public void Only_campaign_and_conquest_commit_on_leave(int mode, bool commits) =>
        Assert.Equal(commits, ProfileStore.CommitsOnLeave(mode));

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void Leaving_campaign_or_conquest_commits_in_battle_changes(int mode)
    {
        ProfileStore store = Committed();
        store.Set(Account(1, 40));

        store.Leave(mode);

        Assert.Equal(40, OnDisk().Medals);
        Assert.False(store.Dirty);
    }

    [Fact]
    public void Leaving_other_modes_discards_in_battle_changes()
    {
        ProfileStore store = Committed();
        store.Set(Account(1, 40));

        store.Leave(mode: 0);

        Assert.Equal(100, OnDisk().Medals);
        Assert.Null(store.Current);
    }

    [Fact]
    public void Set_rejects_invalid_account_without_changing_state()
    {
        ProfileStore store = Committed();

        Assert.Throws<RuleViolation>(() => store.Set(Account(1, -5)));

        Assert.False(store.Dirty);
        Assert.Equal(100, store.Current!.Medals);
    }

    [Fact]
    public void Open_loads_committed_account_as_clean()
    {
        Committed(55);
        ProfileStore reopened = Store();

        PlayerAccount account = reopened.Open();

        Assert.Equal(55, account.Medals);
        Assert.False(reopened.Dirty);
    }
}
