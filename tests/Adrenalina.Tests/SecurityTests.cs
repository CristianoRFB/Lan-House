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
}
