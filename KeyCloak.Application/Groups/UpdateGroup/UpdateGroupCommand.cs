using KeyCloak.Application.Messaging;

namespace KeyCloak.Application.Groups.UpdateGroup;

public sealed record UpdateGroupCommand(Guid GroupId, string GroupName): ICommand<Guid>;
