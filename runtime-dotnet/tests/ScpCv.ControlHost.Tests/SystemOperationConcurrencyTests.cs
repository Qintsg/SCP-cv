// 并发HTTP启停不得交错执行状态重置与原生操作；只替代外部边界，不启动真实进程。
using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ScpCv.ControlHost.Runtime;
using ScpCv.Domain.Model;
using ScpCv.Infrastructure.Persistence;
using ScpCv.Infrastructure.Runtime;

namespace ScpCv.ControlHost.Tests;

public sealed class SystemOperationConcurrencyTests
{
    /// <summary>
    /// :param firstAction: 已持有运行组操作的请求。
    /// :param secondAction: 并发请求，不允许排队到另一代次后才执行。
    /// :returns: 并发请求409且不写意图/发外部动作；首请求完成后新请求仍可执行。
    /// </summary>
    [Theory]
    [InlineData("shutdown", "shutdown")]
    [InlineData("shutdown", "restart")]
    [InlineData("restart", "shutdown")]
    [InlineData("restart", "restart")]
    public async Task ConcurrentSystemRequestIsRejectedBeforeAnySideEffect(string firstAction, string secondAction)
    {
        var control = new BlockingSupervisor();
        using var factory = new ControlHostApplicationFactory();
        using var configured = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IRuntimeSupervisorControl>();
            services.AddSingleton<IRuntimeSupervisorControl>(control);
        }));
        using var client = configured.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"), HandleCookies = true,
        });
        var csrf = await AuthenticateAsync(client);
        var authority = configured.Services.GetRequiredService<RuntimeAuthorityRepository>();
        var requestId = Guid.NewGuid();
        var previous = await authority.BeginStartAsync(requestId);
        await authority.ArmAsync(requestId, previous.GroupEpoch);
        var contexts = configured.Services.GetRequiredService<IDbContextFactory<ControlDbContext>>();
        using var firstRequest = Request(firstAction, csrf);
        var first = client.SendAsync(firstRequest);
        HttpResponseMessage? firstResponse = null;
        try
        {
            await control.StopEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var blockedGroup = await authority.GetGroupAsync();
            await using var before = await contexts.CreateDbContextAsync();
            var commandCount = await before.CommandRecords.CountAsync();

            using var secondRequest = Request(secondAction, csrf);
            using var second = await client.SendAsync(secondRequest).WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
            using var body = JsonDocument.Parse(await second.Content.ReadAsStringAsync());
            Assert.Equal("runtime_operation_in_progress", body.RootElement.GetProperty("code").GetString());
            Assert.Equal(["stop"], control.Actions.ToArray());
            var unchanged = await authority.GetGroupAsync();
            Assert.Equal(blockedGroup.GroupEpoch, unchanged.GroupEpoch);
            Assert.Equal(blockedGroup.State, unchanged.State);
            await using var after = await contexts.CreateDbContextAsync();
            Assert.Equal(commandCount, await after.CommandRecords.CountAsync());
        }
        finally
        {
            control.ReleaseStop.TrySetResult();
            firstResponse = await first.WaitAsync(TimeSpan.FromSeconds(5));
            firstResponse.Dispose();
        }

        Assert.True(firstResponse.IsSuccessStatusCode);
        using var following = await client.SendAsync(Request("shutdown", csrf)).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(HttpStatusCode.OK, following.StatusCode);
    }

    /// <summary>:param client: 自有测试客户端。:returns: 固定账号的CSRF token。</summary>
    private static async Task<string> AuthenticateAsync(HttpClient client)
    {
        using var csrf = await client.GetAsync("/api/auth/csrf/");
        using var data = JsonDocument.Parse(await csrf.Content.ReadAsStringAsync());
        using var login = await client.PostAsJsonAsync("/api/auth/login/", new { username = "operator", password = "Old-password-123" });
        login.EnsureSuccessStatusCode();
        return data.RootElement.GetProperty("csrfToken").GetString()!;
    }

    /// <summary>:param action: 启停动作。:param csrf: token。:returns: 同站点HTTP请求。</summary>
    private static HttpRequestMessage Request(string action, string csrf)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/system/{action}/") { Content = JsonContent.Create(new { }) };
        request.Headers.Add("X-CSRFToken", csrf);
        return request;
    }

    private sealed class BlockingSupervisor : IRuntimeSupervisorControl
    {
        private int _stopCount;
        public bool IsConfigured => true;
        public ConcurrentQueue<string> Actions { get; } = new();
        public TaskCompletionSource StopEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleaseStop { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>:param action: 外部动作。:param cancellationToken: 寿命。:returns: 阻塞首个stop的测试结果。</summary>
        public Task<SupervisorLaunchResult> LaunchAsync(string action, CancellationToken cancellationToken = default) =>
            LaunchAsync(action, null, cancellationToken);

        /// <summary>:param action: 外部动作。:param expectedGroupEpoch: 代次。:param cancellationToken: 寿命。:returns: 只记录明确测试边界。</summary>
        public async Task<SupervisorLaunchResult> LaunchAsync(string action, long? expectedGroupEpoch, CancellationToken cancellationToken)
        {
            Actions.Enqueue(action);
            if (action == "stop" && Interlocked.Increment(ref _stopCount) == 1)
            {
                StopEntered.TrySetResult();
                await ReleaseStop.Task.WaitAsync(cancellationToken);
            }
            return new SupervisorLaunchResult(true, "completed", "受控并发测试结果");
        }
    }
}
