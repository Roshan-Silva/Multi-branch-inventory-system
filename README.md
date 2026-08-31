# Multi-Branch Inventory & Procurement Management System

A production-style inventory and procurement management platform designed
for organizations operating across multiple branches.

## Backend MVP

The backend MVP implements authentication, branch-scoped inventory,
procurement workflows, partial goods receiving, and an auditable inventory
transaction ledger.

## Tech Stack

### Backend
- ASP.NET Core Web API on .NET 10
- C#
- Entity Framework Core
- JWT authentication

### Frontend
- React
- TypeScript
- Vite

### Database
- PostgreSQL

## Architecture

The backend follows a Clean Architecture-style dependency flow:

```text
API
 -> Application Services
 -> Repository Interfaces
 -> Infrastructure Repositories
 -> Entity Framework Core
 -> PostgreSQL
```

The projects are separated into:

- Domain
- Application
- Infrastructure
- API

Controllers translate HTTP requests and responses, application services own
business rules and authorization checks, and infrastructure repositories own
EF Core persistence concerns.

## Roles

- `SuperAdmin`: system-wide administration and approval authority.
- `BranchManager`: branch-scoped visibility and purchase-request approval.
- `InventoryOfficer`: branch-scoped inventory operations, purchase requests,
  and goods receiving.
- `ProcurementOfficer`: system-wide procurement and read-only goods-receiving
  visibility.

## Primary Workflow

```text
Inventory Officer creates Purchase Request
 -> Branch Manager approves Purchase Request
 -> Procurement Officer creates Purchase Order
 -> SuperAdmin approves Purchase Order
 -> Procurement Officer sends and confirms Purchase Order
 -> Inventory Officer records a Goods Received Note
 -> GRN confirmation updates branch inventory
 -> PurchaseReceipt InventoryTransaction is recorded
 -> partial receipt: Purchase Order becomes PartiallyReceived
 -> full receipt: Purchase Order becomes Completed
```

Important system rules:

- Creating, approving, sending, or confirming a purchase order does not change
  stock.
- Creating a Draft GRN does not change stock or create a ledger entry.
- Stock increases only when a GRN is confirmed.
- Inventory is uniquely scoped by branch and product.
- Every inventory quantity change has an `InventoryTransaction` audit record
  with before and after quantities.
- Operational roles are branch-scoped where applicable; client-supplied branch
  identifiers do not override the authenticated user's scope.
- Important business records use lifecycle states or soft deactivation instead
  of hard deletion.

## Backend Development

Prerequisites:

- .NET 10 SDK
- PostgreSQL

Restore and build:

```powershell
cd backend
dotnet restore
dotnet build
```

Run the API during local development:

```powershell
cd backend/src/MultiBranchInventory.Api
dotnet watch run
```

Configure the database connection, JWT signing key, and initial administrator
credentials with [.NET User Secrets](https://learn.microsoft.com/aspnet/core/security/app-secrets).
Do not store connection strings, passwords, or signing keys in tracked settings
files.

## Backend Smoke Tests

With the API already running at `http://localhost:5296`, run every backend
suite in order with:

```powershell
.\scripts\test-backend.ps1
```

The master runner requires these environment-variable names:

- `MBI_ADMIN_EMAIL`
- `MBI_ADMIN_PASSWORD`
- `MBI_EMPLOYEE_EMAIL`
- `MBI_EMPLOYEE_PASSWORD`
- `MBI_MANAGER_EMAIL`
- `MBI_MANAGER_PASSWORD`
- `MBI_INVENTORY_EMAIL`
- `MBI_INVENTORY_PASSWORD`
- `MBI_PROCUREMENT_EMAIL`
- `MBI_PROCUREMENT_PASSWORD`

It checks API availability, runs all six smoke-test scripts even if an earlier
suite fails, prints a per-suite summary, and exits with code `0` only when every
suite passes.

## Future Work

- Audit logging
- Reporting and dashboards
- Stock transfers
- Docker
- CI/CD
