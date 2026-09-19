using DataMigration.Application.Models.Matching;
using DataMigration.Core.Models;
using DataMigration.Matching;
using Xunit;

namespace DataMigration.Tests;

public sealed class ClientMatcherTests
{
    [Fact]
    public void Match_WhenNameHasMissingDiacriticAndFinalCharacter_ReturnsClient()
    {
        var andi = new Client("Andi", "Muçobegaj") { Id = 7 };
        var matcher = new ClientMatcher();
        matcher.BuildIndex([andi, new Client("Blerim", "Hoxha") { Id = 8 }]);

        MatchResult<Client> result = matcher.Match("Andi Mucobega");

        Assert.Equal(MatchOutcome.Confident, result.Outcome);
        Assert.Same(andi, result.Matched);
        Assert.True(result.Confidence >= 0.85);
    }

    [Fact]
    public void Match_WhenNameDoesNotResembleAnyClient_ReturnsNoMatch()
    {
        var matcher = new ClientMatcher();
        matcher.BuildIndex([new Client("Andi", "Muçobegaj")]);

        MatchResult<Client> result = matcher.Match("Elona Kola");

        Assert.Equal(MatchOutcome.NoMatch, result.Outcome);
        Assert.Null(result.Matched);
    }

    [Fact]
    public void Match_WhenCandidateContainsExtraCapitalizedWords_MatchesTheNameWindow()
    {
        var fiona = new Client("Fiona", "Llalla") { Id = 9 };
        var matcher = new ClientMatcher();
        matcher.BuildIndex([fiona]);

        MatchResult<Client> result = matcher.Match("Wi-Fi Fiona Llalla");

        Assert.Equal(MatchOutcome.Confident, result.Outcome);
        Assert.Same(fiona, result.Matched);
    }

    [Fact]
    public void Match_WhenGivenTheCompleteNote_MatchesTheEmbeddedClientName()
    {
        var andi = new Client("Andi", "Muçobegaj") { Id = 7 };
        var matcher = new ClientMatcher();
        matcher.BuildIndex([andi]);

        MatchResult<Client> result = matcher.Match(
            "U be sherbimi i printerit te klienti Andi Mucobega me daten 10/12/2024.");

        Assert.Equal(MatchOutcome.Confident, result.Outcome);
        Assert.Same(andi, result.Matched);
    }

    [Fact]
    public void Match_WhenBothFirstAndLastNameContainSmallTypos_ReturnsTheClearBestClient()
    {
        var arian = new Client("Arian", "Jaupaj") { Id = 12 };
        var matcher = new ClientMatcher();
        matcher.BuildIndex([arian, new Client("Valentina", "Pano") { Id = 13 }]);

        MatchResult<Client> result = matcher.Match("Arjan Jaupai");

        Assert.Equal(MatchOutcome.Confident, result.Outcome);
        Assert.Same(arian, result.Matched);
    }
}
