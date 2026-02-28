namespace Shintio.FileSystem.Abstractions;

public interface IFileSystem
{
	string GetFullPath(string path);
	string Combine(params string[] parts);

	bool Exists(string path);

	void Delete(string path);
	void Copy(string from, string to);
	void Move(string from, string to);
	void Rename(string from, string newName);

	void CreateDirectory(string path);
	void CopyAllFiles(string from, string to);

	void CreateFile(string path, byte[] content);
	byte[] ReadFile(string path);
}