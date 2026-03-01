namespace Shintio.FileSystem.GoogleDrive;

public sealed class GoogleDriveFileSystemOptions
{
	/// <summary>
	/// Enables global search across all available drives.
	/// This is more flexible but usually slower than parent-scoped search.
	/// </summary>
	public bool UseAllDrivesSearch { get; init; }
}