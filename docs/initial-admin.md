# Development initial administrator

Bootstrap is **disabled by default** and is available only in the Development environment. It is not a production provisioning procedure. First have an operator review and apply the [initial schema](database.md) to an authorized empty database; startup never creates or migrates it.

## Quick path

1. Configure `ConnectionStrings:ClassSchedule` privately for that database. Set `ASPNETCORE_ENVIRONMENT` to `Development` only for a development installation.
2. Supply `DevelopmentAdmin:Enabled` = `true`, `DevelopmentAdmin:Email`, and `DevelopmentAdmin:Password` via a private environment or secret store. Environment variable keys use double underscores, for example `DevelopmentAdmin__Enabled`. Do not place credentials in source, command arguments, shell history, or logs.
3. Start the application, obtain an antiforgery token and sign in over HTTPS through the [account API](authentication.md). The email is the username; its mailbox is not verified.
4. Disable the flag and remove the initial secret from the application's configuration and secret store after successful setup.

The password must satisfy Identity's policy (at least 12 characters, uppercase, lowercase, digit and non-alphanumeric) and the account API maximum of 128 characters. The email must be valid and at most 256 characters. Missing or invalid configuration fails startup. Enabling this outside Development fails startup before database access.

Bootstrap creates a user only when the user store is empty. An existing Administrator role without users is reused. If the requested email already belongs to an Administrator, startup is idempotent: it does not reset the password or create another user. Any other existing user blocks bootstrap; no ordinary account is promoted and no password is reset. Failed Identity operations roll back the transaction. Correct the configuration or consult an authorized operator; do not clear an existing database to force bootstrap.

No public endpoint creates administrators. This development convenience does not provide account recovery, MFA, trusted production operator provisioning, or production deployment certification.
