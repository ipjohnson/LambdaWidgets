using System.Net.Sockets;
using Xunit;

namespace LambdaWidgets.Harness.Tests;

/// <summary>
/// The flags, and the two failures a first run actually meets.
/// </summary>
public class WhatTheCommandLineDoesTests {

    /// <summary>
    /// <c>--help</c> used to reach <c>InvokeRouting.From</c>, which ignores what it does not
    /// recognise, so it bound the port, served an empty dashboard, printed nothing and did not
    /// exit. An instance that outlives its shell then blocks the port for the rest of the session.
    /// </summary>
    [Theory]
    [InlineData("--help")]
    [InlineData("-h")]
    [InlineData("-?")]
    public void HelpIsAFlagRatherThanARun(string flag) {
        Assert.True(CommandLine.WantsHelp([flag]));
    }

    [Fact]
    public void AnOrdinaryRunIsNeitherHelpNorVersion() {
        string[] arguments = ["--dashboard", "dashboard.json", "--test-tool", "5050"];

        Assert.False(CommandLine.WantsHelp(arguments));
        Assert.False(CommandLine.WantsVersion(arguments));
    }

    /// <summary>Every flag the parsers read is in it, or it is documentation that has drifted.</summary>
    [Theory]
    [InlineData("--dashboard")]
    [InlineData("--test-tool")]
    [InlineData("--rie")]
    [InlineData("--sam")]
    [InlineData("--function")]
    [InlineData("--version")]
    [InlineData("PORT")]
    public void EveryFlagIsInTheUsage(string flag) {
        Assert.Contains(flag, CommandLine.Usage);
    }

    [Fact]
    public void TheVersionIsAVersionRatherThanABuildLog() {
        Assert.DoesNotContain("+", CommandLine.Version);
        Assert.NotEqual("unknown", CommandLine.Version);
    }

    /// <summary>
    /// Null and a path are different answers. A file named explicitly and not there is a typo;
    /// no flag at all is a run in a directory that may not have one yet.
    /// </summary>
    [Fact]
    public void TheDashboardFlagIsToldApartFromItsDefault() {
        Assert.Equal("./typo.json", CommandLine.Dashboard(["--dashboard", "./typo.json"]));
        Assert.Null(CommandLine.Dashboard(["--test-tool", "5050"]));
        Assert.Null(CommandLine.Dashboard(["--dashboard"]));
    }

    /// <summary>
    /// Kestrel wraps the socket error, and how deep is its business. A match that only read the
    /// outermost exception would quietly stop working on a framework release.
    /// </summary>
    [Fact]
    public void APortAlreadyHeldIsRecognisedThroughItsWrappers() {
        var inUse = new SocketException((int)SocketError.AddressAlreadyInUse);

        Assert.True(CommandLine.IsPortInUse(inUse));
        Assert.True(CommandLine.IsPortInUse(new IOException("failed to bind", inUse)));
        Assert.True(CommandLine.IsPortInUse(
            new InvalidOperationException("start", new IOException("bind", inUse))));
    }

    [Fact]
    public void AnythingElseIsNotThePortBeingHeld() {
        Assert.False(CommandLine.IsPortInUse(
            new SocketException((int)SocketError.ConnectionRefused)));
        Assert.False(CommandLine.IsPortInUse(new InvalidOperationException("something else")));
        Assert.False(CommandLine.IsPortInUse(null));
    }
}
