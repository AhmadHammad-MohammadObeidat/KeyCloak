using KeyCloak.Api.Controllers.Users;
using KeyCloak.Application.Groups;
using KeyCloak.Application.Groups.CreateGroup;
using KeyCloak.Application.Groups.DeleteGroup;
using KeyCloak.Application.Groups.GetAllGroups;
using KeyCloak.Application.Groups.GetGroup;
using KeyCloak.Application.Groups.GetGroupWithUsers;
using KeyCloak.Application.Groups.UpdateGroup;
using KeyCloak.Application.Users.RegisterUser;
using KeyCloak.Domian;
using KeyCloak.Infrastructure.Identity;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KeyCloak.Api.Controllers.Groups;

[ApiController]
[Authorize]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/groups")]
public class GroupsController(ISender sender) : ControllerBase
{
    [HttpPost("create_group")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(Guid))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> CreateGroup(CreateGroupRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var command = new CreateGroupCommand(request.GroupName);

        var result = await sender.Send(command, cancellationToken);
        return result.IsFailure ? BadRequest(result.Error) : Ok(result.Value);
    }

    [HttpPut("update_group")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(Guid))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UpdateGroup(UpdateGroupRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var command = new UpdateGroupCommand(request.GroupId, request.GroupName);

        var result = await sender.Send(command, cancellationToken);
        return result.IsFailure ? BadRequest(result.Error) : Ok(result.Value);
    }

    [HttpDelete("delete_group/{groupId}")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(Guid))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DeleteGroup([FromRoute]Guid groupId, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var command = new DeleteGroupCommand(groupId);

        var result = await sender.Send(command, cancellationToken);
        return result.IsFailure ? BadRequest(result.Error) : Ok(result.Value);
    }

    [HttpGet("get-all-groups")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(List<Dictionary<string, object>>))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetAllGroups(CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var query = new GetAllGroupsQuery();
        var result = await sender.Send(query, cancellationToken);

        return result.IsFailure ? BadRequest(result.Error) : Ok(result.Value);
    }

    [HttpGet("get-groups")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(List<Dictionary<string, object>>))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetFilteredGroups(CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetFilteredGroupsQuery(User), cancellationToken);
        return Ok(result);
    }

    [HttpGet("get-groups-with-users")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(List<Dictionary<string, object>>))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetGroupsWithUsers(CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetGroupsWithUsersQuery(User), cancellationToken);
        return Ok(result);
    }
}
