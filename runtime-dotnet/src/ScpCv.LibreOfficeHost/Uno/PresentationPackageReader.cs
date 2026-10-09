// 只读读取PPTX布局语义和外部关系，拒绝自动联网；不重写或保存源文件。
using System;
using System.Collections.Generic;
using System.Linq;
using ScpCv.Runtime.Presentations;
using unoidl.com.sun.star.beans;
using unoidl.com.sun.star.container;
using unoidl.com.sun.star.drawing;
using unoidl.com.sun.star.lang;
using UnoText = unoidl.com.sun.star.text.XText;

namespace ScpCv.LibreOfficeHost.Uno;

internal sealed class PresentationPackageReader
{
    private readonly PresentationPackageMetadata _metadata;

    /// <summary>:param path: 摘要已核原件。:returns: 有界读取的语义提示；旧二进制PPT无PPTX提示。</summary>
    internal PresentationPackageReader(string path)
        => _metadata = new PresentationPackageMetadata(path);

    /// <summary>:param document: 只读导入模型。:returns: 恢复明确的“不换行+形状适应文字”语义，不动字体或修改原件。</summary>
    internal void ApplyLayout(XComponent document)
    {
        var pages = ((XDrawPagesSupplier)document).getDrawPages();
        foreach (var item in _metadata.Unwrapped)
        {
            if (item.Key > pages.getCount()) continue;
            var shapes = Flatten((XShapes)pages.getByIndex(item.Key - 1).Value).ToArray();
            foreach (var hint in item.Value)
            {
                var matches = shapes.Where(shape => shape is XNamed named && named.getName() == hint.Name &&
                    shape is UnoText text && text.getString() == hint.Text).ToArray();
                if (matches.Length != 1) continue;
                var props = (XPropertySet)matches[0];
                props.setPropertyValue("TextAutoGrowWidth", new uno.Any(true));
            }
        }
    }

    private static IEnumerable<XShape> Flatten(XShapes shapes)
    {
        for (var index = 0; index < shapes.getCount(); index++)
        {
            var shape = (XShape)shapes.getByIndex(index).Value;
            yield return shape;
            if (shape is XShapes nested) foreach (var child in Flatten(nested)) yield return child;
        }
    }
}
