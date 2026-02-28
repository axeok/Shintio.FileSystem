using System.Text;

namespace Shintio.FileSystem.Abstractions.Sync;

public static class FileSystemSyncExtensions
{
	extension(IFileSystem fileSystem)
	{
		public bool Exists(string path)
		{
			return fileSystem.ExistsAsync(path).ConfigureAwait(false).GetAwaiter().GetResult();
		}

		public void Delete(string path)
		{
			fileSystem.DeleteAsync(path).ConfigureAwait(false).GetAwaiter().GetResult();
		}

		public void Copy(string from, string to)
		{
			fileSystem.CopyAsync(from, to).ConfigureAwait(false).GetAwaiter().GetResult();
		}

		public void Move(string from, string to)
		{
			fileSystem.MoveAsync(from, to).ConfigureAwait(false).GetAwaiter().GetResult();
		}

		public void Rename(string from, string newName)
		{
			fileSystem.RenameAsync(from, newName).ConfigureAwait(false).GetAwaiter().GetResult();
		}

		public void CreateDirectory(string path)
		{
			fileSystem.CreateDirectoryAsync(path).ConfigureAwait(false).GetAwaiter().GetResult();
		}

		public void CopyAllFiles(string from, string to)
		{
			fileSystem.CopyAllFilesAsync(from, to).ConfigureAwait(false).GetAwaiter().GetResult();
		}

		public void CreateFile(string path, byte[] content)
		{
			fileSystem.CreateFileAsync(path, content).ConfigureAwait(false).GetAwaiter().GetResult();
		}

		public void CreateFile(string path, string content)
		{
			fileSystem.CreateFile(path, Encoding.UTF8.GetBytes(content));
		}

		public byte[] ReadFile(string path)
		{
			return fileSystem.ReadFileAsync(path).ConfigureAwait(false).GetAwaiter().GetResult();
		}

		public string ReadFileText(string path)
		{
			return Encoding.UTF8.GetString(fileSystem.ReadFile(path));
		}
	}
}