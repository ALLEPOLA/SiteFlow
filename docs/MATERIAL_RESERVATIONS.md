# Material reservations

## Setup
This release requires the **AddMaterialReservations** migration before using inventory, planning, or issue posting.

From backend/SiteFlow.API, use a migration account with schema privileges:
```powershell
dotnet ef database update
```
Confirm the target connection first with `dotnet ef dbcontext info`. The development connection was verified as localhost/siteflow_db during implementation. A migration attempt from the coding session failed before schema changes because Windows TLS credentials were unavailable. Do not disable TLS on the hosted connection. Apply the same migration to the hosted database with the deployment migration procedure before deploying the updated API/frontend.

An incremental SQL script is provided in sql/AddMaterialReservations.sql for databases already on AddInventoryOperationReversals. Execute it only once, after checking __EFMigrationsHistory. Use a database backup and a schema-capable migration account; keep runtime accounts restricted.

## Workflow
1. Submit a material request and have a different authorized account approve it.
2. Open Reservations. Choose an approved request line, quantity, and reason.
3. Engineers can reserve their own approved requests. Administrators and Project Managers can reserve approved work in projects they can access.
4. Inventory displays physical on-hand, still reserved, and available quantities.
5. Storekeepers select a reservation on each Material Issue line to consume it. Unreserved issues can use only free stock.
6. The creator, Administrator, or Project Manager can release the unused remainder with a reason.

Every hold is linked to approved work. Remaining = original quantity - consumed - released. Active holds cannot exceed available physical stock, and cumulative reserved/consumed quantities cannot exceed that approved request line. Released amounts can be reserved again, but consumed amounts continue to use the approved quota.

## Integrity
- Holds do not create stock movements or change stock valuation.
- Reserve/create, release, issues, and all stock reductions share the existing project lock under a database transaction.
- Counts, adjustments, transfers, supplier returns, receipt reversals, and operation reversals cannot reduce stock below remaining holds.
- Reversing an issue restores its physical stock and reservation together. If a hold's unused remainder was released, reversing a consumed portion reactivates the restored portion; review and release it if no longer needed.
- UUIDs prevent duplicate reservation creation. Reusing a UUID with different data returns a conflict. Repeating a release cannot release the same remainder twice.
- Existing issue UUID replay protection now also compares the reservation reference.
- Project assignment filters apply to reservation reads and writes.
- Planning and low-stock notifications use unreserved stock. Planning is for additional demand, so it subtracts all existing holds.
- Reservations do not expire automatically. Release or issue them explicitly. The register shows up to 500 rows; filter by project for larger registers.
- Incoming purchase orders cannot be reserved until their stock is received.
- Unlinked legacy issues cannot be attributed retroactively to an approved work request; only reservation-linked fulfillment counts toward its consumed quota.

## Verification
Offline checks cover arithmetic, outbound protection, project-filter SQL generation, migration integrity, and role access. Database tests cover duplicate/replayed holds, simultaneous reservations, blocked issues/transfers/counts/returns/reversals, reserved fulfillment, issue reversal, release, and planning.

The full integration harness requires a separate local MySQL account able to create/drop its own uniquely named disposable test database; the restricted siteflow_app account cannot run those database tests. Never grant production runtime credentials broad create/drop permissions for testing.
