namespace KeyCloak.Infrastructure.Identity;

public sealed record CredentialRepresentation(string Type, string Value, bool Temporary);

