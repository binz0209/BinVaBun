using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using MemoriesApp.Api.Services;
using System.Text;
using System.Text.Json.Serialization;
using MongoDB.Driver;
using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Memories App API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// Configure CORS
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReactApp", policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// Configure JWT Authentication
var jwtKey = builder.Configuration["Jwt:Key"]
    ?? Environment.GetEnvironmentVariable("JWT_SECRET_KEY")
    ?? throw new InvalidOperationException("JWT key is not configured");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"],
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
        
        // Configure SignalR JWT
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var path = context.HttpContext.Request.Path;
                if (path.Value?.Contains("/memoriesHub") == true)
                {
                    var accessToken = context.Request.Query["access_token"];
                    if (!string.IsNullOrEmpty(accessToken))
                    {
                        context.Token = accessToken;
                    }
                    else if (context.Request.Headers.ContainsKey("Authorization"))
                    {
                        var authHeader = context.Request.Headers["Authorization"].ToString();
                        if (authHeader.StartsWith("Bearer "))
                        {
                            context.Token = authHeader.Substring(7);
                        }
                    }
                }
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

// Add SignalR
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
});

// Register services
builder.Services.AddSingleton<MongoDbService>();
builder.Services.AddSingleton<CloudinaryService>();
builder.Services.AddScoped<JwtService>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<SignalRService>();
builder.Services.AddScoped<CalendarService>();

// Add health checks
builder.Services.AddHealthChecks();

// Configure request size limits for file uploads
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 10 * 1024 * 1024; // 10MB
});

// Configure Rate Limiting
builder.Services.AddRateLimiter(options =>
{
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.User.Identity?.Name ?? httpContext.Request.Headers.Host.ToString(),
            factory: partition => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 100,
                QueueLimit = 0,
                Window = TimeSpan.FromMinutes(1)
            }));
    
    options.RejectionStatusCode = 429;
});

var app = builder.Build();

// Global exception handling middleware
app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (Exception ex)
    {
        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Unhandled exception occurred");
        
        context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
        context.Response.ContentType = "application/json";
        
        var response = new { error = "Đã xảy ra lỗi hệ thống. Vui lòng thử lại sau." };
        await context.Response.WriteAsJsonAsync(response);
    }
});

// Swagger only in Development
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Memories App API v1");
        c.RoutePrefix = "swagger";
    });
}

app.UseCors("AllowReactApp");

app.UseRateLimiter();

// Authentication BEFORE mapping endpoints (fixed ordering)
app.UseAuthentication();
app.UseAuthorization();

// Map SignalR hub AFTER authentication
app.MapHub<MemoriesApp.Api.Hubs.MemoriesHub>("/memoriesHub");

app.MapControllers();

// Health check endpoint
app.MapHealthChecks("/health");

// Seed data and create indexes
using (var scope = app.Services.CreateScope())
{
    var mongoDb = scope.ServiceProvider.GetRequiredService<MongoDbService>();
    await SeedDataAsync(mongoDb);
    await CreateIndexesAsync(mongoDb);
}

app.Run();

async Task SeedDataAsync(MongoDbService mongoDb)
{
    var filter = Builders<MemoriesApp.Api.Models.User>.Filter.Empty;
    var existingUsers = await mongoDb.Users.Find(filter).ToListAsync();
    if (existingUsers.Count == 0)
    {
        var users = new[]
        {
            new MemoriesApp.Api.Models.User
            {
                Username = "Hlinh",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("123@abc"),
                DisplayName = "Hlinh",
                CreatedAt = DateTime.UtcNow
            },
            new MemoriesApp.Api.Models.User
            {
                Username = "Pthao",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("123@abc"),
                DisplayName = "Pthao",
                CreatedAt = DateTime.UtcNow
            }
        };

        await mongoDb.Users.InsertManyAsync(users);
    }
}

async Task CreateIndexesAsync(MongoDbService mongoDb)
{
    // Posts indexes
    var postIndexes = new List<CreateIndexModel<MemoriesApp.Api.Models.MemoryPost>>
    {
        new(Builders<MemoriesApp.Api.Models.MemoryPost>.IndexKeys.Descending(p => p.CreatedAt)),
        new(Builders<MemoriesApp.Api.Models.MemoryPost>.IndexKeys.Ascending(p => p.AuthorId))
    };
    await mongoDb.Posts.Indexes.CreateManyAsync(postIndexes);

    // Notifications indexes
    var notificationIndexes = new List<CreateIndexModel<MemoriesApp.Api.Models.Notification>>
    {
        new(Builders<MemoriesApp.Api.Models.Notification>.IndexKeys
            .Ascending(n => n.UserId)
            .Descending(n => n.CreatedAt)),
        new(Builders<MemoriesApp.Api.Models.Notification>.IndexKeys
            .Ascending(n => n.UserId)
            .Ascending(n => n.IsRead))
    };
    await mongoDb.Notifications.Indexes.CreateManyAsync(notificationIndexes);

    var userIndexes = new List<CreateIndexModel<MemoriesApp.Api.Models.User>>
    {
        new(Builders<MemoriesApp.Api.Models.User>.IndexKeys.Ascending(u => u.Username),
            new CreateIndexOptions { Unique = true })
    };
    await mongoDb.Users.Indexes.CreateManyAsync(userIndexes);

    // MarkedDates indexes
    var markedDateIndexes = new List<CreateIndexModel<MemoriesApp.Api.Models.MarkedDate>>
    {
        new(Builders<MemoriesApp.Api.Models.MarkedDate>.IndexKeys.Ascending(m => m.Date))
    };
    await mongoDb.MarkedDates.Indexes.CreateManyAsync(markedDateIndexes);
}
