using class_schedule.Application.DTOs.Account;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace class_schedule.Web.Controllers;

[ApiController]
[Route("api/account")]
public class AccountController(UserManager<IdentityUser> users, SignInManager<IdentityUser> signIn, IAntiforgery antiforgery) : ControllerBase
{
    [HttpGet("csrf")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public IActionResult Csrf() => Ok(new { requestToken = antiforgery.GetAndStoreTokens(HttpContext).RequestToken });

    [HttpPost("register")]
    public async Task<IActionResult> Register(Credentials credentials)
    {
        var user = new IdentityUser { UserName = credentials.Email, Email = credentials.Email };
        var result = await users.CreateAsync(user, credentials.Password);
        return result.Succeeded ? StatusCode(201) : BadRequest(new { error = "Registration failed." });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(Credentials credentials)
    {
        var result = await signIn.PasswordSignInAsync(credentials.Email, credentials.Password, false, lockoutOnFailure: true);
        return result.Succeeded ? Ok(new { message = "Signed in." }) : Unauthorized(new { error = "Invalid credentials or account unavailable." });
    }

    [Authorize, HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await signIn.SignOutAsync();
        return NoContent();
    }

    [Authorize, HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        var user = await users.GetUserAsync(User);
        return user is null ? Unauthorized() : Ok(new { email = user.Email });
    }
}
