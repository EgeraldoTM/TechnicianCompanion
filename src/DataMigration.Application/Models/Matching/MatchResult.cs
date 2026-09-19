namespace DataMigration.Application.Models.Matching;

public record MatchResult<T>(T? Matched, double Confidence, MatchOutcome Outcome);
