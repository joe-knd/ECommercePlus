using Microsoft.AspNetCore.Identity;

namespace ECommercePlus.Identity;

public class AppUser : IdentityUser
{
    public bool MustChangePassword { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
