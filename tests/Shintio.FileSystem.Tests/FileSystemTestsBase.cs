using System.Threading.Tasks;
using Shintio.FileSystem.Abstractions;
using Shintio.FileSystem.Abstractions.Sync;
using Xunit;

namespace Shintio.FileSystem.Tests;

public abstract class FileSystemTestsBase
{
	protected abstract IFileSystem FileSystem { get; }
	protected abstract string BasePath { get; }

	[Fact]
	public void CreateFileBytesSync()
	{
		var content = new byte[] { 0, 50, 30, 225 };
		var filePath = FileSystem.Combine(BasePath, "test.bin");

		FileSystem.CreateFile(filePath, content);

		Assert.True(FileSystem.Exists(filePath));
		Assert.Equal(content, FileSystem.ReadFile(filePath));
	}

	[Fact]
	public async Task CreateFileBytesAsync()
	{
		var content = new byte[] { 0, 50, 30, 225 };
		var filePath = FileSystem.Combine(BasePath, "test.bin");

		await FileSystem.CreateFileAsync(filePath, content);

		Assert.True(await FileSystem.ExistsAsync(filePath));
		Assert.Equal(content, await FileSystem.ReadFileAsync(filePath));
	}

	[Fact]
	public void CreateDirectoryCreatesPathSync()
	{
		var dirPath = FileSystem.Combine(BasePath, "folder", "nested");

		FileSystem.CreateDirectory(dirPath);

		Assert.True(FileSystem.Exists(dirPath));
	}

	[Fact]
	public async Task CreateDirectoryCreatesPathAsync()
	{
		var dirPath = FileSystem.Combine(BasePath, "folder", "nested");

		await FileSystem.CreateDirectoryAsync(dirPath);

		Assert.True(await FileSystem.ExistsAsync(dirPath));
	}

	[Fact]
	public void CreateFileTextSync()
	{
		var content = "Hello World!";
		var filePath = FileSystem.Combine(BasePath, "test.txt");

		FileSystem.CreateFile(filePath, content);

		Assert.True(FileSystem.Exists(filePath));
		Assert.Equal(content, FileSystem.ReadFileText(filePath));
	}

	[Fact]
	public async Task CreateFileTextAsync()
	{
		var content = "Hello World!";
		var filePath = FileSystem.Combine(BasePath, "test.txt");

		await FileSystem.CreateFileAsync(filePath, content);

		Assert.True(await FileSystem.ExistsAsync(filePath));
		Assert.Equal(content, await FileSystem.ReadFileTextAsync(filePath));
	}

	[Fact]
	public void ExistsReturnsTrueForDirectoryAndFileSync()
	{
		var dirPath = FileSystem.Combine(BasePath, "dir");
		var filePath = FileSystem.Combine(dirPath, "file.txt");

		FileSystem.CreateDirectory(dirPath);
		FileSystem.CreateFile(filePath, "content");

		Assert.True(FileSystem.Exists(dirPath));
		Assert.True(FileSystem.Exists(filePath));
	}

	[Fact]
	public async Task ExistsReturnsTrueForDirectoryAndFileAsync()
	{
		var dirPath = FileSystem.Combine(BasePath, "dir");
		var filePath = FileSystem.Combine(dirPath, "file.txt");

		await FileSystem.CreateDirectoryAsync(dirPath);
		await FileSystem.CreateFileAsync(filePath, "content");

		Assert.True(await FileSystem.ExistsAsync(dirPath));
		Assert.True(await FileSystem.ExistsAsync(filePath));
	}

	[Fact]
	public void DeleteRemovesFileSync()
	{
		var filePath = FileSystem.Combine(BasePath, "delete-me.txt");
		FileSystem.CreateFile(filePath, "content");

		FileSystem.Delete(filePath);

		Assert.False(FileSystem.Exists(filePath));
	}

	[Fact]
	public async Task DeleteRemovesFileAsync()
	{
		var filePath = FileSystem.Combine(BasePath, "delete-me.txt");
		await FileSystem.CreateFileAsync(filePath, "content");

		await FileSystem.DeleteAsync(filePath);

		Assert.False(await FileSystem.ExistsAsync(filePath));
	}

	[Fact]
	public void DeleteRemovesDirectoryRecursivelySync()
	{
		var dirPath = FileSystem.Combine(BasePath, "delete-dir");
		var nestedFilePath = FileSystem.Combine(dirPath, "nested", "file.txt");
		FileSystem.CreateFile(nestedFilePath, "content");

		FileSystem.Delete(dirPath);

		Assert.False(FileSystem.Exists(dirPath));
		Assert.False(FileSystem.Exists(nestedFilePath));
	}

	[Fact]
	public async Task DeleteRemovesDirectoryRecursivelyAsync()
	{
		var dirPath = FileSystem.Combine(BasePath, "delete-dir");
		var nestedFilePath = FileSystem.Combine(dirPath, "nested", "file.txt");
		await FileSystem.CreateFileAsync(nestedFilePath, "content");

		await FileSystem.DeleteAsync(dirPath);

		Assert.False(await FileSystem.ExistsAsync(dirPath));
		Assert.False(await FileSystem.ExistsAsync(nestedFilePath));
	}

	[Fact]
	public void CopyCopiesFileSync()
	{
		var sourcePath = FileSystem.Combine(BasePath, "source.txt");
		var targetPath = FileSystem.Combine(BasePath, "target.txt");
		var content = "copy-content";
		FileSystem.CreateFile(sourcePath, content);

		FileSystem.Copy(sourcePath, targetPath);

		Assert.True(FileSystem.Exists(sourcePath));
		Assert.True(FileSystem.Exists(targetPath));
		Assert.Equal(content, FileSystem.ReadFileText(targetPath));
	}

	[Fact]
	public async Task CopyCopiesFileAsync()
	{
		var sourcePath = FileSystem.Combine(BasePath, "source.txt");
		var targetPath = FileSystem.Combine(BasePath, "target.txt");
		var content = "copy-content";
		await FileSystem.CreateFileAsync(sourcePath, content);

		await FileSystem.CopyAsync(sourcePath, targetPath);

		Assert.True(await FileSystem.ExistsAsync(sourcePath));
		Assert.True(await FileSystem.ExistsAsync(targetPath));
		Assert.Equal(content, await FileSystem.ReadFileTextAsync(targetPath));
	}

	[Fact]
	public void CopyCopiesDirectoryRecursivelySync()
	{
		var sourceDir = FileSystem.Combine(BasePath, "source-dir");
		var sourceFile = FileSystem.Combine(sourceDir, "nested", "file.txt");
		var targetDir = FileSystem.Combine(BasePath, "target-dir");
		var targetFile = FileSystem.Combine(targetDir, "nested", "file.txt");
		var content = "directory-copy-content";
		FileSystem.CreateFile(sourceFile, content);

		FileSystem.Copy(sourceDir, targetDir);

		Assert.True(FileSystem.Exists(sourceDir));
		Assert.True(FileSystem.Exists(targetDir));
		Assert.Equal(content, FileSystem.ReadFileText(targetFile));
	}

	[Fact]
	public async Task CopyCopiesDirectoryRecursivelyAsync()
	{
		var sourceDir = FileSystem.Combine(BasePath, "source-dir");
		var sourceFile = FileSystem.Combine(sourceDir, "nested", "file.txt");
		var targetDir = FileSystem.Combine(BasePath, "target-dir");
		var targetFile = FileSystem.Combine(targetDir, "nested", "file.txt");
		var content = "directory-copy-content";
		await FileSystem.CreateFileAsync(sourceFile, content);

		await FileSystem.CopyAsync(sourceDir, targetDir);

		Assert.True(await FileSystem.ExistsAsync(sourceDir));
		Assert.True(await FileSystem.ExistsAsync(targetDir));
		Assert.Equal(content, await FileSystem.ReadFileTextAsync(targetFile));
	}

	[Fact]
	public void MoveMovesFileSync()
	{
		var sourcePath = FileSystem.Combine(BasePath, "move-source.txt");
		var targetPath = FileSystem.Combine(BasePath, "move-target.txt");
		var content = "move-content";
		FileSystem.CreateFile(sourcePath, content);

		FileSystem.Move(sourcePath, targetPath);

		Assert.False(FileSystem.Exists(sourcePath));
		Assert.True(FileSystem.Exists(targetPath));
		Assert.Equal(content, FileSystem.ReadFileText(targetPath));
	}

	[Fact]
	public async Task MoveMovesFileAsync()
	{
		var sourcePath = FileSystem.Combine(BasePath, "move-source.txt");
		var targetPath = FileSystem.Combine(BasePath, "move-target.txt");
		var content = "move-content";
		await FileSystem.CreateFileAsync(sourcePath, content);

		await FileSystem.MoveAsync(sourcePath, targetPath);

		Assert.False(await FileSystem.ExistsAsync(sourcePath));
		Assert.True(await FileSystem.ExistsAsync(targetPath));
		Assert.Equal(content, await FileSystem.ReadFileTextAsync(targetPath));
	}

	[Fact]
	public void MoveMovesDirectoryRecursivelySync()
	{
		var sourceDir = FileSystem.Combine(BasePath, "move-source-dir");
		var sourceFile = FileSystem.Combine(sourceDir, "nested", "file.txt");
		var targetDir = FileSystem.Combine(BasePath, "move-target-dir");
		var targetFile = FileSystem.Combine(targetDir, "nested", "file.txt");
		var content = "move-directory-content";
		FileSystem.CreateFile(sourceFile, content);

		FileSystem.Move(sourceDir, targetDir);

		Assert.False(FileSystem.Exists(sourceDir));
		Assert.True(FileSystem.Exists(targetDir));
		Assert.Equal(content, FileSystem.ReadFileText(targetFile));
	}

	[Fact]
	public async Task MoveMovesDirectoryRecursivelyAsync()
	{
		var sourceDir = FileSystem.Combine(BasePath, "move-source-dir");
		var sourceFile = FileSystem.Combine(sourceDir, "nested", "file.txt");
		var targetDir = FileSystem.Combine(BasePath, "move-target-dir");
		var targetFile = FileSystem.Combine(targetDir, "nested", "file.txt");
		var content = "move-directory-content";
		await FileSystem.CreateFileAsync(sourceFile, content);

		await FileSystem.MoveAsync(sourceDir, targetDir);

		Assert.False(await FileSystem.ExistsAsync(sourceDir));
		Assert.True(await FileSystem.ExistsAsync(targetDir));
		Assert.Equal(content, await FileSystem.ReadFileTextAsync(targetFile));
	}

	[Fact]
	public void RenameRenamesFileSync()
	{
		var sourcePath = FileSystem.Combine(BasePath, "source-file.txt");
		var expectedPath = FileSystem.Combine(BasePath, "renamed-file.txt");
		var content = "rename-file-content";
		FileSystem.CreateFile(sourcePath, content);

		FileSystem.Rename(sourcePath, "renamed-file.txt");

		Assert.False(FileSystem.Exists(sourcePath));
		Assert.True(FileSystem.Exists(expectedPath));
		Assert.Equal(content, FileSystem.ReadFileText(expectedPath));
	}

	[Fact]
	public async Task RenameRenamesFileAsync()
	{
		var sourcePath = FileSystem.Combine(BasePath, "source-file.txt");
		var expectedPath = FileSystem.Combine(BasePath, "renamed-file.txt");
		var content = "rename-file-content";
		await FileSystem.CreateFileAsync(sourcePath, content);

		await FileSystem.RenameAsync(sourcePath, "renamed-file.txt");

		Assert.False(await FileSystem.ExistsAsync(sourcePath));
		Assert.True(await FileSystem.ExistsAsync(expectedPath));
		Assert.Equal(content, await FileSystem.ReadFileTextAsync(expectedPath));
	}

	[Fact]
	public void RenameRenamesDirectorySync()
	{
		var sourceDir = FileSystem.Combine(BasePath, "source-dir");
		var sourceFile = FileSystem.Combine(sourceDir, "file.txt");
		var expectedDir = FileSystem.Combine(BasePath, "renamed-dir");
		var expectedFile = FileSystem.Combine(expectedDir, "file.txt");
		var content = "rename-dir-content";
		FileSystem.CreateFile(sourceFile, content);

		FileSystem.Rename(sourceDir, "renamed-dir");

		Assert.False(FileSystem.Exists(sourceDir));
		Assert.True(FileSystem.Exists(expectedDir));
		Assert.Equal(content, FileSystem.ReadFileText(expectedFile));
	}

	[Fact]
	public async Task RenameRenamesDirectoryAsync()
	{
		var sourceDir = FileSystem.Combine(BasePath, "source-dir");
		var sourceFile = FileSystem.Combine(sourceDir, "file.txt");
		var expectedDir = FileSystem.Combine(BasePath, "renamed-dir");
		var expectedFile = FileSystem.Combine(expectedDir, "file.txt");
		var content = "rename-dir-content";
		await FileSystem.CreateFileAsync(sourceFile, content);

		await FileSystem.RenameAsync(sourceDir, "renamed-dir");

		Assert.False(await FileSystem.ExistsAsync(sourceDir));
		Assert.True(await FileSystem.ExistsAsync(expectedDir));
		Assert.Equal(content, await FileSystem.ReadFileTextAsync(expectedFile));
	}

	[Fact]
	public void CopyAllFilesCopiesNestedFilesSync()
	{
		var sourceDir = FileSystem.Combine(BasePath, "copy-all-source");
		var sourceFile1 = FileSystem.Combine(sourceDir, "root.txt");
		var sourceFile2 = FileSystem.Combine(sourceDir, "nested", "child.txt");
		var targetDir = FileSystem.Combine(BasePath, "copy-all-target");
		var targetFile1 = FileSystem.Combine(targetDir, "root.txt");
		var targetFile2 = FileSystem.Combine(targetDir, "nested", "child.txt");
		FileSystem.CreateFile(sourceFile1, "root-content");
		FileSystem.CreateFile(sourceFile2, "child-content");

		FileSystem.CopyAllFiles(sourceDir, targetDir);

		Assert.True(FileSystem.Exists(targetFile1));
		Assert.True(FileSystem.Exists(targetFile2));
		Assert.Equal("root-content", FileSystem.ReadFileText(targetFile1));
		Assert.Equal("child-content", FileSystem.ReadFileText(targetFile2));
	}

	[Fact]
	public async Task CopyAllFilesCopiesNestedFilesAsync()
	{
		var sourceDir = FileSystem.Combine(BasePath, "copy-all-source");
		var sourceFile1 = FileSystem.Combine(sourceDir, "root.txt");
		var sourceFile2 = FileSystem.Combine(sourceDir, "nested", "child.txt");
		var targetDir = FileSystem.Combine(BasePath, "copy-all-target");
		var targetFile1 = FileSystem.Combine(targetDir, "root.txt");
		var targetFile2 = FileSystem.Combine(targetDir, "nested", "child.txt");
		await FileSystem.CreateFileAsync(sourceFile1, "root-content");
		await FileSystem.CreateFileAsync(sourceFile2, "child-content");

		await FileSystem.CopyAllFilesAsync(sourceDir, targetDir);

		Assert.True(await FileSystem.ExistsAsync(targetFile1));
		Assert.True(await FileSystem.ExistsAsync(targetFile2));
		Assert.Equal("root-content", await FileSystem.ReadFileTextAsync(targetFile1));
		Assert.Equal("child-content", await FileSystem.ReadFileTextAsync(targetFile2));
	}

	[Fact]
	public void CopyAllFilesDoesNothingWhenSourceDoesNotExistSync()
	{
		var sourceDir = FileSystem.Combine(BasePath, "missing-source");
		var targetDir = FileSystem.Combine(BasePath, "target");

		FileSystem.CopyAllFiles(sourceDir, targetDir);

		Assert.False(FileSystem.Exists(targetDir));
	}

	[Fact]
	public async Task CopyAllFilesDoesNothingWhenSourceDoesNotExistAsync()
	{
		var sourceDir = FileSystem.Combine(BasePath, "missing-source");
		var targetDir = FileSystem.Combine(BasePath, "target");

		await FileSystem.CopyAllFilesAsync(sourceDir, targetDir);

		Assert.False(await FileSystem.ExistsAsync(targetDir));
	}
}