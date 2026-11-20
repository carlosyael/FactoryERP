using FactoryERP.Web.Services;
using FluentAssertions;
using Moq;

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

    [Fact]
    public async Task SaveImageStreamAsync_WithDirectUploadsRoot_SavesDirectlyInsideConfiguredPath()
    {
        string customUploadsDir = Path.Combine(_tempRoot, "persistent-uploads");
        var service = new FileUploadService(customUploadsDir, isUploadsRoot: true);
        await using var ms = new MemoryStream([0x89, 0x50, 0x4E, 0x47]);

        string relativeUrl = await service.SaveImageStreamAsync(ms, "logo.png", 4, "branding");

        relativeUrl.Should().StartWith("/uploads/branding/");
        relativeUrl.Should().EndWith(".png");
        string fileName = Path.GetFileName(relativeUrl);
        string expectedPath = Path.Combine(customUploadsDir, "branding", fileName);
        File.Exists(expectedPath).Should().BeTrue();
        Directory.Exists(Path.Combine(customUploadsDir, "uploads")).Should().BeFalse();
    }

    [Theory]
    [InlineData("../escape")]
    [InlineData("branding\\..\\outside")]
    public async Task SaveImageStreamAsync_WithDirectUploadsRoot_InvalidTraversal_ThrowsInvalidOperationException(string folder)
    {
        string customUploadsDir = Path.Combine(_tempRoot, "persistent-uploads");
        var service = new FileUploadService(customUploadsDir, isUploadsRoot: true);
        await using var ms = new MemoryStream([1, 2, 3]);

        Func<Task> act = () => service.SaveImageStreamAsync(ms, "logo.png", 3, folder);
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task SaveImageStreamAsync_WhenExternalStoreEnabled_UploadsToBlobWithoutLocalFile()
    {
        var mockStore = new Mock<IExternalImageStore>();
        mockStore.SetupGet(s => s.IsEnabled).Returns(true);
        string? capturedPath = null;
        string? capturedContentType = null;
        mockStore
            .Setup(s => s.UploadAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, Stream, string, CancellationToken>((path, _, ct, _) =>
            {
                capturedPath = path;
                capturedContentType = ct;
            })
            .Returns(Task.CompletedTask);

        var service = new FileUploadService(_tempRoot, isUploadsRoot: true, mockStore.Object);
        await using var ms = new MemoryStream([0x89, 0x50, 0x4E, 0x47]);

        string relativeUrl = await service.SaveImageStreamAsync(ms, "logo.png", 4, "branding");

        relativeUrl.Should().StartWith("/uploads/branding/");
        relativeUrl.Should().EndWith(".png");
        capturedPath.Should().Be($"branding/{Path.GetFileName(relativeUrl)}");
        capturedContentType.Should().Be("image/png");
        Directory.Exists(_tempRoot).Should().BeFalse();
        mockStore.Verify(s => s.UploadAsync(It.IsAny<string>(), It.IsAny<Stream>(), "image/png", It.IsAny<CancellationToken>()), Times.Once);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot)) Directory.Delete(_tempRoot, recursive: true);
    }
}
