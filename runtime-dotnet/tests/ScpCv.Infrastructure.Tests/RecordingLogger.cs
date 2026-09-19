using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace ScpCv.Infrastructure.Tests;

/// <summary>
/// 收集日志条目的测试替身。视频墙下发阶段内最多 25 个并发发送，写入必须线程安全；
/// 只保留级别与格式化后的文本，够断言「记了什么」和「没有记什么」。
/// </summary>
internal sealed class RecordingLogger<T> : ILogger<T>
{
    public ConcurrentQueue<(LogLevel Level, string Message)> Entries { get; } = new();

    public string[] Messages => [.. Entries.Select(entry => entry.Message)];

    public string[] MessagesAt(LogLevel level) =>
        [.. Entries.Where(entry => entry.Level == level).Select(entry => entry.Message)];

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        ArgumentNullException.ThrowIfNull(formatter);
        Entries.Enqueue((logLevel, formatter(state, exception)));
    }
}
