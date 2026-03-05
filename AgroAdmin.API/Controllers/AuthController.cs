using AgroAdmin.Infrastructure.Abstractions;
using AgroAdmin.Shared.Dto.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace AgroAdmin.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IAuthService authService, ILogger<AuthController> logger)
    {
        _authService = authService;
        _logger = logger;
    }

    [HttpPost("login")]
    public async Task<ActionResult<LoginResponseDto>> Login([FromBody] LoginRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new LoginResponseDto
            {
                Success = false,
                Message = "Username and password are required"
            });
        }

        var success = await _authService.LoginAsync(request.Username, request.Password);

        if (success)
        {
            return Ok(new LoginResponseDto
            {
                Success = true,
                Message = "Login successful",
                Username = request.Username
            });
        }

        return StatusCode(401, new LoginResponseDto
        {
            Success = false,
            Message = "Invalid username or password"
        });
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await  _authService.LogoutAsync();
        return Ok(new { message = "Logged out successfully" });
    }

    [HttpGet("status")]
    public ActionResult<AuthStatusDto> GetStatus()
    {
        var isAuth = _authService.IsAuthenticated();
        var username = _authService.GetCurrentUser();

        _logger.LogInformation("Status check - IsAuthenticated: {IsAuth}, Username: {Username}", isAuth, username);

        return Ok(new AuthStatusDto
        {
            IsAuthenticated = isAuth,
            Username = username
        });
    }
}