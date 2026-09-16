# Production Readiness

This document records the checks that can be verified in the repository and the
checks that require the target Windows/LAN environment.

## Verified in this repository

- Administrative MVC routes require the `Admin` authorization policy.
- Client requests require protocol version compatibility, a time-bounded HMAC
  proof, a nonce, and replay protection. The loopback proof bypass is limited to
  `Development`.
- Request bodies are limited to 64 KiB and client/admin login endpoints have
  rate limits.
- SQLite integrity can be checked through `/health/ready` and the protected
  administrative diagnostic endpoint.
- Generated backups are validated as SQLite databases and, when a recorded
  checksum exists, are rejected if the file has changed.
- Pending client requests are restored after a failed or cancelled network
  submission, with restoration failures written to the client log.
- The NuGet solution audit reports no vulnerable direct or transitive packages
  with the configured package sources.
- The repository contains a Windows CI workflow that repeats restore, Release
  build, tests, and the package vulnerability audit on pushes and pull requests.
- `dotnet build Adrenalina.slnx --no-restore` and the test project must pass
  before a release commit is accepted.

## Implemented deployment controls

- LAN bindings in production reject plain HTTP at startup.
- HTTPS can load a certificate by external PFX path or Windows certificate-store thumbprint; no certificate secret is stored in the repository.
- `deployment\Publish-Release.ps1` produces self-contained `win-x64` Admin and Client packages after restore, Release build, and tests.
- `deployment\Install-Production.ps1` provisions the server certificate, writes machine deployment settings, creates a Private-profile/LocalSubnet firewall rule, and preserves the previous installation for rollback.
- `deployment\Validate-Production.ps1` checks the installed binaries, deployment settings, certificate, firewall, `/health`, `/health/ready`, and the latest SQLite backup header.
- `deployment\Install-ClientCertificate.ps1` installs the server certificate into the current user's trusted root store on client stations.
- `Adrenalina.Launcher.exe` provides a single entry point with explicit `ADMIN` and `CLIENTE` choices.
- The Client discovers the active Admin URL over the LAN and stores it locally; machine credentials remain explicit and authenticated.

## Required target-environment validation

1. Run the deployment scripts on the target server, use a certificate issued by
   the client's CA when available, install trust on every station, and record
   the output of `Validate-Production.ps1`.
2. The shipped Client delegates station control to the separately published
   `Adrenalina.Agent` Windows service. The Agent applies only reversible,
   allowlisted policies and explicit restart/shutdown/logoff actions. Assigned
   Access, Shell Launcher, edition compatibility and recovery still require
   validation by the client's authorized IT owner on Windows.
3. Test firewall scope, client enrollment, clock synchronization, reconnects,
   simultaneous stations, and operation under a non-administrator account.
4. Confirm backup retention, off-machine copy, restore procedure, and a recent
   successful restore drill. The application validates backups but does not
   perform an unattended destructive restore.
5. Use the included publish/install package and record the installation,
   upgrade, rollback, certificate provisioning, and data migration result.

## Release gate

Do not describe a deployment as production-ready until every target-environment
item above has an owner, evidence, and a recorded result. Passing repository
tests and running the scripts are not evidence of the client's actual TLS trust,
firewall, LAN behavior, backup restore, or optional Windows station policy.
