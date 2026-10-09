// 经公开命令接口与认证HTTP验证最早未终结命令；独立SQLite库不启动播放器或Office。
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ScpCv.Contracts.Ipc;
using ScpCv.ControlHost.Events;
using ScpCv.Domain.Model;
using ScpCv.Infrastructure.Commands;
using ScpCv.Infrastructure.Persistence;

namespace ScpCv.ControlHost.Tests;

public sealed class UnfinishedCommandProjectionTests
{
    /// <summary>:returns: 命令领取后HTTP仍报告正在执行的最早命令，而非空闲。</summary>
    [Fact]
    public async Task DisplayClaimKeepsProcessingCommandVisibleThroughHttp()
    {
        using var factory = new ControlHostApplicationFactory();
        using var client = await AuthenticatedClientAsync(factory);
        var worker = await SeedOwnerAsync(factory, CommandTargetKind.Display);
        var commands = factory.Services.GetRequiredService<CommandRepository>();
        var first = await commands.EnqueueAsync(new EnqueueCommand(CommandTargetKind.Display, 1, "NEXT_EFFECT", "{}", 3, 1));
        var claimed = await factory.Services.GetRequiredService<CommandLeaseService>().ClaimAsync(
            new LeaseClaimRequest(CommandTargetKind.Display, 1, worker, 2, 3, TimeSpan.FromSeconds(30)));

        Assert.NotNull(claimed);
        Assert.Equal(first.CommandId, claimed.CommandId);
        Assert.Equal("NEXT_EFFECT", await ReadPendingAsync(client, CommandTargetKind.Display));
    }

    /// <summary>:param target: 显示/音频目标。:returns: Worker状态采样不会跳过仍在执行的最早命令。</summary>
    [Theory]
    [InlineData(CommandTargetKind.Display)]
    [InlineData(CommandTargetKind.Audio)]
    public async Task StateReportKeepsProcessingAheadOfLaterPendingCommand(CommandTargetKind target)
    {
        using var factory = new ControlHostApplicationFactory();
        using var client = await AuthenticatedClientAsync(factory);
        var worker = await SeedOwnerAsync(factory, target);
        var commands = factory.Services.GetRequiredService<CommandRepository>();
        var firstName = target == CommandTargetKind.Display ? "NEXT_EFFECT" : "PLAY";
        var secondName = target == CommandTargetKind.Display ? "PREVIOUS_EFFECT" : "PAUSE";
        var first = await commands.EnqueueAsync(new EnqueueCommand(target, 1, firstName, "{}", 3, 1));
        await commands.EnqueueAsync(new EnqueueCommand(target, 1, secondName, "{}", 3, 1));
        var claimed = await factory.Services.GetRequiredService<CommandLeaseService>().ClaimAsync(
            new LeaseClaimRequest(target, 1, worker, 2, 3, TimeSpan.FromSeconds(30)));
        Assert.NotNull(claimed);
        Assert.Equal(first.CommandId, claimed.CommandId);
        Assert.Equal(firstName, await ReadPendingAsync(client, target));

        var accepted = await factory.Services.GetRequiredService<RuntimeProjectionPublisher>().ApplyStateReportAsync(
            target, 1, worker, 2, new StateReportDto
            { SourceGeneration = 3, State = JsonSerializer.SerializeToElement(new { playback_state = "playing", current_slide = 6, position_ms = 1200 }) });

        Assert.True(accepted.Accepted);
        Assert.Equal(firstName, await ReadPendingAsync(client, target));
    }

    /// <summary>:param target: 显示/音频目标。:returns: 新HTTP意图排在正在执行的命令之后，不抢占其投影。</summary>
    [Theory]
    [InlineData(CommandTargetKind.Display)]
    [InlineData(CommandTargetKind.Audio)]
    public async Task HttpEnqueueDoesNotHideEarlierProcessingCommand(CommandTargetKind target)
    {
        using var factory = new ControlHostApplicationFactory();
        using var client = await AuthenticatedClientAsync(factory);
        var worker = await SeedOwnerAsync(factory, target);
        var firstName = target == CommandTargetKind.Display ? "NEXT_EFFECT" : "PLAY";
        await factory.Services.GetRequiredService<CommandRepository>().EnqueueAsync(
            new EnqueueCommand(target, 1, firstName, "{}", 3, 1));
        Assert.NotNull(await factory.Services.GetRequiredService<CommandLeaseService>().ClaimAsync(
            new LeaseClaimRequest(target, 1, worker, 2, 3, TimeSpan.FromSeconds(30))));
        Assert.Equal(firstName, await ReadPendingAsync(client, target));

        using var response = await EnqueueVolumeAsync(client, target, 45);
        response.EnsureSuccessStatusCode();
        Assert.Equal(firstName, await ReadPendingAsync(client, target));
    }

    /// <summary>:param target: 显示/音频目标。:param terminal: 真实终态分类。:returns: 终态推进队列，最后一条结束才清空。</summary>
    [Theory]
    [InlineData(CommandTargetKind.Display, CommandStatus.Completed)]
    [InlineData(CommandTargetKind.Display, CommandStatus.Failed)]
    [InlineData(CommandTargetKind.Display, CommandStatus.Uncertain)]
    [InlineData(CommandTargetKind.Audio, CommandStatus.Completed)]
    [InlineData(CommandTargetKind.Audio, CommandStatus.Failed)]
    [InlineData(CommandTargetKind.Audio, CommandStatus.Uncertain)]
    public async Task TerminalResultAdvancesQueueAndDuplicateCannotHideNextCommand(CommandTargetKind target, CommandStatus terminal)
    {
        using var factory = new ControlHostApplicationFactory();
        using var client = await AuthenticatedClientAsync(factory);
        var worker = await SeedOwnerAsync(factory, target);
        var commands = factory.Services.GetRequiredService<CommandRepository>();
        await commands.EnqueueAsync(new EnqueueCommand(target, 1, "PLAY", "{}", 3, 1));
        var first = await ClaimAsync(factory, target, worker);
        using var response = await EnqueueVolumeAsync(client, target, 45);
        response.EnsureSuccessStatusCode();
        Assert.Equal("PLAY", await ReadPendingAsync(client, target));
        Assert.Null(await factory.Services.GetRequiredService<CommandLeaseService>().ClaimAsync(
            new LeaseClaimRequest(target, 1, worker, 2, 3, TimeSpan.FromSeconds(30))));

        var result = Completion(first, terminal);
        var results = factory.Services.GetRequiredService<CommandResultService>();
        Assert.True((await results.AcceptAsync(result)).Accepted);
        Assert.Equal("SET_VOLUME", await ReadPendingAsync(client, target));
        var second = await ClaimAsync(factory, target, worker);
        Assert.True((await results.AcceptAsync(result)).Duplicate);
        Assert.Equal("SET_VOLUME", await ReadPendingAsync(client, target));
        Assert.True((await results.AcceptAsync(Completion(second, terminal))).Accepted);
        Assert.Equal(string.Empty, await ReadPendingAsync(client, target));
    }

    /// <summary>:param target: 显示/音频目标。:returns: 同事务退役的旧音量意图不会复活或先于最新值被领取。</summary>
    [Theory]
    [InlineData(CommandTargetKind.Display)]
    [InlineData(CommandTargetKind.Audio)]
    public async Task CoalescedPendingIntentCannotReplaceOrOutliveProcessingHead(CommandTargetKind target)
    {
        using var factory = new ControlHostApplicationFactory();
        using var client = await AuthenticatedClientAsync(factory);
        var worker = await SeedOwnerAsync(factory, target);
        await factory.Services.GetRequiredService<CommandRepository>().EnqueueAsync(
            new EnqueueCommand(target, 1, "PLAY", "{}", 3, 1));
        var first = await ClaimAsync(factory, target, worker);
        using var earlier = await EnqueueVolumeAsync(client, target, 45);
        earlier.EnsureSuccessStatusCode();
        using var later = await EnqueueVolumeAsync(client, target, 55);
        later.EnsureSuccessStatusCode();
        Assert.Equal("PLAY", await ReadPendingAsync(client, target));
        await factory.Services.GetRequiredService<CommandResultService>().AcceptAsync(Completion(first, CommandStatus.Completed));

        var next = await ClaimAsync(factory, target, worker);
        Assert.Equal("SET_VOLUME", next.Command);
        using var arguments = JsonDocument.Parse(next.ArgsJson);
        Assert.Equal(55, arguments.RootElement.GetProperty("volume").GetInt32());
        await factory.Services.GetRequiredService<CommandResultService>().AcceptAsync(Completion(next, CommandStatus.Completed));
        Assert.Equal(string.Empty, await ReadPendingAsync(client, target));
    }

    /// <summary>:returns: CLOSE替换未领取的显示意图，但不掩盖已领取动作或复活退役项。</summary>
    [Fact]
    public async Task DisplayReplacementKeepsProcessingHeadAndRetiresLaterPendingIntent()
    {
        using var factory = new ControlHostApplicationFactory();
        using var client = await AuthenticatedClientAsync(factory);
        var worker = await SeedOwnerAsync(factory, CommandTargetKind.Display);
        var commands = factory.Services.GetRequiredService<CommandRepository>();
        await commands.EnqueueAsync(new EnqueueCommand(CommandTargetKind.Display, 1, "NEXT_EFFECT", "{}", 3, 1));
        var first = await ClaimAsync(factory, CommandTargetKind.Display, worker);
        await commands.EnqueueAsync(new EnqueueCommand(CommandTargetKind.Display, 1, "PREVIOUS_EFFECT", "{}", 3, 1));
        using var close = await EnqueueCloseAsync(client);
        close.EnsureSuccessStatusCode();
        Assert.Equal("NEXT_EFFECT", await ReadPendingAsync(client, CommandTargetKind.Display));
        await factory.Services.GetRequiredService<CommandResultService>().AcceptAsync(Completion(first, CommandStatus.Completed));
        Assert.Equal("CLOSE", await ReadPendingAsync(client, CommandTargetKind.Display));
        var next = await ClaimAsync(factory, CommandTargetKind.Display, worker);
        Assert.Equal("CLOSE", next.Command);
        await factory.Services.GetRequiredService<CommandResultService>().AcceptAsync(Completion(next, CommandStatus.Completed));
        Assert.Equal(string.Empty, await ReadPendingAsync(client, CommandTargetKind.Display));
    }

    /// <summary>:returns: 三目标同时排队时，仅完成目标的投影推进，其余名称/参数仍可正常领取。</summary>
    [Fact]
    public async Task CompletingDisplayOneDoesNotAdvanceDisplayTwoOrAudioQueue()
    {
        using var factory = new ControlHostApplicationFactory();
        using var client = await AuthenticatedClientAsync(factory);
        var firstWorker = await SeedOwnerAsync(factory, CommandTargetKind.Display);
        var secondWorker = await SeedOwnerAsync(factory, CommandTargetKind.Display, 2);
        var audioWorker = await SeedOwnerAsync(factory, CommandTargetKind.Audio);
        var commands = factory.Services.GetRequiredService<CommandRepository>();
        await commands.EnqueueAsync(new EnqueueCommand(CommandTargetKind.Display, 1, "NEXT_EFFECT", "{\"marker\":\"display-one\"}", 3, 1));
        await commands.EnqueueAsync(new EnqueueCommand(CommandTargetKind.Display, 2, "PREVIOUS_EFFECT", "{\"marker\":\"display-two\"}", 3, 1));
        await commands.EnqueueAsync(new EnqueueCommand(CommandTargetKind.Audio, 1, "PAUSE", "{\"marker\":\"audio-one\"}", 3, 1));
        var first = await ClaimAsync(factory, CommandTargetKind.Display, firstWorker);
        var results = factory.Services.GetRequiredService<CommandResultService>();
        Assert.True((await results.AcceptAsync(Completion(first, CommandStatus.Completed))).Accepted);

        Assert.Equal(string.Empty, await ReadPendingAsync(client, CommandTargetKind.Display));
        Assert.Equal("PREVIOUS_EFFECT", await ReadPendingAsync(client, CommandTargetKind.Display, 2));
        Assert.Equal("PAUSE", await ReadPendingAsync(client, CommandTargetKind.Audio));
        var second = await ClaimAsync(factory, CommandTargetKind.Display, secondWorker, 2);
        var audio = await ClaimAsync(factory, CommandTargetKind.Audio, audioWorker);
        Assert.Equal("PREVIOUS_EFFECT", second.Command);
        Assert.Equal("PAUSE", audio.Command);
        using var secondArguments = JsonDocument.Parse(second.ArgsJson);
        using var audioArguments = JsonDocument.Parse(audio.ArgsJson);
        Assert.Equal("display-two", secondArguments.RootElement.GetProperty("marker").GetString());
        Assert.Equal("audio-one", audioArguments.RootElement.GetProperty("marker").GetString());
        Assert.True((await results.AcceptAsync(Completion(second, CommandStatus.Completed))).Accepted);
        Assert.Equal(string.Empty, await ReadPendingAsync(client, CommandTargetKind.Display, 2));
        Assert.Equal("PAUSE", await ReadPendingAsync(client, CommandTargetKind.Audio));
        Assert.True((await results.AcceptAsync(Completion(audio, CommandStatus.Completed))).Accepted);
        Assert.Equal(string.Empty, await ReadPendingAsync(client, CommandTargetKind.Audio));
        Assert.Equal(string.Empty, await ReadPendingAsync(client, CommandTargetKind.Display));
    }

    /// <summary>:param target: 显示/音频目标。:param worker: 已授权测试Worker。:param targetId: 目标编号。:returns: 公开租约入口实际领取的命令。</summary>
    private static async Task<CommandRecord> ClaimAsync(ControlHostApplicationFactory factory, CommandTargetKind target, Guid worker, int targetId = 1)
    {
        var command = await factory.Services.GetRequiredService<CommandLeaseService>().ClaimAsync(
            new LeaseClaimRequest(target, targetId, worker, 2, 3, TimeSpan.FromSeconds(30)));
        Assert.NotNull(command);
        return command;
    }

    /// <summary>:param command: 已领取命令。:param status: 终态。:returns: 已授权完成请求；无原生完成伪证。</summary>
    private static CompleteCommand Completion(CommandRecord command, CommandStatus status) => new(
        command.CommandId, command.ClaimToken!.Value, command.OwnerEpoch, status, "test_terminal", string.Empty, "{}");

    /// <summary>:param target: 自有测试Worker目标。:param targetId: 目标编号。:returns: 预置在线归属及armed组，不制造实际播放完成。</summary>
    private static async Task<Guid> SeedOwnerAsync(ControlHostApplicationFactory factory, CommandTargetKind target, int targetId = 1)
    {
        var worker = Guid.NewGuid();
        var contexts = factory.Services.GetRequiredService<IDbContextFactory<ControlDbContext>>();
        await using var database = await contexts.CreateDbContextAsync();
        var group = await database.RuntimeGroupControls.SingleAsync();
        group.GroupEpoch = 3;
        group.State = RuntimeGroupState.Armed;
        if (target == CommandTargetKind.Display)
            (await database.PlaybackSessions.SingleAsync(item => item.WindowId == targetId)).DesiredGeneration = 3;
        else
            (await database.BackgroundAudioStates.SingleAsync()).DesiredGeneration = 3;
        database.WorkerOwnerships.Add(new WorkerOwnership
        {
            TargetKind = target, TargetId = targetId, WorkerInstanceId = worker, OwnerEpoch = 2,
            ProcessId = 12345, ProcessStartTime = DateTimeOffset.UtcNow,
            LastTransportHeartbeat = DateTimeOffset.UtcNow, Status = WorkerOwnershipState.Online,
        });
        await database.SaveChangesAsync();
        return worker;
    }

    /// <summary>:param target: 显示或音频目标。:param targetId: 目标编号。:returns: 认证HTTP报告的未终结命令，不直接读业务表断言。</summary>
    private static async Task<string> ReadPendingAsync(HttpClient client, CommandTargetKind target, int targetId = 1)
    {
        using var response = await client.GetAsync(target == CommandTargetKind.Display ? $"/api/sessions/{targetId}/" : "/api/background-audio/");
        response.EnsureSuccessStatusCode();
        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var state = target == CommandTargetKind.Display ? body.RootElement.GetProperty("session")
            : body.RootElement.GetProperty("background_audio").GetProperty("state");
        return state.GetProperty("pending_command").GetString()!;
    }

    /// <summary>:param target: 显示/音频目标。:param volume: 有效音量意图。:returns: HTTP入队响应；Simulation不改变声卡。</summary>
    private static async Task<HttpResponseMessage> EnqueueVolumeAsync(HttpClient client, CommandTargetKind target, int volume)
    {
        using var csrfResponse = await client.GetAsync("/api/auth/csrf/");
        using var csrf = await JsonDocument.ParseAsync(await csrfResponse.Content.ReadAsStreamAsync());
        using var request = new HttpRequestMessage(HttpMethod.Patch,
            target == CommandTargetKind.Display ? "/api/playback/1/volume/" : "/api/background-audio/volume/")
        { Content = JsonContent.Create(new { volume }) };
        request.Headers.Add("X-CSRFToken", csrf.RootElement.GetProperty("csrfToken").GetString()!);
        return await client.SendAsync(request);
    }

    /// <summary>:returns: 真实CLOSE HTTP入队响应，测试主机不启动或关闭原生播放器。</summary>
    private static async Task<HttpResponseMessage> EnqueueCloseAsync(HttpClient client)
    {
        using var csrfResponse = await client.GetAsync("/api/auth/csrf/");
        using var csrf = await JsonDocument.ParseAsync(await csrfResponse.Content.ReadAsStreamAsync());
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/playback/1/close/")
        { Content = JsonContent.Create(new { }) };
        request.Headers.Add("X-CSRFToken", csrf.RootElement.GetProperty("csrfToken").GetString()!);
        return await client.SendAsync(request);
    }

    /// <summary>:returns: 独立测试主机的已认证客户端，不连接现场。</summary>
    private static async Task<HttpClient> AuthenticatedClientAsync(ControlHostApplicationFactory factory)
    {
        var client = factory.CreateHttpsClient();
        using var csrf = await client.GetAsync("/api/auth/csrf/");
        using var login = await client.PostAsJsonAsync("/api/auth/login/",
            new { username = "operator", password = "Old-password-123" });
        login.EnsureSuccessStatusCode();
        return client;
    }
}
