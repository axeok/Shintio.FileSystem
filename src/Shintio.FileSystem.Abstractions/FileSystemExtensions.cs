using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Shintio.FileSystem.Abstractions;

public static class FileSystemExtensions
{
	extension(IFileSystem fileSystem)
	{
		public Task CreateFileAsync(string path, string content, CancellationToken cancellationToken = default)
		{
			return fileSystem.CreateFileAsync(path, Encoding.UTF8.GetBytes(content), cancellationToken);
		}

		public async Task<string> ReadFileTextAsync(string path, CancellationToken cancellationToken = default)
		{
			var bytes = await fileSystem.ReadFileAsync(path, cancellationToken);
			return Encoding.UTF8.GetString(bytes);
		}
	}
}