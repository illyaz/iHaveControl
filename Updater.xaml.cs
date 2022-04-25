namespace iHaveControl
{
    using ByteSizeLib;

    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Text.Json;
    using System.Threading.Tasks;
    using System.Windows;

    /// <summary>
    /// Interaction logic for Updater.xaml
    /// </summary>
    public partial class Updater : Window
    {
        private const string API_URL = "https://api.github.com/repos/illyaz/iHaveControl/releases/tags/latest";
        private static readonly HttpClient _http;

        static Updater()
        {
            _http = new();
            _http.DefaultRequestHeaders
                .Add("User-Agent", $"iHaveControl/{typeof(Updater).Assembly.GetName().Version}");
        }

        public Updater()
        {
            InitializeComponent();
            Loaded += Updater_Loaded;
        }

        private void Updater_Loaded(object sender, RoutedEventArgs e)
        {
            Task.Run(async () =>
            {
                try
                {
                    var gitRelease = JsonSerializer
                        .Deserialize<GitRelease>(await _http.GetStringAsync(API_URL))!;

                    var latestId = gitRelease.Name.Split('-',
                        StringSplitOptions.RemoveEmptyEntries)[^1].Trim();

                    var isLatest = latestId == ThisAssembly.Git.Commit;

                    if (isLatest)
                        await Dispatcher.InvokeAsync(() =>
                            AlertAndClose("No update available"));
                    else
                        await DownloadAndUpdate(gitRelease.Assets
                            .First(x => x.Name == "iHaveControl.exe"));
                }
                catch (Exception ex)
                {
                    await Dispatcher.InvokeAsync(() =>
                        AlertAndClose(ex.Message));
                }
            });
        }

        private async Task DownloadAndUpdate(Asset asset)
        {
            try
            {
                var temp = Path.GetTempFileName();
                using var req = new HttpRequestMessage(HttpMethod.Get, asset.BrowserDownloadUrl);
                using var res = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);
                using var stream = await res.Content.ReadAsStreamAsync();

                var length = (double)res.Content.Headers.ContentLength!;
                var bytesRead = 0;
                var read = 0;
                var buf = new byte[1024];

                using (var target = File.OpenWrite(temp))
                {
                    while ((read = await stream.ReadAsync(buf)) > 0)
                    {
                        bytesRead += read;
                        var percent = bytesRead / length;

                        await target.WriteAsync(buf, 0, read);
                        await Dispatcher.InvokeAsync(() =>
                        {
                            status.Content = $"[{ByteSize.FromBytes(bytesRead):0.00}/{ByteSize.FromBytes(length):0.00}] Downloading...";
                            progress.IsIndeterminate = false;
                            progress.Value = percent * 100;
                        });
                    }
                }

                await Dispatcher.InvokeAsync(() =>
                {
                    status.Content = "Updating ...";
                    progress.IsIndeterminate = true;
                });

                Process.Start(new ProcessStartInfo(temp,
                    $"--update \"{Environment.ProcessId},{Environment.ProcessPath!}\"")
                {
                    UseShellExecute = false
                });
            }
            catch (Exception ex)
            {
                await Dispatcher.InvokeAsync(() =>
                    AlertAndClose(ex.Message));
            }
        }

        private void AlertAndClose(string text, string? title = null)
        {
            MessageBox.Show(text, title ?? Title, MessageBoxButton.OK, MessageBoxImage.Warning);
            Close();
        }
    }
}
