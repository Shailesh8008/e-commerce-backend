using Ecommerce.Api.Models;

namespace Ecommerce.Api.Services;

public interface ITokenService
{
    string GenerateToken(ApplicationUser user, IEnumerable<string> roles);
    DateTime GetTokenExpiryUtc();
}
