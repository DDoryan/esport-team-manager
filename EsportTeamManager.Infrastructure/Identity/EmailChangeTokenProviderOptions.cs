using Microsoft.AspNetCore.Identity;

namespace EsportTeamManager.Infrastructure.Identity;

public sealed class EmailChangeTokenProviderOptions : DataProtectionTokenProviderOptions
{
    public EmailChangeTokenProviderOptions()
    {
        Name = "EmailChangeDataProtectorTokenProvider";
        TokenLifespan = TimeSpan.FromHours(1);
    }
}