# Ticketera Online

Full-stack online ticketing platform — event creation, Mercado Pago payments, QR-code ticket validation, and organizer/admin dashboards.

**Stack**: React 19 + ASP.NET Core 9.0 + PostgreSQL (Supabase)

## Quick Start

```bash
# Backend
cd backend
cp appsettings.json.template appsettings.json   # then fill in your keys
dotnet ef database update
dotnet run

# Frontend (separate terminal)
cd frontend
cp .env.template .env
npm install
npm run dev
```

Backend API: `http://localhost:5193` · Swagger: `http://localhost:5193/swagger`
Frontend: `http://localhost:5173`

## Prerequisites

| Tool | Version | Check |
|------|---------|-------|
| .NET SDK | 9.0+ | `dotnet --version` |
| Node.js | 18+ | `node --version` |
| PostgreSQL | 15+ | local install or [Supabase](https://supabase.com) |
| EF Core CLI | (auto-installed) | `dotnet tool install --global dotnet-ef` |

### External services (free-tier dev accounts work)

| Service | Purpose | Config section in `appsettings.json` |
|---------|---------|--------------------------------------|
| [Supabase](https://supabase.com) | PostgreSQL database | `ConnectionStrings` |
| [Cloudflare R2](https://www.cloudflare.com/products/r2/) | Event image storage | `CloudflareR2` |
| [Mercado Pago](https://www.mercadopago.com.ar/developers) | Payment processing | `MercadoPago` |
| [Resend](https://resend.com) | Email delivery | `Resend` |

## Environment Variables

### Backend (`backend/appsettings.json`)

Copy `appsettings.json.template` → `appsettings.json` and fill in your values. Never commit `appsettings.json` or `appsettings.Development.json`.

Key configuration sections:

```
ConnectionStrings:DefaultConnection   — Supabase pooler (port 6543, runtime)
ConnectionStrings:MigrationConnection — Supabase direct  (port 5432, migrations only)
Jwt:SecretKey                         — 32+ character random string
CloudflareR2:*                        — R2 bucket access + secret keys
MercadoPago:AccessToken               — MP access token
MercadoPago:WebhookSecret             — MP webhook signing secret
Resend:ApiKey                         — Resend API key
Resend:FromEmail                      — Verified sender email
QRCode:HmacSecretKey                  — 32+ char HMAC key for QR signing
Reservation:TokenSecretKey            — 32+ char HMAC key for reservation token
```

### Frontend (`frontend/.env`)

Copy `.env.template` → `.env`.

| Variable | Default | Description |
|----------|---------|-------------|
| `VITE_API_BASE_URL` | `/api` | Backend API URL. `/api` uses the Vite dev proxy; set to a full URL for production. |

The Vite dev server proxies `/api/*` → `http://localhost:5029`. Adjust `vite.config.js` if your backend port differs.

## Database Migrations

```bash
cd backend

# Create a new migration (only if you change models)
dotnet ef migrations add YourMigrationName

# Apply all pending migrations
dotnet ef database update

# Use the migration connection string for migrations (port 5432)
dotnet ef database update --connection "Host=...;Port=5432;..."
```

The `MigrationConnection` in `appsettings.json` targets Supabase port 5432 (direct, no pooler). The `DefaultConnection` targets port 6543 (pooler) and is used at runtime.

## Running Locally

Two terminals:

**Terminal 1 — Backend**
```bash
cd backend
dotnet run
```

**Terminal 2 — Frontend**
```bash
cd frontend
npm run dev
```

## Testing

```bash
# Backend (333+ unit + property tests)
cd backend && dotnet test

# Frontend (208+ unit tests)
cd frontend && npm test
```

## API Reference

All endpoints are prefixed with `/api`. Authenticated endpoints require `Authorization: Bearer <jwt>`.

### Auth

| Method | Endpoint | Auth | Description |
|--------|----------|------|-------------|
| `POST` | `/api/auth/register` | — | Register a new user |
| `POST` | `/api/auth/login` | — | Login, returns JWT |

### Events

| Method | Endpoint | Auth | Description |
|--------|----------|------|-------------|
| `GET` | `/api/events` | — | List published events |
| `GET` | `/api/events/{id}` | — | Event detail with ticket types |
| `POST` | `/api/events` | Organizador / Admin | Create event |
| `PUT` | `/api/events/{id}` | Event owner / Admin | Update event |
| `DELETE` | `/api/events/{id}` | Event owner / Admin | Delete event |
| `POST` | `/api/events/{id}/image` | Event owner / Admin | Upload event image |

### Reservations

| Method | Endpoint | Auth | Description |
|--------|----------|------|-------------|
| `POST` | `/api/reservations` | — | Create 10-min reservation (requires `purchaserDNI`, returns token) |
| `GET` | `/api/reservations/{id}` | — | Get reservation status |

### Payments

| Method | Endpoint | Auth | Description |
|--------|----------|------|-------------|
| `POST` | `/api/payments/create-preference` | — | Create Mercado Pago checkout preference (requires reservation token) |
| `POST` | `/api/payments/webhook` | — | Mercado Pago webhook receiver (validates `x-signature`) |

### Tickets

| Method | Endpoint | Auth | Description |
|--------|----------|------|-------------|
| `GET` | `/api/tickets/lookup?email=&dni=` | — | Lookup tickets by email + DNI (returns QR images) |
| `POST` | `/api/tickets/validate` | Staff / Admin | Validate QR code at event entrance |

### Metrics

| Method | Endpoint | Auth | Description |
|--------|----------|------|-------------|
| `GET` | `/api/metrics/events/{id}` | Event owner / Admin | Single-event metrics |
| `GET` | `/api/metrics/organizer` | Organizador / Admin | All events metrics for organizer |

### Admin

| Method | Endpoint | Auth | Description |
|--------|----------|------|-------------|
| `GET` | `/api/admin/users?page=&pageSize=` | Admin | List all users (paginated, max 200) |
| `GET` | `/api/admin/events?page=&pageSize=` | Admin | List all events (paginated, max 200) |
| `GET` | `/api/admin/audit-logs?userId=` | Admin | View audit log (optional user filter) |

### Authentication

All protected endpoints use JWT Bearer tokens. Register or login to obtain a token, then include it in requests:

```
Authorization: Bearer eyJhbGciOiJIUzI1NiIs...
```

Roles: `Organizador`, `Staff`, `Admin`. Role claims are embedded in the JWT and enforced via ASP.NET Core authorization policies.

Interactive API docs (Swagger UI) are available at `/swagger` when running in Development mode.

## Project Structure

```
ticketera-online/
├── backend/                    # ASP.NET Core 9.0 Web API
│   ├── Controllers/            # API endpoints
│   ├── Services/               # Business logic
│   ├── Models/                 # Domain entities + DTOs
│   ├── Data/                   # EF Core DbContext
│   ├── Middleware/              # Global exception handler
│   ├── Helpers/                # Log redactor, HMAC helper
│   ├── Authorization/          # Custom policies + handlers
│   ├── Migrations/             # EF Core migrations
│   └── Tests/                  # xUnit + FsCheck property tests
├── frontend/                   # React 19 + Vite SPA
│   └── src/
│       ├── pages/              # Route-level components
│       ├── components/         # Reusable UI (Button, Modal, FormField, etc.)
│       ├── context/            # Auth + Toast providers
│       └── api/                # Axios client
└── openspec/                   # SDD artifacts (proposal, specs, design, tasks)
```

## Features

- **JWT authentication** with role-based access (Guest / Organizador / Staff / Admin)
- **Event CRUD** with image upload to Cloudflare R2
- **10-minute ticket reservations** with automatic expiration and concurrency control
- **Mercado Pago Checkout Pro** integration with webhook processing
- **HMAC-signed QR codes** for ticket validation with double-scan prevention
- **Ticket lookup** by email + DNI with downloadable QR images
- **QR scanner** (Staff) with camera integration, visual + audio feedback, and scan history
- **Organizer dashboard** with real-time metrics (sales, revenue, inventory, scans)
- **Admin panel** with system-wide event/user management and audit logging
- **Email delivery** via Resend (confirmation + refund notifications)
- **Structured logging** with sensitive-data redaction
- **Global exception handling** with ProblemDetails (RFC 7807)

## Building for Production

```bash
# Backend
cd backend && dotnet publish -c Release -o ./publish

# Frontend
cd frontend && npm run build   # output in dist/
```

## Security

- Passwords hashed with BCrypt
- JWT with configurable expiration
- HMAC-SHA256 signatures for QR codes and reservation tokens
- Webhook signature validation for Mercado Pago
- Role-based authorization on all protected endpoints
- PII (email, DNI) hashed in logs; query-string redaction
- Global `IExceptionHandler` with self-protection

## Troubleshooting

**Database connection fails**: Verify Supabase credentials, use port 6543 for runtime / 5432 for migrations, and whitelist your IP in Supabase settings.

**Migrations fail**: Ensure `dotnet-ef` is installed (`dotnet tool install --global dotnet-ef`). Use the `MigrationConnection` string (port 5432).

**Frontend can't reach API**: Check `VITE_API_BASE_URL` in `.env`. With the dev proxy (`/api`), the backend must be running. For direct access, set the full URL (`http://localhost:5193/api`).

**Build errors**: Clear `node_modules` and reinstall: `rm -rf node_modules && npm install`. Clear Vite cache: `rm -rf node_modules/.vite`.

## License

MIT
