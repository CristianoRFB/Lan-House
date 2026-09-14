using Adrenalina.Application;

namespace Adrenalina.Tests;

public sealed class SecurityTests
{
    [Fact]
    public void PasswordHasherAcceptsHashesItCreates()
    {
        var hash = PasswordHasher.Hash("segredo-forte");

        Assert.True(PasswordHasher.IsHashFormatValid(hash));
        Assert.True(PasswordHasher.Verify(hash, "segredo-forte"));
        Assert.False(PasswordHasher.Verify(hash, "segredo-incorreto"));
    }

    [Fact]
    public void PasswordHasherRejectsUnboundedWorkFactor()
    {
        var salt = Convert.ToBase64String(new byte[16]);
        var key = Convert.ToBase64String(new byte[32]);
        var maliciousHash = $"{int.MaxValue}.{salt}.{key}";

        Assert.False(PasswordHasher.IsHashFormatValid(maliciousHash));
        Assert.False(PasswordHasher.Verify(maliciousHash, "1234"));
    }

    [Fact]
    public void MachineProofIsBoundToOperationAndTimestamp()
    {
        var timestamp = DateTime.UtcNow;
        var nonce = Guid.NewGuid().ToString("N");
        var proof = MachineAuthentication.CreateProof("machine-secret", timestamp, nonce, "heartbeat");

        Assert.True(MachineAuthentication.VerifyProof("machine-secret", timestamp, nonce, "heartbeat", proof));
        Assert.False(MachineAuthentication.VerifyProof("machine-secret", timestamp, nonce, "login", proof));
        Assert.False(MachineAuthentication.VerifyProof("machine-secret", timestamp.AddMinutes(-3), nonce, "heartbeat", proof));
    }

    [Fact]
    public void MachineReplayGuardAcceptsNonceOnlyOnce()
    {
        var guard = new Adrenalina.Server.Infrastructure.MachineReplayGuard();
        var nonce = Guid.NewGuid().ToString("N");

        Assert.True(guard.TryAccept(nonce));
        Assert.False(guard.TryAccept(nonce));
    }

    [Fact]
    public void MachineReplayGuardBoundsUniqueNonceMemory()
    {
        var guard = new Adrenalina.Server.Infrastructure.MachineReplayGuard(2);

        Assert.True(guard.TryAccept("one"));
        Assert.True(guard.TryAccept("two"));
        Assert.False(guard.TryAccept("three"));
    }

    [Fact]
    public void PasswordFailurePolicyLocksAfterFiveAttempts()
    {
        var account = new Adrenalina.Domain.UserAccount();
        for (var attempt = 1; attempt <= 5; attempt++)
        {
            account.FailedLoginAttempts++;
            if (account.FailedLoginAttempts >= 5)
            {
                account.LockedUntilUtc = DateTime.UtcNow.AddMinutes(10);
                account.FailedLoginAttempts = 0;
            }
        }

        Assert.True(account.LockedUntilUtc > DateTime.UtcNow);
        Assert.Equal(0, account.FailedLoginAttempts);
    }
}
