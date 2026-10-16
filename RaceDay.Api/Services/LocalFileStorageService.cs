namespace RaceDay.Api.Services;

public class LocalFileStorageService : IFileStorageService
{
    private static readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
    private const long MaxBytes = 5 * 1024 * 1024;

    private readonly IWebHostEnvironment _env;

    public LocalFileStorageService(IWebHostEnvironment env)
    {
        _env = env;
    }

    public async Task<FileUploadResult> SaveImageAsync(IFormFile file, string folder)
    {
        if (file is null || file.Length == 0)
            return new FileUploadResult(false, null, "No file was provided.");

        if (file.Length > MaxBytes)
            return new FileUploadResult(false, null, "The file is too large (maximum 5 MB).");

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var looksLikeImage = file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);
        if (!AllowedExtensions.Contains(extension) || !looksLikeImage)
            return new FileUploadResult(false, null, "Only image files (jpg, png, gif, webp) are allowed.");

        var webRoot = _env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot");
        var targetDirectory = Path.Combine(webRoot, "uploads", folder);
        Directory.CreateDirectory(targetDirectory);

        var fileName = $"{Guid.NewGuid():N}{extension}";
        var fullPath = Path.Combine(targetDirectory, fileName);

        await using (var stream = new FileStream(fullPath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        return new FileUploadResult(true, $"/uploads/{folder}/{fileName}", null);
    }
}
