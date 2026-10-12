// HTTP/HTTPS同主机Cookie往返与CSRF隔离回归；仅自有Simulation数据库，无物理动作。
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;

namespace ScpCv.ControlHost.Tests;

public sealed class ProtocolAuthTests
{
    [Theory]
    [InlineData("http", "https")]
    [InlineData("https", "http")]
    public async Task RenamingTicketCannotAuthenticateAnotherProtocol(string original, string target)
    {
        using var baseFactory = new ControlHostApplicationFactory();
        using var factory = baseFactory.WithWebHostBuilder(builder => builder.UseSetting("Authentication:CrossSiteCookies", "false"));
        using var handler = new BrowserCookieHandler { InnerHandler = factory.Server.CreateHandler() };
        using var owner = new HttpClient(handler);
        await LoginAsync(owner, original);
        var ticket = handler.SessionTicket(original);
        using var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { HandleCookies = false, AllowAutoRedirect = false });
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{target}://localhost/api/auth/me/");
        request.Headers.Add("Cookie", $"{(target == "https" ? "sessionid" : "sessionid_http")}={ticket}");
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ThreeRoundTripsKeepProtocolCookiesAndLogoutIndependent(bool crossSiteCookies)
    {
        using var baseFactory = new ControlHostApplicationFactory();
        using var factory = baseFactory.WithWebHostBuilder(builder => builder.UseSetting("Authentication:CrossSiteCookies", crossSiteCookies.ToString()));
        using var handler = new BrowserCookieHandler { InnerHandler = factory.Server.CreateHandler() };
        using var client = new HttpClient(handler);
        for (var round = 0; round < 3; round++)
        {
            var http = await LoginAsync(client, "http", crossSiteCookies);
            var https = await LoginAsync(client, "https", crossSiteCookies);
            Assert.NotEqual(http, https);
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("http://localhost/api/auth/me/")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("https://localhost/api/auth/me/")).StatusCode);
            await ValidateWriteAsync(client, "http", http, "invalid_password");
            await ValidateWriteAsync(client, "https", https, "invalid_password");
            await ValidateWriteAsync(client, "http", https, "csrf_failed");
            await ValidateWriteAsync(client, "https", http, "csrf_failed");

            var first = round % 2 == 0 ? "https" : "http";
            var second = first == "https" ? "http" : "https";
            await LogoutAsync(client, first, first == "https" ? https : http);
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync($"{first}://localhost/api/auth/me/")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"{second}://localhost/api/auth/me/")).StatusCode);
            await LogoutAsync(client, second, second == "https" ? https : http);
        }
    }

    /// <summary>检查协议独立名称与Secure属性，返回对应CSRF。</summary>
    private static async Task<string> LoginAsync(HttpClient client, string protocol, bool crossSiteCookies = false)
    {
        using var csrf = await client.GetAsync($"{protocol}://localhost/api/auth/csrf/");
        csrf.EnsureSuccessStatusCode();
        using var payload = await JsonDocument.ParseAsync(await csrf.Content.ReadAsStreamAsync());
        var token = payload.RootElement.GetProperty("csrfToken").GetString()!;
        var cookie = Assert.Single(csrf.Headers.GetValues("Set-Cookie"));
        Assert.StartsWith(protocol == "https" ? "csrftoken=" : "csrftoken_http=", cookie, StringComparison.Ordinal);
        Assert.Equal(protocol == "https", cookie.Contains("; secure", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(protocol == "https" && crossSiteCookies ? "samesite=none" : "samesite=lax", cookie, StringComparison.OrdinalIgnoreCase);
        using var login = await client.PostAsJsonAsync($"{protocol}://localhost/api/auth/login/", new { username = "operator", password = "Old-password-123" });
        login.EnsureSuccessStatusCode();
        var session = Assert.Single(login.Headers.GetValues("Set-Cookie"), value => value.StartsWith("sessionid", StringComparison.Ordinal));
        Assert.StartsWith(protocol == "https" ? "sessionid=" : "sessionid_http=", session, StringComparison.Ordinal);
        Assert.Equal(protocol == "https", session.Contains("; secure", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("httponly", session, StringComparison.OrdinalIgnoreCase);
        return token;
    }

    /// <summary>错误旧密码仅检查正式认证/防伪后拒绝，不修改数据库密码。</summary>
    private static async Task ValidateWriteAsync(HttpClient client, string protocol, string token, string expectedCode)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{protocol}://localhost/api/auth/change-password/")
        {
            Content = JsonContent.Create(new { current_password = "wrong", new_password = "Valid-new-test-456" }),
        };
        request.Headers.Add("X-CSRFToken", token);
        request.Headers.Add("Origin", $"{protocol}://localhost");
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var payload = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        Assert.Equal(expectedCode, payload.RootElement.GetProperty("code").GetString());
    }

    /// <summary>注销只清当前协议会话及防伪Cookie。</summary>
    private static async Task LogoutAsync(HttpClient client, string protocol, string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{protocol}://localhost/api/auth/logout/");
        request.Headers.Add("X-CSRFToken", token);
        using var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var cookies = response.Headers.GetValues("Set-Cookie").ToArray();
        Assert.Contains(cookies, cookie => cookie.StartsWith(protocol == "https" ? "csrftoken=" : "csrftoken_http=", StringComparison.Ordinal));
        Assert.DoesNotContain(cookies, cookie => cookie.StartsWith(protocol == "https" ? "csrftoken_http=" : "csrftoken=", StringComparison.Ordinal));
    }

    /// <summary>同一主机的浏览器Cookie罐，不按协议/端口错误地分成两个测试客户端。</summary>
    private sealed class BrowserCookieHandler : DelegatingHandler
    {
        private readonly CookieContainer _cookies = new();

        /// <summary>只在内存取本测试签发的会话，绝不输出到日志。</summary>
        public string SessionTicket(string protocol) => _cookies.GetCookies(new Uri($"{protocol}://localhost"))
            [protocol == "https" ? "sessionid" : "sessionid_http"]!.Value;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var uri = request.RequestUri!;
            var header = _cookies.GetCookieHeader(uri);
            if (header.Length > 0) request.Headers.Add("Cookie", header);
            var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (response.Headers.TryGetValues("Set-Cookie", out var values))
            {
                foreach (var value in values) _cookies.SetCookies(uri, value);
            }
            return response;
        }
    }
}
