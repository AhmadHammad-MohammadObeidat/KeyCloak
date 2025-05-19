using Microsoft.AspNetCore.Authorization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KeyCloak.Application.Permissions;

public class KeycloakPermissionRequirement : IAuthorizationRequirement
{
    public string Resource { get; }
    public string Scope { get; }

    public KeycloakPermissionRequirement(string resource, string scope)
    {
        Resource = resource;
        Scope = scope;
    }
}
