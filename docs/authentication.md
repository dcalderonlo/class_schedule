# Same-origin authentication

Account access uses ASP.NET Core Identity session cookies, not JWT. Configure `ConnectionStrings:ClassSchedule` through deployment configuration or a secret store before using account or schedule APIs; no database is created or migrated at startup. Prepare and review schema migrations separately before deploying.

## Request flow

1. GET `/api/account/csrf` over HTTPS; retain its antiforgery cookie and read `requestToken` from JSON.
2. Send that token in `X-CSRF-TOKEN` with each POST, PUT, or DELETE, including registration, login and logout. Fetch a fresh token after login or logout because the identity changes.
3. POST `/api/account/register` and `/api/account/login` with JSON `{ "email": "student@example.test", "password": "ExampleStrongPassword1!" }`. GET `/api/account/me` returns the signed-in email; POST `/api/account/logout` ends the session.

Cookies require HTTPS, are HttpOnly and SameSite Strict, and the session is nonpersistent with a 30-minute lifetime. Registration does not grant roles. Passwords require at least 12 characters and Identity's default complexity; repeated failed login attempts lock the account temporarily. Login errors are intentionally generic.

Schedule reads require authentication; schedule writes require the Administrator role. No public endpoint creates administrators. An explicit, default-disabled [development-only bootstrap](initial-admin.md) can create the first administrator in an empty user store. It is not a production provisioning procedure.

The account endpoints do not supply password recovery or email verification. Production deployment also needs managed data-protection keys shared across instances and an operational abuse-control strategy; neither is provisioned here. No permissive CORS or proxy trust is configured.
