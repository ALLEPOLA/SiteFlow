# Workspace role permissions

Navigation, dashboard actions, and server controller access follow this role matrix. Assigned-project database filters still apply to every project-owned record. Multiple roles combine their allowed workspaces.

- Administrator: all workspaces.
- SiteEngineer: projects, material catalog, material planning, requests, inventory, request corrections, project documents, notifications, stock reports, profile.
- ProjectManager: projects, costs, catalogs, planning, request review, purchase/delivery/issue oversight, inventory, documents, notifications, reports, profile. No correction or staff/finance posting tools.
- ProcurementOfficer: projects/costs, catalogs/suppliers, planning, approved requests, purchase orders, delivery oversight, inventory, order corrections, supplier accounts, documents, notifications, reports, profile.
- Storekeeper: projects/material catalog, order reference, goods receipts, inventory, material issues, stock operations, receipt/issue corrections, documents, notifications, stock/order reports, profile.

Catalog lookup APIs remain shared so permitted forms can load material/supplier choices. Catalog writes still require their original administrator/procurement roles. Read access to an oversight workspace does not grant permission to create, post, or reverse transactions.

Dashboard reviewers see submitted requests from other creators only. Engineers see their own submitted count. Purchasing metrics belong to administrators/procurement; receipt and issue panels follow workspace access. Unavailable summary values are null and unrelated activity lists are empty in the server response.

Reports and document resource types are restricted to accessible workspaces. Correction selectors show only document types the user's role can correct. APIs enforce workspace restrictions even if someone manually sends requests. Original creator checks, no-self-approval, first-password-change gating, active-account checks, and project assignment filters remain in force.

No schema migration. Restart/redeploy the API and rebuild/redeploy the frontend together.


All five roles can view Reservations in assigned projects. Administrators, Project Managers, and Site Engineers can create holds; engineers can reserve only their own approved requests. Release is limited to the creator, Administrator, or Project Manager. Only Administrator/Storekeeper issue posting can consume a hold. The reservations release requires its database migration.
