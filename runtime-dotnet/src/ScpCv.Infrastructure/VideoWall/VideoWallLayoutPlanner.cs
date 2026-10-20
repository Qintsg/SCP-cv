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
        var signIndex = 0;
        foreach (var mapping in layout.Mappings.OrderBy(item => WallLayoutPolicy.Rectangle(item).Row)
            .ThenBy(item => WallLayoutPolicy.Rectangle(item).Column))
        {
            var input = Resolve(mapping.Input, options);
            var rect = WallLayoutPolicy.Rectangle(mapping);
            var startColumn = rect.Column;
            var columns = rect.Columns;
            // 此受限路径要求整除且4像素对齐，避免猜测原程序未充分验证的余数分支。
            if (input.Width % (columns * 4) != 0 || input.Height % (rect.Rows * 4) != 0)
                throw new VideoWallException("该输入尺寸尚无已验证的整除裁剪规则，未向设备发送命令。");
            var fragment = 0;
            var sign = mapping.Region == WallRegion.Right ? 2 : mapping.Region == WallRegion.Left ? 1 : ++signIndex;
            for (var row = rect.Row; row < rect.Row + rect.Rows; row++)
            {
                for (var column = startColumn; column < startColumn + columns; column++)
                {
                    var source = new VideoWallRectangle((column - startColumn) * input.Width / columns,
                        (row - rect.Row) * input.Height / rect.Rows, input.Width / columns, input.Height / rect.Rows);
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
        if (input.Kind is WallInputKind.Window3 or WallInputKind.Window4)
            throw new VideoWallException("窗口3/4仅前端预留，尚未接入实体输出，不能应用。");
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
