namespace RaceDay.Api.Services;

public record FileUploadResult(bool Success, string? RelativeUrl, string? Error);


// Abstraction over where uploaded images are stored. Part 2 saves to local disk;
// Part 3 swaps in an Azure Blob Storage implementation without changing any controller.

public interface IFileStorageService
{
    Task<FileUploadResult> SaveImageAsync(IFormFile file, string folder);
}
