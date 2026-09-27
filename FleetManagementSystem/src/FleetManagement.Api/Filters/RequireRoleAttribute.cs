using FleetManagement.Domain.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace FleetManagement.Api.Filters;

/// <summary>
/// Filtro de autorización basado en roles, coherente con el
/// ClaimsPrincipal construido por TokenAuthenticationMiddleware. Se aplica
/// como atributo sobre acciones o controladores: [RequireRole(UserRole.Admin)].
/// Se implementa como IAuthorizationFilter (parte del pipeline de MVC) en
/// lugar de depender del esquema completo de autenticación de ASP.NET Core,
/// manteniendo el prototipo simple y sin dependencias adicionales.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class RequireRoleAttribute : Attribute, IAuthorizationFilter
{
    private readonly UserRole _role;

    public RequireRoleAttribute(UserRole role)
    {
        _role = role;
    }

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var user = context.HttpContext.User;
        if (user.Identity is null || !user.Identity.IsAuthenticated)
        {
            context.Result = new UnauthorizedObjectResult(new { message = "Se requiere autenticación." });
            return;
        }

        if (!user.IsInRole(_role.ToString()))
        {
            context.Result = new ObjectResult(new { message = "No tiene permisos suficientes para esta acción." })
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
        }
    }
}
