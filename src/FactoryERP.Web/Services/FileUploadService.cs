using Microsoft.AspNetCore.Components.Forms;

namespace FactoryERP.Web.Services;

public interface IFileUploadService
{
    Task<string> SaveImageAsync(IBrowserFile file, string folder);
    Task<string> SaveImageStreamAsync(Stream stream, string fileName, long fileSize, string folder);
}

public sealed class FileUploadService : IFileUploadService
{
    public const long MaxImageBytes = 5 * 1024 * 1024;
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp"
    };

    private readonly string _webRootPath;

    public FileUploadService(IWebHostEnvironment env)
        : this(env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot"))
    {
    }

    public FileUploadService(string webRootPath)
    {
        _webRootPath = Path.GetFullPath(webRootPath);
    }

    public async Task<string> SaveImageAsync(IBrowserFile file, string folder)
    {
        await using var stream = file.OpenReadStream(MaxImageBytes);
        return await SaveImageStreamAsync(stream, file.Name, file.Size, folder);
    }

    public async Task<string> SaveImageStreamAsync(Stream stream, string fileName, long fileSize, string folder)
    {
        if (fileSize <= 0 || fileSize > MaxImageBytes)
            throw new InvalidOperationException("La imagen debe pesar entre 1 byte y 5 MB.");

        string safeFolder = SanitizeFolder(folder);
        string ext = Path.GetExtension(Path.GetFileName(fileName));
        if (string.IsNullOrWhiteSpace(ext) || !AllowedExtensions.Contains(ext))
            throw new InvalidOperationException("Formato no permitido. Use JPG, PNG o WEBP.");

        string uploadsBase = Path.GetFullPath(Path.Combine(_webRootPath, "uploads"));
        string targetDir = Path.GetFullPath(Path.Combine(uploadsBase, safeFolder));
        if (!targetDir.StartsWith(uploadsBase, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Ruta de carga inválida.");

        Directory.CreateDirectory(targetDir);
        string uniqueName = $"{Guid.NewGuid():N}{ext.ToLowerInvariant()}";
        string targetFile = Path.Combine(targetDir, uniqueName);

        await using var fs = new FileStream(targetFile, FileMode.CreateNew, FileAccess.Write);
        await stream.CopyToAsync(fs);

        return $"/uploads/{safeFolder}/{uniqueName}";
    }

    private static string SanitizeFolder(string folder)
    {
        if (string.IsNullOrWhiteSpace(folder) || folder.Contains("..") || folder.Contains('/') || folder.Contains('\\'))
            throw new InvalidOperationException("Nombre de carpeta de carga inválido.");

        return folder.Trim().ToLowerInvariant();
    }
}
