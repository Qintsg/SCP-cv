// 转换加载阶段的已知交互终止与未知原生失败分开；仅自有MTA owner读取Desktop，不影响导出阶段。
using System;
using System.IO;
using unoidl.com.sun.star.frame;
using unoidl.com.sun.star.lang;
using static ScpCv.LibreOfficeHost.Uno.UnoConnection;

namespace ScpCv.LibreOfficeHost.Uno;

internal static class PresentationConversionLoader
{
    /// <summary>:param connection: 独占自有Office，尚无转换模型。:param source: 已校验原件。:param output: 空自有stage。:returns: 实际模型；仅已终止且无副作用的加载拒绝为typed，其余错误原样保持。</summary>
    internal static XComponent Load(UnoConnection connection, string source, string output)
    {
        var interaction = new RejectInteractiveRequest();
        XComponent? loaded;
        try
        {
            loaded = ((XComponentLoader)connection.Desktop).loadComponentFromURL(new Uri(source).AbsoluteUri,
                "_blank", 0, new[] { Property("Hidden", true), Property("ReadOnly", true), Property("MacroExecutionMode", (short)0),
                    Property("UpdateDocMode", (short)0), interaction.Descriptor });
        }
        catch (Exception error) when (interaction.Aborted)
        {
            try { VerifyNoComponent(connection, output); }
            catch (Exception verification)
            {
                throw new AggregateException("加载失败且加载拒绝安全验证未确认，保留两原因。", error, verification);
            }
            throw new PresentationLoadRejectedException(error);
        }
        if (loaded != null) return loaded;
        if (!interaction.Aborted) throw new InvalidOperationException("转换加载返回空文稿，但没有交互终止证明。");
        VerifyNoComponent(connection, output);
        throw new PresentationLoadRejectedException();
    }

    /// <summary>:param connection: 本次独占转换Office。:param output: 自有stage。:returns: Desktop无组件、没有输出；失败仍沿原生不确定路径。</summary>
    private static void VerifyNoComponent(UnoConnection connection, string output)
    {
        if (connection.Desktop.getComponents().hasElements())
            throw new InvalidOperationException("加载终止后仍有自有文稿组件，不能记明确失败。");
        PresentationLoadRejectedException.VerifyEmptyOutput(output);
    }
}
