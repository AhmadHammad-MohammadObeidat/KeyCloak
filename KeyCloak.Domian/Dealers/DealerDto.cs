namespace KeyCloak.Domian.Dealers;

public class DealerDto
{
    public string DealerId { get; set; } = default!;
    public string DealerName { get; set; } = default!;
    public string GroupId { get; set; } = default!;
    public string GroupName { get; set; } = default!;
    public string? SubGroupId { get; set; }
    public string? SubGroupName { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Email { get; set; }
    public List<string> Roles { get; set; } = new();
}