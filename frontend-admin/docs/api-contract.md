# API Contract & Integration

This document maps the Admin Panel pages to the Backend API endpoints used.

## Auth
- **Login**: `POST /api/Auth/login`
- **Logout**: `POST /api/Auth/logout`

## Modules

### Dashboard (`/dashboard`)
- **Stats**: (Planned) `/api/Reports/sales`, `/api/AuditLog/statistics`, `/api/Payment/statistics`
- *Current implementation uses mock data for visual demonstration.*

### Invoices (`/invoices`)
- **List**: `GET /api/Invoice` (or `GET /api/Invoice/search`)
- **Details**: `GET /api/Invoice/{id}`
- **Export**: (Planned) CSV Export feature

### Audit Logs (`/audit-logs`)
- **List**: `GET /api/AuditLog`
  - Params: `page`, `pageSize`, `startDate`, `endDate`, `userId`, `action`
- **Details**: `GET /api/AuditLog/{id}`

## API Client Generation
The client is generated using Orval from `swagger.json`.
Configuration: `orval.config.ts`
Output: `src/api/generated`
