// 受限10×5均匀墙的左右/全屏计划；旧预设保留黄金包兼容路径。
using ScpCv.Domain.Model;
using ScpCv.Domain.Rules;

namespace ScpCv.Infrastructure.VideoWall;

public static class VideoWallLayoutPlanner
{
    /// <summary>预检完整计划后返回帧，任何错误均发生在清屏前。</summary>
    public static IReadOnlyList<VideoWallSendItem> Build(WallLayout layout, VideoWallProtocolOptions options)
    {
        try { layout = WallLayoutPolicy.Validate(layout); }
        catch (ArgumentException exception) { throw new VideoWallException(exception.Message, exception); }
        options.Validate();
        foreach (var mapping in layout.Mappings) Resolve(mapping.Input, options).Validate();
        var preset = WallLayoutPolicy.CapturedPreset(layout);
        if (preset is not null && IsLegacyProfile(options))
            return Wrap(VideoWallSequenceBuilder.Build(preset == BigScreenMode.Single ? "single" : "double"), options);

        var result = new List<VideoWallSendItem>();
        AddControl("clear", 0x61FF);
        foreach (var mapping in layout.Mappings.OrderBy(item => item.Region))
        {
            var input = Resolve(mapping.Input, options);
            var full = mapping.Region == WallRegion.Fullscreen;
            var startColumn = mapping.Region == WallRegion.Right ? 5 : 0;
            var columns = full ? 10 : 5;
            // 此受限路径要求整除且4像素对齐，避免猜测原程序未充分验证的余数分支。
            if (input.Width % (columns * 4) != 0 || input.Height % 20 != 0)
                throw new VideoWallException("该输入尺寸尚无已验证的整除裁剪规则，未向设备发送命令。");
            var fragment = 0;
            var sign = mapping.Region == WallRegion.Right ? 2 : 1;
            for (var row = 0; row < 5; row++)
            {
                for (var column = startColumn; column < startColumn + columns; column++)
                {
                    var source = new VideoWallRectangle((column - startColumn) * input.Width / columns,
                        row * input.Height / 5, input.Width / columns, input.Height / 5);
                    var packets = VideoWallPacketCodec.Mapping(input, new(0, 0, 1920, 1080), source,
                        sign, sign, layout.Mappings.Count, ++fragment, row * 10 + column + 1, options);
                    foreach (var packet in packets)
                        result.Add(new($"mapping_{sign}", VideoWallSequenceBuilder.TargetIp(column, row),
                            VideoWallSequenceBuilder.TcpPort, packet));
                }
            }
        }
        AddControl("commit", 0x61B8);
        AddControl("refresh", 0x61B9);
        return Wrap(result, options);

        void AddControl(string phase, int command)
        {
            foreach (var ip in VideoWallSequenceBuilder.AllTargetIps())
                result.Add(new(phase, ip, VideoWallSequenceBuilder.TcpPort, VideoWallPacketCodec.Control(command)));
        }
    }

    /// <summary>旧运行态入口的明确布局投影。</summary>
    public static WallLayout Preset(string mode) => mode switch
    {
        "single" => new("窗口1全屏", [new(WallRegion.Fullscreen, new(WallInputKind.Window1))]),
        "double" => new("窗口1左／窗口2右", [new(WallRegion.Left, new(WallInputKind.Window1)), new(WallRegion.Right, new(WallInputKind.Window2))]),
        _ => throw new VideoWallException($"未知的视频墙模式：{mode}"),
    };

    private static VideoWallInputDescriptor Resolve(WallInput input, VideoWallProtocolOptions options)
    {
        var key = input.Kind switch
        {
            WallInputKind.Window1 => "window_1",
            WallInputKind.Window2 => "window_2",
            WallInputKind.Laptop => "laptop",
            _ => input.IpAddress,
        };
        if (options.Inputs.TryGetValue(key, out var source) && source is not null) return source;
        throw new VideoWallException("该输入缺少已确认的设备流描述，当前只能保存和预览。");
    }

    private static bool IsLegacyProfile(VideoWallProtocolOptions options) =>
        options.PreserveLegacyPresets && options.SendType == 1 && options.CutSync && !options.IgmpV3 &&
        Matches("window_1", 55, options) && Matches("window_2", 56, options);

    private static bool Matches(string key, int number, VideoWallProtocolOptions? options = null) =>
        options is not null && options.Inputs.TryGetValue(key, out var input) &&
        input.MulticastAddress == $"224.1.1.{number}" && input.Port == 5000 && input.Width == 3840 && input.Height == 2160;

    private static VideoWallSendItem[] Wrap(IReadOnlyList<VideoWallSendItem> items, VideoWallProtocolOptions options) =>
        items.Select(item => item with { Packet = VideoWallPacketCodec.Wrap(item.Packet, options) }).ToArray();
}
