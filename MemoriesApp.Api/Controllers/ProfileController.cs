using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MemoriesApp.Api.DTOs;
using MemoriesApp.Api.Models;
using MemoriesApp.Api.Services;
using System.Security.Claims;
using MongoDB.Driver;
using BCrypt.Net;

namespace MemoriesApp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ProfileController : ControllerBase
{
    private readonly MongoDbService _mongoDb;
    private readonly CloudinaryService _cloudinaryService;

    public ProfileController(MongoDbService mongoDb, CloudinaryService cloudinaryService)
    {
        _mongoDb = mongoDb;
        _cloudinaryService = cloudinaryService;
    }

    private string GetUserId()
    {
        return User.FindFirst(ClaimTypes.NameIdentifier)!.Value;
    }

    [HttpGet]
    public async Task<ActionResult<ProfileDto>> GetProfile()
    {
        var userId = GetUserId();
        var filter = Builders<User>.Filter.Eq(u => u.Id, userId);
        var user = await _mongoDb.Users.Find(filter).FirstOrDefaultAsync();

        if (user == null)
        {
            return NotFound();
        }

        return Ok(new ProfileDto
        {
            Id = user.Id,
            Username = user.Username,
            DisplayName = user.DisplayName,
            Avatar = user.Avatar,
            CreatedAt = user.CreatedAt
        });
    }

    [HttpPut]
    public async Task<ActionResult<ProfileDto>> UpdateProfile([FromForm] UpdateProfileDto dto)
    {
        try
        {
            var userId = GetUserId();
            var filter = Builders<User>.Filter.Eq(u => u.Id, userId);
            var user = await _mongoDb.Users.Find(filter).FirstOrDefaultAsync();

            if (user == null)
            {
                return NotFound();
            }

            var update = Builders<User>.Update.Set(u => u.DisplayName, dto.DisplayName);

            // Upload avatar nếu có
            if (dto.Avatar != null && dto.Avatar.Length > 0)
            {
                try
                {
                    var avatarUrl = await _cloudinaryService.UploadImageAsync(dto.Avatar);
                    update = update.Set(u => u.Avatar, avatarUrl);
                }
                catch (Exception ex)
                {
                    return StatusCode(500, new { error = "Lỗi khi upload avatar: " + ex.Message });
                }
            }

            await _mongoDb.Users.UpdateOneAsync(filter, update);

            // Get updated user
            var updatedUser = await _mongoDb.Users.Find(filter).FirstOrDefaultAsync();

            return Ok(new ProfileDto
            {
                Id = updatedUser!.Id,
                Username = updatedUser.Username,
                DisplayName = updatedUser.DisplayName,
                Avatar = updatedUser.Avatar,
                CreatedAt = updatedUser.CreatedAt
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Lỗi khi cập nhật profile: " + ex.Message });
        }
    }

    [HttpPost("change-password")]
    public async Task<ActionResult> ChangePassword([FromBody] ChangePasswordDto dto)
    {
        try
        {
            var userId = GetUserId();
            var filter = Builders<User>.Filter.Eq(u => u.Id, userId);
            var user = await _mongoDb.Users.Find(filter).FirstOrDefaultAsync();

            if (user == null)
            {
                return NotFound();
            }

            // Verify old password
            if (!BCrypt.Net.BCrypt.Verify(dto.OldPassword, user.PasswordHash))
            {
                return BadRequest(new { message = "Mật khẩu cũ không đúng" });
            }

            // Validate new password
            if (string.IsNullOrWhiteSpace(dto.NewPassword) || dto.NewPassword.Length < 6)
            {
                return BadRequest(new { message = "Mật khẩu mới phải có ít nhất 6 ký tự" });
            }

            // Update password
            var newPasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);
            var update = Builders<User>.Update.Set(u => u.PasswordHash, newPasswordHash);
            await _mongoDb.Users.UpdateOneAsync(filter, update);

            return Ok(new { message = "Đổi mật khẩu thành công" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Lỗi khi đổi mật khẩu: " + ex.Message });
        }
    }
}

