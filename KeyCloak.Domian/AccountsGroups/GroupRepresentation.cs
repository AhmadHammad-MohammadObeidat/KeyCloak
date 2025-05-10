using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace KeyCloak.Domian.AccountsGroups;

public sealed record GroupRepresentation
{
    public string Name { get; init; }
    public Guid? GroupId { get; init; }
    public Guid? ParentId { get; init; }

    public GroupRepresentation(string name, Guid? groupId = null, Guid? parentId = null)
    {
        GroupId = groupId ?? Guid.Empty;
        Name = name;
        ParentId = parentId ?? Guid.Empty;
    }
}
