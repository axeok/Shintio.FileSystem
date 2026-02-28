using System.Text;

namespace Shintio.FileSystem.Abstractions;

public static class FileSystemExtensions
{
	extension(IFileSystem fileSystem)
	{
		public void CreateFile(string path, string content)
		{
			fileSystem.CreateFile(path, Encoding.UTF8.GetBytes(content));
		}

		public string ReadFileText(string path)
		{
			return Encoding.UTF8.GetString(fileSystem.ReadFile(path));
		}
	}
}