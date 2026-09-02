using Wikidown.Cli;
using Xunit;

namespace Wikidown.Core.Tests;

public class CommandRunnerHelpTests
{
    private static (int ExitCode, string Stdout, string Stderr) Run(params string[] args)
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var exitCode = CommandRunner.Run(args, stdout, stderr);
        return (exitCode, stdout.ToString(), stderr.ToString());
    }

    [Theory]
    [InlineData("--help")]
    [InlineData("-h")]
    [InlineData("help")]
    public void TopLevelHelp_ExitsZeroAndWritesToStdout(string flag)
    {
        var (exitCode, stdout, stderr) = Run(flag);

        Assert.Equal(0, exitCode);
        Assert.Contains("Usage:", stdout);
        Assert.Equal("", stderr);
    }

    [Theory]
    [InlineData("new", "--help")]
    [InlineData("new", "-h")]
    [InlineData("help", "new")]
    public void CommandHelp_DoesNotRequirePath(string a, string b)
    {
        var (exitCode, stdout, stderr) = Run(a, b);

        Assert.Equal(0, exitCode);
        Assert.Contains("wikidown new --path", stdout);
        Assert.Contains("Examples:", stdout);
        Assert.Equal("", stderr);
    }

    [Fact]
    public void CommandHelp_TakesPriorityOverOtherArguments()
    {
        var (exitCode, stdout, stderr) = Run("new", "--path", "/Foo", "--help");

        Assert.Equal(0, exitCode);
        Assert.Contains("wikidown new --path", stdout);
        Assert.Equal("", stderr);
    }

    [Fact]
    public void MissingRequiredOption_StillErrorsToStderrWithNonZeroExit()
    {
        var (exitCode, stdout, stderr) = Run("new");

        Assert.NotEqual(0, exitCode);
        Assert.Equal("", stdout);
        Assert.Contains("missing --path", stderr);
    }

    [Fact]
    public void HelpForUnknownCommand_ErrorsToStderr()
    {
        var (exitCode, stdout, stderr) = Run("help", "bogus");

        Assert.NotEqual(0, exitCode);
        Assert.Equal("", stdout);
        Assert.Contains("unknown command 'bogus'", stderr);
    }
}
