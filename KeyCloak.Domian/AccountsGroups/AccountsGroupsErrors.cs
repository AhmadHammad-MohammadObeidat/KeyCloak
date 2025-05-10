using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KeyCloak.Domian.AccountsGroups;

public static class AccountsGroupsErrors
{
    public static Error GroupNameIsNotUnique(string name) =>
            Error.NotFound("AccountsGroups.GroupNameIsNotUnique", $"The accounts group with the name {name} already exists");

    public static Error InvalidGroupIdentifier(string id) =>
            Error.NotFound("AccountsGroups.InvalidGroupIdentifier", $"Invalid returned group id {id} from the keycloak");


    public static Error NotFound(Guid accountsGroupId) =>
            Error.NotFound("AccountsGroups.NotFound", $"The accounts group with the identifier {accountsGroupId} was not found");

    public static Error GroupCreationFailed(string parentId) =>
           Error.NotFound("GroupCreationFailed.NotFound", $"The accounts group with the identifier {parentId} Group Creation Failed");

    public static Error GroupUpdateFailed(string parentId) =>
           Error.NotFound("GroupUpdateFailed.NotFound", $"The accounts group with the identifier {parentId} Group Update Failed");
}
