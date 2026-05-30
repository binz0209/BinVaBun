using Microsoft.AspNetCore.Mvc;
using MemoriesApp.Api.DTOs;
using MemoriesApp.Api.Models;
using MemoriesApp.Api.Services;
using BCrypt.Net;
using MongoDB.Driver;

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

