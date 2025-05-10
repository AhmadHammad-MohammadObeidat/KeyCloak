using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KeyCloak.Infrastructure.Identity;

public sealed class KeyCloakOptions
{
    public string AdminUrl { get; set; }

    public string TokenUrl { get; set; }

    public string ConfidentialClientId { get; set; }

    public string ConfidentialClientSecret { get; set; }

    public string PublicClientId { get; set; }

    // For admin
    public string ClientId { get; set; }
    public string ClientSecret { get; set; }
    public string AdminUsername { get; set; }
    public string AdminPassword { get; set; }
}
