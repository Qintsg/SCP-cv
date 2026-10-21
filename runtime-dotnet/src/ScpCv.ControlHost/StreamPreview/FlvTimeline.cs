// 文件描述：编码代次之间的单调媒体时间线；项目：SCP-cv；文件：FlvTimeline.cs；作者：Qintsg；日期：2026-10-21。
namespace ScpCv.ControlHost.StreamPreview;

public sealed class FlvTimeline
{
    private ulong _last, _epoch;
    private uint _first;
    private bool _hasTimestamp, _started;

    /// <summary>:returns: 新编码代次从上一代之后接续。</summary>
    public void BeginEpoch() { _epoch = _hasTimestamp ? _last + 1 : 0; _started = false; }

    /// <summary>:param source: 库提供的源毫秒。:returns: 单调毫秒，保留32位正常回绕。</summary>
    public uint Map(uint source)
    {
        if (!_started) { _first = source; _started = true; }
        var delta = unchecked((int)(source - _first));
        var next = _epoch + (ulong)Math.Max(delta, 0);
        _last = _hasTimestamp ? Math.Max(next, _last) : next; _hasTimestamp = true;
        return unchecked((uint)_last);
    }
}
