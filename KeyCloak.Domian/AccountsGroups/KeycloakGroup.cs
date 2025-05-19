namespace KeyCloak.Domian.AccountsGroups;

public class KeycloakGroup
{
    public string Id { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string? Path { get; set; }  // optional: /demo/pending etc.
}
