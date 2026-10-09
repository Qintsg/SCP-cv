// 仅表示加载交互已终止且没有模型/组件/输出；不是原生导出故障或正常退出证明。
using System;
using System.IO;

namespace ScpCv.LibreOfficeHost.Uno;

internal sealed class PresentationLoadRejectedException : Exception
{
    /// <summary>:param cause: 加载调用的原始错误，null表示明确返回空模型。:returns: 固定不泄路径/密码的拒绝原因。</summary>
    internal PresentationLoadRejectedException(Exception? cause = null)
        : base("文稿加载交互已终止；密码、修复或确认请求不支持，未生成转换制品。", cause) { }

    /// <summary>:param output: 已验证的自有暂存目录。:returns: 没有任何输出；读目录失败或有文件/目录则不允许明确拒绝。</summary>
    internal static void VerifyEmptyOutput(string output)
    {
        if (!Directory.Exists(output) || Directory.GetFileSystemEntries(output).Length != 0)
            throw new InvalidOperationException("加载拒绝没有取得空输出证明，不能记明确失败。");
    }
}
