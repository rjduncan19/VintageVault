using Microsoft.Graph;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Extensions.Msal;
using Microsoft.Kiota.Abstractions.Authentication;

namespace VintageVault.Cli.Graph;

public sealed class GraphClientFactory
{
    // Placeholder — replace with actual Entra app registration client ID
    public const string DefaultClientId = "YOUR-CLIENT-ID-HERE";

    private static readonly string[] s_scopes = { "Files.ReadWrite", "User.Read" };

    private const string CacheFileName = "vintagevault_msal_cache.bin";
    private const string KeychainService = "VintageVault";
    private const string KeychainAccount = "MSALCache";
    private const string LinuxKeyringSchema = "com.vintagevault.msalcache";
    private const string LinuxKeyringCollection = MsalCacheHelper.LinuxKeyRingDefaultCollection;
    private const string LinuxKeyringLabel = "VintageVault MSAL Token Cache";
    private static readonly KeyValuePair<string, string> LinuxKeyringAttr1 =
        new("Version", "1");
    private static readonly KeyValuePair<string, string> LinuxKeyringAttr2 =
        new("Product", "VintageVault");

    private readonly string _clientId;
    private IPublicClientApplication? _pca;

    public GraphClientFactory(string? clientId = null)
    {
        _clientId = clientId ?? DefaultClientId;
    }

    public async Task<IPublicClientApplication> GetPublicClientAppAsync()
    {
        if (_pca is not null)
            return _pca;

        _pca = PublicClientApplicationBuilder.Create(_clientId)
            .WithAuthority(AadAuthorityAudience.PersonalMicrosoftAccount)
            .WithDefaultRedirectUri()
            .Build();

        await RegisterCacheAsync(_pca).ConfigureAwait(false);
        return _pca;
    }

    public async Task<GraphServiceClient> CreateAuthenticatedClientAsync(
        Func<DeviceCodeResult, Task>? deviceCodeCallback = null)
    {
        var pca = await GetPublicClientAppAsync().ConfigureAwait(false);

        var authProvider = new BaseBearerTokenAuthenticationProvider(
            new MsalTokenProvider(pca, s_scopes, deviceCodeCallback));

        return new GraphServiceClient(authProvider);
    }

    public async Task<AuthenticationResult> AuthenticateWithDeviceCodeAsync(
        Func<DeviceCodeResult, Task> deviceCodeCallback)
    {
        var pca = await GetPublicClientAppAsync().ConfigureAwait(false);

        return await pca.AcquireTokenWithDeviceCode(s_scopes, deviceCodeCallback)
            .ExecuteAsync()
            .ConfigureAwait(false);
    }

    public async Task<AuthenticationResult?> AcquireTokenSilentAsync()
    {
        var pca = await GetPublicClientAppAsync().ConfigureAwait(false);
        var accounts = await pca.GetAccountsAsync().ConfigureAwait(false);
        var account = accounts.FirstOrDefault();
        if (account is null)
            return null;

        return await pca.AcquireTokenSilent(s_scopes, account)
            .ExecuteAsync()
            .ConfigureAwait(false);
    }

    private static async Task RegisterCacheAsync(IPublicClientApplication pca)
    {
        var cacheDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".vintagevault");

        Directory.CreateDirectory(cacheDir);

        var storageProperties = new StorageCreationPropertiesBuilder(
                CacheFileName, cacheDir)
            .WithMacKeyChain(KeychainService, KeychainAccount)
            .WithLinuxKeyring(
                LinuxKeyringSchema,
                LinuxKeyringCollection,
                LinuxKeyringLabel,
                LinuxKeyringAttr1,
                LinuxKeyringAttr2)
            .Build();

        var cacheHelper = await MsalCacheHelper.CreateAsync(storageProperties)
            .ConfigureAwait(false);

        cacheHelper.RegisterCache(pca.UserTokenCache);
    }

    /// <summary>
    /// Token provider that uses MSAL to acquire tokens silently or via device code flow.
    /// NEVER logs or exposes token values.
    /// </summary>
    private sealed class MsalTokenProvider : IAccessTokenProvider
    {
        private readonly IPublicClientApplication _pca;
        private readonly string[] _scopes;
        private readonly Func<DeviceCodeResult, Task>? _deviceCodeCallback;

        public MsalTokenProvider(
            IPublicClientApplication pca,
            string[] scopes,
            Func<DeviceCodeResult, Task>? deviceCodeCallback)
        {
            _pca = pca;
            _scopes = scopes;
            _deviceCodeCallback = deviceCodeCallback;
        }

        public AllowedHostsValidator AllowedHostsValidator { get; } = new();

        public async Task<string> GetAuthorizationTokenAsync(
            Uri uri,
            Dictionary<string, object>? additionalAuthenticationContext = null,
            CancellationToken cancellationToken = default)
        {
            var accounts = await _pca.GetAccountsAsync().ConfigureAwait(false);
            var account = accounts.FirstOrDefault();

            AuthenticationResult result;

            if (account is not null)
            {
                try
                {
                    result = await _pca.AcquireTokenSilent(_scopes, account)
                        .ExecuteAsync(cancellationToken)
                        .ConfigureAwait(false);
                    return result.AccessToken;
                }
                catch (MsalUiRequiredException)
                {
                    // Fall through to device code
                }
            }

            if (_deviceCodeCallback is null)
                throw new InvalidOperationException(
                    "Authentication expired. Run: vintagevault auth");

            result = await _pca.AcquireTokenWithDeviceCode(_scopes, _deviceCodeCallback)
                .ExecuteAsync(cancellationToken)
                .ConfigureAwait(false);

            return result.AccessToken;
        }
    }
}
