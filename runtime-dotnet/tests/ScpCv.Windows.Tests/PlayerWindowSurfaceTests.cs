// 验证播放器预备画面不会提前移除当前画面，切入时也不会卸载已就绪的资源。
using System.Windows.Controls;
using ScpCv.PlayerWorker;

namespace ScpCv.Windows.Tests;

public sealed class PlayerWindowSurfaceTests
{
    [Fact]
    public async Task PendingSurfaceStaysInVisualTreeWhenPromoted()
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                var window = new PlayerWindow();
                var host = Assert.IsType<Grid>(window.FindName("SurfaceHost"));
                var previous = new Border();
                var pending = new Border();
                window.SetSurface(previous);

                window.PrepareSurface(pending);

                Assert.Equal(2, host.Children.Count);
                Assert.Same(pending, host.Children[0]);
                Assert.Same(previous, host.Children[1]);

                window.SetSurface(pending);

                Assert.Single(host.Children);
                Assert.Same(pending, host.Children[0]);
                window.Close();
                completion.SetResult();
            }
            catch (Exception exception)
            {
                completion.SetException(exception);
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        await completion.Task.WaitAsync(TimeSpan.FromSeconds(10));
    }
}
