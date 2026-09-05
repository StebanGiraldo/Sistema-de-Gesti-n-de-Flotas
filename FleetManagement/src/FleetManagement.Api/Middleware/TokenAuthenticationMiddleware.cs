using System.Security.Claims;
using FleetManagement.Application.Interfaces;

namespace FleetManagement.Api.Middleware;

/// <summary>
/// Middleware ligero de autenticación basada en token de sesión en memoria
/// (alternativa simplificada a JWT para este prototipo, evitando
/// dependencias externas adicionales; ver README para cómo migrar a JWT
/// real en producción). Lee el header "Authorization: Bearer {token}",
/// valida la sesión contra ISessionTokenStore y, si es válida, construye un
/// ClaimsPrincipal para que HttpContext.User quede disponible en los
/// controladores y en RequireRoleAttribute.
/// </summary>
public class TokenAuthenticationMiddleware
{
    private readonly RequestDelegate _next;

    public TokenAuthenticationMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ISessionTokenStore sessionStore)
    {
        var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
        if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            var token = authHeader["Bearer ".Length..].Trim();
            var session = sessionStore.GetSession(token);
            if (session is not null)
            {
                var claims = new List<Claim>
                {
                    new(ClaimTypes.NameIdentifier, session.UserId.ToString()),
                    new(ClaimTypes.Name, session.Username),
                    new(ClaimTypes.Role, session.Role.ToString())
                };
                var identity = new ClaimsIdentity(claims, "FleetToken");
                context.User = new ClaimsPrincipal(identity);
            }
        }

        await _next(context);
    }
}
