using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KeyCloak.Application.Abstractions.Identity;

public sealed record LoginModel(string Username, string Password);

