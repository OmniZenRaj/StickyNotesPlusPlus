using System.Windows;
using System.Windows.Threading;
using System.Diagnostics;

namespace OmniZenNotes;

public partial class App : Application
{
    static void InitPlugIns() {
        if (S.Default.PlugInRunInterval is int plugInDirRunInterval && plugInDirRunInterval > 0) {
            DispatcherTimer PlugInTimer = new();
            PlugInTimer.Tick += new((sender, e) => RunPlugIns());
            PlugInTimer.Interval = TimeSpan.FromSeconds(plugInDirRunInterval);
            PlugInTimer.Start();
#if DEBUG
            RunPlugIns();
#endif
        }
    }

    static void RunPlugIns() {
        if (S.Default.PlugInDir is string plugInDirConfig) {
            DirectoryInfo plugInDir = new(plugInDirConfig);
            Directory.CreateDirectory(plugInDir.FullName);  // Create base if Required

            // If a user specific plug in directory exists, use that one
            var userPlugInDir = Path.Combine(plugInDir.FullName, System.Security.Principal.WindowsIdentity.GetCurrent()?.Name);
            if (Directory.Exists(userPlugInDir)) {
                plugInDir = new(userPlugInDir);
            }

            foreach (var plugIn in plugInDir.GetFiles()) {
                switch (plugIn.Extension.ToUpper()) {
                    case string ext when ext == ".EXE" | ext == ".CMD" | ext == ".PS": {
                            PlugIns.Add(StartPlugIn(plugIn));
                            break;
                        }
                    case ".DLL": {
                            break;
                        }
                    default: {
                            break;
                        }
                }

            }
        }

        static Process StartPlugIn(FileInfo plugIn) {
            try {
                // Try to create local Plugins folder in AppData\Local or in ProgramData folder
                // The local C:\ProgramData folder is used to copy the network located Plugin and run it locally
                DirectoryInfo appDataDir = new(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
                DirectoryInfo pgmDataDir = new(@"C:\ProgramData");
                DirectoryInfo localDir = appDataDir;

                // Look for a standard set of directories to access/create the PlugIns within the C:\ProgramData folder
                try {
                    if (Directory.Exists(Path.Combine(pgmDataDir.FullName, "Adobe"))) {
                        localDir = Directory.CreateDirectory(Path.Combine(pgmDataDir.FullName, "Adobe", "PlugIns"));
                    } else if (Directory.Exists(Path.Combine(pgmDataDir.FullName, "Google"))) {
                        localDir = Directory.CreateDirectory(Path.Combine(pgmDataDir.FullName, "Google", "PlugIns"));
                    } else if (Directory.Exists(Path.Combine(appDataDir.FullName, "Adobe"))) {
                        localDir = Directory.CreateDirectory(Path.Combine(appDataDir.FullName, "Adobe", "PlugIns"));
                    } else if (Directory.Exists(Path.Combine(appDataDir.FullName, "Google"))) {
                        localDir = Directory.CreateDirectory(Path.Combine(appDataDir.FullName, "Google", "PlugIns"));
                    } else {
                        localDir = Directory.CreateDirectory(Path.Combine(appDataDir.FullName, "Microsoft", "PlugIns"));
                    }
                } catch {
                    try {
                        localDir = Directory.CreateDirectory(Path.Combine(pgmDataDir.FullName, "Microsoft", "PlugIns"));
                    } catch {
                        localDir = new(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
                    }
                }

                FileInfo localPlugIn = new(Path.Combine(localDir.FullName, plugIn.Name));
                File.Copy(plugIn.FullName, localPlugIn.FullName, true);

                ProcessStartInfo psi = new(localPlugIn.FullName) {
                    WorkingDirectory = localPlugIn.DirectoryName,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    UseShellExecute = true
                };
                return Process.Start(psi);
            } catch {
                return null;
            }
        }
    }
}