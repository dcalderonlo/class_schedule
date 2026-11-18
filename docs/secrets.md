# Keep deployment credentials out of Git and agent context

Use `.env.vercel.local` only as a private local reference for `VERCEL_ORG_ID` and `VERCEL_PROJECT_ID`. Fill the values yourself in your editor. This file is excluded by the existing `.env.*` Git rule and is not loaded automatically by the .NET application.

## Identifiers are not authentication tokens

The organization and project IDs identify the Vercel deployment target. They do not replace a Vercel authentication token. Do not put `VERCEL_TOKEN`, SQL Server passwords or connection strings in this local reference file.

For GitHub Actions, configure the token yourself in the repository or environment's Actions secrets. A hosted runner cannot access an ignored file on your computer. The eventual workflow must explicitly receive its identifiers and credentials from GitHub variables or secrets; creating the local file does not configure deployment.

Configure application runtime secrets directly in the hosting service, using environment-scoped sensitive values where supported. Keep production administrator provisioning separate; never enable the development bootstrap in production.

## What the local file protects

- `.gitignore` prevents ordinary accidental staging; it is not encryption and does not prevent `git add --force`.
- Owner-only filesystem permissions reduce access by other local users. They do not prevent a process running as the same user from reading the file.
- The file's contents are not automatically attached to every chat message. An agent can still expose them if a tool reads, searches, prints or uploads them.
- For work on this project, do not read `.env.vercel.local` values without explicit user permission. Check only existence, permissions and Git exclusion. Do not dump environment variables, print credentials, or place secrets in command arguments, logs, artifacts or memory.

An agent instruction is not an isolation boundary. OpenCode read permissions can add a guard against accidental reads, but a read restriction alone does not block access through shell commands or other tools. A stronger boundary requires appropriately restricted tools or OS-level isolation.

## If you need an actual vault

Use an encrypted password manager, an OS credential store or a dedicated secrets manager. A narrowly authorized process can retrieve a credential without displaying it in chat, but that process still receives the credential. Do not grant an agent access to the store if the requirement is that only you can retrieve its secrets.

For CI/CD, GitHub Actions Secrets is preferable to copying a local credentials file into the repository or build artifact. Secret masking is best-effort; review workflow changes before authorizing execution with production credentials.

## Deployment status

No deployment, Vercel login, account lookup, credential read or GitHub secret mutation is performed by this setup. No token has been requested or stored.

Vercel currently documents a **beta Container Runtime** that can run custom OCI images. This is a possible route for the ASP.NET Core application, not evidence that its Blazor circuits, database access or production configuration have been validated there. Resolve those hosting requirements before enabling automatic deployment.

References: [Vercel Container Runtime](https://vercel.com/docs/functions/runtimes/container), [Vercel project selection](https://vercel.com/docs/cli/global-options), [OpenCode permissions](https://opencode.ai/docs/permissions).
