using KeyCloak.Application.Messaging;

namespace KeyCloak.Application.Dealers.DeleteDealer;

public sealed record DeleteDealerCommand(
    string DealerId
) : ICommand<string>;
