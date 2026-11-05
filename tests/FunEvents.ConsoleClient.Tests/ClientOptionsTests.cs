namespace FunEvents.ConsoleClient.Tests;

public sealed class ClientOptionsTests
{
    [Fact]
    public void TryParse_WithEventAndUser_UsesDefaults()
    {
        var parsed = ClientOptions.TryParse(Args("--event ROCK-BCN-2027 --user USR-001"), out var options, out var error);

        Assert.True(parsed, error);
        var expected = new ClientOptions("ROCK-BCN-2027", "USR-001", Quantity: 1, Parallel: 1, IdempotencyKey: null, ApiUrl: null);
        Assert.Equal(expected, options);
    }

    [Fact]
    public void TryParse_WithEveryOption_ReadsThemAll()
    {
        var args = Args("--event JAZZ-VLC-2027 --user USR-002 --quantity 3 --parallel 50 --idempotency-key retry-1 --api-url http://localhost:5080");

        var parsed = ClientOptions.TryParse(args, out var options, out var error);

        Assert.True(parsed, error);
        var expected = new ClientOptions("JAZZ-VLC-2027", "USR-002", 3, 50, "retry-1", new Uri("http://localhost:5080"));
        Assert.Equal(expected, options);
    }

    [Theory]
    [InlineData("")]
    [InlineData("--user USR-001")]
    [InlineData("--event ROCK-BCN-2027")]
    public void TryParse_WithoutEventOrUser_Fails(string commandLine)
    {
        var parsed = ClientOptions.TryParse(Args(commandLine), out _, out var error);

        Assert.False(parsed);
        Assert.Equal("Both --event and --user are required.", error);
    }

    [Theory]
    [InlineData("--quantity", "0")]
    [InlineData("--quantity", "-1")]
    [InlineData("--quantity", "two")]
    [InlineData("--parallel", "0")]
    [InlineData("--api-url", "not-a-url")]
    [InlineData("--idempotency-key", "")]
    [InlineData("--idempotency-key", "   ")]
    public void TryParse_WithInvalidValue_Fails(string option, string value)
    {
        var parsed = ClientOptions.TryParse([.. Args("--event ROCK-BCN-2027 --user USR-001"), option, value], out _, out var error);

        Assert.False(parsed);
        Assert.Equal($"Invalid value '{value}' for '{option}'.", error);
    }

    [Fact]
    public void TryParse_WithUnknownOption_Fails()
    {
        var parsed = ClientOptions.TryParse(Args("--event ROCK-BCN-2027 --user USR-001 --verbose true"), out _, out var error);

        Assert.False(parsed);
        Assert.Equal("Unknown option '--verbose'.", error);
    }

    [Fact]
    public void TryParse_WithOptionMissingItsValue_Fails()
    {
        var parsed = ClientOptions.TryParse(Args("--event ROCK-BCN-2027 --user"), out _, out var error);

        Assert.False(parsed);
        Assert.Equal("Missing value for '--user'.", error);
    }

    private static string[] Args(string commandLine) => commandLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
}
