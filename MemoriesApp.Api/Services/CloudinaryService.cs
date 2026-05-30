using CloudinaryDotNet;
using CloudinaryDotNet.Actions;

namespace MemoriesApp.Api.Services;

public class CloudinaryService
{
    private readonly Cloudinary _cloudinary;

    public CloudinaryService(IConfiguration configuration)
    {
        var cloudName = configuration["Cloudinary:CloudName"];
        var apiKey = configuration["Cloudinary:ApiKey"];
        var apiSecret = configuration["Cloudinary:ApiSecret"];

        var account = new Account(cloudName, apiKey, apiSecret);
        _cloudinary = new Cloudinary(account);
    }

    public async Task<string> UploadImageAsync(IFormFile file)
    {
        if (file == null || file.Length == 0)
            throw new ArgumentException("File is empty");

        // Validate file size (10MB)
        if (file.Length > 10 * 1024 * 1024)
            throw new ArgumentException("File size cannot exceed 10MB");

        // Validate file extension
        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!allowedExtensions.Contains(extension))
            throw new ArgumentException($"File type {extension} is not supported");

        using var stream = file.OpenReadStream();
        var uploadParams = new ImageUploadParams
        {
            File = new FileDescription(file.FileName, stream),
            Folder = "memories",
            Transformation = new Transformation().Quality("auto").FetchFormat("auto")
        };

        var uploadResult = await _cloudinary.UploadAsync(uploadParams);
        return uploadResult.SecureUrl.ToString();
    }

    public async Task<List<string>> UploadImagesAsync(List<IFormFile> files)
    {
        if (files == null || files.Count == 0)
            return new List<string>();

        var uploadTasks = files.Select(file => UploadImageAsync(file));
        var results = await Task.WhenAll(uploadTasks);
        return results.ToList();
    }
}

