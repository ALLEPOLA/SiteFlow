using Microsoft.EntityFrameworkCore;
using SiteFlow.API.Data;
using SiteFlow.API.Models;
namespace SiteFlow.API.Services;

public static class StorageRules
{
    public static Store DefaultStore(Project project) => new()
    {
        Project = project, Code = "MAIN", Name = "Main Store",
        Locations = new() { new StorageLocation { Code = "DEFAULT", Name = "Default Location" } }
    };
    // Omitted IDs preserve compatibility with existing clients, using only the system default.
    public static Task<StorageLocation?> Resolve(AppDbContext db, int projectId, int? locationId, CancellationToken ct = default) =>
        db.StorageLocations.Include(l => l.Store).SingleOrDefaultAsync(l =>
            l.Store.ProjectId == projectId && l.IsActive && l.Store.IsActive &&
            (locationId.HasValue ? l.Id == locationId.Value : l.Store.Code == "MAIN" && l.Code == "DEFAULT"), ct);
    public static bool Matches(int? requestedId, StorageLocation actual) =>
        requestedId.HasValue ? requestedId.Value == actual.Id : actual.Code == "DEFAULT" && actual.Store.Code == "MAIN";
    public static bool SameEndpoint(int sourceProject, int sourceLocation, int destinationProject, int destinationLocation) =>
        sourceProject == destinationProject && sourceLocation == destinationLocation;
}
