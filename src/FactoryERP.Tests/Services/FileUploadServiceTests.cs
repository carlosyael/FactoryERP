using FactoryERP.Web.Services;
using FluentAssertions;

namespace FactoryERP.Tests.Services;

public class FileUploadServiceTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), $"factoryerp_upload_{Guid.NewGuid():N}");

    [Fact]
    public async Task SaveImageStreamAsync_ValidPng_SavesUnderUploadsWithGuidName()
    {
        var service = new FileUploadService(_tempRoot);
        await using var ms = new MemoryStream([0x89, 0x50, 0x4E, 0x47]);

        string relativeUrl = await service.SaveImageStreamAsync(ms, "shampoo.png", 4, "products");

        relativeUrl.Should().StartWith("/uploads/products/");
        relativeUrl.Should().EndWith(".png");
        string fullPath = Path.Combine(_tempRoot, relativeUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
        File.Exists(fullPath).Should().BeTrue();
    }

    [Theory]
    [InlineData("malware.exe", "products")]
    [InlineData("script.svg", "products")]
    [InlineData("photo.jpg", "../escape")]
    [InlineData("photo.jpg", "a/b")]
    public async Task SaveImageStreamAsync_InvalidExtensionOrTraversal_ThrowsInvalidOperationException(
        string fileName, string folder)
    {
        var service = new FileUploadService(_tempRoot);
        await using var ms = new MemoryStream([1, 2, 3]);

        Func<Task> act = () => service.SaveImageStreamAsync(ms, fileName, 3, folder);
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task SaveImageStreamAsync_ExceedsMaxSize_ThrowsInvalidOperationException()
    {
        var service = new FileUploadService(_tempRoot);
        await using var ms = new MemoryStream([1, 2, 3]);

        Func<Task> act = () => service.SaveImageStreamAsync(ms, "big.jpg", FileUploadService.MaxImageBytes + 1, "products");
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot)) Directory.Delete(_tempRoot, recursive: true);
    }
}
