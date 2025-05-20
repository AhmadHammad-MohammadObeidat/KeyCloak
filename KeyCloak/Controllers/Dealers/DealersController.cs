using KeyCloak.Application.Dealers.DeleteDealer;
using KeyCloak.Application.Dealers.GetDealers;
using KeyCloak.Application.Dealers.MoveDealerGroup;
using KeyCloak.Application.Dealers.ResetDealerPassword;
using KeyCloak.Application.Dealers.UpdateDealer;
using KeyCloak.Domian.Dealers;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KeyCloak.Api.Controllers.Dealers;



[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/dealers")]
public class DealersController(ISender sender) : ControllerBase
{
    [HttpGet("dealers")]
    [Authorize(Policy = "view-dealer-management")] // Add this policy
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(List<DealerDto>))] // Fixed the return type
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)] // Added forbidden response
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetDealers(CancellationToken cancellationToken)
    {
        var command = new GetDealersQueryCommand(HttpContext.User);
        var result = await sender.Send(command, cancellationToken);
        return Ok(result);
    }

    [HttpPut("update/{adminId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UpdateAdmin(string adminId, [FromBody] UpdateDealerRequest request, CancellationToken cancellationToken)
    {
        var user = HttpContext.User;
        var roles = user.Claims.Where(c => c.Type == "roles").Select(c => c.Value).ToList();

        var groups = user.Claims.Where(c => c.Type == "groups").Select(c => c.Value).ToList();


        var command = new UpdateDealerCommand(adminId, request.Username, request.FirstName, request.LastName);
        var result = await sender.Send(command, cancellationToken);
        return result.IsFailure ? BadRequest(result.Error) : Ok(result.Value);
    }

    [HttpDelete("delete")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DeleteAdmin([FromQuery] string adminId, CancellationToken cancellationToken)
    {
        var command = new DeleteDealerCommand(adminId);
        var result = await sender.Send(command, cancellationToken);
        return result.IsFailure ? BadRequest(result.Error) : Ok(result.Value);
    }

    [HttpPost("reset-password/{adminId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ResetPassword(string adminId, [FromBody] ResetDealerPasswordRequest request, CancellationToken cancellationToken)
    {
        var command = new ResetDealerPasswordCommand(adminId, request.NewPassword);
        var result = await sender.Send(command, cancellationToken);
        return result.IsFailure ? BadRequest(result.Error) : Ok(result.Value);
    }

    [HttpPost("move-group/{adminId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> MoveAdminGroup(string adminId, [FromBody] MoveDealerGroupRequest request, CancellationToken cancellationToken)
    {
        var command = new MoveDealerGroupCommand(adminId, request.NewGroupId);
        var result = await sender.Send(command, cancellationToken);
        return result.IsFailure ? BadRequest(result.Error) : Ok(result.Value);
    }
}
