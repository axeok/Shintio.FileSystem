using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using Microsoft.Extensions.Configuration;
using Shintio.FileSystem.Abstractions;
using Shintio.FileSystem.Abstractions.Sync;
using Xunit;

namespace Shintio.FileSystem.Tests;

public sealed class GoogleDriveFileSystemTests : FileSystemTestsBase, IClassFixture<GoogleDriveFileSystemFixture>,
	IDisposable
{
	private readonly GoogleDriveFileSystemFixture _fixture;

	protected override IFileSystem FileSystem => _fixture.FileSystem!;
	protected override string BasePath { get; }

	public GoogleDriveFileSystemTests(GoogleDriveFileSystemFixture fixture)
	{
		_fixture = fixture;
		BasePath = FileSystem.Combine(_fixture.RootPath, Guid.NewGuid().ToString("N"));
		FileSystem.CreateDirectory(BasePath);
	}

	public void Dispose()
	{
		if (FileSystem.Exists(BasePath))
		{
			FileSystem.Delete(BasePath);
		}
	}
}

public sealed class GoogleDriveFileSystemFixture : IDisposable
{
	private const string EnvClientSecretsPath = "SHINTIO_GOOGLEDRIVE_CLIENT_SECRETS_PATH";
	private const string EnvTokenDir = "SHINTIO_GOOGLEDRIVE_TOKEN_DIR";
	private const string EnvRootFolderId = "SHINTIO_GOOGLEDRIVE_ROOT_FOLDER_ID";
	private const string EnvApplicationName = "SHINTIO_GOOGLEDRIVE_APPLICATION_NAME";
	private const string EnvServiceAccountPath = "SHINTIO_GOOGLEDRIVE_SERVICE_ACCOUNT_JSON_PATH";
	private const string EnvOAuthClientId = "SHINTIO_GOOGLEDRIVE_OAUTH_CLIENT_ID";
	private const string EnvOAuthClientSecret = "SHINTIO_GOOGLEDRIVE_OAUTH_CLIENT_SECRET";

	public IFileSystem FileSystem { get; }
	public string RootPath { get; }

	public GoogleDriveFileSystemFixture()
	{
		var settings = LoadSettings();
		var driveService = CreateDriveService(settings);
		var rootFolderId = ResolveSetting(settings.RootFolderId, EnvRootFolderId) ?? "root";
		var options = new GoogleDrive.GoogleDriveFileSystemOptions
		{
			UseAllDrivesSearch = settings.UseAllDrivesSearch ?? false
		};

		FileSystem = new GoogleDrive.FileSystem(driveService, rootFolderId, options);
		RootPath = FileSystem.Combine("/.temp", "Shintio.FileSystem.Tests", Guid.NewGuid().ToString("N"));
		FileSystem.CreateDirectory(RootPath);
	}

	public void Dispose()
	{
		if (FileSystem.Exists(RootPath))
		{
			FileSystem.Delete(RootPath);
		}
	}

	private static DriveService CreateDriveService(GoogleDriveTestsSettings settings)
	{
		var applicationName = ResolveSetting(settings.ApplicationName, EnvApplicationName) ?? "Shintio.FileSystem.Tests";
		var serviceAccountPath = ResolveSetting(settings.ServiceAccountJsonPath, EnvServiceAccountPath);
		ICredential credential;

		if (!string.IsNullOrWhiteSpace(serviceAccountPath))
		{
			serviceAccountPath = ResolveFilePath(serviceAccountPath);

			if (!File.Exists(serviceAccountPath))
			{
				throw new InvalidOperationException($"Service account json was not found at '{serviceAccountPath}'.");
			}

			credential = GoogleCredential
				.FromFile(serviceAccountPath)
				.CreateScoped(DriveService.Scope.Drive);
		}
		else
		{
			credential = CreateOAuthCredential(settings);
		}

		return new DriveService(new BaseClientService.Initializer
		{
			HttpClientInitializer = credential,
			ApplicationName = applicationName
		});
	}

	private static ICredential CreateOAuthCredential(GoogleDriveTestsSettings settings)
	{
		var clientSecretsPath = ResolveSetting(settings.ClientSecretsPath, EnvClientSecretsPath);
		var oauthClientId = ResolveSetting(settings.OAuthClientId, EnvOAuthClientId);
		var oauthClientSecret = ResolveSetting(settings.OAuthClientSecret, EnvOAuthClientSecret);

		if (string.IsNullOrWhiteSpace(clientSecretsPath) && (string.IsNullOrWhiteSpace(oauthClientId) || string.IsNullOrWhiteSpace(oauthClientSecret)))
		{
			throw new InvalidOperationException(
				$"Set '{EnvClientSecretsPath}' or provide '{EnvOAuthClientId}' + '{EnvOAuthClientSecret}' to enable OAuth-based Google Drive tests."
			);
		}

		ClientSecrets secrets;

		if (!string.IsNullOrWhiteSpace(clientSecretsPath))
		{
			clientSecretsPath = ResolveFilePath(clientSecretsPath);

			if (!File.Exists(clientSecretsPath))
			{
				throw new InvalidOperationException($"OAuth client secrets file was not found at '{clientSecretsPath}'.");
			}

			using var stream = new FileStream(clientSecretsPath, FileMode.Open, FileAccess.Read);
			secrets = GoogleClientSecrets.FromStream(stream).Secrets;
		}
		else
		{
			secrets = new ClientSecrets
			{
				ClientId = oauthClientId!,
				ClientSecret = oauthClientSecret!
			};
		}

		var tokenDirectory = ResolveSetting(settings.TokenDirectory, EnvTokenDir)
		                     ?? Path.Combine(".temp", "google-drive-oauth-token");
		tokenDirectory = ResolveDirectoryPath(tokenDirectory);
		Directory.CreateDirectory(tokenDirectory);

		var flow = new GoogleAuthorizationCodeFlow(new GoogleAuthorizationCodeFlow.Initializer
		{
			ClientSecrets = secrets,
			Scopes = new[] { DriveService.Scope.Drive },
			DataStore = new FileDataStore(tokenDirectory, fullPath: true)
		});

		var installedApp = new AuthorizationCodeInstalledApp(flow, new LocalServerCodeReceiver());
		return installedApp.AuthorizeAsync("user", default).GetAwaiter().GetResult();
	}

	private static GoogleDriveTestsSettings LoadSettings()
	{
		var environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
		                  ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
		                  ?? "Production";

		var builder = new ConfigurationBuilder();
		var names = new[] { "appsettings.json", "appsettings.Production.json", $"appsettings.{environment}.json" };
		foreach (var directory in GetConfigDirectories())
		{
			foreach (var name in names)
			{
				var path = Path.Combine(directory, name);
				if (File.Exists(path))
				{
					builder.AddJsonFile(path, optional: true, reloadOnChange: false);
				}
			}
		}

		var configuration = builder.Build();

		var section = configuration.GetSection("GoogleDrive");
		return new GoogleDriveTestsSettings
		{
			ApplicationName = section["ApplicationName"],
			RootFolderId = section["RootFolderId"],
			ServiceAccountJsonPath = section["ServiceAccountJsonPath"],
			ClientSecretsPath = section["ClientSecretsPath"],
			OAuthClientId = section["OAuthClientId"],
			OAuthClientSecret = section["OAuthClientSecret"],
			TokenDirectory = section["TokenDirectory"],
			UseAllDrivesSearch = TryParseBoolean(section["UseAllDrivesSearch"])
		};
	}

	private static IReadOnlyList<string> GetConfigDirectories()
	{
		var directories = new List<string>();
		AddIfMissing(directories, AppContext.BaseDirectory);
		AddIfMissing(directories, Directory.GetCurrentDirectory());

		var projectDirectory = FindProjectDirectory();
		if (projectDirectory != null)
		{
			AddIfMissing(directories, projectDirectory);
		}

		return directories;
	}

	private static string? FindProjectDirectory()
	{
		static string? Walk(string startDirectory)
		{
			var current = new DirectoryInfo(startDirectory);
			while (current != null)
			{
				if (File.Exists(Path.Combine(current.FullName, "Shintio.FileSystem.Tests.csproj")))
				{
					return current.FullName;
				}

				current = current.Parent;
			}

			return null;
		}

		return Walk(AppContext.BaseDirectory) ?? Walk(Directory.GetCurrentDirectory());
	}

	private static void AddIfMissing(List<string> items, string directory)
	{
		var fullPath = Path.GetFullPath(directory);
		if (!items.Any(x => string.Equals(x, fullPath, StringComparison.OrdinalIgnoreCase)))
		{
			items.Add(fullPath);
		}
	}

	private static string? ResolveSetting(string? configuredValue, string envName)
	{
		var envValue = Environment.GetEnvironmentVariable(envName);
		if (!string.IsNullOrWhiteSpace(envValue))
		{
			return envValue;
		}

		return string.IsNullOrWhiteSpace(configuredValue) ? null : configuredValue;
	}

	private static string ResolveFilePath(string path)
	{
		return Path.IsPathFullyQualified(path)
			? path
			: Path.GetFullPath(path, Directory.GetCurrentDirectory());
	}

	private static string ResolveDirectoryPath(string path)
	{
		return Path.IsPathFullyQualified(path)
			? path
			: Path.GetFullPath(path, Directory.GetCurrentDirectory());
	}

	private static bool? TryParseBoolean(string? value)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return null;
		}

		return bool.TryParse(value, out var parsed) ? parsed : null;
	}

	private sealed class GoogleDriveTestsSettings
	{
		public string? ApplicationName { get; init; }
		public string? RootFolderId { get; init; }
		public string? ServiceAccountJsonPath { get; init; }
		public string? ClientSecretsPath { get; init; }
		public string? OAuthClientId { get; init; }
		public string? OAuthClientSecret { get; init; }
		public string? TokenDirectory { get; init; }
		public bool? UseAllDrivesSearch { get; init; }
	}
}
