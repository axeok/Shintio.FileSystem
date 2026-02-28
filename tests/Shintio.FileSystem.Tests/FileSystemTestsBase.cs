using Shintio.FileSystem.Abstractions;

namespace Shintio.FileSystem.Tests;

public abstract class FileSystemTestsBase
{
	protected abstract IFileSystem FileSystem { get; }
	protected abstract string BasePath { get; }

	[Fact]
	public void CreateFileBytes()
	{
		var content = new byte[] { 0, 50, 30, 225 };

		var filePath = FileSystem.Combine(BasePath, "test.bin");

		FileSystem.CreateFile(filePath, content);

		Assert.True(FileSystem.Exists(filePath));
		Assert.Equal(content, FileSystem.ReadFile(filePath));
	}

	[Fact]
	public void CreateDirectoryCreatesPath()
	{
		var dirPath = FileSystem.Combine(BasePath, "folder", "nested");

		FileSystem.CreateDirectory(dirPath);

		Assert.True(FileSystem.Exists(dirPath));
	}

	[Fact]
	public void CreateFileText()
	{
		var content = "Hello World!";

		var filePath = FileSystem.Combine(BasePath, "test.txt");

		FileSystem.CreateFile(filePath, content);

		Assert.True(FileSystem.Exists(filePath));
		Assert.Equal(content, FileSystem.ReadFileText(filePath));
	}

	[Fact]
	public void ExistsReturnsTrueForDirectoryAndFile()
	{
		var dirPath = FileSystem.Combine(BasePath, "dir");
		var filePath = FileSystem.Combine(dirPath, "file.txt");

		FileSystem.CreateDirectory(dirPath);
		FileSystem.CreateFile(filePath, "content");

		Assert.True(FileSystem.Exists(dirPath));
		Assert.True(FileSystem.Exists(filePath));
	}

	[Fact]
	public void DeleteRemovesFile()
	{
		var filePath = FileSystem.Combine(BasePath, "delete-me.txt");
		FileSystem.CreateFile(filePath, "content");

		FileSystem.Delete(filePath);

		Assert.False(FileSystem.Exists(filePath));
	}

	[Fact]
	public void DeleteRemovesDirectoryRecursively()
	{
		var dirPath = FileSystem.Combine(BasePath, "delete-dir");
		var nestedFilePath = FileSystem.Combine(dirPath, "nested", "file.txt");
		FileSystem.CreateFile(nestedFilePath, "content");

		FileSystem.Delete(dirPath);

		Assert.False(FileSystem.Exists(dirPath));
		Assert.False(FileSystem.Exists(nestedFilePath));
	}

	[Fact]
	public void CopyCopiesFile()
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
	public void CopyCopiesDirectoryRecursively()
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
	public void MoveMovesFile()
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
	public void MoveMovesDirectoryRecursively()
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
	public void RenameRenamesFile()
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
	public void RenameRenamesDirectory()
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
	public void CopyAllFilesCopiesNestedFiles()
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
	public void CopyAllFilesDoesNothingWhenSourceDoesNotExist()
	{
		var sourceDir = FileSystem.Combine(BasePath, "missing-source");
		var targetDir = FileSystem.Combine(BasePath, "target");

		FileSystem.CopyAllFiles(sourceDir, targetDir);

		Assert.False(FileSystem.Exists(targetDir));
	}
}
