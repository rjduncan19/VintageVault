using System.Net;
using System.Text;
using Microsoft.Graph;
using Microsoft.Graph.Models.ODataErrors;
using Microsoft.Kiota.Abstractions.Authentication;
using VintageVault.Cli.Graph;

namespace VintageVault.Tests;

/// <summary>
/// Tests DriveOperations against a stubbed HTTP layer, so the real Graph SDK request/response
/// handling (paths, pagination, error mapping) runs without a OneDrive account.
/// </summary>
public class DriveOperationsTests
{
    private const string BaseUrl = "https://graph.test/v1.0";

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;
        public List<(HttpMethod Method, string Url)> Requests { get; } = new();

        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) => _respond = respond;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            lock (Requests)
                Requests.Add((request.Method, Uri.UnescapeDataString(request.RequestUri!.ToString())));
            return Task.FromResult(_respond(request));
        }
    }

    private static (DriveOperations Ops, StubHandler Handler) Create(Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        var handler = new StubHandler(respond);
        var client = new GraphServiceClient(new HttpClient(handler), new AnonymousAuthenticationProvider(), BaseUrl);
        return (new DriveOperations(client), handler);
    }

    private static HttpResponseMessage Json(string json, HttpStatusCode status = HttpStatusCode.OK)
        => new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private static HttpResponseMessage NotFound()
        => Json("""{"error":{"code":"itemNotFound","message":"not found"}}""", HttpStatusCode.NotFound);

    private static string Url(HttpRequestMessage r) => Uri.UnescapeDataString(r.RequestUri!.ToString());

    #region GetDeltaAsync

    [Fact]
    public async Task GetDelta_FullEnumeration_FollowsNextLinksAndExtractsToken()
    {
        var (ops, handler) = Create(r => Url(r).Contains("page=2")
            ? Json($$"""
                {"value":[{"id":"3","name":"c.txt","file":{} }],
                 "@odata.deltaLink":"{{BaseUrl}}/drives/d1/items/root/delta?token=final-token"}
                """)
            : Json($$"""
                {"value":[{"id":"1","name":"a.txt","file":{} },{"id":"2","name":"b.txt","file":{} }],
                 "@odata.nextLink":"{{BaseUrl}}/drives/d1/items/root/delta?page=2"}
                """));

        var (changes, token) = await ops.GetDeltaAsync("d1");

        Assert.Equal(new[] { "1", "2", "3" }, changes.Select(c => c.Id));
        Assert.Equal("final-token", token);
        Assert.Equal(2, handler.Requests.Count);
        Assert.Contains("/drives/d1/items/root/delta", handler.Requests[0].Url);
    }

    [Fact]
    public async Task GetDelta_WithToken_RequestsIncrementalDelta()
    {
        var (ops, handler) = Create(_ => Json($$"""
            {"value":[],"@odata.deltaLink":"{{BaseUrl}}/drives/d1/items/root/delta?token=next"}
            """));

        var (changes, token) = await ops.GetDeltaAsync("d1", "prev-token");

        Assert.Empty(changes);
        Assert.Equal("next", token);
        Assert.Contains("prev-token", Assert.Single(handler.Requests).Url);
    }

    #endregion

    #region File and folder operations

    [Fact]
    public async Task ReadFileContent_ReturnsBody()
    {
        var (ops, handler) = Create(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"version\":1}")
        });

        var content = await ops.ReadFileContentAsync("d1", "folder-1", "manifest.json");

        Assert.Equal("{\"version\":1}", content);
        Assert.Contains("/drives/d1/items/folder-1:/manifest.json:/content", Assert.Single(handler.Requests).Url);
    }

    [Fact]
    public async Task ReadFileContent_NotFound_ReturnsNull()
    {
        var (ops, _) = Create(_ => NotFound());

        Assert.Null(await ops.ReadFileContentAsync("d1", "folder-1", "manifest.json"));
    }

    [Fact]
    public async Task EnsureBackupRoot_Exists_ReturnsItWithoutCreating()
    {
        var (ops, handler) = Create(_ => Json("""{"id":"root-backup","name":"VintageVault-Backup","folder":{}}"""));

        var item = await ops.EnsureBackupRootAsync("d1");

        Assert.Equal("root-backup", item?.Id);
        Assert.Equal(HttpMethod.Get, Assert.Single(handler.Requests).Method);
    }

    [Fact]
    public async Task EnsureBackupRoot_Missing_CreatesFolderUnderRoot()
    {
        var (ops, handler) = Create(r => r.Method == HttpMethod.Get
            ? NotFound()
            : Json("""{"id":"new-backup","name":"VintageVault-Backup","folder":{}}""", HttpStatusCode.Created));

        var item = await ops.EnsureBackupRootAsync("d1");

        Assert.Equal("new-backup", item?.Id);
        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal(HttpMethod.Post, handler.Requests[1].Method);
        Assert.Contains("/drives/d1/items/root/children", handler.Requests[1].Url);
    }

    [Fact]
    public async Task GetDriveInfo_MapsOwnerAndQuota()
    {
        var (ops, _) = Create(_ => Json("""
            {"id":"d1","owner":{"user":{"displayName":"Pat","id":"u1"}},
             "quota":{"total":1000,"used":250,"remaining":750}}
            """));

        var (owner, total, used, remaining) = await ops.GetDriveInfoAsync();

        Assert.Equal("Pat", owner);
        Assert.Equal(1000, total);
        Assert.Equal(250, used);
        Assert.Equal(750, remaining);
    }

    #endregion

    #region ExtractDeltaToken

    [Theory]
    [InlineData("https://graph.microsoft.com/v1.0/drives/d/items/root/delta?token=abc123", "abc123")]
    [InlineData("https://graph.microsoft.com/v1.0/delta?$select=id&token=a%2Fb%3D%3D", "a/b==")]
    [InlineData("https://graph.microsoft.com/v1.0/delta?TOKEN=upper", "upper")]
    [InlineData("https://graph.microsoft.com/v1.0/delta?other=1", null)]
    [InlineData("https://graph.microsoft.com/v1.0/delta", null)]
    [InlineData("not a url", null)]
    public void ExtractDeltaToken_ParsesTokenParameter(string link, string? expected)
    {
        Assert.Equal(expected, DriveOperations.ExtractDeltaToken(link));
    }

    #endregion

    #region Throttling retry

    private static readonly Func<TimeSpan, CancellationToken, Task> NoDelay = (_, _) => Task.CompletedTask;

    [Fact]
    public async Task Retry_On429_RetriesUntilSuccess()
    {
        int attempts = 0;
        var delays = new List<TimeSpan>();

        await DriveOperations.RetryOnThrottleAsync(() =>
        {
            if (++attempts < 3)
                throw new ODataError { ResponseStatusCode = 429 };
            return Task.CompletedTask;
        }, CancellationToken.None, (d, _) => { delays.Add(d); return Task.CompletedTask; });

        Assert.Equal(3, attempts);
        Assert.Equal(2, delays.Count);
    }

    [Fact]
    public async Task Retry_PersistentThrottling_GivesUpAndThrows()
    {
        int attempts = 0;

        var ex = await Assert.ThrowsAsync<ODataError>(() => DriveOperations.RetryOnThrottleAsync(() =>
        {
            attempts++;
            throw new ODataError { ResponseStatusCode = 429 };
        }, CancellationToken.None, NoDelay));

        Assert.Equal(429, ex.ResponseStatusCode);
        Assert.Equal(6, attempts); // initial attempt + 5 retries
    }

    [Fact]
    public async Task Retry_NonThrottleError_ThrowsImmediately()
    {
        int attempts = 0;

        await Assert.ThrowsAsync<ODataError>(() => DriveOperations.RetryOnThrottleAsync(() =>
        {
            attempts++;
            throw new ODataError { ResponseStatusCode = 500 };
        }, CancellationToken.None, NoDelay));

        Assert.Equal(1, attempts);
    }

    [Theory]
    [InlineData(0, 1000)]
    [InlineData(1, 2000)]
    [InlineData(4, 16000)]
    public void CalculateBackoff_IsExponentialWithUpTo50PercentJitter(int attempt, int baseMs)
    {
        for (int i = 0; i < 20; i++)
        {
            var delay = DriveOperations.CalculateBackoff(attempt).TotalMilliseconds;
            Assert.InRange(delay, baseMs, baseMs * 1.5);
        }
    }

    #endregion
}
