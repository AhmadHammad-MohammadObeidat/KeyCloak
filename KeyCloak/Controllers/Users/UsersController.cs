using KeyCloak.Api.Controllers.Users;
using KeyCloak.Application.Abstractions.Identity;
using KeyCloak.Application.Users.ForgotPassword;
using KeyCloak.Application.Users.GetUsersByGroup;
using KeyCloak.Application.Users.LoginUser;
using KeyCloak.Application.Users.RefreshToken;
using KeyCloak.Application.Users.RegisterAdminUser;
using KeyCloak.Application.Users.RegisterUser;
using KeyCloak.Application.Users.ResendConfirmation;
using KeyCloak.Domian;
using KeyCloak.Domian.Users;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace TraderVolt.ApiService.Controllers.Users;

[ApiController]
[Authorize]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/users")]
public class UsersController(ISender sender) : ControllerBase
{
    // ========== Public Access Endpoints ==========

    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(Guid))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RegisterUser(RegisterUserRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var command = new RegisterUserCommand(
            request.Username,
            request.Email,
            request.FirstName,
            request.LastName,
            request.Password,
            request.InvestorPassword,
            request.GroupName); // used only for public signup

        var result = await sender.Send(command, cancellationToken);
        return result.IsFailure ? BadRequest(result.Error) : Ok(result.Value);
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(TokenResponse))]
    public async Task<IActionResult> UserLogin(UserLoginRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var command = new UserLoginCommand(request.Username, request.Password);
        var result = await sender.Send(command, cancellationToken);

        return result.IsFailure ? BadRequest(result.Error) : Ok(result.Value);
    }

    [HttpPost("refresh_token")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(TokenResponse))]
    public async Task<IActionResult> RefreshToken(RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var command = new RefreshTokenCommand(request.RefreshToken);
        var result = await sender.Send(command, cancellationToken);

        return result.IsFailure ? BadRequest(result.Error) : Ok(result.Value);
    }

    [HttpPost("forgot_password")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(bool))]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var command = new ForgotPasswordCommand(request.Email);
        var result = await sender.Send(command, cancellationToken);

        return result.IsFailure ? BadRequest(result.Error) : Ok(result.Value);
    }

    [HttpPost("resend_confirmation")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(bool))]
    public async Task<IActionResult> ResendConfirmation(ResendConfirmationRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var command = new ResendConfirmationCommand(request.Email);
        var result = await sender.Send(command, cancellationToken);

        return result.IsFailure ? BadRequest(result.Error) : Ok(result.Value);
    }

    // ========== Admin Actions ==========

    [HttpPost("register-admin")]
    [Authorize] // enforce custom logic
    [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(string))]
    public async Task<IActionResult> RegisterAdminUser(RegisterAdminUserRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        // Validate role manually for group-based admin creation
        var requiredRole = $"admin-{request.GroupName}-create";
        if (!User.IsInRole(requiredRole))
        {
            return Forbid($"You do not have permission to create {request.GroupName} admins.");
        }

        var command = new RegisterAdminUserCommand(
            request.Username,
            request.Email,
            request.FirstName,
            request.LastName,
            request.Password,
            request.InvestorPassword,
            request.GroupName);

        var result = await sender.Send(command, cancellationToken);
        return result.IsFailure ? BadRequest(result.Error) : CreatedAtAction(nameof(RegisterAdminUser), new { id = result.Value }, result.Value);
    }

    [HttpGet("group-users")]
    [Authorize(Policy = "RequireAdminRole")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(List<UserDto>))]
    public async Task<IActionResult> GetUsersByGroup(CancellationToken cancellationToken)
    {
        var query = new GetUsersByGroupQuery(User);
        var result = (Result<List<UserDto>>)await sender.Send(query, cancellationToken);

        return result.IsFailure ? StatusCode(StatusCodes.Status500InternalServerError, result.Error) : Ok(result.Value);
    }

    // ========== Restricted Group-Based Registration ==========

    [HttpPost("register/demo")]
    [Authorize(Policy = "CreateDemoUser")]
    public async Task<IActionResult> RegisterDemoUser(RegisterUserRequest request, CancellationToken cancellationToken)
    {
        var command = new RegisterUserCommand(
            request.Username,
            request.Email,
            request.FirstName,
            request.LastName,
            request.Password,
            request.InvestorPassword,
            "demo"); // ✅ forcefully set

        var result = await sender.Send(command, cancellationToken);
        return result.IsFailure ? BadRequest(result.Error) : Ok(result.Value);
    }

    [HttpPost("register/real")]
    [Authorize(Policy = "CreateRealUser")]
    public async Task<IActionResult> RegisterRealUser(RegisterUserRequest request, CancellationToken cancellationToken)
    {
        var command = new RegisterUserCommand(
            request.Username,
            request.Email,
            request.FirstName,
            request.LastName,
            request.Password,
            request.InvestorPassword,
            "real");

        var result = await sender.Send(command, cancellationToken);
        return result.IsFailure ? BadRequest(result.Error) : Ok(result.Value);
    }

    [HttpPost("register/pending")]
    [Authorize(Policy = "CreatePendingUser")]
    public async Task<IActionResult> RegisterPendingUser(RegisterUserRequest request, CancellationToken cancellationToken)
    {
        var command = new RegisterUserCommand(
            request.Username,
            request.Email,
            request.FirstName,
            request.LastName,
            request.Password,
            request.InvestorPassword,
            "pending");

        var result = await sender.Send(command, cancellationToken);
        return result.IsFailure ? BadRequest(result.Error) : Ok(result.Value);
    }

}
