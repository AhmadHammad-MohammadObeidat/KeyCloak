using KeyCloak.Application.Messaging;

namespace KeyCloak.Application.Dealers.UpdateDealer;

public sealed record UpdateDealerCommand(
    string DealerId,
    string Username,
    string FirstName,
    string LastName
) : ICommand<string>;
