# ERP operations

## Setup

Build the API, apply migrations using the configured MySQL connection, and restart
the API. From `backend/SiteFlow.API`:

```powershell
dotnet build
dotnet ef database update
dotnet run
```

Stop the running API before building if Windows reports locked output files.
The migrations add ERP tables, account activation, reorder levels, and stock
movement references. Existing accounts default to active. Existing stock and
documents are retained.

An administrator must assign existing staff to their projects in **Users**.
Staff without assignments see no projects. Administrators can access all projects.
Project restrictions are enforced by the API for documents, costs, inventory,
attachments, notifications, and exports, including requests made directly by ID.
Transfers require access to both projects. Role permissions and independent
request approval rules still apply within assigned projects.

## Features and access

| Feature | Access and behavior |
| --- | --- |
| Corrections | Engineers edit their own draft material requests; procurement staff and administrators edit draft purchase orders and cancel unreceived orders. Storekeepers and administrators reverse receipts and issues. Every correction requires a reason. |
| Users | Administrators list staff, edit names and roles, disable/reactivate accounts, and reset passwords. Administrator accounts cannot be changed through staff controls. Role changes and resets invalidate existing sessions. Temporary passwords require a password change. |
| Project assignments | Administrators assign or remove project access with a reason. |
| Audit history | Administrators search dated actor/action/resource/reason records for successful write operations. Passwords and uploaded content are excluded. This is operational history, not a tamper-proof external audit archive or a field-by-field diff. |
| Stock controls | Storekeepers and administrators record physical counts, signed adjustments, inter-project transfers, and receipt-linked supplier returns, and reverse these operations with a reason. |
| Reports | Authenticated staff export accessible purchase orders, stock balances, and project costs as PDF or XLSX, with project and order filters. |
| Supplier accounts | Procurement staff and administrators match invoices to posted receipt quantities at purchase-order prices, view outstanding balances, record payments, and cancel unpaid invoices. |
| Attachments | Assigned staff attach PDF, JPG, PNG, and TXT files up to 5 MB to accessible projects and supported transaction records. Invoice documents also require a finance role. Only the uploader or an administrator can delete an attachment. |
| Notifications | In-app pending approvals, overdue deliveries, low stock, and overdue invoices; read state is personal. Administrators and procurement staff set material reorder levels. |

## Corrections and valuation

Original postings remain in the ledger. Reversals add compensating stock movements
and change the original document's status. Duplicate reversal attempts fail.
Negative stock is rejected. Receipts with active dependent stock activity, invoices,
or supplier returns cannot be reversed until those dependencies are resolved.
Reverse later dependent stock operations first to preserve valuation.

Physical counts enter the actual quantity on hand; adjustments enter a signed
quantity difference. Transfers post matching outgoing and incoming movements in
one transaction. Material issues, counts, adjustments, and transfers use the current
moving weighted stock value. Supplier returns use the original receipt price and
cannot exceed unreturned receipt quantities or available stock quantity/value.
Backdated stock activity is rejected when it precedes later ledger activity.
Previous issue costs remain as originally posted.

Orders with posted receipts or invoices cannot be cancelled. Draft edits preserve
approved request quantity limits. Cancelled orders release their reserved request
quantities. Costs exclude reversed receipts/issues and subtract posted supplier
returns from received material value; purchase commitments remain purchase-order
values. Counts and adjustments affect stock value, rather than receipt value.

## Invoice and reporting scope

Invoices cover material values at saved purchase-order prices. Taxes, discounts,
credit notes, general ledger accounts, and payment reversals are not modeled.
Payments cannot exceed the outstanding invoice balance, including concurrent
submissions. Submission keys prevent duplicate stock operations, invoices, and
payments. Supplier returns against invoiced receipt lines require the unpaid
invoice to be cancelled first. Paid invoices cannot be cancelled.

Notifications are generated when the in-app list is loaded; there is no email,
SMS, scheduled delivery, or push service. Low-stock notices apply to existing
project/material balances with a configured reorder level.

Attachments are stored in the application database. Include them in backup and
capacity planning. XLSX exports preserve Unicode and store user text as text, so
it cannot execute spreadsheet formulas. The built-in PDF renderer uses basic
Latin fonts; use XLSX for Sinhala/Tamil or other Unicode content. The reports
cover materials, rather than a complete construction profit-and-loss statement.

## Verification

```powershell
cd frontend
npm run build
npm run lint
cd ../backend
dotnet run --project SiteFlow.IntegrationTests -- --offline
```

The full integration suite uses a disposable `siteflow_test_<guid>` MySQL database,
applies every migration, starts an isolated API, runs authenticated workflows, and
removes the database afterward. Supply a connection for an isolated MySQL test
server with CREATE/DROP DATABASE privileges:

```powershell
$env:SITEFLOW_TEST_CONNECTION = '<test MySQL connection string>'
dotnet run --project SiteFlow.IntegrationTests
```

Do not use production administrator credentials for tests. The configured
application connection is used if the test override is absent. The suite never
uses the application database name for test data. Its API build output is
`SiteFlow.API/bin/erp-check` to avoid replacing a running development API.
