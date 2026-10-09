// 已复现的主序重复风险判断；纯值层不引用UNO，不调整或保存动画模型。
namespace ScpCv.LibreOfficeHost.Uno;

internal static class PresentationAnimationSafety
{
    /// <summary>:param repeatCount: UNO已解码的double次数；void/其它类型为空。:param indefinite: 是否为INDEFINITE。:returns: 是否可能进入已复现的第二轮主序。</summary>
    internal static bool RepeatsMainSequence(double? repeatCount, bool indefinite) => indefinite || repeatCount >= 2.0;
}
