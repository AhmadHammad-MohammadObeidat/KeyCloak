namespace KeyCloak.Domian.Dealers;

public class GroupWithAdminsDto
{
    public string GroupId { get; set; } = default!;
    public string GroupName { get; set; } = default!;
    public List<DealerDto> Dealers { get; set; } = new();
}
