using KeyCloak.Domian.Users;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KeyCloak.Application.Groups.GetGroupWithUsers;

public class GroupWithUsersDto
{
    public string GroupId { get; set; } = string.Empty;
    public string GroupName { get; set; } = string.Empty;
    public List<UserDto> Users { get; set; } = new();
}