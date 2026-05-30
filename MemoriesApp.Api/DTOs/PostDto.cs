using Microsoft.AspNetCore.Http;

namespace MemoriesApp.Api.DTOs;

public class CreatePostDto
{
    public string Content { get; set; } = string.Empty;
    public List<IFormFile>? Images { get; set; }
}

public class UpdatePostDto
{
    public string Content { get; set; } = string.Empty;
    public List<IFormFile>? NewImages { get; set; }
    public List<string>? KeepImages { get; set; } // URLs của ảnh giữ lại
}

public class PostDto
{
    public string Id { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public List<string> Images { get; set; } = new();
    public string AuthorId { get; set; } = string.Empty;
    public string AuthorName { get; set; } = string.Empty;
    public string? AuthorAvatar { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public int LikesCount { get; set; }
    public bool IsLiked { get; set; }
    public bool IsOwner { get; set; } // Thêm field để biết có phải chủ bài viết không
    public List<CommentDto> Comments { get; set; } = new();
}

public class CommentDto
{
    public string Id { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string AuthorId { get; set; } = string.Empty;
    public string AuthorName { get; set; } = string.Empty;
    public string? AuthorAvatar { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateCommentDto
{
    public string Content { get; set; } = string.Empty;
}

