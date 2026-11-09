# Sean: Authentication and Security

This package extends the teammate's uploaded API branch. It has not been pushed to GitHub. Existing API routes and Functions code are preserved. Do not replace main blindly: integrate these changes on a branch based on API.

## Where the code lives

The uploaded branch already contains duplicated project directories. The API to use is:
`CoffeeNChill_Project-main/CoffeeNChill_Project-main/CoffeeNChill.Functions/CoffeeNChill.API`.

The new tests are in its sibling `CoffeeNChill.Auth.Tests` directory. The outer solution only includes the older Functions project; open the nested solution instead. Do not move folders independently of your teammate.

## What Sean's code adds

- `Auth/UserEntity.cs`: Azure `Users` profiles with role as PartitionKey, normalized email as RowKey, PasswordHash, FullName and CreatedDate.
- `Auth/TableUserStore.cs`: profile lookup and creation, with atomic global email reservations in `UserEmailIndex` to reject duplicate emails across roles and concurrent requests.
- ASP.NET Core Identity PasswordHasher: salted PBKDF2 hashes; plaintext passwords never persisted or returned.
- Registration validation: email suitable for an Azure Table key, full name, 12–128 character password with uppercase, lowercase, digit and symbol.
- Signed HMAC-SHA256 JWTs: Name, Email, Role, sub, jti, iat, issuer, audience and expiration. Validates signature, algorithm, issuer, audience and expiry; 30-second clock skew.
- Authentication middleware before authorization, StaffOrAdmin/AdminOnly policies and an authenticated fallback policy for future endpoints.
- Optional first-Admin seeding from private configuration. Existing accounts are never silently promoted or reset.
- A per-IP limit of 20 authentication requests per minute per process, including registration/login. Limit is intentionally simple for the assignment, not a distributed anti-abuse system.
- Swagger Bearer entry and automatic tests using real token verification with fake storage/downstream Functions.

## Endpoint rules

| Endpoint | Allowed |
|---|---|
| POST /api/auth/register | Public; creates Customer only, even if role is supplied |
| POST /api/auth/login | Public |
| GET /api/auth/me | Any valid authenticated account |
| POST /api/auth/register-staff | Admin only; requested role must be Staff or Admin |
| GET /api/menu | Public |
| POST /api/orders/queue | Public |
| POST, PUT, DELETE menu endpoints | Staff or Admin |
| All document endpoints, including download | Staff or Admin |
| PUT /api/orders/status | Staff or Admin |

Missing/invalid/expired token: 401. Valid Customer token on staff endpoint: 403. Staff token on account creation: 403. Invalid registration: 400. Duplicate email: 409. Throttled auth request: 429.

Order-read controllers were not in the uploaded API project. If your teammate adds them, mark them `[Authorize(Policy = "StaffOrAdmin")]`. The current fallback requires authentication but does not by itself impose a staff role.

## Local setup (Windows PowerShell)

1. Install .NET 10 SDK, Node.js if using Azurite, and trust the ASP.NET development HTTPS certificate:
   `dotnet dev-certs https --trust`
2. Start Azurite in a separate terminal:
   `npx azurite --location ./azurite-data`
   This uses standard ports 10000–10002. If your existing Docker Azurite is already running on those ports, use it instead. Do not start two instances.
3. Run `./Start-Auth.ps1` from this package root. It asks for an Admin email and password without displaying the password. Use at least 12 characters and the required character types.
4. Open `https://localhost:7043/swagger`.
5. Run the existing Azure Functions separately at `http://localhost:7071/` for menu/order/document requests. Registration/login only require Table Storage; they can be tested before Functions is started.

The launcher creates a random signing key in memory and clears its environment values on exit. Tokens stop working after restarting the launcher; login again. Seed values are used only when that email has no account. Restarting with another password does not change an existing user's password.

For normal development use .NET user-secrets, or configure environment variables privately:

| Variable | Value |
|---|---|
| AuthStorage__ConnectionString | `UseDevelopmentStorage=true` locally, or your private Azure Storage connection string |
| Jwt__Key | Random secret containing at least 32 UTF-8 bytes; never use a predictable phrase |
| Jwt__Issuer | CoffeeNChill.API |
| Jwt__Audience | CoffeeNChill.Clients |
| Jwt__ExpiryMinutes | 60 (allowed 1–120) |
| SeedAdmin__Email | First Admin email, optional after bootstrap |
| SeedAdmin__Password | Private password, optional after bootstrap |
| Functions__BaseUrl | http://localhost:7071/ locally; internal Functions service URL in containers |
| EnableSwagger | true for the assignment's production demo only |

No JWT key or seed password is committed. appsettings.json includes only non-secret defaults. The .NET user-secrets ID is set on the API project. A .env file is not automatically loaded by dotnet; Compose may inject it when configured by the deployment teammate.

## Demonstration

1. POST /api/auth/register with email, password, fullName. Response is 201 and role Customer.
2. POST /api/auth/login with those credentials. Copy the token, then Swagger Authorize → paste the token only.
3. GET /api/auth/me succeeds; GET /api/documents returns 403 with the Customer token.
4. Clear Authorize: GET /api/documents returns 401.
5. Log in with seeded Admin; use that token to POST /api/auth/register-staff with email, password, fullName, role Staff.
6. Log in as Staff and test a document or menu change. With Functions running and a valid request it reaches the existing API successfully.
7. A Staff token cannot call register-staff (403). An expired or altered token is rejected (401).

## Tests

From the package root:
`dotnet test CoffeeNChill_Project-main/CoffeeNChill_Project-main/CoffeeNChill.Functions/CoffeeNChill.Auth.Tests/CoffeeNChill.Auth.Tests.csproj`

These tests exercise real bearer authentication and controller authorization with in-memory users and a stubbed Functions response. They do not prove the existing Functions backend or production deployment works. See AUTH-VALIDATION.md for execution results.

## Integration and deployment handoff

Create `feature/auth-security` from the team's API branch in your real clone. Copy only changed/new files listed in AUTH-CHANGES.txt, review the diff, run tests, and commit under your own configured identity. No GitHub merge/push has been performed here.

The deployment teammate must expose only the API gateway; Functions and Azurite must stay on the private container network in production. Otherwise anonymous Functions endpoints bypass these gateway checks. Use HTTPS for public logins and bearer tokens, inject a stable private JWT key and storage credentials, and remove SeedAdmin password configuration after bootstrap. Swagger requires EnableSwagger=true for the demo. Never expose Azurite publicly.

The login implementation accepts a successful hash verification that requests a future rehash; it does not yet persist a rehash. Password reset, account disabling, immediate token revocation and refresh tokens are outside this assignment implementation; tokens remain valid until expiry. Do not treat this as a complete production identity platform.

## Email uniqueness and recovery

Azure transactions do not span partitions/tables. The email index is reserved atomically before creating the role-partitioned profile. If profile creation fails or the process stops between writes, the email remains reserved and registration is blocked. This fails closed rather than permitting duplicate privileged identities. Stop registration writers, inspect Users across all roles and the matching UserEmailIndex row, and delete ONLY an orphan index row after confirming no profile exists; then retry. Never delete a live account's reservation. Existing duplicate profiles are treated as a data error and require repair. This limitation is intentional and documented.

## References

- https://learn.microsoft.com/aspnet/core/security/authentication/configure-jwt-bearer-authentication?view=aspnetcore-10.0
- https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.identity.passwordhasher-1
- https://learn.microsoft.com/rest/api/storageservices/performing-entity-group-transactions

## Postman collection

Import `CoffeeNChill-Auth.postman_collection.json`, fill in its email/password variables locally, and run requests in order. Use fresh Customer/Staff emails on repeated full runs (registration deliberately rejects duplicates). The Admin must match your seeded account. Token variables are populated by login scripts. Do not export or share the collection after filling secrets or tokens. The collection tests only authentication and permission denials, so it does not require the Functions backend. Trust your local HTTPS development certificate in Postman.
