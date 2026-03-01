using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Google.Apis.Download;
using Google.Apis.Drive.v3;
using Google.Apis.Upload;
using Shintio.FileSystem.Abstractions;

namespace Shintio.FileSystem.GoogleDrive;

public class FileSystem : IFileSystem
{
	private const string FolderMimeType = "application/vnd.google-apps.folder";

	private readonly DriveService _driveService;
	private readonly string _rootFolderId;
	private readonly GoogleDriveFileSystemOptions _options;
	private readonly ConcurrentDictionary<string, string> _directoryIdCache;

	public FileSystem(
		DriveService driveService,
		string rootFolderId = "root",
		GoogleDriveFileSystemOptions? options = null
	)
	{
		_driveService = driveService ?? throw new ArgumentNullException(nameof(driveService));
		_rootFolderId = string.IsNullOrWhiteSpace(rootFolderId) ? "root" : rootFolderId;
		_options = options ?? new GoogleDriveFileSystemOptions();
		_directoryIdCache = new ConcurrentDictionary<string, string>(StringComparer.Ordinal)
		{
			["/"] = _rootFolderId
		};
	}

	public string GetFullPath(string path)
	{
		return NormalizePath(path, absolute: true);
	}

	public string Combine(params string[] parts)
	{
		if (parts == null)
		{
			throw new ArgumentNullException(nameof(parts));
		}

		return CombineParts(parts);
	}

	public async Task<bool> ExistsAsync(string path, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();

		var node = await TryResolveNodeAsync(GetFullPath(path), cancellationToken);

		return node != null;
	}

	public async Task DeleteAsync(string path, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();

		var normalizedPath = GetFullPath(path);
		var node = await TryResolveNodeAsync(normalizedPath, cancellationToken);
		if (node == null)
		{
			return;
		}

		if (node.Id == _rootFolderId)
		{
			throw new InvalidOperationException("Cannot delete adapter root folder.");
		}

		await DeleteNodeAsync(node.Id, cancellationToken);
		if (node.IsFolder)
		{
			InvalidateDirectoryCacheSubtree(normalizedPath);
		}
	}

	public async Task CopyAsync(string from, string to, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();

		var source = await TryResolveNodeAsync(GetFullPath(from), cancellationToken);
		if (source == null)
		{
			return;
		}

		if (source.IsFolder)
		{
			var destinationFolderId = await ResolveDirectoryCopyDestinationAsync(to, cancellationToken);
			await CopyDirectoryContentsAsync(source.Id, destinationFolderId, cancellationToken);

			return;
		}

		await CopyFileFromNodeAsync(source, to, cancellationToken);
	}

	public async Task MoveAsync(string from, string to, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();

		var source = await TryResolveNodeAsync(GetFullPath(from), cancellationToken);
		if (source == null)
		{
			return;
		}

		if (source.IsFolder)
		{
			var destinationFolderId = await ResolveDirectoryCopyDestinationAsync(to, cancellationToken);
			await CopyDirectoryContentsAsync(source.Id, destinationFolderId, cancellationToken);
			await DeleteNodeAsync(source.Id, cancellationToken);
			ClearDirectoryCache();

			return;
		}

		var destination = await ResolveFileDestinationAsync(source.Name, to, cancellationToken);
		var existingTarget = destination.ExistingTarget ??
		                     await FindChildByNameAsync(destination.ParentId, destination.Name, cancellationToken);
		if (existingTarget != null)
		{
			if (existingTarget.IsFolder)
			{
				throw new IOException(
					$"Cannot move file '{source.Name}' to '{to}' because destination is a directory.");
			}

			if (existingTarget.Id == source.Id)
			{
				return;
			}

			await DeleteNodeAsync(existingTarget.Id, cancellationToken);
		}

		await MoveFileNodeAsync(source, destination.ParentId, destination.Name, cancellationToken);
	}

	public async Task RenameAsync(string from, string newName, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();

		if (string.IsNullOrWhiteSpace(newName))
		{
			throw new ArgumentException("New name cannot be empty.", nameof(newName));
		}

		if (newName.IndexOfAny(new[] { '/', '\\' }) >= 0)
		{
			throw new ArgumentException("New name cannot contain path separators.", nameof(newName));
		}

		var source = await TryResolveNodeAsync(GetFullPath(from), cancellationToken);
		if (source == null)
		{
			return;
		}

		var parentId = source.ParentId ?? _rootFolderId;
		var siblingWithSameName = await FindChildByNameAsync(parentId, newName, cancellationToken);
		if (siblingWithSameName != null && siblingWithSameName.Id != source.Id)
		{
			throw new IOException($"Item '{newName}' already exists in destination directory.");
		}

		var updateRequest = _driveService.Files.Update(
			new Google.Apis.Drive.v3.Data.File { Name = newName },
			source.Id
		);
		updateRequest.SupportsAllDrives = true;
		await updateRequest.ExecuteAsync(cancellationToken);
		if (source.IsFolder)
		{
			ClearDirectoryCache();
		}
	}

	public Task CreateDirectoryAsync(string path, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();

		return EnsureDirectoryByPathAsync(GetFullPath(path), cancellationToken);
	}

	public async Task CopyAllFilesAsync(string from, string to, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();

		var source = await TryResolveNodeAsync(GetFullPath(from), cancellationToken);
		if (source == null || !source.IsFolder)
		{
			return;
		}

		var destinationRootPath = GetFullPath(to);
		await CopyAllFilesRecursiveAsync(source.Id, destinationRootPath, string.Empty, cancellationToken);
	}

	public async Task CreateFileAsync(string path, byte[] content, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();

		if (content == null)
		{
			throw new ArgumentNullException(nameof(content));
		}

		var normalizedPath = GetFullPath(path);
		var fileName = GetFileName(normalizedPath);
		var parentFolderPath = GetParentPath(normalizedPath);
		var parentFolderId = await EnsureDirectoryByPathAsync(parentFolderPath, cancellationToken);

		var existing = await FindChildByNameAsync(parentFolderId, fileName, cancellationToken);
		if (existing != null)
		{
			if (existing.IsFolder)
			{
				throw new IOException(
					$"Cannot create file '{normalizedPath}' because a directory already exists at this path.");
			}

			await DeleteNodeAsync(existing.Id, cancellationToken);
		}

		await UploadFileAsync(parentFolderId, fileName, content, cancellationToken);
	}

	public async Task<byte[]> ReadFileAsync(string path, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();

		var normalizedPath = GetFullPath(path);
		var node = await TryResolveNodeAsync(normalizedPath, cancellationToken);
		if (node == null || node.IsFolder)
		{
			throw new FileNotFoundException($"File '{normalizedPath}' was not found.");
		}

		return await DownloadFileAsync(node.Id, cancellationToken);
	}

	private async Task CopyAllFilesRecursiveAsync(
		string sourceFolderId,
		string destinationRootPath,
		string relativePath,
		CancellationToken cancellationToken
	)
	{
		var children = await ListChildrenAsync(sourceFolderId, cancellationToken);
		foreach (var child in children)
		{
			cancellationToken.ThrowIfCancellationRequested();

			if (child.IsFolder)
			{
				var nestedRelative = string.IsNullOrEmpty(relativePath)
					? child.Name
					: Combine(relativePath, child.Name);

				await CopyAllFilesRecursiveAsync(child.Id, destinationRootPath, nestedRelative, cancellationToken);
			}
			else
			{
				var destinationDirectoryPath = string.IsNullOrEmpty(relativePath)
					? destinationRootPath
					: Combine(destinationRootPath, relativePath);
				var destinationParentId = await EnsureDirectoryByPathAsync(destinationDirectoryPath, cancellationToken);
				await CopyFileByIdAsync(child.Id, child.Name, destinationParentId, cancellationToken);
			}
		}
	}

	private async Task CopyDirectoryContentsAsync(
		string sourceFolderId,
		string destinationFolderId,
		CancellationToken cancellationToken
	)
	{
		var children = await ListChildrenAsync(sourceFolderId, cancellationToken);

		foreach (var child in children)
		{
			cancellationToken.ThrowIfCancellationRequested();

			if (child.IsFolder)
			{
				var destinationChild = await FindChildByNameAsync(destinationFolderId, child.Name, cancellationToken);
				if (destinationChild == null)
				{
					destinationChild = await CreateFolderAsync(destinationFolderId, child.Name, cancellationToken);
				}
				else if (!destinationChild.IsFolder)
				{
					throw new IOException(
						$"Cannot copy directory '{child.Name}' because destination contains a file with the same name."
					);
				}

				await CopyDirectoryContentsAsync(child.Id, destinationChild.Id, cancellationToken);
			}
			else
			{
				await CopyFileByIdAsync(child.Id, child.Name, destinationFolderId, cancellationToken);
			}
		}
	}

	private async Task CopyFileFromNodeAsync(
		DriveNode source,
		string destinationPath,
		CancellationToken cancellationToken
	)
	{
		var destination = await ResolveFileDestinationAsync(source.Name, destinationPath, cancellationToken);

		var existingTarget = destination.ExistingTarget ??
		                     await FindChildByNameAsync(destination.ParentId, destination.Name, cancellationToken);
		if (existingTarget != null)
		{
			if (existingTarget.IsFolder)
			{
				throw new IOException(
					$"Cannot copy file '{source.Name}' to '{destinationPath}' because destination is a directory.");
			}

			await DeleteNodeAsync(existingTarget.Id, cancellationToken);
		}

		await CopyFileByIdAsync(source.Id, destination.Name, destination.ParentId, cancellationToken);
	}

	private async Task CopyFileByIdAsync(
		string sourceFileId,
		string targetName,
		string targetParentId,
		CancellationToken cancellationToken
	)
	{
		var existing = await FindChildByNameAsync(targetParentId, targetName, cancellationToken);
		if (existing != null)
		{
			if (existing.IsFolder)
			{
				throw new IOException($"Cannot overwrite destination '{targetName}' because it is a directory.");
			}

			await DeleteNodeAsync(existing.Id, cancellationToken);
		}

		var copyRequest = _driveService.Files.Copy(
			new Google.Apis.Drive.v3.Data.File
			{
				Name = targetName,
				Parents = new[] { targetParentId }
			},
			sourceFileId
		);
		copyRequest.SupportsAllDrives = true;
		copyRequest.Fields = "id";
		await copyRequest.ExecuteAsync(cancellationToken);
	}

	private async Task MoveFileNodeAsync(
		DriveNode source,
		string destinationParentId,
		string destinationName,
		CancellationToken cancellationToken
	)
	{
		var updateRequest = _driveService.Files.Update(
			new Google.Apis.Drive.v3.Data.File { Name = destinationName },
			source.Id
		);
		updateRequest.SupportsAllDrives = true;
		updateRequest.AddParents = destinationParentId;
		if (source.Parents.Length > 0)
		{
			updateRequest.RemoveParents = string.Join(",", source.Parents);
		}

		await updateRequest.ExecuteAsync(cancellationToken);
	}

	private async Task<string> ResolveDirectoryCopyDestinationAsync(
		string destinationPath,
		CancellationToken cancellationToken
	)
	{
		var normalizedDestinationPath = GetFullPath(destinationPath);
		var existing = await TryResolveNodeAsync(normalizedDestinationPath, cancellationToken);
		if (existing != null)
		{
			if (!existing.IsFolder)
			{
				throw new IOException($"Cannot copy directory to '{destinationPath}' because destination is a file.");
			}

			return existing.Id;
		}

		return await EnsureDirectoryByPathAsync(normalizedDestinationPath, cancellationToken);
	}

	private async Task<FileDestination> ResolveFileDestinationAsync(
		string sourceName,
		string destinationPath,
		CancellationToken cancellationToken
	)
	{
		var normalizedDestinationPath = GetFullPath(destinationPath);
		var existingDestination = await TryResolveNodeAsync(normalizedDestinationPath, cancellationToken);
		if (existingDestination != null)
		{
			if (existingDestination.IsFolder)
			{
				return new FileDestination(existingDestination.Id, sourceName, null);
			}

			return new FileDestination(existingDestination.ParentId ?? _rootFolderId, existingDestination.Name,
				existingDestination);
		}

		if (IsDirectoryHint(destinationPath))
		{
			var folderId = await EnsureDirectoryByPathAsync(normalizedDestinationPath, cancellationToken);
			return new FileDestination(folderId, sourceName, null);
		}

		var parentPath = GetParentPath(normalizedDestinationPath);
		var fileName = GetFileName(normalizedDestinationPath);
		var parentId = await EnsureDirectoryByPathAsync(parentPath, cancellationToken);
		var existingTarget = await FindChildByNameAsync(parentId, fileName, cancellationToken);
		return new FileDestination(parentId, fileName, existingTarget);
	}

	private async Task<string> EnsureDirectoryByPathAsync(
		string normalizedAbsolutePath,
		CancellationToken cancellationToken
	)
	{
		if (TryGetCachedDirectoryId(normalizedAbsolutePath, out var cachedDirectoryId))
		{
			return cachedDirectoryId;
		}

		var segments = SplitPathSegments(normalizedAbsolutePath);
		var parentId = _rootFolderId;
		var currentPath = "/";

		foreach (var segment in segments)
		{
			cancellationToken.ThrowIfCancellationRequested();
			currentPath = CombinePath(currentPath, segment);

			if (TryGetCachedDirectoryId(currentPath, out var cachedSegmentId))
			{
				parentId = cachedSegmentId;
				continue;
			}

			var existing = await FindChildByNameAsync(parentId, segment, cancellationToken);
			if (existing == null)
			{
				existing = await CreateFolderAsync(parentId, segment, cancellationToken);
			}
			else if (!existing.IsFolder)
			{
				throw new IOException(
					$"Cannot create directory '{normalizedAbsolutePath}' because '{segment}' already exists as file."
				);
			}

			parentId = existing.Id;
			SetCachedDirectoryId(currentPath, parentId);
		}

		return parentId;
	}

	private async Task<DriveNode?> TryResolveNodeAsync(
		string normalizedAbsolutePath,
		CancellationToken cancellationToken
	)
	{
		var segments = SplitPathSegments(normalizedAbsolutePath);
		if (segments.Length == 0)
		{
			return new DriveNode(_rootFolderId, string.Empty, FolderMimeType, Array.Empty<string>());
		}

		var parentId = _rootFolderId;
		DriveNode? current = null;
		var currentPath = "/";

		for (var i = 0; i < segments.Length; i++)
		{
			cancellationToken.ThrowIfCancellationRequested();
			currentPath = CombinePath(currentPath, segments[i]);

			if (TryGetCachedDirectoryId(currentPath, out var cachedSegmentId))
			{
				var parentPath = GetParentPath(currentPath);
				var parentDirectoryId = TryGetCachedDirectoryId(parentPath, out var cachedParentId)
					? cachedParentId
					: _rootFolderId;

				current = new DriveNode(cachedSegmentId, segments[i], FolderMimeType, new[] { parentDirectoryId });
				parentId = cachedSegmentId;
				continue;
			}

			var next = await FindChildByNameAsync(parentId, segments[i], cancellationToken);
			if (next == null)
			{
				return null;
			}

			if (i < segments.Length - 1 && !next.IsFolder)
			{
				return null;
			}

			current = next;
			if (next.IsFolder)
			{
				SetCachedDirectoryId(currentPath, next.Id);
			}
			parentId = next.Id;
		}

		return current;
	}

	private async Task<IReadOnlyList<DriveNode>> ListChildrenAsync(string parentId, CancellationToken cancellationToken)
	{
		var list = new List<DriveNode>();
		string? pageToken = null;

		do
		{
			var request = _driveService.Files.List();
			request.SupportsAllDrives = true;
			if (_options.UseAllDrivesSearch)
			{
				request.IncludeItemsFromAllDrives = true;
				request.Corpora = "allDrives";
			}
			request.Q = $"'{EscapeQueryValue(parentId)}' in parents and trashed = false";
			request.Fields = "nextPageToken, files(id, name, mimeType, parents)";
			request.PageToken = pageToken;

			var response = await request.ExecuteAsync(cancellationToken);
			if (response.Files != null)
			{
				list.AddRange(response.Files.Select(ToNode));
			}

			pageToken = response.NextPageToken;
		} while (!string.IsNullOrEmpty(pageToken));

		return list;
	}

	private async Task<DriveNode?> FindChildByNameAsync(
		string parentId,
		string name,
		CancellationToken cancellationToken
	)
	{
		string? pageToken = null;

		do
		{
			var request = _driveService.Files.List();
			request.SupportsAllDrives = true;
			if (_options.UseAllDrivesSearch)
			{
				request.IncludeItemsFromAllDrives = true;
				request.Corpora = "allDrives";
			}
			request.Q =
				$"'{EscapeQueryValue(parentId)}' in parents and name = '{EscapeQueryValue(name)}' and trashed = false";
			request.Fields = "nextPageToken, files(id, name, mimeType, parents)";
			request.PageSize = 200;
			request.PageToken = pageToken;

			var response = await request.ExecuteAsync(cancellationToken);
			var file = response.Files?.FirstOrDefault();
			if (file != null)
			{
				return ToNode(file);
			}

			pageToken = response.NextPageToken;
		} while (!string.IsNullOrEmpty(pageToken));

		return null;
	}

	private async Task<DriveNode> CreateFolderAsync(string parentId, string name, CancellationToken cancellationToken)
	{
		var createRequest = _driveService.Files.Create(
			new Google.Apis.Drive.v3.Data.File
			{
				Name = name,
				MimeType = FolderMimeType,
				Parents = new[] { parentId }
			}
		);
		createRequest.SupportsAllDrives = true;
		createRequest.Fields = "id, name, mimeType, parents";
		var created = await createRequest.ExecuteAsync(cancellationToken);

		return ToNode(created);
	}

	private async Task DeleteNodeAsync(string nodeId, CancellationToken cancellationToken)
	{
		var deleteRequest = _driveService.Files.Delete(nodeId);
		deleteRequest.SupportsAllDrives = true;
		await deleteRequest.ExecuteAsync(cancellationToken);
	}

	private async Task UploadFileAsync(
		string parentId,
		string fileName,
		byte[] content,
		CancellationToken cancellationToken
	)
	{
		await using var stream = new MemoryStream(content, writable: false);
		var createRequest = _driveService.Files.Create(
			new Google.Apis.Drive.v3.Data.File
			{
				Name = fileName,
				Parents = new[] { parentId }
			},
			stream,
			"application/octet-stream"
		);
		createRequest.SupportsAllDrives = true;
		createRequest.Fields = "id";

		var progress = await createRequest.UploadAsync(cancellationToken);
		if (progress.Status == UploadStatus.Failed)
		{
			throw new IOException("File upload to Google Drive failed.", progress.Exception);
		}
	}

	private async Task<byte[]> DownloadFileAsync(string fileId, CancellationToken cancellationToken)
	{
		var getRequest = _driveService.Files.Get(fileId);
		getRequest.SupportsAllDrives = true;

		await using var destination = new MemoryStream();
		var progress = await getRequest.DownloadAsync(destination, cancellationToken);
		if (progress.Status == DownloadStatus.Failed)
		{
			throw new IOException("File download from Google Drive failed.", progress.Exception);
		}

		return destination.ToArray();
	}

	private static DriveNode ToNode(Google.Apis.Drive.v3.Data.File file)
	{
		if (file.Id == null || file.Name == null || file.MimeType == null)
		{
			throw new InvalidOperationException("Google Drive returned an invalid file descriptor.");
		}

		var parents = file.Parents?.Where(p => !string.IsNullOrWhiteSpace(p)).ToArray() ?? Array.Empty<string>();

		return new DriveNode(file.Id, file.Name, file.MimeType, parents);
	}

	private static string EscapeQueryValue(string value)
	{
		return value.Replace("\\", "\\\\").Replace("'", "\\'");
	}

	private static string[] SplitPathSegments(string normalizedAbsolutePath)
	{
		if (normalizedAbsolutePath == "/")
		{
			return Array.Empty<string>();
		}

		return normalizedAbsolutePath
			.Trim('/')
			.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
	}

	private static bool IsDirectoryHint(string path)
	{
		return !string.IsNullOrEmpty(path) && (path[^1] == '/' || path[^1] == '\\');
	}

	private static string GetParentPath(string normalizedAbsolutePath)
	{
		if (normalizedAbsolutePath == "/")
		{
			return "/";
		}

		var segments = SplitPathSegments(normalizedAbsolutePath);
		if (segments.Length <= 1)
		{
			return "/";
		}

		return "/" + string.Join("/", segments.Take(segments.Length - 1));
	}

	private static string GetFileName(string normalizedAbsolutePath)
	{
		var segments = SplitPathSegments(normalizedAbsolutePath);
		if (segments.Length == 0)
		{
			throw new IOException("Path points to root directory and does not contain a file name.");
		}

		return segments[^1];
	}

	private static string NormalizePath(string path, bool absolute)
	{
		if (path == null)
		{
			throw new ArgumentNullException(nameof(path));
		}

		var replaced = path.Replace('\\', '/');
		var isAbsolute = absolute || replaced.StartsWith("/", StringComparison.Ordinal);
		var segments = new List<string>();

		foreach (var part in replaced.Split('/', StringSplitOptions.RemoveEmptyEntries))
		{
			if (part == ".")
			{
				continue;
			}

			if (part == "..")
			{
				if (segments.Count > 0)
				{
					segments.RemoveAt(segments.Count - 1);
				}

				continue;
			}

			segments.Add(part);
		}

		var joined = string.Join("/", segments);
		if (isAbsolute)
		{
			return string.IsNullOrEmpty(joined) ? "/" : "/" + joined;
		}

		return joined;
	}

	private static string CombinePath(string basePath, string segment)
	{
		if (basePath == "/")
		{
			return "/" + segment;
		}

		return basePath + "/" + segment;
	}

	private bool TryGetCachedDirectoryId(string path, out string directoryId)
	{
		var normalized = NormalizePath(path, absolute: true);
		return _directoryIdCache.TryGetValue(normalized, out directoryId!);
	}

	private void SetCachedDirectoryId(string path, string directoryId)
	{
		var normalized = NormalizePath(path, absolute: true);
		_directoryIdCache[normalized] = directoryId;
	}

	private void InvalidateDirectoryCacheSubtree(string rootPath)
	{
		var normalizedRoot = NormalizePath(rootPath, absolute: true);
		foreach (var key in _directoryIdCache.Keys)
		{
			if (string.Equals(key, normalizedRoot, StringComparison.Ordinal) ||
			    key.StartsWith(normalizedRoot + "/", StringComparison.Ordinal))
			{
				_directoryIdCache.TryRemove(key, out _);
			}
		}

		_directoryIdCache.TryAdd("/", _rootFolderId);
	}

	private void ClearDirectoryCache()
	{
		_directoryIdCache.Clear();
		_directoryIdCache.TryAdd("/", _rootFolderId);
	}

	private static string CombineParts(IEnumerable<string> parts)
	{
		var result = string.Empty;

		foreach (var part in parts)
		{
			if (string.IsNullOrWhiteSpace(part))
			{
				continue;
			}

			var normalized = part.Replace('\\', '/');
			if (normalized.StartsWith("/", StringComparison.Ordinal))
			{
				result = NormalizePath(normalized, absolute: true);
				continue;
			}

			if (string.IsNullOrEmpty(result))
			{
				result = NormalizePath(normalized, absolute: false);
			}
			else
			{
				result = NormalizePath(result.TrimEnd('/') + "/" + normalized.TrimStart('/'),
					result.StartsWith("/", StringComparison.Ordinal));
			}
		}

		return result;
	}

	private readonly record struct FileDestination(string ParentId, string Name, DriveNode? ExistingTarget);

	private sealed record DriveNode(string Id, string Name, string MimeType, string[] Parents)
	{
		public bool IsFolder => MimeType == FolderMimeType;
		public string? ParentId => Parents.FirstOrDefault();
	}
}
