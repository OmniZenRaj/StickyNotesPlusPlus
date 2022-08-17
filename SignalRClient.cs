using System.Windows;
using System.Windows.Documents;
using System.Windows.Threading;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.WindowsAPICodePack.Taskbar;

namespace OmniZenNotes
{
    internal class SignalRClient {
        // TODO: Setup proper Collaboration settings
        internal static void Init(NoteViewer nv, Note note) {
            if (note.Task.Reminder.LongNotification == true) {
                App.CollaborateHubConnection.On<string, string>("broadcastMessage", (user, message) => {
                    nv.Dispatcher.Invoke(() => {
                        var newMessage = $"{user}: {message}";
                        FlowDocument doc = nv.uxRichTextBox.Document;
                        TextRange tr = new (doc.ContentStart, doc.ContentEnd);
                        TextPointer tp = nv.uxRichTextBox.CaretPosition;
                        Paragraph para = new (new Run(newMessage));
                        doc.Blocks.Add(para);
                    });
                });
            }
        }

        async internal static void OnSendSignalR() {
            // RND Trying to get a icon to display message count
            /* TaskbarManager tm = TaskbarManager.Instance;
            tm.SetOverlayIcon(U.Shell.GetShellIcon(new (@"C:\Chrome.ico")), "Icon Text"); 
            */

            DispatcherTimer dt = new DispatcherTimer();
            dt.Tick += new ((sender, e) => {
                TaskbarManager tm = TaskbarManager.Instance;
                tm.SetProgressValue(2, 10);
            });
            dt.Interval = TimeSpan.FromMilliseconds(100);
            dt.Start();

            if (App.CollaborateHubConnection.State == HubConnectionState.Connected) {
                await App.CollaborateHubConnection.InvokeAsync("Send", "NoteViewer", "TEST 123");
            } else {
                MessageBox.Show("Cannot Send. No Connection Exists to Server");
            }
        }
    }
}