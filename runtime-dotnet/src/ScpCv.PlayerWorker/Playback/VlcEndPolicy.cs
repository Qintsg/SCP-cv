// VLC 自然结束回调的资源身份与源代次判定。
namespace ScpCv.PlayerWorker.Playback;

public enum VlcEndAction { Ignore, Replay, Stop }

public static class VlcEndPolicy
{
    public static VlcEndAction Decide(
        object? currentPlayer,
        object endedPlayer,
        long currentGeneration,
        long endedGeneration,
        bool loopEnabled)
    {
        ArgumentNullException.ThrowIfNull(endedPlayer);
        if (!ReferenceEquals(currentPlayer, endedPlayer) || currentGeneration != endedGeneration)
            return VlcEndAction.Ignore;
        return loopEnabled ? VlcEndAction.Replay : VlcEndAction.Stop;
    }
}
