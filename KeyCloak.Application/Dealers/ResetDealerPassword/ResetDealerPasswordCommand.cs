using KeyCloak.Application.Messaging;

namespace KeyCloak.Application.Dealers.ResetDealerPassword;

public sealed record ResetDealerPasswordCommand(
    string DealerId,
    string NewPassword
) : ICommand<string>;
