# Stores and storage locations

Apply **AddStoresAndLocations** before starting the updated application. Stop the backend, back up the database, verify the target with `dotnet ef dbcontext info`, and run `dotnet ef database update` from backend/SiteFlow.API. Then restart the API and rebuild/redeploy the frontend.

The migration creates Main Store / Default Location for every existing project and assigns existing stock, receipts, issues, reservations, and inventory operations there. Quantities, values, dates, and original references are preserved. New projects create their default store automatically.

The coding session could not apply the migration because its Windows/MySQL TLS credentials were unavailable. Run it from your own terminal or Workbench without weakening hosted TLS. The incremental SQL script in sql/AddStoresAndLocations.sql is only for databases already on AddMaterialReservations; check migration history before running it once with a schema-capable account.

## Workflow

- All roles can view assigned project stores. Administrators and Storekeepers create/edit stores and locations, including optional rack and bin labels.
- Receive or issue each document into/from one selected location. Split deliveries into separate receipts when using multiple locations.
- Reserve stock against an approved request at a specific location. An issue must select that same location to consume the hold.
- Transfer free stock between locations in the same project or across assigned projects through Stock Operations. Release reservations before transferring and reserve again at the destination if needed.
- Counts, adjustments, supplier returns, and reversals use the selected/original location. Returns must use the original receipt location.
- Inventory and stock exports trace locations. Whole-project balance API totals remain available; location-balances provides the breakdown.
- Optional location-specific planning excludes stock elsewhere and incoming orders whose receiving location is not allocated yet. Consider a transfer before purchasing.

## Controls

Project locks serialize postings, holds, transfers, and deactivation. Locations/stores with stock or remaining holds cannot be deactivated. Codes and transaction history remain unchanged; system MAIN/DEFAULT stays active. Costs are calculated from the source location; transfer value moves atomically with stock.

New forms select locations explicitly. Old API clients that omit a nullable location ID resolve only to MAIN/DEFAULT. Replay checks reject location changes; inventory-operation hashes omit null new location fields for backward compatibility.

Builds, lint, and 49 offline checks pass. Full database workflow tests are added but remain unverified without a disposable MySQL test account. Do not downgrade after location-specific activity without a reviewed recovery plan.
