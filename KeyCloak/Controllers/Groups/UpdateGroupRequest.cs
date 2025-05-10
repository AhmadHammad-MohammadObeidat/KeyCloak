namespace KeyCloak.Api.Controllers.Groups;

public sealed record UpdateGroupRequest(Guid GroupId, string GroupName);
