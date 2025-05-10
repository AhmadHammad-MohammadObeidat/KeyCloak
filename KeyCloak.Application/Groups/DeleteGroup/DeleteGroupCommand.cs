using KeyCloak.Application.Messaging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KeyCloak.Application.Groups.DeleteGroup;

public sealed record DeleteGroupCommand(Guid GroupId) : ICommand<Guid>;
