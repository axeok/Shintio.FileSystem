using System.Threading;
using System.Threading.Tasks;

namespace Shintio.FileSystem.Abstractions;

public interface IFileSystem
{
	string GetFullPath(string path);
	string Combine(params string[] parts);

	Task<bool> ExistsAsync(string path, CancellationToken cancellationToken = default);

	Task DeleteAsync(string path, CancellationToken cancellationToken = default);
	Task CopyAsync(string from, string to, CancellationToken cancellationToken = default);
	Task MoveAsync(string from, string to, CancellationToken cancellationToken = default);
	Task RenameAsync(string from, string newName, CancellationToken cancellationToken = default);

	Task CreateDirectoryAsync(string path, CancellationToken cancellationToken = default);
	Task CopyAllFilesAsync(string from, string to, CancellationToken cancellationToken = default);

	Task CreateFileAsync(string path, byte[] content, CancellationToken cancellationToken = default);
	Task<byte[]> ReadFileAsync(string path, CancellationToken cancellationToken = default);
}