using KeyCloak.Application.Messaging;

namespace KeyCloak.Application.Dealers.MoveDealerGroup;

public sealed record MoveDealerGroupCommand(
    string DealerId,
    string NewGroupId
) : ICommand<string>;

