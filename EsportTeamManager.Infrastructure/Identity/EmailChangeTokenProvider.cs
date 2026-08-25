using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EsportTeamManager.Infrastructure.Identity;

public sealed class EmailChangeTokenProvider<TUser> : DataProtectorTokenProvider<TUser> where TUser : class
{
    public EmailChangeTokenProvider(IDataProtectionProvider dataProtectionProvider, IOptions<EmailChangeTokenProviderOptions> options, ILogger<DataProtectorTokenProvider<TUser>> logger)
        : base(dataProtectionProvider, options, logger)
    {
    }
}