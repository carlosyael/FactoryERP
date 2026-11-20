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
    private static readonly Dictionary<string, string> ContentTypesByExtension = new(StringComparer.OrdinalIgnoreCase)
    {
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".png"] = "image/png",
        [".webp"] = "image/webp"
    };

    private readonly string _uploadsBasePath;
    private readonly IExternalImageStore? _externalStore;

    public FileUploadService(IWebHostEnvironment env, IExternalImageStore? externalStore = null)
        : this(ResolveDefaultUploadsRoot(env), isUploadsRoot: true, externalStore)
    {
    }

    public FileUploadService(string webRootPath)
        : this(Path.Combine(webRootPath, "uploads"), isUploadsRoot: true, null)
    {
    }

    public FileUploadService(string basePath, bool isUploadsRoot, IExternalImageStore? externalStore = null)
    {
        _uploadsBasePath = Path.GetFullPath(isUploadsRoot ? basePath : Path.Combine(basePath, "uploads"));
        _externalStore = externalStore;
    }

    public static bool TryGetContentType(string fileName, out string contentType)
    {
        string ext = Path.GetExtension(Path.GetFileName(fileName));
        if (!string.IsNullOrWhiteSpace(ext) && ContentTypesByExtension.TryGetValue(ext, out string? mapped))
        {
            contentType = mapped;
            return true;
        }

        contentType = string.Empty;
        return false;
    }

    public static bool IsSafePathSegment(string segment) =>
        !string.IsNullOrWhiteSpace(segment) &&
        !segment.Contains("..") &&
        !segment.Contains('/') &&
        !segment.Contains('\\');

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
        if (!TryGetContentType(fileName, out string contentType))
            throw new InvalidOperationException("Formato no permitido. Use JPG, PNG o WEBP.");

        string uniqueName = $"{Guid.NewGuid():N}{ext.ToLowerInvariant()}";
        if (_externalStore?.IsEnabled == true)
        {
            await _externalStore.UploadAsync($"{safeFolder}/{uniqueName}", stream, contentType);
            return $"/uploads/{safeFolder}/{uniqueName}";
        }

        string normalizedBase = _uploadsBasePath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        string targetDir = Path.GetFullPath(Path.Combine(_uploadsBasePath, safeFolder));
        string normalizedTarget = targetDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        if (!normalizedTarget.StartsWith(normalizedBase, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Ruta de carga inválida.");

        Directory.CreateDirectory(targetDir);
        string targetFile = Path.Combine(targetDir, uniqueName);

        await using var fs = new FileStream(targetFile, FileMode.CreateNew, FileAccess.Write);
        await stream.CopyToAsync(fs);

        return $"/uploads/{safeFolder}/{uniqueName}";
    }

    private static string ResolveDefaultUploadsRoot(IWebHostEnvironment env)
    {
        string? configured = Environment.GetEnvironmentVariable("UPLOADS_PATH");
        if (!string.IsNullOrWhiteSpace(configured))
            return configured;

        string webRoot = env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot");
        return Path.Combine(webRoot, "uploads");
    }

    private static string SanitizeFolder(string folder)
    {
        if (!IsSafePathSegment(folder))
            throw new InvalidOperationException("Nombre de carpeta de carga inválido.");

        return folder.Trim().ToLowerInvariant();
    }
}
