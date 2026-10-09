// 在自有只读内存模型中补零时长收尾，使末组动画快进后由原生队列继续推进；不保存原件。
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using unoidl.com.sun.star.animations;
using unoidl.com.sun.star.container;
using unoidl.com.sun.star.drawing;
using unoidl.com.sun.star.lang;
using unoidl.com.sun.star.presentation;
using unoidl.com.sun.star.uno;

namespace ScpCv.LibreOfficeHost.Uno;

internal sealed class PresentationEffectAdvance
{
    private readonly Dictionary<object, PresentationMainSequence?> _pages = new();

    /// <summary>:param context: 唯一MTA owner的官方上下文。:param document: 自有只读文稿。:returns: 只调整主序末尾，不改交互序列或现有效果。</summary>
    internal PresentationEffectAdvance(XComponentContext context, XComponent document)
    {
        var visited = new HashSet<object>();
        var pages = ((XDrawPagesSupplier)document).getDrawPages();
        for (var page = 0; page < pages.getCount(); page++)
        {
            var drawPage = pages.getByIndex(page).Value;
            _pages.Add(drawPage, drawPage is XAnimationNodeSupplier supplier
                ? Visit(context, supplier.getAnimationNode(), visited, 0) : null);
        }
    }

    /// <summary>:param page: 当前放映列表对应的真实模型页，不猜文稿编号。:returns: 是否有主序效果；未知页明确拒绝。</summary>
    internal bool HasMainEffects(XDrawPage page) => _pages.TryGetValue(page, out var present)
        ? present != null : throw new InvalidOperationException("当前放映页未绑定到自有动画模型");

    /// <summary>:param page: 真实控制器模型页。:returns: 预绑定主序与原末组，静态页为空，不靠时长猜测末组。</summary>
    internal PresentationMainSequence? MainSequence(XDrawPage page) => _pages.TryGetValue(page, out var sequence)
        ? sequence : throw new InvalidOperationException("实际页未绑定到自有动画模型");

    /// <summary>:param context: owner上下文。:param node: 当前模型节点。:param visited: 防重复/循环的有界集合。:param depth: 深度。:returns: 不改原子效果的时长/目标/顺序，也不补发控制调用。</summary>
    private static PresentationMainSequence? Visit(XComponentContext context, XAnimationNode node, HashSet<object> visited, int depth)
    {
        if (depth > 64 || visited.Count >= 4096 || !visited.Add(node))
            throw new InvalidDataException("演示文稿动画节点图超出有界预算或包含重复身份");
        var effectType = EffectNodeType.DEFAULT;
        foreach (var value in node.UserData)
            if (value.Name == "node-type" && value.Value.Value is short or int)
                effectType = (short)Convert.ToInt32(value.Value.Value, CultureInfo.InvariantCulture);
        // 交互序列独立于主序推进，不遍历/调整它，也不读取其叶子媒体属性。
        if (effectType == EffectNodeType.INTERACTIVE_SEQUENCE) return null;
        if (node is not XEnumerationAccess access) return null;
        var children = new List<XAnimationNode>();
        var enumeration = access.createEnumeration();
        while (enumeration.hasMoreElements()) children.Add((XAnimationNode)enumeration.nextElement().Value);
        if (effectType == EffectNodeType.MAIN_SEQUENCE && children.Count > 0)
        {
            if (node.Type != AnimationNodeType.SEQ || node is not XTimeContainer sequence)
                throw new InvalidDataException("演示文稿主序不是可支持的顺序容器");
            var repeat = node.RepeatCount.Value;
            // 26.8.0.3的主序第二轮已实测原生越界；只拦此模型，不动并行组/叶子重复或原件。
            if (PresentationAnimationSafety.RepeatsMainSequence(repeat is double count ? count : null,
                    repeat is Timing timing && timing == Timing.INDEFINITE))
                throw new InvalidDataException("当前LibreOffice版本暂不支持主动画序列重复播放；请取消主序重复，或手动选择静态逐页图片/PDF播放。原文件未修改。");
            var tail = (XAnimationNode)context.getServiceManager().createInstanceWithContext("com.sun.star.animations.ParallelTimeContainer", context);
            tail.Begin = new uno.Any(0.0);
            tail.Duration = new uno.Any(0.0);
            // 官方末子项skipTriggersNextEffect=false。自动空尾让原末组变成非末项，原生一次skip即继续next。
            sequence.appendChild(tail);
            PresentationGeometry.Note("presentation_effect_terminal_tick: children_before=" + children.Count);
            // 主序叶子目标、时长和媒体原样留给引擎；无需再做逐叶RPC扫描。
            return new PresentationMainSequence(node, sequence, children[children.Count - 1], tail);
        }
        PresentationMainSequence? present = null;
        foreach (var child in children)
        {
            var found = Visit(context, child, visited, depth + 1);
            if (found == null) continue;
            if (present != null) throw new InvalidDataException("同一页面含多个主动画序列，无法安全绑定末页完成事实");
            present = found;
        }
        return present;
    }
}
