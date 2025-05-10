using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KeyCloak.Domian.Users;

public class User : Entity
{
    private User() { }

    //private readonly List<Role> _roles = [];

    public string Username { get; private set; }
    public string Email { get; private set; }
    public string FirstName { get; private set; }
    public string LastName { get; private set; }
    public string? InvestorPassword { get; private set; }
    public string? ApiKey { get; private set; }

    //public IReadOnlyCollection<Role> Roles => _roles.ToList();

    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    // Property to store XMIN --> MVCC (Multi-Version Concurrency Control)
    public uint RowVersion { get; private set; }

    public static User Create(string username, string email, string firstName,
        string lastName, string? investorPassword, Guid identityId, string apiKey = null)
    {

        var User = new User
        {
            Id = identityId,
            Username = username,
            Email = email,
            FirstName = firstName,
            LastName = lastName,
            InvestorPassword = investorPassword,
            ApiKey = apiKey
        };
        return User;
    }
    public static User FromDto(UserDto dto)
    {
        return new User
        {
            Id = Guid.TryParse(dto.Id, out var guid) ? guid : Guid.Empty,
            Username = dto.Username,
            Email = dto.Email,
            FirstName = string.Empty, // or map from Keycloak if available
            LastName = string.Empty,
            InvestorPassword = null,
            ApiKey = null,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }
}

