# Material requirements planning

Open **Material Planning** under Materials & purchasing. Select an assigned active project, a required date, and additional upcoming material demand. Enter safety quantities, supplier lead times in calendar days, and estimated unit prices.

The server calculates each material separately:
- Shortage = max(0, demand + safety quantity - recorded stock - eligible incoming).
- Available stock is the signed ledger balance minus remaining material reservations. Corrections and supplier returns are included.
- Incoming is the unreceived quantity on issued/partially received purchase orders, expected between today's UTC date and the required date (inclusive). Posted receipts are subtracted; reversed receipts are excluded.
- Overdue and later promises are disclosed but not trusted as timely supply. Draft/cancelled orders do not count.
- Order-by date = required date minus the user-entered lead time.
- Replenishment value uses per-line rounding to 2 decimals; quantity precision is 3 decimals.
- Budget comparison subtracts issued purchase commitments and the suggested purchase estimate from the project's budget. It is a material purchasing indicator, not general-ledger cash availability. Zero prices are flagged as incomplete estimates.

All signed-in staff can analyze their assigned projects. Administrators and Site Engineers can convert shortages into an ordinary draft request, using the existing creator/submission/review controls. The UI rechecks supply before conversion and asks for another review if the result changed.

## Limits
Planning is a what-if calculator, not a reservation engine or prediction. Inputs exist only while the page is open. The resulting draft request is saved using the existing API. Other plans, approved but unpurchased requests, and draft POs are not allocations; review them before buying again. Supplier returns do not automatically reopen completed POs. Lead times/prices are manual assumptions, taxes and transport are excluded. Concurrent supply changes can still happen after a recheck. If a save response is lost, check Material Requests before starting another plan.

The original planning page requires no schema changes, but the reservations release requires AddMaterialReservations. Redeploy both the API and rebuilt frontend after applying it.

## Verification
The console integration harness includes standalone calculations for first-day plans, deadline boundaries, overdue/later deliveries, safety stock, cost, and lead time. HTTP tests cover anonymous/assignment access, precision, duplicates, and partially received PO accounting. Run its offline mode for calculations and SQL/model checks; the full mode needs a disposable MySQL test database (see the harness instructions).

For local test runs, --local-only refuses remote hosts. --local-no-tls optionally bypasses TLS only for localhost disposable tests in Windows environments without TLS credentials. It does not change the application connection or hosted TLS. The test account still requires CREATE/DROP rights; do not grant these to the production runtime account.
