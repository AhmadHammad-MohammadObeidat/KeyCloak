using KeyCloak.Domian.AccountsGroups;
using KeyCloak.Domian;

namespace KeyCloak.Application.Services.GroupsService;

public interface IGroupManagementService
{
    Task<Result<string>> CreateGroupAsync(GroupRepresentation group, CancellationToken cancellationToken = default);
    Task<Result<string>> UpdateGroupAsync(GroupRepresentation group, CancellationToken cancellationToken = default);
    Task<Result<string>> DeleteGroupAsync(Guid groupId, CancellationToken cancellationToken = default);
}
