# Authentication validation

Validated in this workspace on 9 November 2026 (Africa/Johannesburg).

- API compiled successfully using .NET SDK 10.0.403: zero warnings, zero errors at the initial build.
- Automated tests: 51 passed, 0 failed, 0 skipped. Real JWT middleware, controller routing and role checks; in-memory user store and stubbed Functions backend.
- Local Azurite integration: seeded Admin login; Customer registration and case-normalized login; Customer and anonymous document access denied; Admin creates Staff; Staff login and identity; Staff denied privileged account creation; duplicate email rejected; wrong password rejected. All 11 HTTP checks passed.
- Emulator data inspection: Users and UserEmailIndex persisted; PasswordHash values present; none of the plaintext test passwords present in the persisted table data.

Automated coverage includes salted hashing; required JWT claims and expiry; invalid signature/issuer/audience/expired/malformed tokens; registration validation; role-injection attempt; cross-role duplicate rejection; generic login errors; all eight existing protected route patterns across Anonymous, Customer, Staff and Admin; Admin-only account creation; public routes; and Swagger generation.

Limits: the live check used loopback HTTP only with disposable test credentials. Production must use HTTPS. Existing Functions menu/order/document business operations, cloud Azure, Linux VPS deployment, GitHub merging and the PowerShell launcher on Windows were not run. The tests exercise authorization with a stubbed backend, not end-to-end production behavior. The Postman collection is provided for your own demonstration and was not run in Postman here.
