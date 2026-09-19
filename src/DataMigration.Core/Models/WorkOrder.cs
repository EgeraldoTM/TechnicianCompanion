namespace DataMigration.Core.Models;

public class WorkOrder
{
    public int Id { get; set; }
    public required int TechnicianId { get; set; }
    public required int ClientId { get; set; }
    public required string Information { get; set; }
    public required DateOnly Date { get; set; }
    public required decimal Total { get; set; }
}
