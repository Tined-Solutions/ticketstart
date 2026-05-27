# Ticketera Online MVP

A full-stack online ticketing platform built with React and ASP.NET Core. This system enables event organizers to create and manage events, sell tickets through Mercado Pago integration, and validate attendees via QR code scanning.

## Project Structure

This is a monorepo containing both frontend and backend applications:

```
ticketera-online/
├── backend/              # ASP.NET Core 8.0 Web API
│   └── TicketeraOnline.Api/
├── frontend/             # React 18+ SPA (Vite)
├── TicketeraOnline.sln   # .NET Solution file
└── README.md             # This file
```

## Technology Stack

### Backend
- **Framework**: ASP.NET Core 8.0 Web API
- **ORM**: Entity Framework Core 8.0
- **Database**: PostgreSQL (Supabase)
- **Authentication**: JWT (JSON Web Tokens)
- **Image Storage**: Cloudflare R2 (S3-compatible)
- **Payment Gateway**: Mercado Pago Checkout Pro
- **Email Service**: Resend
- **QR Code Generation**: QRCoder
- **Password Hashing**: BCrypt.Net

### Frontend
- **Framework**: React 18+
- **Build Tool**: Vite
- **Routing**: React Router
- **HTTP Client**: Axios
- **QR Scanner**: html5-qrcode or react-qr-reader
- **QR Display**: qrcode.react

## Prerequisites

Before you begin, ensure you have the following installed:

- **.NET SDK 8.0 or higher**: [Download here](https://dotnet.microsoft.com/download)
- **Node.js 18+ and npm**: [Download here](https://nodejs.org/)
- **PostgreSQL** (or Supabase account): [Supabase](https://supabase.com/)
- **Git**: [Download here](https://git-scm.com/)

### External Service Accounts

You'll need accounts and API keys for:

1. **Supabase** (PostgreSQL database)
   - Create a project at [supabase.com](https://supabase.com/)
   - Note your connection string (both direct and pooled)

2. **Cloudflare R2** (Image storage)
   - Create an R2 bucket at [cloudflare.com](https://www.cloudflare.com/products/r2/)
   - Generate access key and secret key

3. **Mercado Pago** (Payment processing)
   - Create a developer account at [mercadopago.com](https://www.mercadopago.com.ar/developers)
   - Get your access token and webhook secret

4. **Resend** (Email delivery)
   - Sign up at [resend.com](https://resend.com/)
   - Generate an API key

## Local Development Setup

### 1. Clone the Repository

```bash
git clone <repository-url>
cd ticketera-online
```

### 2. Backend Setup

#### 2.1 Navigate to Backend Directory

```bash
cd backend
```

#### 2.2 Configure Application Settings

Create an `appsettings.Development.json` file in the `backend` directory:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "ConnectionStrings": {
    "DefaultConnection": "Host=<your-supabase-host>;Port=6543;Database=postgres;Username=<username>;Password=<password>;Pooling=true;",
    "MigrationConnection": "Host=<your-supabase-host>;Port=5432;Database=postgres;Username=<username>;Password=<password>;"
  },
  "Jwt": {
    "SecretKey": "<generate-a-secure-random-key>",
    "Issuer": "TicketeraOnline",
    "Audience": "TicketeraOnlineUsers",
    "ExpirationMinutes": 1440
  },
  "CloudflareR2": {
    "AccessKey": "<your-r2-access-key>",
    "SecretKey": "<your-r2-secret-key>",
    "BucketName": "<your-bucket-name>",
    "Endpoint": "https://<account-id>.r2.cloudflarestorage.com",
    "PublicUrl": "https://<your-custom-domain-or-r2-dev-url>"
  },
  "MercadoPago": {
    "AccessToken": "<your-mercadopago-access-token>",
    "WebhookSecret": "<your-webhook-secret>"
  },
  "Resend": {
    "ApiKey": "<your-resend-api-key>",
    "FromEmail": "noreply@yourdomain.com"
  },
  "QRCode": {
    "SecretKey": "<generate-a-secure-random-key-for-hmac>"
  }
}
```

**Important Notes:**
- Use **port 6543** (transaction mode pooler) for the runtime connection string
- Use **port 5432** (direct connection) for migrations
- Generate secure random keys for JWT and QR code signing (at least 32 characters)
- Never commit `appsettings.Development.json` to version control

#### 2.3 Restore Dependencies

```bash
dotnet restore
```

#### 2.4 Run Database Migrations

Once Entity Framework Core is configured (in later tasks):

```bash
# Create migration
dotnet ef migrations add InitialCreate

# Apply migration to database
dotnet ef database update
```

#### 2.5 Run the Backend

```bash
dotnet run
```

The API will be available at `https://localhost:7000` (or the port specified in `launchSettings.json`).

### 3. Frontend Setup

#### 3.1 Navigate to Frontend Directory

```bash
cd ../frontend
```

#### 3.2 Install Dependencies

Dependencies should already be installed, but if needed:

```bash
npm install
```

#### 3.3 Configure Environment Variables

Create a `.env` file in the `frontend` directory:

```env
VITE_API_BASE_URL=https://localhost:7000/api
```

Adjust the port if your backend runs on a different port.

#### 3.4 Run the Frontend

```bash
npm run dev
```

The React app will be available at `http://localhost:5173`.

### 4. Running Both Applications

For development, you'll need two terminal windows:

**Terminal 1 (Backend):**
```bash
cd backend
dotnet run
```

**Terminal 2 (Frontend):**
```bash
cd frontend
npm run dev
```

## Project Features

### User Roles

- **Guest**: Browse events and purchase tickets
- **Organizador**: Create and manage events, view dashboard metrics
- **Staff**: Scan and validate tickets at event entrances
- **Admin**: Full system access, manage all events and users

### Core Functionality

1. **Authentication & Authorization**
   - JWT-based authentication
   - Role-based access control
   - Secure password hashing with BCrypt

2. **Event Management**
   - Create, edit, and delete events
   - Upload event images to Cloudflare R2
   - Define multiple ticket types per event
   - Real-time ticket availability tracking

3. **Ticket Reservation System**
   - 10-minute temporary reservations
   - Automatic inventory management
   - Background service for expired reservation cleanup
   - Concurrency control to prevent overselling

4. **Payment Processing**
   - Mercado Pago Checkout Pro integration
   - Webhook handling for payment notifications
   - Automatic refunds on stock failures
   - Transaction logging and audit trail

5. **QR Code Tickets**
   - HMAC-SHA256 signed QR codes
   - Unique ticket identifiers
   - Double-scan prevention
   - Event-specific validation

6. **Email Delivery**
   - Ticket confirmation emails with QR codes
   - Event details and purchase information
   - Refund notifications
   - Retry logic for failed deliveries

7. **Ticket Lookup**
   - Retrieve tickets by email and DNI
   - Download/print QR codes

8. **QR Scanner (Staff)**
   - Web-based camera scanner
   - Real-time validation
   - Visual and audio feedback
   - Scan history logging

9. **Organizer Dashboard**
   - Event metrics (sales, revenue, inventory, scans)
   - Event management interface
   - Real-time data updates

10. **Admin Panel**
    - System-wide event management
    - User account management
    - Audit logging for admin actions

## Testing

### Backend Tests

```bash
cd backend
dotnet test
```

The project includes:
- Unit tests for services and controllers
- Property-based tests for correctness properties
- Integration tests for external services

### Frontend Tests

```bash
cd frontend
npm test
```

## Building for Production

### Backend

```bash
cd backend
dotnet publish -c Release -o ./publish
```

### Frontend

```bash
cd frontend
npm run build
```

The production build will be in the `frontend/dist` directory.

## Database Schema

The system uses the following main entities:

- **User**: User accounts with roles
- **Event**: Event information and details
- **TicketType**: Ticket categories with pricing and inventory
- **Reservation**: Temporary ticket holds with expiration
- **Ticket**: Confirmed tickets with QR codes
- **Transaction**: Payment transaction records

See the design document for detailed entity relationships and properties.

## API Documentation

Once the backend is running, API documentation is available at:

- Swagger UI: `https://localhost:7000/swagger`

## Security Considerations

- All passwords are hashed using BCrypt
- JWT tokens for authentication
- HMAC-SHA256 signatures for QR codes
- Webhook signature validation for payment notifications
- Role-based authorization on all protected endpoints
- Sensitive data excluded from logs and error messages

## Troubleshooting

### Backend Issues

**Database Connection Errors:**
- Verify Supabase connection string is correct
- Ensure you're using port 6543 for runtime, 5432 for migrations
- Check that your IP is allowed in Supabase settings

**Migration Errors:**
- Ensure you're using the MigrationConnection string (port 5432)
- Verify Entity Framework Core tools are installed: `dotnet tool install --global dotnet-ef`

### Frontend Issues

**API Connection Errors:**
- Verify `VITE_API_BASE_URL` in `.env` matches your backend URL
- Check CORS configuration in backend
- Ensure backend is running

**Build Errors:**
- Clear node_modules and reinstall: `rm -rf node_modules && npm install`
- Clear Vite cache: `rm -rf node_modules/.vite`

## Contributing

1. Create a feature branch from `main`
2. Make your changes
3. Write tests for new functionality
4. Ensure all tests pass
5. Submit a pull request

## License

[Specify your license here]

## Support

For issues and questions, please open an issue in the repository.
# ticketstart
