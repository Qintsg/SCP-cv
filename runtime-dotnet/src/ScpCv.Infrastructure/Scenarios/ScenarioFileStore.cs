// 按目录持久化完整预案YAML；暂存成功后才提交索引，再逐文件原子发布，失败明确报告。
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ScpCv.Contracts.Http;
using ScpCv.Infrastructure.Media;
using ScpCv.Infrastructure.Persistence;
using ScpCv.Infrastructure.Devices;
using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace ScpCv.Infrastructure.Scenarios;

public sealed class ScenarioFileStore(string dataRoot) : IWriteProjection
{
    private readonly MediaPathResolver _paths = new(Path.Combine(dataRoot, "scenarios"));
    private static ISerializer Serializer() => new SerializerBuilder().WithNamingConvention(UnderscoredNamingConvention.Instance)
        .DisableAliases().WithMaximumRecursion(20).Build();
    private static IDeserializer Deserializer() => new DeserializerBuilder().WithNamingConvention(UnderscoredNamingConvention.Instance)
        .WithTypeMapping<IReadOnlyDictionary<string, int?>, Dictionary<string, int?>>()
        .WithTypeMapping<IReadOnlyList<ScenarioTargetDto>, List<ScenarioTargetDto>>()
        .WithDuplicateKeyChecking().WithMaximumRecursion(20).Build();

    public async Task<IPreparedWriteProjection> PrepareAsync(ControlDbContext database, CancellationToken cancellationToken)
    {
        var staged = new List<StagedFile>(); string? staging = null;
        try
        {
            _paths.EnsureNoReparsePoint(_paths.RootPath); Directory.CreateDirectory(_paths.RootPath);
            var manifestPath = Path.Combine(_paths.RootPath, ".index.json");
            _paths.EnsureNoReparsePoint(manifestPath);
            var previous = File.Exists(manifestPath) ? JsonSerializer.Deserialize<Dictionary<long, string>>(
                await File.ReadAllTextAsync(manifestPath, cancellationToken).ConfigureAwait(false)) ?? [] : [];
            var scenarios = await database.Scenarios.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false);
            var items = await ScenarioService.MapAsync(database, scenarios, cancellationToken).ConfigureAwait(false);
            staging = Path.Combine(_paths.RootPath, ".staging", Guid.NewGuid().ToString("N"));
            _paths.EnsureNoReparsePoint(staging); Directory.CreateDirectory(staging);
            var next = new Dictionary<long, string>();
            foreach (var item in items)
            {
                var segments = item.Path.TrimStart('/').Split('/');
                foreach (var segment in segments) _ = MediaPathResolver.ValidateSegment(segment);
                var relative = Path.Combine(segments) + ".yaml";
                var target = Resolve(relative);
                if (next.Values.Contains(relative, StringComparer.OrdinalIgnoreCase)) throw new IOException("预案文件路径冲突。");
                next.Add(item.Id, relative);
                var existing = File.Exists(target) ? Read(target) : null;
                if (existing is not null && existing.Scenario.Id != item.Id) throw new IOException("目标文件不属于该预案，已拒绝覆盖。");
                var file = Path.Combine(staging, item.Id + ".yaml");
                var yaml = "# SCP-cv 预案文件；请通过网页编辑，禁止在运行时直接修改。\n" +
                    Serializer().Serialize(new ScenarioFileDocument { Scenario = Snapshot(item) });
                staged.Add(new(file, target, File.Exists(target) ? Digest(target) : null));
                await File.WriteAllTextAsync(file, yaml.Replace("\r\n", "\n", StringComparison.Ordinal), new UTF8Encoding(false), cancellationToken).ConfigureAwait(false);
            }
            var obsolete = previous.Where(pair => !next.TryGetValue(pair.Key, out var path) || !string.Equals(path, pair.Value, StringComparison.OrdinalIgnoreCase))
                .Select(pair => (pair.Key, Path: Resolve(pair.Value))).Where(pair => File.Exists(pair.Path)).ToArray();
            foreach (var (id, path) in obsolete) if (Read(path).Scenario.Id != id) throw new IOException("旧路径文件身份已改变，已拒绝删除。");
            var manifest = Path.Combine(staging, "index.json");
            await File.WriteAllTextAsync(manifest, JsonSerializer.Serialize(next), new UTF8Encoding(false), cancellationToken).ConfigureAwait(false);
            return new Prepared(staged, obsolete.Select(pair => (pair.Path, Digest(pair.Path))).ToArray(), manifest, manifestPath, staging, _paths);
        }
        catch (OperationCanceledException)
        {
            Cleanup(staged.Select(item => item.Stage).Concat(staging is null ? [] : new[] { Path.Combine(staging, "index.json") }), staging);
            throw;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or YamlException or MediaServiceException)
        {
            Cleanup(staged.Select(item => item.Stage).Concat(staging is null ? [] : new[] { Path.Combine(staging, "index.json") }), staging);
            throw new ScenarioServiceException("预案YAML文件准备失败，索引修改未提交：" + exception.Message, code: "scenario_file_prepare_failed");
        }
    }

    /// <summary>激活前检查YAML与索引一致；缺失/手动改动时拒绝全部动作，不默默使用旧值。</summary>
    public void Verify(ScenarioDto item)
    {
        try
        {
            var path = Resolve(Path.Combine(item.Path.TrimStart('/').Split('/')) + ".yaml");
            if (!File.Exists(path)) throw new IOException("预案YAML缺失，请编辑并重新保存预案。");
            var document = Read(path);
            if (document.Version != 1 || JsonSerializer.Serialize(Snapshot(document.Scenario)) != JsonSerializer.Serialize(Snapshot(item)))
                throw new IOException("预案YAML与索引不一致，请通过网页核对并重新保存。");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or YamlException or MediaServiceException)
        { throw new ScenarioServiceException(exception.Message, code: "scenario_file_invalid"); }
    }

    private string Resolve(string relative)
    {
        var target = Path.GetFullPath(Path.Combine(_paths.RootPath, relative));
        _paths.EnsureNoReparsePoint(target); return target;
    }

    private static ScenarioDto Snapshot(ScenarioDto item)
    {
        ScenarioAdvancedDto advanced;
        try
        {
            advanced = item.Advanced with
            {
                Audio = item.Advanced.Audio is { } audio ? VenueAudioAliases.Normalize(audio) : null,
                CameraPositions = item.Advanced.CameraPositions.ToDictionary(pair => pair.Key, pair => pair.Value == 0 ? null : pair.Value),
            };
        }
        catch (VenueDeviceException exception) { throw new ScenarioServiceException(exception.Message, code: "scenario_advanced_invalid"); }
        return item with
        {
            Advanced = advanced,
            Targets = item.Targets.Select(target => target with { SourceName = string.Empty }).ToArray(),
            Window1SourceName = string.Empty,
            Window2SourceName = string.Empty,
        };
    }

    private static ScenarioFileDocument Read(string path)
    {
        if (new FileInfo(path).Length > 1024 * 1024) throw new IOException("预案YAML过大。");
        return ParseDocument(File.ReadAllText(path, Encoding.UTF8));
    }

    /// <summary>解析受限预案YAML；为只读合同集合指定具体实现，不激活任何设备。</summary>
    public static ScenarioFileDocument ParseDocument(string yaml)
    {
        if (yaml.Length > 1024 * 1024) throw new IOException("预案YAML过大。");
        var document = Deserializer().Deserialize<ScenarioFileDocument>(yaml);
        return document is { Version: 1, Scenario.Id: > 0, Scenario.Advanced.CameraPositions: not null, Scenario.Targets: not null }
            ? document : throw new IOException("预案YAML文件身份或结构无效。");
    }

    private static string Digest(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
    private static void Cleanup(IEnumerable<string> files, string? staging)
    {
        foreach (var file in files) if (File.Exists(file)) File.Delete(file);
        if (staging is not null && Directory.Exists(staging) && !Directory.EnumerateFileSystemEntries(staging).Any()) Directory.Delete(staging);
    }

    private sealed record StagedFile(string Stage, string Target, string? OriginalDigest);
    private sealed class Prepared(List<StagedFile> staged, (string Path, string Digest)[] obsolete,
        string manifest, string manifestTarget, string staging, MediaPathResolver paths) : IPreparedWriteProjection
    {
        public async Task PublishAsync()
        {
            try
            {
                foreach (var item in staged)
                {
                    paths.EnsureNoReparsePoint(item.Target);
                    if ((File.Exists(item.Target) ? Digest(item.Target) : null) != item.OriginalDigest)
                        throw new IOException("预案目标文件在保存期间改变，未覆盖。");
                    Directory.CreateDirectory(Path.GetDirectoryName(item.Target)!);
                    File.Move(item.Stage, item.Target, true);
                }
                foreach (var item in obsolete)
                {
                    paths.EnsureNoReparsePoint(item.Path);
                    if (File.Exists(item.Path) && Digest(item.Path) == item.Digest) File.Delete(item.Path);
                    else if (File.Exists(item.Path)) throw new IOException("旧预案文件在保存期间改变，未删除。");
                }
                paths.EnsureNoReparsePoint(manifestTarget); File.Move(manifest, manifestTarget, true);
                await Task.CompletedTask.ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or MediaServiceException)
            { throw new ScenarioServiceException("预案索引已提交，但YAML发布未完整确认，请刷新后重新保存；未自动重试：" + exception.Message, code: "scenario_file_publish_failed"); }
        }

        public void Dispose() { Cleanup(staged.Select(item => item.Stage).Append(manifest), staging); }
    }
}

public sealed record ScenarioFileDocument
{
    public int Version { get; init; } = 1;
    public ScenarioDto Scenario { get; init; } = new();
}
