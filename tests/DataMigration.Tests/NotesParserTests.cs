using DataMigration.Application.Models;
using DataMigration.Matching;
using Xunit;

namespace DataMigration.Tests;

public sealed class NotesParserTests
{
    [Fact]
    public void Parse_WhenNotesUseAClientLabel_ExtractsClientAndDateAndPreservesRawInformation()
    {
        const string rawNotes = "Klienti: Andi Mucobega\nInstalim kondicioneri\n12/03/2024";
        var parser = new NotesParser();

        ParsedNotes result = parser.Parse(rawNotes);

        Assert.Equal(new DateOnly(2024, 3, 12), result.Date);
        Assert.Equal("Andi Mucobega", result.ClientName);
        Assert.Equal(rawNotes, result.Information);
    }

    [Fact]
    public void Parse_WhenNamePrecedesTheWorkDescription_ExtractsNameAndDate()
    {
        const string rawNotes = "Andi Muçobegaj - riparim kaldaje - 3/1/2024";
        var parser = new NotesParser();

        ParsedNotes result = parser.Parse(rawNotes);

        Assert.Equal(new DateOnly(2024, 1, 3), result.Date);
        Assert.Equal("Andi Muçobegaj", result.ClientName);
        Assert.Equal(rawNotes, result.Information);
    }

    [Fact]
    public void Parse_WhenClientAppearsInsideAnAlbanianWorkDescription_ExtractsClientAndDate()
    {
        const string rawNotes = "U be sherbimi i printerit dhe u ndryshua toner te klienti Andi Mucobega me daten 10/12/2024.";
        var parser = new NotesParser();

        ParsedNotes result = parser.Parse(rawNotes);

        Assert.Equal(new DateOnly(2024, 12, 10), result.Date);
        Assert.Equal("Andi Mucobega", result.ClientName);
        Assert.Equal(rawNotes, result.Information);
    }

    [Fact]
    public void Parse_WhenNotesContainNoRecognizableName_ReturnsNoClientCandidate()
    {
        var parser = new NotesParser();

        ParsedNotes result = parser.Parse("Kontroll teknik; 12/03/2024");

        Assert.Equal(new DateOnly(2024, 3, 12), result.Date);
        Assert.Null(result.ClientName);
    }
}
