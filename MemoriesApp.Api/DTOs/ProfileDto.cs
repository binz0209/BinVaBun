using Microsoft.AspNetCore.Http;

namespace MemoriesApp.Api.DTOs;

public class ProfileDto
{
    public string Id { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Avatar { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ChangePasswordDto
{
    public string OldPassword { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
}

public class UpdateProfileDto
{
    public string DisplayName { get; set; } = string.Empty;
    public IFormFile? Avatar { get; set; }
}

