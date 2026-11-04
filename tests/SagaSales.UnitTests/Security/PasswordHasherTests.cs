using SagaSales.Identity.Security;
using Xunit;

namespace SagaSales.UnitTests.Security;

public class PasswordHasherTests
{
    private readonly Pbkdf2PasswordHasher _hasher = new();

    [Fact]
    public void Hash_NeverContainsThePlainPassword()
    {
        var hash = _hasher.Hash("SuperSecreto123");

        Assert.DoesNotContain("SuperSecreto123", hash);
        Assert.Equal(3, hash.Split('.').Length);
    }

    [Fact]
    public void Verify_WithTheCorrectPassword_ReturnsTrue()
    {
        var hash = _hasher.Hash("SuperSecreto123");

        Assert.True(_hasher.Verify("SuperSecreto123", hash));
    }

    [Fact]
    public void Verify_WithAWrongPassword_ReturnsFalse()
    {
        var hash = _hasher.Hash("SuperSecreto123");

        Assert.False(_hasher.Verify("supersecreto123", hash));
        Assert.False(_hasher.Verify("SuperSecreto1234", hash));
    }

    [Fact]
    public void Hash_ProducesDifferentOutputForTheSamePassword()
    {
        var first = _hasher.Hash("misma-clave");
        var second = _hasher.Hash("misma-clave");

        Assert.NotEqual(first, second);
        Assert.True(_hasher.Verify("misma-clave", first));
        Assert.True(_hasher.Verify("misma-clave", second));
    }

    [Fact]
    public void Verify_WithAMalformedHash_ReturnsFalse()
    {
        Assert.False(_hasher.Verify("cualquiera", string.Empty));
        Assert.False(_hasher.Verify("cualquiera", "sin-puntos"));
        Assert.False(_hasher.Verify("cualquiera", "abc.def"));
        Assert.False(_hasher.Verify("cualquiera", "notanumber.c2FsdA==.aGFzaA=="));
    }

    [Fact]
    public void Hash_WithAnEmptyPassword_Throws()
    {
        Assert.Throws<ArgumentException>(() => _hasher.Hash(" "));
    }
}
