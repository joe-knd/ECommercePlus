using ECommercePlus.Identity;

namespace ECommercePlus.Tests;

public class TemporaryPasswordTests
{
    [Fact]
    public void Generated_passwords_meet_the_password_policy_and_are_unique()
    {
        var passwords = Enumerable.Range(0, 200).Select(_ => TemporaryPassword.Generate()).ToList();

        Assert.Equal(passwords.Count, passwords.Distinct().Count());
        Assert.All(passwords, p =>
        {
            Assert.Equal(16, p.Length);
            Assert.Contains(p, char.IsUpper);
            Assert.Contains(p, char.IsLower);
            Assert.Contains(p, char.IsDigit);
            Assert.Contains(p, c => !char.IsLetterOrDigit(c));
        });
    }
}
