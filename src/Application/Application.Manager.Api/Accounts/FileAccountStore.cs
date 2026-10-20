using System.Text.Json;

namespace Application.Manager.Api.Accounts;

/// <summary>
/// Accounts in memory, and in a JSON file when a path is given (no path: memory only, for tests and dev). Thread safe.
/// Every change is written to a temp file in the same folder, flushed to disk and then moved over the file, so the file is
/// always either the old or the new version. When that write fails the exception is thrown to the caller and the change
/// is not made in memory either: an account is never handed out that would be gone after a restart.
/// A file that cannot be read as accounts is never overwritten: it is renamed to <c>&lt;path&gt;.corrupt-&lt;utc time&gt;</c>
/// and the store starts empty. When the file cannot be read at all (locked, no access) or cannot be renamed aside, the constructor
/// throws instead of starting empty, because starting empty would overwrite it on the next change.
/// Every change rewrites the whole file: fine for the few thousand accounts this is meant for, Postgres is the plan beyond that.
/// </summary>
public sealed class FileAccountStore : IAccountStore
{
    private const int FileVersion = 1;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    private readonly string? _path;
    private readonly ILogger? _logger;
    private readonly object _lock = new();
    private AccountIndex _index = new();

    public FileAccountStore(string? path, ILogger? logger = null)
    {
        _path = string.IsNullOrWhiteSpace(path) ? null : path;
        _logger = logger;
        Reload();
    }

    /// <summary>Called with the temp file once it is written and flushed, right before it replaces the file. Tests use it to cut a write short.</summary>
    internal Action<string>? BeforeReplacingFile { get; set; }

    /// <summary>Throws away what is in memory and reads the file again.</summary>
    internal void Reload()
    {
        lock (_lock) _index = LoadIndex();
    }

    public Account? Find(Guid id)
    {
        lock (_lock) return _index.ById(id);
    }

    public Account? Find(string username, int tag)
    {
        ArgumentNullException.ThrowIfNull(username);
        lock (_lock) return _index.ByName(NameKey.Of(username, tag));
    }

    public Account? FindByExternalLogin(string provider, string idHash)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(idHash);
        lock (_lock) return _index.ByExternalLogin(LoginKey.Of(provider, idHash));
    }

    public Account? FindByDeviceTokenHash(string tokenHash)
    {
        ArgumentNullException.ThrowIfNull(tokenHash);
        lock (_lock) return _index.ByDeviceTokenHash(tokenHash);
    }

    public IReadOnlySet<int> TagsOf(string username)
    {
        ArgumentNullException.ThrowIfNull(username);
        lock (_lock) return _index.TagsOf(NameKey.Normalize(username));
    }

    /// <summary>
    /// Adds the account. False, and nothing changes, when its id, its username (any case) and tag, or one of its device
    /// token hashes is already used. Throws when it cannot be saved, and then nothing changes either.
    /// </summary>
    public bool TryAdd(Account account)
    {
        var stored = Snapshot(account);
        lock (_lock)
        {
            if (!_index.CanAdd(stored)) return false;
            Save(_index.All.Append(stored));
            _index.Add(stored);
            return true;
        }
    }

    /// <summary>
    /// Replaces the account with the same id. False, and nothing changes, when there is none, when its new username and tag
    /// belong to another account, or when one of its device token hashes does. Throws when it cannot be saved, and then nothing
    /// changes either.
    /// </summary>
    public bool Update(Account account)
    {
        var stored = Snapshot(account);
        lock (_lock)
        {
            var current = _index.ById(stored.Id);
            if (current is null || !_index.CanReplace(stored)) return false;
            Save(_index.All.Select(existing => existing.Id == stored.Id ? stored : existing));
            _index.Replace(current, stored);
            return true;
        }
    }

    private static Account Snapshot(Account account)
    {
        EnsureStorable(account);
        return account with { Devices = account.Devices.ToList().AsReadOnly() };
    }

    private static void EnsureStorable(Account account)
    {
        ArgumentNullException.ThrowIfNull(account);
        ArgumentException.ThrowIfNullOrWhiteSpace(account.Username, nameof(account.Username));
        ArgumentException.ThrowIfNullOrEmpty(account.PhraseHash, nameof(account.PhraseHash));
        if (account.Logins.Any(login => login is null || string.IsNullOrWhiteSpace(login.Provider) || string.IsNullOrWhiteSpace(login.IdHash)))
            throw new ArgumentException("Every social login needs a provider and an id.", nameof(account.ExternalLogins));
        ArgumentNullException.ThrowIfNull(account.Devices, nameof(account.Devices));
        if (!Tags.IsValid(account.Tag)) throw new ArgumentOutOfRangeException(nameof(account.Tag), account.Tag, "A tag is 0 to 99999.");
        if (account.Devices.Any(device => device is null || string.IsNullOrEmpty(device.Hash)))
            throw new ArgumentException("Every device needs a token hash.", nameof(account.Devices));
    }

    private AccountIndex LoadIndex()
    {
        if (_path is null || !File.Exists(_path)) return new AccountIndex();
        var text = File.ReadAllText(_path);
        try
        {
            return AccountIndex.Build(ParseAccounts(text));
        }
        catch (Exception exception) when (exception is JsonException or InvalidDataException or ArgumentException)
        {
            Quarantine(_path, exception);
            return new AccountIndex();
        }
    }

    private static IReadOnlyList<Account> ParseAccounts(string text)
    {
        var document = JsonSerializer.Deserialize<StoredDocument>(text, Json);
        if (document?.Accounts is null) throw new InvalidDataException("The file holds no account list.");
        var accounts = document.Accounts.Select(stored => stored?.ToAccount() ?? throw new InvalidDataException("An account is empty.")).ToList();
        accounts.ForEach(EnsureStorable);
        return accounts;
    }

    private void Quarantine(string path, Exception reason)
    {
        var target = $"{path}.corrupt-{DateTimeOffset.UtcNow:yyyyMMdd'T'HHmmssfff'Z'}";
        if (File.Exists(target)) target += "-" + Guid.NewGuid().ToString("N")[..8];
        File.Move(path, target);
        _logger?.LogError(reason, "The accounts file {Path} could not be read; it was kept as {QuarantinePath} and the store starts empty", path, target);
    }

    private void Save(IEnumerable<Account> accounts)
    {
        if (_path is null) return;
        var temp = $"{_path}.{Guid.NewGuid():N}.tmp";
        try
        {
            WriteAndFlush(temp, new StoredDocument(FileVersion, accounts.Select(StoredAccount.From).ToList<StoredAccount?>()));
            BeforeReplacingFile?.Invoke(temp);
            File.Move(temp, _path, overwrite: true);
        }
        catch (Exception exception)
        {
            DeleteQuietly(temp);
            _logger?.LogError(exception, "The accounts could not be saved to {Path}; the change was not made", _path);
            throw;
        }
    }

    private static void WriteAndFlush(string temp, StoredDocument document)
    {
        var folder = Path.GetDirectoryName(Path.GetFullPath(temp));
        if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);
        using var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        JsonSerializer.Serialize(stream, document, Json);
        stream.Flush(flushToDisk: true);
    }

    private static void DeleteQuietly(string temp)
    {
        try
        {
            File.Delete(temp);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }

    private readonly record struct LoginKey(string Provider, string IdHash)
    {
        public static LoginKey Of(string provider, string idHash) => new(provider.Trim().ToLowerInvariant(), idHash.Trim().ToLowerInvariant());
    }

    private readonly record struct NameKey(string Username, int Tag)
    {
        public static NameKey Of(string username, int tag) => new(Normalize(username), tag);

        public static string Normalize(string username) => username.Trim().ToLowerInvariant();
    }

    private sealed class AccountIndex
    {
        private readonly Dictionary<Guid, Account> _byId = new();
        private readonly Dictionary<NameKey, Guid> _byName = new();
        private readonly Dictionary<string, HashSet<int>> _tagsByName = new(StringComparer.Ordinal);
        private readonly Dictionary<string, Guid> _byDeviceTokenHash = new(StringComparer.Ordinal);
        private readonly Dictionary<LoginKey, Guid> _byExternalLogin = new();

        public IEnumerable<Account> All => _byId.Values;

        public static AccountIndex Build(IEnumerable<Account> accounts)
        {
            var index = new AccountIndex();
            foreach (var account in accounts) index.AddLoaded(account);
            return index;
        }

        public Account? ById(Guid id) => _byId.GetValueOrDefault(id);

        public Account? ByName(NameKey key) => _byName.TryGetValue(key, out var id) ? _byId[id] : null;

        public Account? ByExternalLogin(LoginKey key) => _byExternalLogin.TryGetValue(key, out var id) ? _byId[id] : null;

        public Account? ByDeviceTokenHash(string hash) => _byDeviceTokenHash.TryGetValue(hash, out var id) ? _byId[id] : null;

        public IReadOnlySet<int> TagsOf(string normalizedUsername) =>
            _tagsByName.TryGetValue(normalizedUsername, out var tags) ? new HashSet<int>(tags) : new HashSet<int>();

        public bool CanAdd(Account account) =>
            !_byId.ContainsKey(account.Id) && !_byName.ContainsKey(KeyOf(account)) && !HasDeviceOfAnother(account) && !HasLoginOfAnother(account);

        public bool CanReplace(Account account) => !HasNameOfAnother(account) && !HasDeviceOfAnother(account) && !HasLoginOfAnother(account);

        public void Add(Account account)
        {
            var key = KeyOf(account);
            _byId[account.Id] = account;
            _byName[key] = account.Id;
            TagsFor(key.Username).Add(key.Tag);
            foreach (var device in account.Devices) _byDeviceTokenHash[device.Hash] = account.Id;
            foreach (var login in account.Logins) _byExternalLogin[LoginKey.Of(login.Provider, login.IdHash)] = account.Id;
        }

        public void Replace(Account current, Account replacement)
        {
            Remove(current);
            Add(replacement);
        }

        private void AddLoaded(Account account)
        {
            if (!CanAdd(account)) throw new InvalidDataException($"Account {account.Id} repeats an id, a name and tag, or a device token of another account.");
            Add(account);
        }

        private void Remove(Account account)
        {
            var key = KeyOf(account);
            _byId.Remove(account.Id);
            _byName.Remove(key);
            RemoveTag(key);
            foreach (var device in account.Devices) _byDeviceTokenHash.Remove(device.Hash);
            foreach (var login in account.Logins) _byExternalLogin.Remove(LoginKey.Of(login.Provider, login.IdHash));
        }

        private void RemoveTag(NameKey key)
        {
            var tags = TagsFor(key.Username);
            tags.Remove(key.Tag);
            if (tags.Count == 0) _tagsByName.Remove(key.Username);
        }

        private HashSet<int> TagsFor(string normalizedUsername)
        {
            if (_tagsByName.TryGetValue(normalizedUsername, out var tags)) return tags;
            tags = [];
            _tagsByName[normalizedUsername] = tags;
            return tags;
        }

        private bool HasNameOfAnother(Account account) => _byName.TryGetValue(KeyOf(account), out var owner) && owner != account.Id;

        private bool HasLoginOfAnother(Account account) =>
            account.Logins.Any(login => _byExternalLogin.TryGetValue(LoginKey.Of(login.Provider, login.IdHash), out var owner) && owner != account.Id);

        private bool HasDeviceOfAnother(Account account) =>
            account.Devices.Any(device => _byDeviceTokenHash.TryGetValue(device.Hash, out var owner) && owner != account.Id);

        private static NameKey KeyOf(Account account) => NameKey.Of(account.Username, account.Tag);
    }

    private sealed record StoredDocument(int Version, List<StoredAccount?>? Accounts);

    private sealed record StoredAccount(Guid Id, string Username, int Tag, string PhraseHash, DateTimeOffset CreatedAt, List<StoredDevice?>? Devices, string? DisplayName = null, List<StoredLogin?>? ExternalLogins = null)
    {
        public static StoredAccount From(Account account) =>
            new(account.Id, account.Username, account.Tag, account.PhraseHash, account.CreatedAt, account.Devices.Select(StoredDevice.From).ToList<StoredDevice?>(), account.DisplayName,
                account.Logins.Count == 0 ? null : account.Logins.Select(StoredLogin.From).ToList<StoredLogin?>());

        public Account ToAccount() =>
            new(Id, Username, Tag, PhraseHash, CreatedAt, (Devices ?? throw new InvalidDataException($"Account {Id} has no device list.")).Select(ToDevice).ToList().AsReadOnly(), DisplayName,
                (ExternalLogins ?? []).Select(login => login?.ToLogin() ?? throw new InvalidDataException($"Account {Id} has an empty social login.")).ToList().AsReadOnly());

        private DeviceToken ToDevice(StoredDevice? device) =>
            device?.ToDevice() ?? throw new InvalidDataException($"Account {Id} has an empty device.");
    }

    private sealed record StoredLogin(string Provider, string IdHash, DateTimeOffset LinkedAt)
    {
        public static StoredLogin From(ExternalLogin login) => new(login.Provider, login.IdHash, login.LinkedAt);

        public ExternalLogin ToLogin() => new(Provider, IdHash, LinkedAt);
    }

    private sealed record StoredDevice(string Hash, DateTimeOffset CreatedAt, string? Label)
    {
        public static StoredDevice From(DeviceToken device) => new(device.Hash, device.CreatedAt, device.Label);

        public DeviceToken ToDevice() => new(Hash, CreatedAt, Label);
    }
}
