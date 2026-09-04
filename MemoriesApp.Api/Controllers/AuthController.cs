using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using MemoriesApp.Api.DTOs;
using MemoriesApp.Api.Models;
using MemoriesApp.Api.Services;
using BCrypt.Net;
using MongoDB.Driver;
using System.Security.Claims;

namespace MemoriesApp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly MongoDbService _mongoDb;
    private readonly JwtService _jwtService;

    public AuthController(MongoDbService mongoDb, JwtService jwtService)
    {
        _mongoDb = mongoDb;
        _jwtService = jwtService;
    }

    /// <summary>
    /// Kiểm tra token còn hợp lệ không (dùng cho background validation ở frontend).
    /// Trả 401 tự động nếu token hết hạn hoặc không hợp lệ.
    /// </summary>
    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<UserDto>> GetMe()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? User.FindFirstValue("sub")
                  ?? User.FindFirstValue("id");

        if (userId == null)
            return Unauthorized(new { message = "Token không hợp lệ" });

        var filter = Builders<User>.Filter.Eq(u => u.Id, userId);
        var user = await _mongoDb.Users.Find(filter).FirstOrDefaultAsync();

        if (user == null)
            return Unauthorized(new { message = "Người dùng không tồn tại" });

        return Ok(new UserDto
        {
            Id       = user.Id,
            Username = user.Username,
            DisplayName = user.DisplayName,
            Avatar   = user.Avatar
        });
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponseDto>> Login([FromBody] LoginDto dto)
    {
        var filter = Builders<User>.Filter.Eq(u => u.Username, dto.Username);
        var user = await _mongoDb.Users.Find(filter).FirstOrDefaultAsync();
        
        if (user == null || !BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
        {
            return Unauthorized(new { message = "Tên đăng nhập hoặc mật khẩu không đúng" });
        }

        var token = _jwtService.GenerateToken(user);
        
        return Ok(new AuthResponseDto
        {
            Token = token,
            User = new UserDto
            {
                Id = user.Id,
                Username = user.Username,
                DisplayName = user.DisplayName,
                Avatar = user.Avatar
            }
        });
    }

    [HttpPost("register")]
    public async Task<ActionResult<AuthResponseDto>> Register([FromBody] RegisterDto dto)
    {
        var filter = Builders<User>.Filter.Eq(u => u.Username, dto.Username);
        var existingUser = await _mongoDb.Users.Find(filter).FirstOrDefaultAsync();
        if (existingUser != null)
        {
            return BadRequest(new { message = "Tên đăng nhập đã tồn tại" });
        }

        var user = new User
        {
            Username = dto.Username,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            DisplayName = dto.DisplayName,
            CreatedAt = DateTime.UtcNow
        };

        await _mongoDb.Users.InsertOneAsync(user);

        var token = _jwtService.GenerateToken(user);
        
        return Ok(new AuthResponseDto
        {
            Token = token,
            User = new UserDto
            {
                Id = user.Id,
                Username = user.Username,
                DisplayName = user.DisplayName,
                Avatar = user.Avatar
            }
        });
    }
}

