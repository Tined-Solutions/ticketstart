# Apply Progress: JD Round 1 Fixes

## Batch 2 — User Management

### B2.1 MIGRATION — ✅ Complete (backend)
- Added `Name` property to `User` model with `HasMaxLength(200)`
- Created EF migration `AddUserName`
- Backend: 379/379 tests pass

### B2.2 RED — ✅ Complete (backend)
- Updated `AuthenticationPropertyTests.cs` to remove public-register tests
- Added FsCheck tests for admin-only `POST /api/admin/users`

### B2.3 GREEN — ✅ Complete (backend)
- Added `CreateUserAsync(name, email, password, role)` to `IAuthService`/`AuthService`
- Added shared `ValidateEmail`
- Removed `RegisterAsync` from `AuthService` and `POST /auth/register` from `AuthController`
- Created `POST /api/admin/users` in `AdminController` with `[Authorize(Policy="RequireAdminRole")]`

### B2.4 RED — ✅ Complete (frontend)
- Created `frontend/src/App.test.jsx` with test asserting `/register` shows 404/NotFound
- Extended `frontend/src/pages/AdminPanel.test.jsx` with `describe('User Creation')` block (8 tests):
  1. Form renders correctly (all fields, no Admin role option)
  2. Validation errors for empty fields
  3. Invalid email validation
  4. Short password validation
  5. Successful user creation with feedback
  6. Duplicate email (409) error display
  7. Server error (500) error display
  8. Loading state during submission

### B2.5 GREEN — ✅ Complete (frontend)
- Deleted `frontend/src/pages/Register.jsx`
- Deleted `frontend/src/pages/Register.test.jsx`
- Updated `frontend/src/App.jsx`: removed Register import and `/register` route
- Updated `frontend/src/pages/AdminPanel.jsx`: added user creation form section with:
  - Form fields: Name, Email, Password, Role (Organizador/Staff only, no Admin)
  - Client-side validation (name required, valid email, password >= 8, role required)
  - API call via `apiClient.post('/admin/users', {...})`
  - Success: feedback + form reset + user list refresh
  - Error: uses `getErrorMessage` for consistent error display
  - Loading state: button shows "Creando...", fields disabled

### Verification
- **Frontend**: 206/206 tests pass (16 test files)
- **Backend**: 379/379 tests pass
