using Shintio.FileSystem.Abstractions;

namespace Shintio.FileSystem.Tests;

public class PhysicalFileSystemTests : FileSystemTestsBase, IDisposable
{
	private readonly string _basePath = Path.Combine(".temp", Guid.NewGuid().ToString());

	protected override IFileSystem FileSystem { get; } = new Physical.FileSystem();
	protected override string BasePath => _basePath;

	public PhysicalFileSystemTests()
	{
		Directory.CreateDirectory(_basePath);
	}

	public void Dispose()
	{
		if (Directory.Exists(_basePath))
		{
			Directory.Delete(_basePath, true);
		}
	}

	[Fact]
	public void GetFullPathReturnsAbsolutePath()
	{
		var relativePath = FileSystem.Combine(BasePath, "a", "..", "b", "test.txt");

		var fullPath = FileSystem.GetFullPath(relativePath);

		Assert.True(Path.IsPathFullyQualified(fullPath));
		Assert.Equal(Path.GetFullPath(relativePath), fullPath);
	}

	[Fact]
	public void CombineJoinsParts()
	{
		var combined = FileSystem.Combine(BasePath, "nested", "file.txt");
		var expected = Path.Combine(BasePath, "nested", "file.txt");

		Assert.Equal(expected, combined);
	}

	[Fact]
	public void CreateFileExistsInFileSystem()
	{
		var content = "TestContent";

		var filePath = Path.Combine(BasePath, "test.bin");

		FileSystem.CreateFile(filePath, content);

		Assert.True(File.Exists(filePath));
		Assert.Equal(content, File.ReadAllText(filePath));
	}
}