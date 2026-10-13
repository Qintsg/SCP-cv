// 运动令牌、防迟到、短租约、取消释放与固定监控零写出，不访问现场设备。
using ScpCv.Contracts.Http;
using ScpCv.Infrastructure.Devices;

namespace ScpCv.Infrastructure.Tests;

public sealed class VenueCameraControllerTests
{
    [Fact]
    public async Task HoldDoesNotRepeatPressAndTimeoutReleasesOnce()
    {
        var transport = new SimulationDeviceCommandTransport();
        var clock = new TestClock();
        await using var controller = new VenueCameraController(new(transport), clock);
        var start = Request(clock);
        await controller.MotionAsync("left", start);
        clock.Advance(700);
        await controller.MotionAsync("left", start with { Phase = "hold" });
        clock.Advance(700);
        await controller.SweepAsync();
        Assert.Single(transport.Commands);
        clock.Advance(300);
        await controller.SweepAsync();
        await controller.SweepAsync();
        Assert.Equal(["FF06010A001F0001FA", "FF06010A001F0000FA"], transport.Commands.SelectMany(command => command.Frames));
        Assert.Equal("idle", controller.Intent("left"));
    }

    [Fact]
    public async Task StopBeforeStartAndLateHoldCannotRestart()
    {
        var transport = new SimulationDeviceCommandTransport(); var clock = new TestClock();
        await using var controller = new VenueCameraController(new(transport), clock);
        var start = Request(clock);
        await controller.MotionAsync("center1", start with { Phase = "stop" });
        await Assert.ThrowsAsync<VenueDeviceException>(() => controller.MotionAsync("center1", start));
        await Assert.ThrowsAsync<VenueDeviceException>(() => controller.MotionAsync("center1", start with { Phase = "hold" }));
        Assert.Empty(transport.Commands);
    }

    [Fact]
    public async Task OldStopCannotReleaseReplacementAndDuplicateStartDoesNotPressAgain()
    {
        var transport = new SimulationDeviceCommandTransport(); var clock = new TestClock();
        await using var controller = new VenueCameraController(new(transport), clock);
        var first = Request(clock); var second = Request(clock) with { Direction = "right" };
        await controller.MotionAsync("center2", first);
        await controller.MotionAsync("center2", second);
        await controller.MotionAsync("center2", second);
        await controller.MotionAsync("center2", first with { Phase = "stop" });
        Assert.Equal(3, transport.Commands.Count);
        Assert.Equal("held", controller.Intent("center2"));
        await controller.MotionAsync("center2", second with { Phase = "stop" }, new CancellationToken(true));
        Assert.Equal(4, transport.Commands.Count);
        Assert.Equal("idle", controller.Intent("center2"));
    }

    [Fact]
    public async Task FailedPressKeepsSafeReleaseIndependentOfCallerCancellation()
    {
        var transport = new RecordingTransport(); var clock = new TestClock();
        await using var controller = new VenueCameraController(new(transport), clock);
        transport.FailPress = true;
        await Assert.ThrowsAsync<VenueDeviceException>(() => controller.MotionAsync("right", Request(clock)));
        Assert.Equal(["FF06010A001F0001FA", "FF06010A001F0000FA"], transport.Commands.SelectMany(command => command.Frames));
        Assert.Equal("idle", controller.Intent("right"));
    }

    [Fact]
    public async Task FailedReleaseRemainsPendingAndWatchdogOnlyRetriesSafeZero()
    {
        var transport = new RecordingTransport(); var clock = new TestClock();
        await using var controller = new VenueCameraController(new(transport), clock);
        var start = Request(clock); await controller.MotionAsync("rear", start);
        transport.FailRelease = true;
        await Assert.ThrowsAsync<VenueDeviceException>(() => controller.MotionAsync("rear", start with { Phase = "stop" }));
        Assert.Equal("stop_pending", controller.Intent("rear"));
        await Assert.ThrowsAsync<VenueDeviceException>(() => controller.MotionAsync("rear", start with { Phase = "hold" }));
        transport.FailRelease = false;
        await controller.SweepAsync();
        Assert.Equal("idle", controller.Intent("rear"));
        Assert.Equal(1, transport.Commands.SelectMany(command => command.Frames).Count(frame => frame.EndsWith("0001FA", StringComparison.Ordinal)));
    }

    [Theory]
    [InlineData("fixed", "up", "start", 0)]
    [InlineData("unknown", "up", "start", 0)]
    [InlineData("left", "evil", "start", 0)]
    [InlineData("left", "up", "invalid", 0)]
    [InlineData("left", "up", "start", 2001)]
    public async Task UnknownOrStaleRequestsHaveZeroWrites(string id, string direction, string phase, int age)
    {
        var transport = new SimulationDeviceCommandTransport(); var clock = new TestClock();
        await using var controller = new VenueCameraController(new(transport), clock);
        await Assert.ThrowsAsync<VenueDeviceException>(() => controller.MotionAsync(id, Request(clock) with
        { Direction = direction, Phase = phase, IssuedAtMilliseconds = clock.GetUtcNow().ToUnixTimeMilliseconds() - age }));
        Assert.Empty(transport.Commands);
    }

    [Fact]
    public async Task SixPositionsUseModeThenSlotAndInvalidPositionHasZeroWrites()
    {
        var transport = new SimulationDeviceCommandTransport(); var clock = new TestClock();
        await using var controller = new VenueCameraController(new(transport), clock);
        await Assert.ThrowsAsync<VenueDeviceException>(() => controller.PositionAsync("left", 7, "call"));
        await Assert.ThrowsAsync<VenueDeviceException>(() => controller.PositionAsync("fixed", 1, "call"));
        Assert.Empty(transport.Commands);
        await controller.PositionAsync("left", 6, "call");
        Assert.Equal(["FF06010A00270001FA", "FF06010A00270000FA", "FF06010A00320001FA", "FF06010A00320000FA"],
            transport.Commands.SelectMany(command => command.Frames));
    }

    [Fact]
    public async Task StopCancelsPendingPressBeforeItCanWriteLate()
    {
        var transport = new PendingPressTransport(); var clock = new TestClock();
        await using var controller = new VenueCameraController(new(transport), clock);
        var request = Request(clock);
        var start = controller.MotionAsync("left", request);
        await transport.Entered.Task.WaitAsync(TimeSpan.FromSeconds(1));
        var stop = controller.MotionAsync("left", request with { Phase = "stop" });
        try
        {
            await Assert.ThrowsAsync<VenueDeviceException>(() => start.WaitAsync(TimeSpan.FromSeconds(1)));
            await stop.WaitAsync(TimeSpan.FromSeconds(1));
        }
        finally { transport.Unblock.TrySetResult(); await Task.WhenAll(start.ContinueWith(_ => { }, TaskScheduler.Default), stop); }
        Assert.False(transport.PressWritten);
        Assert.Equal("idle", controller.Intent("left"));
    }

    private static CameraControlRequest Request(TestClock clock) => new()
    { MotionId = Guid.NewGuid(), Direction = "up", Phase = "start", IssuedAtMilliseconds = clock.GetUtcNow().ToUnixTimeMilliseconds() };
    private sealed class TestClock : TimeProvider
    {
        private DateTimeOffset _now = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => _now;
        internal void Advance(int milliseconds) => _now = _now.AddMilliseconds(milliseconds);
    }
    private sealed class RecordingTransport : IDeviceCommandTransport
    {
        public bool IsSimulation => true;
        internal List<DeviceCommand> Commands { get; } = [];
        internal bool FailPress { get; set; }
        internal bool FailRelease { get; set; }
        public Task SendAsync(DeviceCommand command, CancellationToken token = default)
        {
            Commands.Add(command);
            return (FailPress && command.Frames[0].EndsWith("0001FA", StringComparison.Ordinal)) ||
                (FailRelease && command.Frames[0].EndsWith("0000FA", StringComparison.Ordinal))
                ? Task.FromException(new DeviceServiceException("有界故障替身")) : Task.CompletedTask;
        }
    }
    private sealed class PendingPressTransport : IDeviceCommandTransport
    {
        public bool IsSimulation => true;
        internal TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Unblock { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal bool PressWritten { get; private set; }
        public async Task SendAsync(DeviceCommand command, CancellationToken token = default)
        {
            if (!command.Frames[0].EndsWith("0001FA", StringComparison.Ordinal)) return;
            Entered.TrySetResult();
            await Unblock.Task.WaitAsync(token);
            PressWritten = true;
        }
    }
}
