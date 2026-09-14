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
- `dotnet build Adrenalina.slnx --no-restore` and the test project must pass
  before a release commit is accepted.

## Required target-environment validation

1. Configure a certificate outside the repository using
   `Kestrel:Certificates:Default:Path` and `Password`, then run the LAN binding
   over HTTPS. Do not expose the default HTTP LAN binding to an untrusted network.
2. Install and validate an authorized Windows station-enforcement provider.
   The shipped `SafeNoOpStationEnforcementService` is intentionally not a
   security control.
3. Test firewall scope, client enrollment, clock synchronization, reconnects,
   simultaneous stations, and operation under a non-administrator account.
4. Confirm backup retention, off-machine copy, restore procedure, and a recent
   successful restore drill. The application validates backups but does not
   perform an unattended destructive restore.
5. Package an installer with upgrade, rollback, certificate provisioning, and
   data migration steps. No installer is included in this repository yet.

## Release gate

Do not describe a deployment as production-ready until every target-environment
item above has an owner, evidence, and a recorded result. Passing repository
tests alone is not evidence of Windows policy enforcement, TLS deployment, or
LAN behavior.
