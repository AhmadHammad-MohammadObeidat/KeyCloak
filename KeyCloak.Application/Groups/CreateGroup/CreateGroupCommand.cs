using KeyCloak.Application.Messaging;

namespace KeyCloak.Application.Groups.CreateGroup;

public sealed record CreateGroupCommand(string GroupName) : ICommand<Guid>;
