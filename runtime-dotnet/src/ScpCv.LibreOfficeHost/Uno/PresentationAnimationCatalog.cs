// 仅深度诊断预登记动画模型；回调用官方CLI本地OID相等性查纯值标签，不访问远程节点。
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using unoidl.com.sun.star.animations;
using unoidl.com.sun.star.container;
using unoidl.com.sun.star.drawing;
using unoidl.com.sun.star.lang;

namespace ScpCv.LibreOfficeHost.Uno;

internal sealed class PresentationAnimationCatalog
{
    private readonly Dictionary<object, string> _labels = new();

    /// <summary>:param document: 自有只读文稿，由MTA owner读取。:returns: 有界且建立后不再修改的身份目录。</summary>
    internal PresentationAnimationCatalog(XComponent document)
    {
        var pages = ((XDrawPagesSupplier)document).getDrawPages();
        for (var page = 0; page < pages.getCount(); page++)
        {
            if (pages.getByIndex(page).Value is XAnimationNodeSupplier supplier)
                Visit(supplier.getAnimationNode(), "p" + (page + 1).ToString(CultureInfo.InvariantCulture) + "/root", 0);
        }
    }

    /// <summary>:param node: 官方CLI回调代理，只使用System.Object本地OID hash/equality。:returns: 已登记标签或unknown，不代表执行完成。</summary>
    internal string Label(object node) => _labels.TryGetValue(node, out var label) ? label : "unknown";

    /// <summary>:param node: owner线程的模型节点。:param label: 纯值树路径。:param depth: 有界深度。:returns: 只读属性并登记，不修改动画或原件。</summary>
    private void Visit(XAnimationNode node, string label, int depth)
    {
        if (depth > 64 || _labels.Count >= 4096) throw new InvalidDataException("动画节点诊断超出有界目录预算");
        if (_labels.ContainsKey(node)) throw new InvalidDataException("动画节点诊断遇到重复身份或环");
        _labels.Add(node, label);
        var effectType = 0;
        foreach (var value in node.UserData)
            if (value.Name == "node-type" && value.Value.Value is short or int)
                effectType = Convert.ToInt32(value.Value.Value, CultureInfo.InvariantCulture);
        PresentationGeometry.Note("presentation_animation_node: label=" + label + ";effect_type=" + effectType +
            ";animation_type=" + node.Type + ";begin_type=" + node.Begin.Type.Name + ";end_type=" + node.End.Type.Name +
            ";begin_trigger=" + (node.Begin.Value is Event start ? start.Trigger : -1));
        if (node is not XEnumerationAccess container) return;
        var children = container.createEnumeration();
        var index = 0;
        while (children.hasMoreElements())
            Visit((XAnimationNode)children.nextElement().Value, label + "/" + (index++).ToString(CultureInfo.InvariantCulture), depth + 1);
    }
}
