# SiteFlow

A construction ERP application for managing material
requests, purchasing, inventory, and project costs.

## Technology stack
- React + TypeScript
- ASP.NET Core Web API
- MySQL

## Project structure
- frontend/ — React application
- backend/ — ASP.NET Core API
- docs/ — Project documentation

## First milestone
Material request → Manager approval → Purchase order

## Project cost tracking

Open **Project Costs** to compare all projects or filter a single project.
The authenticated `GET /api/project-costs` endpoint reports LKR values from
existing records. The ERP operations release below requires database migrations.

- Draft orders: planned purchase order value.
- Confirmed purchases: full value of Issued, PartiallyReceived, and Received
  purchase orders. Draft and Cancelled orders are excluded.
- Received value: posted goods receipt lines less posted supplier returns, at saved unit costs.
- Consumed cost: posted material issue lines at their saved unit costs.
- Budget less consumed: project budget minus consumed material cost.
- Budget less purchases: project budget minus confirmed purchase value.

Each line is rounded to two decimal places, away from zero, before summing,
matching transaction details. Projects without transactions show zero costs.
Purchases, receipts, and issues represent different stages of the same materials
and should not be added together. These comparisons cover materials only, not
labour, equipment, invoices, or payments. Negative budget balances are highlighted.

## ERP operations

The application now includes draft corrections, order cancellation, posting
reversals, staff administration, project assignments, audit history, stock controls,
PDF/Excel reports, supplier invoices and payments, attachments, and notifications.

See [ERP operations and setup](docs/ERP_OPERATIONS.md) for access rules,
migration instructions, workflow limits, and verification commands.

See [interface design](docs/UI_DESIGN.md) for the Tailwind setup, responsive layout,
and motion/accessibility conventions.
