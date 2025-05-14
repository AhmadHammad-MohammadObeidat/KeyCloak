using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace KeyCloak.Infrastructure.Helpers;

public static class KeycloakJsonHelpers
{
    public static bool TryGetString(Dictionary<string, object> dict, string key, out string result)
    {
        result = string.Empty;

        if (dict.TryGetValue(key, out var value))
        {
            switch (value)
            {
                case JsonElement el when el.ValueKind == JsonValueKind.String:
                    result = el.GetString() ?? string.Empty;
                    return true;
                case string str:
                    result = str;
                    return true;
            }
        }

        return false;
    }
}