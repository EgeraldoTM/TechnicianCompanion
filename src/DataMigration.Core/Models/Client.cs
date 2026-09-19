namespace DataMigration.Core.Models;

public class Client(string firstName, string lastName)
{
    public int Id { get; set; }
    public string FirstName { get; init; } = firstName;
    public string LastName { get; init; } = lastName;
    public string FullName => $"{FirstName} {LastName}";
}
