using DataMigration.Application.Models;

namespace DataMigration.Application.Interfaces;

public interface INotesParser
{
    ParsedNotes Parse(string rawNotes);
}
