using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Shintio.FileSystem.Abstractions;

namespace Shintio.FileSystem.Physical;

public class FileSystem : IFileSystem
{
	public string GetFullPath(string path)
	{
		return Path.GetFullPath(path);
	}

	public string Combine(params string[] parts)
	{
		return Path.Combine(parts);
	}

	public Task<bool> ExistsAsync(string path, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();

		path = Path.GetFullPath(path);

		return Task.FromResult(File.Exists(path) || Directory.Exists(path));
	}

	public Task DeleteAsync(string path, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();

		path = Path.GetFullPath(path);

		if (File.Exists(path))
		{
			File.Delete(path);
		}
		else if (Directory.Exists(path))
		{
			Directory.Delete(path, true);
		}

		return Task.CompletedTask;
	}

	public Task CopyAsync(string from, string to, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();

		from = Path.GetFullPath(from);
		to = Path.GetFullPath(to);

		if (File.Exists(from))
		{
			CopyFile(from, to);
		}
		else if (Directory.Exists(from))
		{
			CopyDirectory(from, to);
		}

		return Task.CompletedTask;
	}

	public Task MoveAsync(string from, string to, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();

		from = Path.GetFullPath(from);
		to = Path.GetFullPath(to);

		if (File.Exists(from))
		{
			MoveFile(from, to);
		}
		else if (Directory.Exists(from))
		{
			MoveDirectory(from, to);
		}

		return Task.CompletedTask;
	}

	public Task RenameAsync(string from, string newName, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();

		from = Path.GetFullPath(from);

		var directoryName = Path.GetDirectoryName(from);
		var to = Path.GetFullPath(directoryName == null ? newName : Path.Combine(directoryName, newName));

		if (File.Exists(from))
		{
			File.Move(from, to);
		}
		else if (Directory.Exists(from))
		{
			Directory.Move(from, to);
		}

		return Task.CompletedTask;
	}

	public Task CreateDirectoryAsync(string path, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();

		path = Path.GetFullPath(path);

		Directory.CreateDirectory(path);

		return Task.CompletedTask;
	}

	public Task CopyAllFilesAsync(string from, string to, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();

		from = Path.GetFullPath(from);
		to = Path.GetFullPath(to);

		if (!Directory.Exists(from))
		{
			return Task.CompletedTask;
		}

		foreach (var filePath in Directory.EnumerateFiles(from, "*", SearchOption.AllDirectories))
		{
			cancellationToken.ThrowIfCancellationRequested();

			var fromPath = Path.GetRelativePath(from, filePath);
			var toPath = Path.Combine(to, fromPath);

			TryCreateDirectoryForFile(toPath);
			File.Copy(filePath, toPath, true);
		}

		return Task.CompletedTask;
	}

	public Task CreateFileAsync(string path, byte[] content, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();

		path = Path.GetFullPath(path);

		TryCreateDirectoryForFile(path);

		return File.WriteAllBytesAsync(path, content, cancellationToken);
	}

	public Task<byte[]> ReadFileAsync(string path, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		
		path = Path.GetFullPath(path);

		return File.ReadAllBytesAsync(path, cancellationToken);
	}

	private static void CopyFile(string from, string to)
	{
		if (Directory.Exists(to) || EndsWithDirectorySeparator(to))
		{
			Directory.CreateDirectory(to);
			var newFile = Path.Combine(to, Path.GetFileName(from));

			File.Copy(from, newFile, overwrite: true);

			return;
		}

		TryCreateDirectoryForFile(to);

		File.Copy(from, to, overwrite: true);
	}

	private static void CopyDirectory(string from, string to)
	{
		Directory.CreateDirectory(to);

		foreach (var file in Directory.EnumerateFiles(from))
		{
			var destFile = Path.Combine(to, Path.GetFileName(file));
			File.Copy(file, destFile, overwrite: true);
		}

		foreach (var subDir in Directory.EnumerateDirectories(from))
		{
			var destSubDir = Path.Combine(to, Path.GetFileName(subDir));
			CopyDirectory(subDir, destSubDir);
		}
	}

	private static void MoveFile(string from, string to)
	{
		if (Directory.Exists(to) || EndsWithDirectorySeparator(to))
		{
			Directory.CreateDirectory(to);
			var newFile = Path.Combine(to, Path.GetFileName(from));

			File.Move(from, newFile, overwrite: true);

			return;
		}

		TryCreateDirectoryForFile(to);

		File.Move(from, to, overwrite: true);
	}

	private static void MoveDirectory(string from, string to)
	{
		Directory.CreateDirectory(to);

		foreach (var file in Directory.EnumerateFiles(from))
		{
			var destFile = Path.Combine(to, Path.GetFileName(file));
			File.Move(file, destFile, overwrite: true);
		}

		foreach (var subDir in Directory.EnumerateDirectories(from))
		{
			var destSubDir = Path.Combine(to, Path.GetFileName(subDir));
			MoveDirectory(subDir, destSubDir);
		}

		Directory.Delete(from, recursive: false);
	}

	private static bool EndsWithDirectorySeparator(string path)
	{
		if (path.Length == 0)
		{
			return false;
		}

		var c = path[^1];

		return c == Path.DirectorySeparatorChar || c == Path.AltDirectorySeparatorChar;
	}

	private static void TryCreateDirectoryForFile(string path)
	{
		var directory = Path.GetDirectoryName(path);
		if (directory != null)
		{
			Directory.CreateDirectory(directory);
		}
	}
}