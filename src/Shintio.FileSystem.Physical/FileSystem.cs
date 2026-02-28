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

	public bool Exists(string path)
	{
		path = Path.GetFullPath(path);

		return File.Exists(path) || Directory.Exists(path);
	}

	public void Delete(string path)
	{
		path = Path.GetFullPath(path);

		if (File.Exists(path))
		{
			File.Delete(path);
		}
		else if (Directory.Exists(path))
		{
			Directory.Delete(path, true);
		}
	}

	public void Copy(string from, string to)
	{
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
	}

	public void Move(string from, string to)
	{
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
	}

	public void Rename(string from, string newName)
	{
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
	}

	public void CreateDirectory(string path)
	{
		path = Path.GetFullPath(path);

		Directory.CreateDirectory(path);
	}

	public void CopyAllFiles(string from, string to)
	{
		from = Path.GetFullPath(from);
		to = Path.GetFullPath(to);

		if (!Directory.Exists(from))
		{
			return;
		}

		foreach (var filePath in Directory.EnumerateFiles(from, "*", SearchOption.AllDirectories))
		{
			var fromPath = Path.GetRelativePath(from, filePath);
			var toPath = Path.Combine(to, fromPath);

			TryCreateDirectoryForFile(toPath);
			File.Copy(filePath, toPath, true);
		}
	}

	public void CreateFile(string path, byte[] content)
	{
		path = Path.GetFullPath(path);

		TryCreateDirectoryForFile(path);

		File.WriteAllBytes(path, content);
	}

	public byte[] ReadFile(string path)
	{
		path = Path.GetFullPath(path);

		return File.ReadAllBytes(path);
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