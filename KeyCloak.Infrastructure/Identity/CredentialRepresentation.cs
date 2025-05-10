using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KeyCloak.Infrastructure.Identity;

public sealed record CredentialRepresentation(string Type, string Value, bool Temporary);

