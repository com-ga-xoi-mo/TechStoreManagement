using System.Globalization;
using System.Security.Claims;
using TechStore.Api.Identity.Entities;
using TechStore.Shared.Enums;

namespace TechStore.Api.Identity.Services;

public sealed record AuthenticatedUser(Guid Id, RoleType Role, long TokenVersion)
{
    public static RoleType? SoleRole(User? user) => user?.UserRoles.Count == 1
        && user.UserRoles.Single().Role is { } role && Enum.IsDefined(role.Name) ? role.Name : null;

    public static bool TryRead(ClaimsPrincipal principal, out AuthenticatedUser user)
    {
        user = null!;
        var subjects = principal.Claims.Where(c => c.Type is ClaimTypes.NameIdentifier or "sub").Select(c => c.Value).ToArray();
        var roles = principal.Claims.Where(c => c.Type is ClaimTypes.Role or "role").Select(c => c.Value).ToArray();
        var versions = principal.FindAll("token_version").Select(c => c.Value).ToArray();
        if (subjects.Length == 0 || !Guid.TryParse(subjects[0], out var id) || id == Guid.Empty
            || subjects.Any(value => !Guid.TryParse(value, out var other) || other != id)
            || roles.Length != 1 || !Enum.TryParse<RoleType>(roles[0], out var role) || !Enum.IsDefined(role) || roles[0] != role.ToString()
            || versions.Length != 1 || !long.TryParse(versions[0], NumberStyles.None, CultureInfo.InvariantCulture, out var version))
            return false;
        user = new(id, role, version);
        return true;
    }
}
