// 预览的正式装配：模拟模式不可启动真实生产者，管理API只走无代理回环连接。
using ScpCv.ControlHost.Configuration;

namespace ScpCv.ControlHost.StreamPreview;

public static class StreamPreviewRegistration
{
    /// <summary>:param services: 正式容器。:param configuration: 服务端配置。:param safety: 安全模式。:returns: 单一共享池与独立维护生命周期。</summary>
    public static IServiceCollection AddStreamPreview(this IServiceCollection services, IConfiguration configuration, SafetyModeOptions safety)
    {
        var options = configuration.GetSection(PreviewOptions.SectionName).Get<PreviewOptions>() ?? new(); options.Validate();
        services.AddSingleton(options); services.AddSingleton<PreviewSourceResolver>();
        services.AddHttpClient("preview-relay", client => client.Timeout = TimeSpan.FromSeconds(3))
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { UseProxy = false, AllowAutoRedirect = false });
        services.AddSingleton(provider => new MediaMtxPreviewClient(provider.GetRequiredService<IHttpClientFactory>().CreateClient("preview-relay"), options));
        if (safety.IsSimulation) services.AddSingleton<IPreviewProducer, UnavailablePreviewProducer>();
        else services.AddSingleton<IPreviewProducer, PreviewProducer>();
        services.AddSingleton<PreviewBandwidth>(); services.AddSingleton<StreamPreviewRegistry>();
        services.AddHostedService<PreviewMaintenance>(); return services;
    }
}
