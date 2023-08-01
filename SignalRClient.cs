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
using System.Globalization;
using System.Collections.Generic;
using System.Windows.Media.Imaging;

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
    readonly static DispatcherTimer Timer = new() {};
    readonly static Dictionary<Window, int> WindowNotifications = new();
    readonly static System.Media.SoundPlayer SoundPlayer = new();

    internal static string HubURL = S.Default.HUB_URL;
    
    const string SEND_HUB_METHOD = "SEND";
    const string BROADCAST_HUB_METHOD = "BROADCAST";
    const int MAX_SIGNALR_SEND_RETRIES = 5;

    internal static void Init(NoteViewer nv) {
        try {
#if DEBUG
            //HubURL = S.Default.HUB_URL_DEBUG;
#endif
            nv.CollaborateHubConnection = new HubConnectionBuilder()
            .WithUrl(HubURL)
            .WithAutomaticReconnect()
            .ConfigureLogging(logging => { logging.SetMinimumLevel(LogLevel.Trace); })
            .Build();
#if DEBUG
            nv.CollaborateHubConnection.ServerTimeout = new TimeSpan(1, 0, 0); // Time out at 1 Hour
#endif
            nv.CollaborateHubConnection.StartAsync();

            nv.CollaborateHubConnection.On<DateTime, string, string, string>(BROADCAST_HUB_METHOD, (date, id, user, message)
                => { OnBroadcast(nv, date, id, user, message); });

        } catch (Exception ex) {            
            EX.LogException(ex);
        }
    }
    
    internal static void OnBroadcast(NoteViewer nv, DateTime date, string id, string user, string message) {
        nv.Dispatcher.Invoke(() => {
            try {
                string text = nv.AddCollaborationMessage(date, id, user, message);

                if (user != SH.GetUserName() && nv.IsActive && nv.IsVisible ) {
                    NotifyUserOfMessage(nv, date, id, user, text);
                }

            } catch (Exception ex) {
                EX.LogException(ex);
            }
        });
    }

    internal static void OnSendSignalR(NoteViewer nv, string xaml) {
        int retries = 0;
        while (retries++ <= MAX_SIGNALR_SEND_RETRIES){
            if (nv.CollaborateHubConnection?.State == HubConnectionState.Connected) {
                string user = SH.GetUserName();
                string guid = Guid.NewGuid().ToString();
                nv.CollaborateHubConnection.InvokeAsync(SEND_HUB_METHOD, DateTime.UtcNow, guid, user, xaml);
                retries = MAX_SIGNALR_SEND_RETRIES + 1; // exit while
            }
        }
        if (nv.CollaborateHubConnection?.State != HubConnectionState.Connected) {
            MessageBox.Show($"ERROR: Cannot Send. Connection to Hub Server {S.Default.HUB_URL } could NOT be Established.", "Server Connection Problem.");
        }
    }

    internal static void UpdateTaskBar(Window window, int notifications) {

        // Clear the Taskbar stuff (remember the count overlay is for all Note Windows)
        if (notifications == 0) {
            WindowNotifications.Remove(window);
            UpdateCountOverlay(window, WindowNotifications.Count);
            return;     // <-- EXIT Function Early
        }
        
        // Make taskbar Icon Progress Bar cycle up/down for 10 loops to indicate messages pending
        int progressValue = 0; int progressTotalLoops = 10;  bool direction = false;
        const int progressMaxValue = 100; const int progressInterval = 10;
        
        Timer.Tick += new((sender, e) => {
            TaskbarManager.Instance.SetProgressState(TaskbarProgressBarState.Paused);
            if (progressValue >= progressMaxValue || progressValue <= progressInterval) {
                direction = !direction;
                progressTotalLoops -= 1;
                progressValue = progressValue <= progressInterval ? progressInterval : progressMaxValue - progressInterval;
            }
            if (progressTotalLoops >= 0) {
                progressValue = direction ? progressValue += progressInterval : progressValue -= progressInterval;
                TaskbarManager.Instance.SetProgressValue(progressValue, progressMaxValue);
            } else {
                progressValue = 0; progressTotalLoops = 10; direction = false;
                TaskbarManager.Instance.SetProgressValue(0, 0);
                Timer.Stop();
            }
        });
        Timer.Interval = TimeSpan.FromMilliseconds(50);
        Timer.Start();

        UpdateCountOverlay(window, notifications);
    }

    // Remember the overall count if for all Notes windows
    internal static void UpdateCountOverlay(Window window, int count) {

        window.TaskbarItemInfo = new();

        if (count == 0) {
            TaskbarManager.Instance.SetProgressValue(0, 0);
            Timer.Stop();
            return;
        } 

        // DOC: Icon Overlay only works if Taskbar Settings is Small taskbar buttons = Off and Show Badges = On
        DrawingVisual dv = new();
        DrawingContext dc = dv.RenderOpen();
        dc.DrawEllipse(Brushes.Red, null, new Point(0, 0), 25, 25);
        dc.DrawText(new FormattedText($"{count}", CultureInfo.GetCultureInfo("en-us"), FlowDirection.LeftToRight, new Typeface("Consolas"), 36.0, Brushes.
        Yellow, (float)VisualTreeHelper.GetDpi(window).PixelsPerDip), new Point(-12.5, -22.5));
        dc.Close();
        DrawingImage di = new(dv.Drawing);

        window.TaskbarItemInfo = new();
        window.TaskbarItemInfo.Overlay = di;
        window.TaskbarItemInfo.Description = $"You have {count} Unread StickyNote++ Text(s).";
    }
    
    internal static void NotifyUserOfMessage(Window window, DateTime date, string id, string user, string message) {

        if( WindowNotifications.TryGetValue(window, out int notifications)) {
            notifications++;
            WindowNotifications.Remove(window);
        } else {
            notifications = 1;
        }
        WindowNotifications.Add(window, notifications);
    
        int totalNotifications = 0;
        foreach (var n in WindowNotifications.Values) {
            totalNotifications += n;
        }
        UpdateTaskBar(window, totalNotifications);
        
        Assembly assembly = Assembly.GetEntryAssembly();
        System.Drawing.Icon appIcon = System.Drawing.Icon.ExtractAssociatedIcon(SH.GetEXEFileInfo()?.FullName);
        SH.AddNotifyIcon(window, App.GUID, appIcon, assembly.GetName().Name);

        System.Drawing.Icon balloonIcon = System.Drawing.Icon.ExtractAssociatedIcon(SH.GetEXEFileInfo()?.FullName);
        SH.ModifyNotifyIcon(window, App.GUID, appIcon, balloonIcon, user, message);

        Vanara.PInvoke.RECT R = SH.GetNotifyIconLocation(window, App.GUID);
        Console.WriteLine($"GetTaskBarIconLocation RECT = {R}");

        SH.DeleteNotifyIcon(window, App.GUID);

        // Get the wav sound asset resource steam from the 
        Uri resourcePath = new("assets/sounds/alarm01.wav", UriKind.Relative);
        StreamResourceInfo sri = Application.GetResourceStream(resourcePath);
        SoundPlayer.Stop();
        SoundPlayer.Stream = sri.Stream;
        SoundPlayer.Play();
    }
}