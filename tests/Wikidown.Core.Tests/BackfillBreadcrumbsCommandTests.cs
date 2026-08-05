using Wikidown.Cli;
using Wikidown.Core;
using Xunit;

namespace Wikidown.Core.Tests;

public class BackfillBreadcrumbsCommandTests : IDisposable
{
    private readonly string _root;
    private readonly WikiRepository _repo;

    public BackfillBreadcrumbsCommandTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "wikidown-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        _repo = new WikiRepository(_root);
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { /* best-effort */ }
    }

    private int Run(params string[] extra)
    {
        var args = new[] { "backfill-breadcrumbs", "--root", _root }.Concat(extra).ToArray();
        return CommandRunner.Run(args, TextWriter.Null, TextWriter.Null);
    }

    // Simulates a page written before breadcrumbs existed: bypasses
    // WikiRepository.Write so no breadcrumb gets injected.
    private void WriteRaw(PagePath path, string markdown)
    {
        var file = Path.Combine(_root, path.ToFilePath());
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, markdown);
    }

    [Fact]
    public void AddsBreadcrumbs_ToPagesThatPredateTheFeature()
    {
        WriteRaw(PagePath.Parse("/Parent"), "# Parent\n");
        WriteRaw(PagePath.Parse("/Parent/Child"), "# Child\n");
        _repo.WriteOrder(PagePath.Root, new[] { "Parent" });
        _repo.WriteOrder(PagePath.Parse("/Parent"), new[] { "Child" });

        Assert.Equal(0, Run());

        var child = _repo.Read(PagePath.Parse("/Parent/Child")).Markdown;
        Assert.StartsWith("[Parent](../Parent.md) / Child", child);
    }

    [Fact]
    public void SkipsTopLevelPages()
    {
        WriteRaw(PagePath.Parse("/Home"), "# Home\n");
        _repo.WriteOrder(PagePath.Root, new[] { "Home" });

        Assert.Equal(0, Run());

        var home = _repo.Read(PagePath.Parse("/Home")).Markdown;
        Assert.DoesNotContain(Breadcrumb.Marker, home);
    }

    [Fact]
    public void DryRun_ReportsButDoesNotModify()
    {
        WriteRaw(PagePath.Parse("/Parent"), "# Parent\n");
        WriteRaw(PagePath.Parse("/Parent/Child"), "# Child\n");
        _repo.WriteOrder(PagePath.Root, new[] { "Parent" });
        _repo.WriteOrder(PagePath.Parse("/Parent"), new[] { "Child" });

        Assert.Equal(0, Run("--dry-run"));

        var child = _repo.Read(PagePath.Parse("/Parent/Child")).Markdown;
        Assert.DoesNotContain(Breadcrumb.Marker, child);
    }

    [Fact]
    public void AlreadyBackfilled_IsANoOp()
    {
        _repo.Write(new WikiPage(PagePath.Parse("/Parent"), "# Parent\n"));
        _repo.Write(new WikiPage(PagePath.Parse("/Parent/Child"), "# Child\n"));

        Assert.Equal(0, Run());

        var child = _repo.Read(PagePath.Parse("/Parent/Child")).Markdown;
        Assert.Equal(1, child.Split(Breadcrumb.Marker).Length - 1);
    }
}
