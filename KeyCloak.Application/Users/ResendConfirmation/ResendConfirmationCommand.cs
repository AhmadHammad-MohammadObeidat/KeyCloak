using KeyCloak.Application.Messaging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KeyCloak.Application.Users.ResendConfirmation;

public sealed record ResendConfirmationCommand(string Email) : ICommand<bool>;

