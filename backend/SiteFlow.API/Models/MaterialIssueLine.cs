namespace SiteFlow.API.Models;

public class MaterialIssueLine
{
    public int Id { get; set; }
    public int? ReservationId { get; set; }
    public MaterialReservation? Reservation { get; set; }

    public int MaterialIssueId { get; set; }

    public MaterialIssue MaterialIssue { get; set; } = null!;

    public int MaterialId { get; set; }

    public Material Material { get; set; } = null!;

    // The issued quantity is positive here.
    // Its stock movement will have a negative quantity.
    public decimal Quantity { get; set; }

    // The backend will calculate this when posting the issue.
    public decimal UnitCost { get; set; }
}