// 明确选择的文字适配只作用于只读内存模型，不保存、不主动改写原件文字/字体。
using unoidl.com.sun.star.beans;
using unoidl.com.sun.star.drawing;
using unoidl.com.sun.star.lang;
using unoidl.com.sun.star.text;

namespace ScpCv.LibreOfficeHost.Uno;

internal static class PresentationTextLayout
{
    /// <summary>:param document: 只读模型。:param enabled: 用户明确选择，默认关闭。:returns: 使文字适应原形状，保留原件。</summary>
    internal static void Apply(XComponent document, bool enabled)
    {
        if (!enabled) return;
        var pages = ((XDrawPagesSupplier)document).getDrawPages();
        for (var index = 0; index < pages.getCount(); index++) Fit((XShapes)pages.getByIndex(index).Value);
    }

    private static void Fit(XShapes shapes)
    {
        for (var index = 0; index < shapes.getCount(); index++)
        {
            var shape = (XShape)shapes.getByIndex(index).Value;
            if (shape is XShapes nested) Fit(nested);
            if (shape is not XText text || string.IsNullOrEmpty(text.getString())) continue;
            var props = (XPropertySet)shape;
            if (props.getPropertySetInfo().hasPropertyByName("TextFitToSize"))
                props.setPropertyValue("TextFitToSize", new uno.Any(typeof(TextFitToSizeType), TextFitToSizeType.AUTOFIT));
        }
    }
}
