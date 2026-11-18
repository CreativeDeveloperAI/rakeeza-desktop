using System.IO;
using System.Text.Json;
using System.Windows;
using Microsoft.Web.WebView2.Core;
using Rakeeza.Commands.Bridge;

namespace Rakeeza.Desktop;

/// <summary>
/// تستضيف الواجهة في WebView2 وتمرّر رسائل الجسر إلى <see cref="CommandDispatcher"/> كما هي.
/// لا منطق هنا: النافذة ناقل فقط.
/// </summary>
public partial class MainWindow : Window
{
    /// <summary>أصل افتراضي يُربط بمجلّد ui/ المحلّي؛ لا يخرج أيّ طلب إلى الشبكة.</summary>
    internal const string AppHost = "app.rakeeza.local";

    private static readonly Uri AppOrigin = new($"https://{AppHost}/");

    private readonly CommandDispatcher _dispatcher;

    public MainWindow(CommandDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
        InitializeComponent();
        Loaded += OnLoadedAsync;
    }

    private async void OnLoadedAsync(object sender, RoutedEventArgs e)
    {
        await WebView.EnsureCoreWebView2Async();
        var core = WebView.CoreWebView2;

        core.Settings.AreDevToolsEnabled = IsDebugBuild;
        core.Settings.AreDefaultContextMenusEnabled = IsDebugBuild;
        core.Settings.IsStatusBarEnabled = false;

        var uiFolder = Path.Combine(AppContext.BaseDirectory, "ui");
        core.SetVirtualHostNameToFolderMapping(AppHost, uiFolder, CoreWebView2HostResourceAccessKind.Deny);
        core.WebMessageReceived += OnWebMessageReceivedAsync;
        core.Navigate(new Uri(AppOrigin, "index.html").AbsoluteUri);
    }

    private async void OnWebMessageReceivedAsync(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        // لا نقبل رسائل الجسر إلّا من أصل الواجهة نفسها.
        if (!Uri.TryCreate(e.Source, UriKind.Absolute, out var source)
            || !string.Equals(source.Host, AppHost, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var response = await _dispatcher.DispatchJsonAsync(e.WebMessageAsJson, CancellationToken.None);
        WebView.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(response, BridgeJson.Options));
    }

#if DEBUG
    private const bool IsDebugBuild = true;
#else
    private const bool IsDebugBuild = false;
#endif
}
