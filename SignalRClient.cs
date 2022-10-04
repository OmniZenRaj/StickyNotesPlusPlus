using System.Windows;
using System.Windows.Documents;
using System.Windows.Threading;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.WindowsAPICodePack.Taskbar;
using Microsoft.Extensions.Logging;
using System.Windows.Markup;
using System.Windows.Media;
using System.Reflection;
using System.Windows.Resources;

// TODO: Create User Settings for the following Collaboration Settings:
// Collaboration Note = True/False
// - This changes some of the Note behavior such as:
// - Create a text entry paragraph with Avatar & Borders for first message
// - Change the tooltip text of the Note Titlebar to show connection info
// User Alias (CCI/157602 = "RAJ")
// User Avatar (filePath to 64x64 Image)
// Notify Icon = True, Notify Sound = Default
// Use Other Formatting = Yes
// Show Borders = True, Border Color = Auto, Border Width = Auto
// Allow the definition of Groups (including downloading already setup groups)
// Allow the selction of Users to send for this Note

namespace OmniZenNotes;
internal class SignalRClient
{
    const string MS_XAML_SCHEME = @"http://schemas.microsoft.com/winfx/2006/xaml/";
    internal static HubConnection CollaborateHubConnection;
    const string SEND_HUB_METHOD = "SEND";
    const string BROADCAST_HUB_METHOD = "BROADCAST";
    static int progressValue = 0;

    internal async static void InitSignalR() {
        try {
#if DEBUG
            string url = S.Default.HUB_URL_DEBUG;
#else
            string url = S.Default.HUB_URL;
#endif
            CollaborateHubConnection = new HubConnectionBuilder()
            .WithUrl(url)
            .ConfigureLogging(logging => { logging.SetMinimumLevel(LogLevel.Trace); })
            .Build();
            await CollaborateHubConnection.StartAsync();
        } catch (Exception ex) {
            EX.LogException(ex);
        }
    }
    
    internal static void Init(NoteViewer nv) {
        CollaborateHubConnection.On<string, string>(BROADCAST_HUB_METHOD, (user, message) 
            => { OnBroadcast(nv, user, message); });
    }

    internal static void OnBroadcast(NoteViewer nv, string user, string message) {
        nv.Dispatcher.Invoke(() => {
            try {
                // Normally we don't display sent messages on Notes from the same user
                // But, during DEBUG mode, we do want to show it, but only on the other Notes.
#if DEBUG
#else           // Don't display messages if it was from this user 
                if (user.Equals(SH.GetUserName(), StringComparison.OrdinalIgnoreCase)) { return;}
#endif
                var paragraph = new Paragraph();
                // If the message is a MS XAML Scheme (ie from another StickyNotes++ User)
                if (message.Contains(MS_XAML_SCHEME)) {
                    paragraph = (Paragraph)XamlReader.Parse(message); // XAML content
#if DEBUG       // Use the Note.ID in the Tag so that the same Note doesn't display, but others do
                if (paragraph.Tag.Equals(nv.VM.Note.ID)) { return; } // Don't display for same Note
#endif
                } else {
                    paragraph.Inlines.Add(message); // Simple text
                }
                
                var range = new TextRange(paragraph.ContentStart, paragraph.ContentEnd);
                    paragraph.BorderThickness = new Thickness(0.5);
                    paragraph.BorderBrush = new SolidColorBrush(Colors.DarkMagenta);
                    // TODO: If user has custom Avatar (from Avatar list request), use that
                    if (nv.TryFindResource("Avatar_Icon") is System.Windows.Controls.Image i) {
                        InlineUIContainer iuc = new(i) { ToolTip = user };
                        i.Margin = new Thickness(3.0);
                        i.Height = 32; i.Width = 32;
                        paragraph.Inlines.InsertBefore(paragraph.Inlines.FirstInline, iuc);
                        paragraph.ToolTip = $@"{user}: {range.Text}     (Sent {U.Extensions.DateTimeFriendlyText(DateTime.Now)})"; ;
                }
                nv.uxRichTextBox.Document.Blocks.Add(paragraph);
                NotifyUserOfMessage(nv, paragraph.ToolTip.ToString());
                
            } catch (Exception ex) {
                EX.LogException(ex);
            }
        });
    }
    
    internal static void OnSendSignalR(string message) {

        if (CollaborateHubConnection.State == HubConnectionState.Connected) {
            var user = SH.GetUserName();
            CollaborateHubConnection.InvokeAsync(SEND_HUB_METHOD, user, message);
        } else {
            MessageBox.Show($"ERROR: Cannot Send. No Connection Exists to Server {CollaborateHubConnection}", "Server Connection Problem.");
        }
    }

    internal static void UpdateTaskBar(Window window, int number) {
 
        Uri resourcePath = new("assets/images/overlay.png", UriKind.Relative);
        StreamResourceInfo sri = App.GetResourceStream(resourcePath);
        // BUG: Does NOT work (stream is for System.Windows.Controls.Image not ICON)
        // RND: How to make this work via example from Internet
        System.Drawing.Icon overlayIcon = new System.Drawing.Icon(sri.Stream);

        IntPtr intPtr = new System.Windows.Interop.WindowInteropHelper(window).Handle;
        TaskbarManager tm = TaskbarManager.Instance;
        tm.ApplicationId = App.GUID.ToString();
        tm.SetApplicationIdForSpecificWindow(intPtr, App.GUID.ToString());
        tm.SetProgressState(TaskbarProgressBarState.Paused, intPtr);
        tm.SetOverlayIcon(intPtr, overlayIcon, $"{number}");

        DispatcherTimer dt = new DispatcherTimer();
        dt.Tick += new((sender, e) => {
            TaskbarManager tm = TaskbarManager.Instance;
            if (progressValue >= 100) { progressValue = 0; }
            tm.SetProgressValue(progressValue += 10, 100);
        });
        dt.Interval = TimeSpan.FromMilliseconds(100);
        dt.Start();
    }

    internal static void NotifyUserOfMessage(Window window, string message) {

        // UpdateTaskBar(window, 3);
        
        Assembly assembly = Assembly.GetEntryAssembly();
        System.Drawing.Icon appIcon = System.Drawing.Icon.ExtractAssociatedIcon(assembly.Location);
        SH.AddNotifyIcon(window, App.GUID, appIcon, assembly.GetName().Name);

        System.Drawing.Icon balloonIcon = System.Drawing.Icon.ExtractAssociatedIcon(assembly.Location);
        string title = $"{Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyDescriptionAttribute>()?.Description} ";
        SH.ModifyNotifyIcon(window, App.GUID, appIcon, balloonIcon, title, message);
        Vanara.PInvoke.RECT R = SH.GetNotifyIconLocation(window, App.GUID);
        Console.WriteLine($"GetTaskBarIconLocation RECT = {R}");
        SH.DeleteNotifyIcon(window, App.GUID);

        // Get the wav sound asset resource steam from the 
        Uri resourcePath = new("assets/sounds/alarm01.wav", UriKind.Relative);
        StreamResourceInfo sri = App.GetResourceStream(resourcePath);
        System.Media.SoundPlayer sp = new();
        sp.Stream = sri.Stream;
        sp.Play();
    }
}