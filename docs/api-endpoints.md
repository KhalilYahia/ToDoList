# API endpoint catalog

All routes are prefixed by `/api/v1`. Every protected tenant route resolves organization scope from JWT claims.

## Authentication and organization

| Method | Route |
|---|---|
| POST | `/auth/register-organization`, `/auth/login`, `/auth/refresh`, `/auth/logout` |
| GET | `/auth/me`, `/organization` |
| PATCH | `/organization` |
| GET | `/branches`, `/branches/{id}` |
| GET/POST | `/departments`, `/members` |
| GET/PATCH/DELETE | `/departments/{id}` |
| GET/PATCH | `/members/{id}` |
| POST | `/members/{id}/activate`, `/members/{id}/suspend` |
| PUT | `/members/{id}/departments` |

## Tasks

| Method | Route |
|---|---|
| GET/POST | `/task-templates`, `/tasks`, `/task-schedules` (task POST returns a distribution response) |
| GET/PATCH/DELETE | `/task-templates/{id}`, `/task-schedules/{id}` |
| GET/PATCH | `/tasks/{id}` |
| POST | `/task-templates/{id}/clone`, `/activate`, `/deactivate`, `/create-task` (create-task returns a distribution response) |
| POST/PATCH/DELETE | `/task-templates/{id}/items[...]` |
| POST | `/task-schedules/{id}/activate`, `/deactivate`, `/generate` |
| GET | `/tasks/calendar`, `/tasks/my` |
| POST | `/tasks/{id}/assign`, `/start`, `/block`, `/resume`, `/complete`, `/submit-for-approval`, `/approve`, `/return`, `/cancel`, `/clone` |
| PATCH | `/tasks/{taskId}/items/{itemId}` |
| POST/DELETE | Task-item evidence attachment routes |

## Department orders and complaints

| Method | Route |
|---|---|
| GET/POST | `/order-templates`, `/department-orders`, `/complaints` |
| GET/PATCH/DELETE | `/order-templates/{id}` |
| GET | `/department-orders/{id}`, `/incoming`, `/outgoing`, `/complaints/{id}` |
| PATCH | `/complaints/{id}` |
| POST | Order-template clone/activation/item routes and `/order-templates/{id}/create-order` |
| POST | Order `/view`, `/accept`, `/assign`, `/start`, `/mark-ready`, `/deliver`, `/confirm-receipt`, `/reject`, `/cancel`, `/attachments` |
| PATCH | `/department-orders/{id}/items/{itemId}` |
| POST | Complaint `/assign`, `/start-review`, `/request-information`, `/respond`, `/close`, `/messages`, `/attachments` |

## Platform administration

| Method | Route |
|---|---|
| POST/GET | `/platform/auth/login`, `/refresh`, `/logout`, `/me` |
| GET/POST/PATCH | `/platform/subscription-plans[...]` |
| GET | `/platform/organizations`, `/{id}`, `/{id}/subscription` |
| GET/POST | `/platform/organizations/{organizationId}/branches` |
| PATCH/DELETE | `/platform/organizations/{organizationId}/branches/{branchId}` |
| POST | Subscription `/activate`, `/extend`, `/change-plan`, `/suspend`, `/reactivate`, `/expire` |
| GET/POST | `/platform/manual-payments`, `/{id}` |
| POST | Manual payment `/{id}/confirm`, `/reject`, `/refund` |

## Notifications and reports

| Method | Route |
|---|---|
| GET | `/notifications`, `/notifications/unread-count` |
| POST | `/notifications/{id}/read`, `/notifications/read-all` |
| GET | `/reports/tasks/summary`, `/by-department`, `/by-assignee`, `/overdue` |
| GET | `/reports/department-orders/summary`, `/by-route`, `/top-items`, `/late` |
| GET | `/reports/complaints/summary` |
| GET | `/platform/reports/subscriptions/summary`, `/payments/summary` |

Use the generated OpenAPI document for exact schemas, query parameters, status codes, and multipart file fields.
