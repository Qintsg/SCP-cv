// 真实HTTP/SQLite核对停止与重启顺序，外部进程由明确测试接口替代，不改变物理设备。
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ScpCv.ControlHost.Runtime;
using ScpCv.Domain.Model;
using ScpCv.Infrastructure.Commands;
using ScpCv.Infrastructure.Persistence;
using ScpCv.Infrastructure.Runtime;

namespace ScpCv.ControlHost.Tests;

public sealed class SystemStopOrderingTests
{
    /// <summary>:param action: 停机或simulation重启。:returns: 重复Stopped也退休新CLOSE，响应无假待执行。</summary>
    [Theory]
    [InlineData("shutdown")]
    [InlineData("restart")]
    public async Task RepeatedStoppedRequestRetiresQueuedCloseAndReturnsFreshProjection(string action)
    {
        using var factory = new ControlHostApplicationFactory();
        using var client = factory.CreateHttpsClient();
        var csrf = await AuthenticateAsync(client);
        using var first = await client.SendAsync(Request(action, csrf));
        first.EnsureSuccessStatusCode();
        using var second = await client.SendAsync(Request(action, csrf));
        second.EnsureSuccessStatusCode();
        using var body = JsonDocument.Parse(await second.Content.ReadAsStringAsync());
        Assert.All(body.RootElement.GetProperty("sessions").EnumerateArray(), session =>
            Assert.Equal(string.Empty, session.GetProperty("pending_command").GetString()));
        var contexts = factory.Services.GetRequiredService<IDbContextFactory<ControlDbContext>>();
        await using var database = await contexts.CreateDbContextAsync();
        Assert.False(await database.CommandRecords.AnyAsync(command =>
            command.Status == CommandStatus.Pending || command.Status == CommandStatus.Processing));
        Assert.All(await database.CommandRecords.ToListAsync(), command => Assert.Equal(CommandStatus.Superseded, command.Status));
    }

    /// <summary>:returns: 外部stop失败前，不退休旧claim、不发新组、不调用start/restart。</summary>
    [Fact]
    public async Task ConfiguredRestartDoesNotConfirmExitOrStartAfterStopFails()
    {
        var control = new RecordingSupervisor(stopAccepted: false);
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
        var first = await authority.BeginStartAsync(requestId);
        await authority.ArmAsync(requestId, first.GroupEpoch);
        var repository = configured.Services.GetRequiredService<CommandRepository>();
        var intent = await repository.EnqueueAsync(new EnqueueCommand(CommandTargetKind.Display, 1, "OPEN", "{}", 1, 1));
        var old = await repository.ClaimAsync(new ClaimCommand(CommandTargetKind.Display, 1, Guid.NewGuid(), 1,
            first.GroupEpoch, TimeSpan.FromSeconds(30)));
        Assert.NotNull(old);
        control.Observe = async () => (await authority.GetGroupAsync()).State;

        using var response = await client.SendAsync(Request("restart", csrf));
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(["stop"], control.Actions);
        Assert.Equal([RuntimeGroupState.Draining], control.States);
        Assert.Equal(RuntimeGroupState.Draining, (await authority.GetGroupAsync()).State);
        var contexts = configured.Services.GetRequiredService<IDbContextFactory<ControlDbContext>>();
        await using var database = await contexts.CreateDbContextAsync();
        var retained = await database.CommandRecords.SingleAsync(command => command.CommandId == intent.CommandId);
        Assert.Equal(CommandStatus.Processing, retained.Status);
        Assert.Equal(old.ClaimToken, retained.ClaimToken);
    }

    /// <summary>:returns: 只有stop成功后才确认Stopped/发新组并start，不能一条restart掩盖次序。</summary>
    [Fact]
    public async Task ConfiguredRestartStopsOldGroupBeforeStartingNewGroup()
    {
        var control = new RecordingSupervisor(stopAccepted: true);
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
        control.Observe = async () => (await authority.GetGroupAsync()).State;

        using var response = await client.SendAsync(Request("restart", csrf));
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Equal(["stop", "start"], control.Actions);
        Assert.Equal([RuntimeGroupState.Draining, RuntimeGroupState.Starting], control.States);
        var armed = await authority.GetGroupAsync();
        Assert.Equal(RuntimeGroupState.Armed, armed.State);
        Assert.Equal(previous.GroupEpoch + 2, armed.GroupEpoch);
    }

    /// <summary>:param client: 自有测试HTTP客户端。:returns: 固定测试账号认证CSRF。</summary>
    private static async Task<string> AuthenticateAsync(HttpClient client)
    {
        using var csrf = await client.GetAsync("/api/auth/csrf/");
        using var data = JsonDocument.Parse(await csrf.Content.ReadAsStringAsync());
        using var login = await client.PostAsJsonAsync("/api/auth/login/", new { username = "operator", password = "Old-password-123" });
        login.EnsureSuccessStatusCode();
        return data.RootElement.GetProperty("csrfToken").GetString()!;
    }

    /// <summary>:param action: 动作。:param csrf: 测试token。:returns: 同站点请求。</summary>
    private static HttpRequestMessage Request(string action, string csrf)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/system/{action}/") { Content = JsonContent.Create(new { }) };
        request.Headers.Add("X-CSRFToken", csrf);
        return request;
    }

    private sealed class RecordingSupervisor(bool stopAccepted) : IRuntimeSupervisorControl
    {
        public bool IsConfigured => true;
        public List<string> Actions { get; } = [];
        public List<RuntimeGroupState> States { get; } = [];
        public Func<Task<RuntimeGroupState>>? Observe { get; set; }

        /// <summary>:param action: 测试动作。:param cancellationToken: 请求寿命。:returns: 明确测试结果。</summary>
        public Task<SupervisorLaunchResult> LaunchAsync(string action, CancellationToken cancellationToken = default) =>
            LaunchAsync(action, null, cancellationToken);

        /// <summary>:param action: 测试动作。:param expectedGroupEpoch: 明确代次。:param cancellationToken: 请求寿命。:returns: 操作顺序及状态由观测保存。</summary>
        public async Task<SupervisorLaunchResult> LaunchAsync(string action, long? expectedGroupEpoch, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Actions.Add(action);
            if (Observe is not null) States.Add(await Observe());
            var accepted = action == "stop" ? stopAccepted : action == "start";
            return new SupervisorLaunchResult(accepted, accepted ? "completed" : "test_stop_failed", "受控测试结果");
        }
    }
}
