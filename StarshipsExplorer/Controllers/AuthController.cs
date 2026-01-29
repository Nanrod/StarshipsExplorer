using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using StarshipsExplorer.App.Auth;

namespace StarshipsExplorer.App.Controllers;

[ApiExplorerSettings(IgnoreApi = true)]
[Route("auth")]
public sealed class AuthController : Controller
{
    private readonly IOptions<AuthOptions> _options;

    public AuthController(IOptions<AuthOptions> options)
    {
        _options = options;
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromForm] string? username, [FromForm] string? password, [FromForm] string? returnUrl)
    {
        if (!_options.Value.IsValid(username, password))
        {
            var safeReturnUrl = string.IsNullOrWhiteSpace(returnUrl) ? "/starships" : returnUrl;
            return Redirect($"/login?error=1&returnUrl={Uri.EscapeDataString(safeReturnUrl)}");
        }

        var claims = new[]
        {
            new Claim(ClaimTypes.Name, username!),
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

        if (string.IsNullOrWhiteSpace(returnUrl) || !Uri.IsWellFormedUriString(returnUrl, UriKind.Relative))
        {
            return Redirect("/starships");
        }

        return Redirect(returnUrl);
    }

    [HttpGet("logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Redirect("/login");
    }
}

