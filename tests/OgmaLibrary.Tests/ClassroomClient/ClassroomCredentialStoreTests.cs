using Microsoft.Extensions.DependencyInjection;
using OgmaLibrary.Application.ClassroomClient;
using OgmaLibrary.Infrastructure.ClassroomClient;

namespace OgmaLibrary.Tests.ClassroomClient;

/// <summary>Phase 17 classroom credential-store tests.</summary>
public sealed class ClassroomCredentialStoreTests
{
    [Fact]
    public async Task PlatformCredentialStore_ScopesKeysBeforeCallingSecretBackend()
    {
        var backend = new RecordingSecretStore();
        var store = new PlatformClassroomCredentialStore(backend);

        await store.SaveSecretAsync("ogma.classroom.session.profile", "session-token");
        string? secret = await store.GetSecretAsync("ogma.classroom.session.profile");
        await store.DeleteSecretAsync("ogma.classroom.session.profile");

        Assert.Equal("session-token", secret);
        Assert.Equal(
            PlatformClassroomCredentialStore.CreateTarget("ogma.classroom.session.profile"),
            backend.LastSavedKey);
        Assert.Equal(PlatformClassroomCredentialStore.CreateTarget("ogma.classroom.session.profile"), backend.LastDeletedKey);
    }

    [Fact]
    public async Task WindowsCredentialStore_UsesInjectedCredentialManager()
    {
        var manager = new RecordingWindowsCredentialManager();
        var store = new WindowsCredentialManagerClassroomSecretStore(manager);

        await store.SaveAsync("Ogma:Classroom:test", "secret", CancellationToken.None);
        string? secret = await store.GetAsync("Ogma:Classroom:test", CancellationToken.None);
        await store.DeleteAsync("Ogma:Classroom:test", CancellationToken.None);

        Assert.Equal("secret", secret);
        Assert.Equal("Ogma:Classroom:test", manager.LastWrittenTarget);
        Assert.Equal("Ogma:Classroom:test", manager.LastDeletedTarget);
    }

    [Fact]
    public async Task MacOsCredentialStore_UsesGenericPasswordCommands()
    {
        var tool = new FakeMacOsSecurityTool();
        var store = new MacOsKeychainClassroomSecretStore(tool);

        await store.SaveAsync("Ogma:Classroom:test", "secret", CancellationToken.None);
        string? secret = await store.GetAsync("Ogma:Classroom:test", CancellationToken.None);
        await store.DeleteAsync("Ogma:Classroom:test", CancellationToken.None);

        Assert.Equal("secret", secret);
        Assert.Contains(tool.Commands, command => command[0] == "add-generic-password");
        Assert.Contains(tool.Commands, command => command[0] == "find-generic-password");
        Assert.Contains(tool.Commands, command => command[0] == "delete-generic-password");
        Assert.All(tool.Commands, command =>
        {
            Assert.Contains("-s", command);
            Assert.Contains(MacOsKeychainClassroomSecretStore.ServiceName, command);
        });
    }

    [Fact]
    public async Task LinuxSecretServiceStore_UsesSecretToolAttributes()
    {
        var tool = new FakeLinuxSecretTool();
        var store = new LinuxSecretServiceClassroomSecretStore(tool);

        await store.SaveAsync("Ogma:Classroom:test", "secret", CancellationToken.None);
        string? secret = await store.GetAsync("Ogma:Classroom:test", CancellationToken.None);
        await store.DeleteAsync("Ogma:Classroom:test", CancellationToken.None);

        Assert.Equal("secret", secret);
        Assert.Contains(tool.Commands, command => command.Arguments[0] == "store");
        Assert.Contains(tool.Commands, command => command.Arguments[0] == "lookup");
        Assert.Contains(tool.Commands, command => command.Arguments[0] == "clear");
        Assert.All(tool.Commands, command =>
        {
            Assert.Contains("service", command.Arguments);
            Assert.Contains(LinuxSecretServiceClassroomSecretStore.ServiceName, command.Arguments);
            Assert.Contains("key", command.Arguments);
            Assert.Contains("Ogma:Classroom:test", command.Arguments);
        });
        Assert.Contains(tool.Commands, command => command.StandardInput == "secret");
    }

    [Fact]
    public async Task FileCredentialFallback_PersistsAndRestrictsUnixFile()
    {
        string dataDirectory = CreateTempDirectory();

        try
        {
            string path = Path.Combine(dataDirectory, "secrets.json");
            var firstStore = new FileClassroomSecretStore(path);
            var secondStore = new FileClassroomSecretStore(path);

            await firstStore.SaveAsync("Ogma:Classroom:test", "secret", CancellationToken.None);
            string? secret = await secondStore.GetAsync("Ogma:Classroom:test", CancellationToken.None);

            Assert.Equal("secret", secret);
            Assert.True(File.Exists(path));
            if (!OperatingSystem.IsWindows())
            {
                Assert.Equal(
                    UnixFileMode.UserRead | UnixFileMode.UserWrite,
                    File.GetUnixFileMode(path));
            }
        }
        finally
        {
            CleanupTempDirectory(dataDirectory);
        }
    }

    [Fact]
    public void ClassroomClientServices_RegisterCredentialBackedTrustStore()
    {
        using ServiceProvider provider = new ServiceCollection()
            .AddClassroomClientServices(
                Path.Combine(Path.GetTempPath(), $"ogma-classroom-credentials-{Guid.NewGuid():N}"),
                new InMemoryClassroomCredentialStore())
            .BuildServiceProvider();

        Assert.IsType<CredentialBackedHostTrustStore>(provider.GetRequiredService<IHostTrustStore>());
    }

    private static string CreateTempDirectory()
    {
        string dataDirectory = Path.Combine(Path.GetTempPath(), $"ogma-classroom-credential-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dataDirectory);
        return dataDirectory;
    }

    private static void CleanupTempDirectory(string dataDirectory)
    {
        if (Directory.Exists(dataDirectory))
        {
            Directory.Delete(dataDirectory, recursive: true);
        }
    }

    private sealed class RecordingSecretStore : IClassroomSecretStore
    {
        private readonly Dictionary<string, string> _secrets = new(StringComparer.Ordinal);

        public string? LastSavedKey { get; private set; }

        public string? LastDeletedKey { get; private set; }

        public Task SaveAsync(string key, string value, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastSavedKey = key;
            _secrets[key] = value;
            return Task.CompletedTask;
        }

        public Task<string?> GetAsync(string key, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _secrets.TryGetValue(key, out string? value);
            return Task.FromResult(value);
        }

        public Task DeleteAsync(string key, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastDeletedKey = key;
            _secrets.Remove(key);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingWindowsCredentialManager : IWindowsCredentialManager
    {
        private readonly Dictionary<string, string> _secrets = new(StringComparer.Ordinal);

        public string? LastWrittenTarget { get; private set; }

        public string? LastDeletedTarget { get; private set; }

        public bool Write(string target, string secret)
        {
            LastWrittenTarget = target;
            _secrets[target] = secret;
            return true;
        }

        public string? Read(string target)
        {
            _secrets.TryGetValue(target, out string? value);
            return value;
        }

        public bool Delete(string target)
        {
            LastDeletedTarget = target;
            return _secrets.Remove(target);
        }
    }

    private sealed class FakeMacOsSecurityTool : IClassroomMacOsSecurityTool
    {
        private string? _storedSecret;

        public List<IReadOnlyList<string>> Commands { get; } = [];

        public Task<ClassroomMacOsSecurityToolResult> RunAsync(
            IReadOnlyList<string> arguments,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Commands.Add(arguments.ToArray());

            if (arguments.Count > 0 && arguments[0] == "find-generic-password")
            {
                return Task.FromResult(_storedSecret is null
                    ? new ClassroomMacOsSecurityToolResult(44, string.Empty, "not found")
                    : new ClassroomMacOsSecurityToolResult(0, _storedSecret + Environment.NewLine, string.Empty));
            }

            if (arguments.Count > 0 && arguments[0] == "add-generic-password")
            {
                int passwordIndex = -1;
                for (int index = 0; index < arguments.Count; index++)
                {
                    if (arguments[index] == "-w")
                    {
                        passwordIndex = index;
                        break;
                    }
                }

                Assert.True(passwordIndex >= 0 && passwordIndex + 1 < arguments.Count);
                _storedSecret = arguments[passwordIndex + 1];
                return Task.FromResult(new ClassroomMacOsSecurityToolResult(0, string.Empty, string.Empty));
            }

            if (arguments.Count > 0 && arguments[0] == "delete-generic-password")
            {
                _storedSecret = null;
                return Task.FromResult(new ClassroomMacOsSecurityToolResult(0, string.Empty, string.Empty));
            }

            return Task.FromResult(new ClassroomMacOsSecurityToolResult(64, string.Empty, "unsupported"));
        }
    }

    private sealed class FakeLinuxSecretTool : ILinuxSecretTool
    {
        private string? _storedSecret;

        public List<LinuxCommand> Commands { get; } = [];

        public Task<LinuxSecretToolResult> RunAsync(
            IReadOnlyList<string> arguments,
            string? standardInput,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Commands.Add(new LinuxCommand(arguments.ToArray(), standardInput));

            if (arguments.Count > 0 && arguments[0] == "lookup")
            {
                return Task.FromResult(_storedSecret is null
                    ? new LinuxSecretToolResult(1, string.Empty, "not found")
                    : new LinuxSecretToolResult(0, _storedSecret + Environment.NewLine, string.Empty));
            }

            if (arguments.Count > 0 && arguments[0] == "store")
            {
                _storedSecret = standardInput;
                return Task.FromResult(new LinuxSecretToolResult(0, string.Empty, string.Empty));
            }

            if (arguments.Count > 0 && arguments[0] == "clear")
            {
                _storedSecret = null;
                return Task.FromResult(new LinuxSecretToolResult(0, string.Empty, string.Empty));
            }

            return Task.FromResult(new LinuxSecretToolResult(64, string.Empty, "unsupported"));
        }
    }

    private sealed record LinuxCommand(IReadOnlyList<string> Arguments, string? StandardInput);
}
