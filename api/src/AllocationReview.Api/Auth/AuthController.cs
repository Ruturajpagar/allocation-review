using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace AllocationReview.Api.Auth;

public sealed class LoginRequest
{
    [Required, EmailAddress, MaxLength(256)]
    public string Email { get; init; } = string.Empty;

    [Required, MaxLength(256)]
    public string Password { get; init; } = string.Empty;
}

public sealed record CurrentUserResponse(string Email, IReadOnlyList<string> Roles, bool CanReviewRuns);

/// <summary>
/// Sign-in with ASP.NET Core Identity and an HttpOnly session cookie.
/// The browser never sees a token, so script injection cannot steal the session.
/// </summary>
[ApiController]
[Route("api/auth")]
public sealed class AuthController(
    SignInManager<IdentityUser> signInManager,
    UserManager<IdentityUser> userManager,
    ILogger<AuthController> logger) : ControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType<CurrentUserResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is null)
        {
            // Do not log the email address: it is personal data and helps account enumeration.
            logger.LogWarning("Sign-in failed: unknown account");
            return InvalidCredentials();
        }

        var result = await signInManager.PasswordSignInAsync(user, request.Password, isPersistent: false, lockoutOnFailure: true);
        if (result.IsLockedOut)
        {
            logger.LogWarning("Sign-in blocked: account {UserId} is locked out", user.Id);
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Account temporarily locked",
                detail: "Too many failed attempts. Try again in a few minutes.");
        }

        if (!result.Succeeded)
        {
            logger.LogWarning("Sign-in failed for account {UserId}", user.Id);
            return InvalidCredentials();
        }

        logger.LogInformation("Account {UserId} signed in", user.Id);
        var roles = await userManager.GetRolesAsync(user);
        return Ok(ToResponse(user.Email!, roles));
    }

    [HttpPost("logout")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            logger.LogInformation("Account {UserId} signed out", User.FindFirstValue(ClaimTypes.NameIdentifier));
        }

        await signInManager.SignOutAsync();
        return NoContent();
    }

    /// <summary>Returns the signed-in user. The UI calls this on start-up to restore the session.</summary>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType<CurrentUserResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public IActionResult Me()
    {
        var email = User.FindFirstValue(ClaimTypes.Email) ?? User.Identity?.Name ?? string.Empty;
        var roles = User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();
        return Ok(ToResponse(email, roles));
    }

    private static CurrentUserResponse ToResponse(string email, IEnumerable<string> roles)
    {
        var roleList = roles.Order(StringComparer.Ordinal).ToList();
        return new CurrentUserResponse(email, roleList, roleList.Contains(Roles.Admin));
    }

    private ObjectResult InvalidCredentials() =>
        Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Invalid email or password");
}
