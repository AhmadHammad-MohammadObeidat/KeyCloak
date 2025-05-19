namespace KeyCloak.Api.Controllers.Dealers;

public sealed record UpdateDealerRequest(
    string Username,
    string FirstName,
    string LastName);
