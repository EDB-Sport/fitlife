using System.Reflection;
using FitLife.Authentication;
using IdentityService.Domain;

namespace IdentityService.Tests.Domain;

/// <summary>Rolle: what an account may do (GLOSSARY.md). Exactly Medlem, Træner and Admin.</summary>
public sealed class RoleTests
{
    private static readonly string[] GlossaryNames = ["Medlem", "Træner", "Admin"];

    [Test]
    public void All_IsMedlemTraenerAndAdmin_WithTheGlossaryNames()
    {
        // Arrange & Act
        IEnumerable<string> names = Role.All.Select(role => role.Name);

        // Assert
        Assert.That(Role.All, Is.EqualTo(new[] { Role.Medlem, Role.Traener, Role.Admin }));
        Assert.That(names, Is.EqualTo(GlossaryNames));
    }

    [Test]
    public void Role_All_MatchesFitLifeRoles()
    {
        // Arrange: the role claim values the shared library checks against (#362)
        IEnumerable<string?> libraryNames = typeof(FitLifeRoles)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.IsLiteral)
            .Select(field => (string?)field.GetRawConstantValue());

        // Act
        IEnumerable<string> names = Role.All.Select(role => role.Name);

        // Assert
        Assert.That(names, Is.EquivalentTo(libraryNames));
    }

    [TestCase("Medlem")]
    [TestCase("Træner")]
    [TestCase("Admin")]
    public void FromName_WithAGlossaryName_ReturnsTheRoleWithThatName(string name)
    {
        // Arrange & Act
        var role = Role.FromName(name);

        // Assert
        Assert.That(role.Name, Is.EqualTo(name));
        Assert.That(role, Is.SameAs(Role.All.Single(known => string.Equals(known.Name, name, StringComparison.Ordinal))));
    }

    [TestCase("Trainer")]
    [TestCase("medlem")]
    [TestCase("")]
    public void FromName_WithAnotherName_Throws(string name)
    {
        // Arrange & Act & Assert
        Assert.That(() => Role.FromName(name), Throws.InstanceOf<ArgumentException>());
    }
}
