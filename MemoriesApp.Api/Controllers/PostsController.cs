using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MemoriesApp.Api.DTOs;
using MemoriesApp.Api.Models;
using MemoriesApp.Api.Services;
using System.Security.Claims;
using MongoDB.Driver;
using MongoDB.Bson;
using Microsoft.AspNetCore.Http;
using System.Text;
using System.Globalization;
using System.Text.RegularExpressions;

namespace MemoriesApp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PostsController : ControllerBase
{
    private readonly MongoDbService _mongoDb;
    private readonly CloudinaryService _cloudinaryService;
    private readonly NotificationService _notificationService;
    private readonly SignalRService _signalRService;

    public PostsController(MongoDbService mongoDb, CloudinaryService cloudinaryService, NotificationService notificationService, SignalRService signalRService)
    {
        _mongoDb = mongoDb;
        _cloudinaryService = cloudinaryService;
        _notificationService = notificationService;
        _signalRService = signalRService;
    }

    private string GetUserId() => User.FindFirst(ClaimTypes.NameIdentifier)!.Value;

    [HttpGet]
    public async Task<ActionResult<object>> GetPosts(
        [FromQuery] int page = 1, 
        [FromQuery] int pageSize = 10,
        [FromQuery] string? content = null,
        [FromQuery] string? timeType = null,
        [FromQuery] string? beforeDate = null,
        [FromQuery] string? afterDate = null,
        [FromQuery] string? exactDate = null,
        [FromQuery] string? fromDate = null,
        [FromQuery] string? toDate = null)
    {
        var userId = GetUserId();
        var filterBuilder = Builders<MemoryPost>.Filter;
        var filter = filterBuilder.Empty;
        
        // Filter by content (không phân biệt hoa thường, có dấu/không dấu)
        if (!string.IsNullOrWhiteSpace(content))
        {
            // Normalize Vietnamese text để tìm kiếm không phân biệt dấu
            var normalizedContent = NormalizeVietnamese(content);
            // Escape special regex characters
            var escapedContent = Regex.Escape(normalizedContent);
            filter = filter & filterBuilder.Regex(
                p => p.Content,
                new MongoDB.Bson.BsonRegularExpression(escapedContent, "i"));
        }
        
        // Filter by time
        if (!string.IsNullOrWhiteSpace(timeType))
        {
            if (timeType == "before" && !string.IsNullOrWhiteSpace(beforeDate))
            {
                if (DateTime.TryParse(beforeDate, out var before))
                {
                    before = before.Date;
                    filter = filter & filterBuilder.Lt(p => p.CreatedAt, before);
                }
            }
            else if (timeType == "after" && !string.IsNullOrWhiteSpace(afterDate))
            {
                if (DateTime.TryParse(afterDate, out var after))
                {
                    var afterDateValue = after.Date.AddDays(1).AddTicks(-1);
                    filter = filter & filterBuilder.Gt(p => p.CreatedAt, afterDateValue);
                }
            }
            else if (timeType == "exact" && !string.IsNullOrWhiteSpace(exactDate))
            {
                if (DateTime.TryParse(exactDate, out var exact))
                {
                    var startOfDay = exact.Date;
                    var endOfDay = startOfDay.AddDays(1).AddTicks(-1);
                    filter = filter & filterBuilder.Gte(p => p.CreatedAt, startOfDay) 
                             & filterBuilder.Lte(p => p.CreatedAt, endOfDay);
                }
            }
            else if (timeType == "range" && !string.IsNullOrWhiteSpace(fromDate) && !string.IsNullOrWhiteSpace(toDate))
            {
                if (DateTime.TryParse(fromDate, out var from) && DateTime.TryParse(toDate, out var to))
                {
                    var startOfDay = from.Date;
                    var endOfDay = to.Date.AddDays(1).AddTicks(-1);
                    filter = filter & filterBuilder.Gte(p => p.CreatedAt, startOfDay) 
                             & filterBuilder.Lte(p => p.CreatedAt, endOfDay);
                }
            }
        }
        
        // Tính toán skip và limit cho pagination
        var skip = (page - 1) * pageSize;
        var limit = pageSize;
        
        // Lấy tổng số bài viết sau khi filter
        var totalCount = await _mongoDb.Posts.CountDocumentsAsync(filter);
        
        // Lấy bài viết với pagination
        var posts = await _mongoDb.Posts
            .Find(filter)
            .SortByDescending(p => p.CreatedAt)
            .Skip(skip)
            .Limit(limit)
            .ToListAsync();

        var allAuthorIds = posts.SelectMany(p => p.Comments)
            .Where(c => string.IsNullOrEmpty(c.AuthorName) || c.AuthorName == c.AuthorId)
            .Select(c => c.AuthorId)
            .Distinct()
            .ToList();

        var authorLookup = new Dictionary<string, User>();
        if (allAuthorIds.Any())
        {
            var authorFilter = Builders<User>.Filter.In(u => u.Id, allAuthorIds);
            var authors = await _mongoDb.Users.Find(authorFilter).ToListAsync();
            authorLookup = authors.ToDictionary(u => u.Id);
        }

        var postDtos = new List<PostDto>();
        foreach (var p in posts)
        {
            var comments = new List<CommentDto>();
            foreach (var c in p.Comments)
            {
                string commentAuthorName = c.AuthorName;
                string? commentAuthorAvatar = c.AuthorAvatar;
                
                if ((string.IsNullOrEmpty(commentAuthorName) || commentAuthorName == c.AuthorId) && authorLookup.TryGetValue(c.AuthorId, out var commentUser))
                {
                    commentAuthorName = commentUser.DisplayName ?? commentUser.Username;
                    commentAuthorAvatar = commentUser.Avatar;
                }
                
                comments.Add(new CommentDto
                {
                    Id = c.Id,
                    Content = c.Content,
                    AuthorId = c.AuthorId,
                    AuthorName = commentAuthorName,
                    AuthorAvatar = commentAuthorAvatar,
                    CreatedAt = c.CreatedAt
                });
            }
            
            postDtos.Add(new PostDto
            {
                Id = p.Id,
                Content = p.Content,
                Images = p.Images,
                AuthorId = p.AuthorId,
                AuthorName = p.AuthorName,
                AuthorAvatar = p.AuthorAvatar,
                CreatedAt = p.CreatedAt,
                UpdatedAt = p.UpdatedAt,
                LikesCount = p.Likes.Count,
                IsLiked = p.Likes.Contains(userId),
                IsOwner = p.AuthorId == userId,
                Comments = comments
            });
        }

        return Ok(new
        {
            posts = postDtos,
            totalCount,
            currentPage = page,
            pageSize,
            totalPages = (int)Math.Ceiling((double)totalCount / pageSize),
            hasMore = skip + limit < totalCount
        });
    }

    [HttpGet("{postId}")]
    public async Task<ActionResult<PostDto>> GetPost(string postId)
    {
        var userId = GetUserId();
        var filter = Builders<MemoryPost>.Filter.Eq(p => p.Id, postId);
        var post = await _mongoDb.Posts.Find(filter).FirstOrDefaultAsync();
        
        if (post == null)
            return NotFound();

        var allAuthorIds = post.Comments
            .Where(c => string.IsNullOrEmpty(c.AuthorName) || c.AuthorName == c.AuthorId)
            .Select(c => c.AuthorId)
            .Distinct()
            .ToList();

        var authorLookup = new Dictionary<string, User>();
        if (allAuthorIds.Any())
        {
            var authorFilter = Builders<User>.Filter.In(u => u.Id, allAuthorIds);
            var authors = await _mongoDb.Users.Find(authorFilter).ToListAsync();
            authorLookup = authors.ToDictionary(u => u.Id);
        }

        var comments = new List<CommentDto>();
        foreach (var c in post.Comments)
        {
            string commentAuthorName = c.AuthorName;
            string? commentAuthorAvatar = c.AuthorAvatar;
            
            if ((string.IsNullOrEmpty(commentAuthorName) || commentAuthorName == c.AuthorId) && authorLookup.TryGetValue(c.AuthorId, out var commentUser))
            {
                commentAuthorName = commentUser.DisplayName ?? commentUser.Username;
                commentAuthorAvatar = commentUser.Avatar;
            }
            
            comments.Add(new CommentDto
            {
                Id = c.Id,
                Content = c.Content,
                AuthorId = c.AuthorId,
                AuthorName = commentAuthorName,
                AuthorAvatar = commentAuthorAvatar,
                CreatedAt = c.CreatedAt
            });
        }
        
        var postDto = new PostDto
        {
            Id = post.Id,
            Content = post.Content,
            Images = post.Images,
            AuthorId = post.AuthorId,
            AuthorName = post.AuthorName,
            AuthorAvatar = post.AuthorAvatar,
            CreatedAt = post.CreatedAt,
            UpdatedAt = post.UpdatedAt,
            LikesCount = post.Likes.Count,
            IsLiked = post.Likes.Contains(userId),
            IsOwner = post.AuthorId == userId,
            Comments = comments
        };

        return Ok(postDto);
    }

    [HttpPost]
    public async Task<ActionResult<PostDto>> CreatePost([FromForm] CreatePostDto? dto)
    {
        try
        {
            if (dto == null)
            {
                return BadRequest(new { error = "Dữ liệu không hợp lệ" });
            }
            
            Console.WriteLine($"CreatePost called. Content: {dto.Content}, Images count: {dto.Images?.Count ?? 0}");
            
            // Try to get images from Request.Form if dto.Images is null
            var images = dto.Images ?? new List<IFormFile>();
            if (images.Count == 0 && Request.Form.Files != null && Request.Form.Files.Count > 0)
            {
                images = Request.Form.Files.ToList();
                Console.WriteLine($"Got {images.Count} images from Request.Form.Files");
            }
            
            var userId = GetUserId();
            var username = User.FindFirst(ClaimTypes.Name)!.Value;
            var displayName = User.FindFirst("DisplayName")!.Value;

            var userFilter = Builders<User>.Filter.Eq(u => u.Id, userId);
            var user = await _mongoDb.Users.Find(userFilter).FirstOrDefaultAsync();

            var imageUrls = new List<string>();
            if (images != null && images.Count > 0)
            {
                try
                {
                    Console.WriteLine($"Uploading {images.Count} images...");
                    imageUrls = await _cloudinaryService.UploadImagesAsync(images);
                    Console.WriteLine($"Uploaded {imageUrls.Count} images successfully");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error uploading images: {ex.Message}");
                    Console.WriteLine($"Stack trace: {ex.StackTrace}");
                    return StatusCode(500, new { error = "Lỗi khi upload ảnh: " + ex.Message });
                }
            }

            var post = new MemoryPost
            {
                Content = dto.Content ?? string.Empty,
                Images = imageUrls,
                AuthorId = userId,
                AuthorName = user?.DisplayName ?? displayName,
                AuthorAvatar = user?.Avatar,
                CreatedAt = DateTime.UtcNow
            };

            Console.WriteLine($"Inserting post to MongoDB...");
            await _mongoDb.Posts.InsertOneAsync(post);
            Console.WriteLine($"Post inserted with ID: {post.Id}");

            var postDto = new PostDto
            {
                Id = post.Id,
                Content = post.Content,
                Images = post.Images,
                AuthorId = post.AuthorId,
                AuthorName = post.AuthorName,
                AuthorAvatar = post.AuthorAvatar,
                CreatedAt = post.CreatedAt,
                LikesCount = 0,
                IsLiked = false,
                IsOwner = true,
                Comments = new List<CommentDto>()
            };

            // Tạo thông báo cho user còn lại (người kia)
            // Tìm tất cả user khác với user hiện tại
            var otherUserFilter = Builders<User>.Filter.Ne(u => u.Id, userId);
            var allOtherUsers = await _mongoDb.Users.Find(otherUserFilter).ToListAsync();
            
            // Gửi notification cho tất cả user khác (trong trường hợp có nhiều user)
            // Nhưng thường chỉ có 2 user nên sẽ gửi cho "người kia"
            foreach (var otherUser in allOtherUsers)
            {
                try
                {
                    var notification = new NotificationDto
                    {
                        Type = "post",
                        Message = $"{post.AuthorName} đã đăng một bài viết mới",
                        PostId = post.Id,
                        FromUserId = userId,
                        FromUserName = post.AuthorName,
                        IsRead = false,
                        CreatedAt = DateTime.UtcNow
                    };
                    
                    var createdNotification = await _notificationService.CreateNotificationAsync(
                        otherUser.Id,
                        "post",
                        notification.Message,
                        post.Id,
                        userId,
                        post.AuthorName
                    );

                    notification.Id = createdNotification.Id;
                    try
                    {
                        await _signalRService.BroadcastNewNotification(otherUser.Id, notification);
                    }
                    catch (Exception ex)
                    {
                        // Log error but don't fail the request
                    }
                }
                catch (Exception ex)
                {
                    // Log error but don't fail the request
                    Console.WriteLine($"Error creating notification for {otherUser.Username}: {ex.Message}");
                }
            }

            // Broadcast new post to all clients (IsOwner sẽ được tính lại ở frontend)
            try
            {
                var broadcastPost = new PostDto
                {
                    Id = postDto.Id,
                    Content = postDto.Content,
                    Images = postDto.Images,
                    AuthorId = postDto.AuthorId,
                    AuthorName = postDto.AuthorName,
                    AuthorAvatar = postDto.AuthorAvatar,
                    CreatedAt = postDto.CreatedAt,
                    LikesCount = postDto.LikesCount,
                    IsLiked = postDto.IsLiked,
                    IsOwner = false, // Sẽ được tính lại ở frontend dựa trên current user
                    Comments = postDto.Comments
                };
                await _signalRService.BroadcastNewPost(broadcastPost);
            }
            catch (Exception ex)
            {
                // Log error but don't fail the request
                Console.WriteLine($"Error broadcasting post: {ex.Message}");
            }

            Console.WriteLine($"Returning postDto with ID: {postDto.Id}");
            return Ok(postDto);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in CreatePost: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
            }
            return StatusCode(500, new { error = "Lỗi khi tạo bài viết: " + ex.Message, details = ex.StackTrace });
        }
    }

    [HttpPut("{postId}")]
    public async Task<ActionResult<PostDto>> UpdatePost(string postId, [FromForm] UpdatePostDto dto)
    {
        var userId = GetUserId();
        var postFilter = Builders<MemoryPost>.Filter.Eq(p => p.Id, postId);
        var post = await _mongoDb.Posts.Find(postFilter).FirstOrDefaultAsync();

        if (post == null)
            return NotFound();

        if (post.AuthorId != userId)
            return Forbid();

        var imageUrls = new List<string>();

        // Giữ lại ảnh cũ nếu có
        if (dto.KeepImages != null && dto.KeepImages.Count > 0)
        {
            imageUrls.AddRange(dto.KeepImages);
        }

        // Upload ảnh mới nếu có
        if (dto.NewImages != null && dto.NewImages.Count > 0)
        {
            var newImageUrls = await _cloudinaryService.UploadImagesAsync(dto.NewImages);
            imageUrls.AddRange(newImageUrls);
        }

        var update = Builders<MemoryPost>.Update
            .Set(p => p.Content, dto.Content)
            .Set(p => p.Images, imageUrls)
            .Set(p => p.UpdatedAt, DateTime.UtcNow);

        await _mongoDb.Posts.UpdateOneAsync(postFilter, update);

        var updatedPost = await _mongoDb.Posts.Find(postFilter).FirstOrDefaultAsync();
        var userFilter = Builders<User>.Filter.Eq(u => u.Id, userId);
        var user = await _mongoDb.Users.Find(userFilter).FirstOrDefaultAsync();

        var postDto = new PostDto
        {
            Id = updatedPost!.Id,
            Content = updatedPost.Content,
            Images = updatedPost.Images,
            AuthorId = updatedPost.AuthorId,
            AuthorName = updatedPost.AuthorName,
            AuthorAvatar = updatedPost.AuthorAvatar,
            CreatedAt = updatedPost.CreatedAt,
            UpdatedAt = updatedPost.UpdatedAt,
            LikesCount = updatedPost.Likes.Count,
            IsLiked = updatedPost.Likes.Contains(userId),
            IsOwner = true,
            Comments = updatedPost.Comments.Select(c => new CommentDto
            {
                Id = c.Id,
                Content = c.Content,
                AuthorId = c.AuthorId,
                AuthorName = c.AuthorName,
                AuthorAvatar = c.AuthorAvatar,
                CreatedAt = c.CreatedAt
            }).ToList()
        };

        // Broadcast updated post (IsOwner sẽ được tính lại ở frontend)
        var broadcastPost = new PostDto
        {
            Id = postDto.Id,
            Content = postDto.Content,
            Images = postDto.Images,
            AuthorId = postDto.AuthorId,
            AuthorName = postDto.AuthorName,
            AuthorAvatar = postDto.AuthorAvatar,
            CreatedAt = postDto.CreatedAt,
            UpdatedAt = postDto.UpdatedAt,
            LikesCount = postDto.LikesCount,
            IsLiked = postDto.IsLiked,
            IsOwner = false, // Sẽ được tính lại ở frontend dựa trên current user
            Comments = postDto.Comments
        };
        await _signalRService.BroadcastUpdatedPost(broadcastPost);

        return Ok(postDto);
    }

    [HttpDelete("{postId}")]
    public async Task<ActionResult> DeletePost(string postId)
    {
        var userId = GetUserId();
        var postFilter = Builders<MemoryPost>.Filter.Eq(p => p.Id, postId);
        var post = await _mongoDb.Posts.Find(postFilter).FirstOrDefaultAsync();

        if (post == null)
            return NotFound();

        if (post.AuthorId != userId)
            return Forbid();

        await _mongoDb.Posts.DeleteOneAsync(postFilter);

        // Broadcast deleted post
        await _signalRService.BroadcastDeletedPost(postId);

        return Ok();
    }

    [HttpPost("{postId}/like")]
    public async Task<ActionResult> ToggleLike(string postId)
    {
        var userId = GetUserId();
        var postFilter = Builders<MemoryPost>.Filter.Eq(p => p.Id, postId);
        var post = await _mongoDb.Posts.Find(postFilter).FirstOrDefaultAsync();
        
        if (post == null)
            return NotFound();

        var wasAlreadyLiked = post.Likes.Contains(userId);
        UpdateDefinition<MemoryPost> update;
        if (wasAlreadyLiked)
        {
            update = Builders<MemoryPost>.Update.Pull(p => p.Likes, userId);
        }
        else
        {
            update = Builders<MemoryPost>.Update.AddToSet(p => p.Likes, userId);
        }

        await _mongoDb.Posts.UpdateOneAsync(postFilter, update);

        var updatedPost = await _mongoDb.Posts.Find(postFilter).FirstOrDefaultAsync();
        var newLikesCount = updatedPost!.Likes.Count;

        if (!wasAlreadyLiked && post.AuthorId != userId)
        {
            var userFilter = Builders<User>.Filter.Eq(u => u.Id, userId);
            var user = await _mongoDb.Users.Find(userFilter).FirstOrDefaultAsync();
            
            var notification = new NotificationDto
            {
                Type = "like",
                Message = $"{user?.DisplayName ?? "Ai đó"} đã thích bài viết của bạn",
                PostId = postId,
                FromUserId = userId,
                FromUserName = user?.DisplayName ?? "Người dùng",
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            };

            var createdNotification = await _notificationService.CreateNotificationAsync(
                post.AuthorId,
                "like",
                notification.Message,
                postId,
                userId,
                user?.DisplayName ?? "Người dùng"
            );

            notification.Id = createdNotification.Id;
            await _signalRService.BroadcastNewNotification(post.AuthorId, notification);
        }

        await _signalRService.BroadcastPostLiked(postId, !wasAlreadyLiked, newLikesCount, userId);

        return Ok(new { isLiked = !wasAlreadyLiked, likesCount = newLikesCount });
    }

    [HttpPost("{postId}/comments")]
    public async Task<ActionResult<CommentDto>> AddComment(string postId, [FromBody] CreateCommentDto? dto)
    {
        // Log để debug
        Console.WriteLine($"AddComment called - PostId: {postId}");
        Console.WriteLine($"Received DTO is null: {dto == null}");
        
        // Nếu dto null, thử đọc từ form hoặc query
        if (dto == null)
        {
            // Thử đọc từ form
            var contentFromForm = Request.Form["content"].FirstOrDefault();
            if (!string.IsNullOrEmpty(contentFromForm))
            {
                dto = new CreateCommentDto { Content = contentFromForm };
                Console.WriteLine($"Got content from form: '{contentFromForm}'");
            }
            else
            {
                Console.WriteLine("DTO is null and no content in form");
                return BadRequest(new { error = "Dữ liệu không hợp lệ - DTO null và không có content trong form" });
            }
        }
        
        Console.WriteLine($"Received DTO - Content: '{dto.Content}', Content length: {dto.Content?.Length ?? 0}");
        
        if (string.IsNullOrWhiteSpace(dto.Content))
        {
            Console.WriteLine("DTO Content is null or empty");
            return BadRequest(new { error = "Nội dung bình luận không được để trống" });
        }
        
        // Kiểm tra xem content có phải là postId không (bug)
        if (dto.Content == postId)
        {
            Console.WriteLine($"ERROR: Content is same as postId! This is a bug. Content: '{dto.Content}', PostId: '{postId}'");
            return BadRequest(new { error = "Lỗi: Nội dung bình luận không được là ID của bài viết" });
        }
        
        var userId = GetUserId();
        var userFilter = Builders<User>.Filter.Eq(u => u.Id, userId);
        var user = await _mongoDb.Users.Find(userFilter).FirstOrDefaultAsync();
        
        // Lấy displayName từ JWT claim hoặc từ user trong DB
        string authorName;
        if (user != null && !string.IsNullOrEmpty(user.DisplayName))
        {
            authorName = user.DisplayName;
        }
        else
        {
            var displayNameClaim = User.FindFirst("DisplayName");
            authorName = displayNameClaim?.Value ?? "Người dùng";
        }

        var comment = new Comment
        {
            Id = ObjectId.GenerateNewId().ToString(),
            Content = dto.Content,
            AuthorId = userId,
            AuthorName = authorName,
            AuthorAvatar = user?.Avatar,
            CreatedAt = DateTime.UtcNow
        };
        
        Console.WriteLine($"Adding comment - AuthorId: {userId}, AuthorName: {authorName}, Content: '{comment.Content}'");

        var update = Builders<MemoryPost>.Update.Push(p => p.Comments, comment);
        var postFilter = Builders<MemoryPost>.Filter.Eq(p => p.Id, postId);
        await _mongoDb.Posts.UpdateOneAsync(postFilter, update);

        var commentDto = new CommentDto
        {
            Id = comment.Id,
            Content = comment.Content,
            AuthorId = comment.AuthorId,
            AuthorName = comment.AuthorName,
            AuthorAvatar = comment.AuthorAvatar,
            CreatedAt = comment.CreatedAt
        };

        // Tạo thông báo cho chủ bài viết khi có người comment (chỉ gửi khi người comment KHÔNG phải là tác giả)
        var post = await _mongoDb.Posts.Find(postFilter).FirstOrDefaultAsync();
        if (post != null && post.AuthorId != userId) // Chỉ gửi notification nếu người comment không phải tác giả
        {
            var notification = new NotificationDto
            {
                Type = "comment",
                Message = $"{comment.AuthorName} đã bình luận bài viết của bạn",
                PostId = postId,
                FromUserId = userId,
                FromUserName = comment.AuthorName,
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            };

            var createdNotification = await _notificationService.CreateNotificationAsync(
                post.AuthorId,
                "comment",
                notification.Message,
                postId,
                userId,
                comment.AuthorName
            );

            notification.Id = createdNotification.Id;
            try
            {
                await _signalRService.BroadcastNewNotification(post.AuthorId, notification);
            }
            catch (Exception ex)
            {
            }
        }
        else if (post != null && post.AuthorId == userId)
        {
            Console.WriteLine($"User {userId} commented on their own post, skipping notification");
        }

        // Broadcast new comment
        await _signalRService.BroadcastNewComment(postId, commentDto);

        return Ok(commentDto);
    }

    [HttpGet("{postId}/comments")]
    public async Task<ActionResult<object>> GetComments(string postId, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        var userId = GetUserId();
        var filter = Builders<MemoryPost>.Filter.Eq(p => p.Id, postId);
        var post = await _mongoDb.Posts.Find(filter).FirstOrDefaultAsync();
        
        if (post == null)
            return NotFound();

        var comments = post.Comments.OrderByDescending(c => c.CreatedAt).ToList();
        var totalCount = comments.Count;
        
        // Tính toán skip và limit cho pagination
        var skip = (page - 1) * pageSize;
        var limit = pageSize;
        
        var paginatedComments = comments.Skip(skip).Take(limit).ToList();
        
        var allAuthorIds = paginatedComments
            .Where(c => string.IsNullOrEmpty(c.AuthorName) || c.AuthorName == c.AuthorId)
            .Select(c => c.AuthorId)
            .Distinct()
            .ToList();

        var authorLookup = new Dictionary<string, User>();
        if (allAuthorIds.Any())
        {
            var authorFilter = Builders<User>.Filter.In(u => u.Id, allAuthorIds);
            var authors = await _mongoDb.Users.Find(authorFilter).ToListAsync();
            authorLookup = authors.ToDictionary(u => u.Id);
        }

        var commentDtos = new List<CommentDto>();
        foreach (var c in paginatedComments)
        {
            string commentAuthorName = c.AuthorName;
            string? commentAuthorAvatar = c.AuthorAvatar;
            
            if ((string.IsNullOrEmpty(commentAuthorName) || commentAuthorName == c.AuthorId) && authorLookup.TryGetValue(c.AuthorId, out var commentUser))
            {
                commentAuthorName = commentUser.DisplayName ?? commentUser.Username;
                commentAuthorAvatar = commentUser.Avatar;
            }
            
            commentDtos.Add(new CommentDto
            {
                Id = c.Id,
                Content = c.Content,
                AuthorId = c.AuthorId,
                AuthorName = commentAuthorName,
                AuthorAvatar = commentAuthorAvatar,
                CreatedAt = c.CreatedAt
            });
        }

        var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

        return Ok(new
        {
            comments = commentDtos,
            totalCount,
            currentPage = page,
            pageSize,
            totalPages,
            hasMore = page < totalPages
        });
    }

    // Helper method to normalize Vietnamese text (remove diacritics)
    private string NormalizeVietnamese(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        
        var normalized = text.Normalize(NormalizationForm.FormD);
        var stringBuilder = new StringBuilder();
        
        foreach (var c in normalized)
        {
            var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
            if (unicodeCategory != UnicodeCategory.NonSpacingMark)
            {
                stringBuilder.Append(c);
            }
        }
        
        return stringBuilder.ToString().Normalize(NormalizationForm.FormC);
    }
}

