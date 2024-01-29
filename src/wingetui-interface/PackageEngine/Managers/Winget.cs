﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Security.Cryptography.Certificates;
using Windows.Management.Deployment;
using Windows.Graphics.Display;
using System.IO;
using System.Diagnostics;
using System.Data.Common;
using System.Text.RegularExpressions;
using System.Net.Http.Headers;

namespace ModernWindow.PackageEngine.Managers
{
    public class Winget : PackageManagerWithSources
    {
        new public static string[] FALSE_PACKAGE_NAMES = new string[] { "", "have", "the", "Id" };
        new public static string[] FALSE_PACKAGE_IDS = new string[] { "", "have", "an", "'winget", "pin'", "have", "an", "Version" };
        new public static string[] FALSE_PACKAGE_VERSIONS = new string[] { "", "have", "an", "'winget", "pin'", "have", "an", "Version" };
        private static LocalPcSource LocalPcSource { get; } = new LocalPcSource();
        private static AndroidSubsystemSource AndroidSubsystemSource { get; } = new AndroidSubsystemSource();
        private static SteamSource SteamSource { get; } = new SteamSource();
        private static UbisoftConnectSource UbisoftConnectSource { get; } = new UbisoftConnectSource();
        private static GOGSource GOGSource { get; } = new GOGSource();
        private static MicrosoftStoreSource MicrosoftStoreSource { get; } = new MicrosoftStoreSource();

        public override async Task<Package[]> FindPackages_UnSafe(string query)
        {
            var Packages = new List<Package>();
            Process p = new Process();
            ProcessStartInfo startInfo = new ProcessStartInfo()
            {
                FileName = Status.ExecutablePath,
                Arguments = Properties.ExecutableCallArgs + " search " + query,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            Console.WriteLine(Status.ExecutablePath);
            p.StartInfo = startInfo;
            p.Start();

            string OldLine = "";
            int IdIndex = -1;
            int VersionIndex =-1;
            int SourceIndex = -1;
            bool DashesPassed = false;
            string line;
            while((line = await p.StandardOutput.ReadLineAsync()) != null)
            {
                if(!DashesPassed && line.Contains("---"))
                {
                    var HeaderPrefix = OldLine.Contains("SearchId")? "Search": "";
                    IdIndex = OldLine.IndexOf(HeaderPrefix+"Id");
                    VersionIndex = OldLine.IndexOf(HeaderPrefix+"Version");
                    SourceIndex = OldLine.IndexOf(HeaderPrefix+"Source");
                    DashesPassed = true;
                }
                else if (DashesPassed && IdIndex > 0 && VersionIndex > 0 && IdIndex < VersionIndex && VersionIndex < line.Length)
                {
                    string name = line[..IdIndex].Trim();
                    string id = line[IdIndex..].Trim().Split(' ')[0];
                    string version = line[VersionIndex..].Trim().Split(' ')[0];
                    ManagerSource source;
                    if (SourceIndex == -1 || SourceIndex >= line.Length)
                        source = MainSource;
                    else
                    {
                        string sourceName = line[SourceIndex..].Trim().Split(' ')[0];
                        if (SourceReference.ContainsKey(sourceName))
                            source = SourceReference[sourceName];
                        else
                        {
                            source = new ManagerSource(this, sourceName, new Uri("https://microsoft.com/winget"));
                            SourceReference.Add(source.Name, source);
                        }    
                    }
                    Packages.Add(new Package(name, id, version, source, this));
                }
                OldLine = line;
            }

            await Task.Run(p.WaitForExit);

            return Packages.ToArray();

        }

        public override async Task<UpgradablePackage[]> GetAvailableUpdates_UnSafe()
        {
            var Packages = new List<UpgradablePackage>();
            Process p = new Process();
            ProcessStartInfo startInfo = new ProcessStartInfo()
            {
                FileName = Status.ExecutablePath,
                Arguments = Properties.ExecutableCallArgs + " update --include-unknown",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            Console.WriteLine(Status.ExecutablePath);
            p.StartInfo = startInfo;
            p.Start();

            string OldLine = "";
            int IdIndex = -1;
            int VersionIndex =-1;
            int NewVersionIndex =-1;
            int SourceIndex = -1;
            bool DashesPassed = false;
            string line;
            while((line = await p.StandardOutput.ReadLineAsync()) != null)
            {
                if(!DashesPassed && line.Contains("---"))
                {
                    var HeaderPrefix = OldLine.Contains("SearchId")? "Search": "";
                    var HeaderSuffix = OldLine.Contains("SearchId")? "Header": "";
                    IdIndex = OldLine.IndexOf(HeaderPrefix+"Id");
                    VersionIndex = OldLine.IndexOf(HeaderPrefix+"Version");
                    NewVersionIndex = OldLine.IndexOf("Available"+HeaderSuffix);
                    SourceIndex = OldLine.IndexOf(HeaderPrefix+"Source");
                    DashesPassed = true;
                }
                else if (line.Trim() == "")
                {
                    DashesPassed = false;
                }
                else if (DashesPassed && IdIndex > 0 && VersionIndex > 0 && NewVersionIndex > 0 && IdIndex < VersionIndex && VersionIndex < NewVersionIndex && NewVersionIndex < line.Length )
                {
                    string name = line[..IdIndex].Trim();
                    string id = line[IdIndex..].Trim().Split(' ')[0];
                    string version = line[VersionIndex..NewVersionIndex].Trim();
                    string newVersion;
                    if(SourceIndex != -1)
                        newVersion = line[NewVersionIndex..SourceIndex].Trim();
                    else
                        newVersion = line[NewVersionIndex..].Trim().Split(' ')[0];

                    ManagerSource source;
                    if (SourceIndex == -1 || SourceIndex >= line.Length)
                        source = MainSource;
                    else
                    {
                        string sourceName = line[SourceIndex..].Trim().Split(' ')[0];
                        if (SourceReference.ContainsKey(sourceName))
                            source = SourceReference[sourceName];
                        else
                        {
                            source = new ManagerSource(this, sourceName, new Uri("https://microsoft.com/winget"));
                            SourceReference.Add(source.Name, source);
                        }    
                    }

                    Packages.Add(new UpgradablePackage(name, id, version, newVersion, source, this));
                }
                OldLine = line;
            }

            await Task.Run(p.WaitForExit);
            
            return Packages.ToArray();
        }

        public override async Task<Package[]> GetInstalledPackages_UnSafe()
        {
            var Packages = new List<Package>();
            Process p = new Process();
            ProcessStartInfo startInfo = new ProcessStartInfo()
            {
                FileName = Status.ExecutablePath,
                Arguments = Properties.ExecutableCallArgs + " list",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            Console.WriteLine(Status.ExecutablePath);
            p.StartInfo = startInfo;
            p.Start();

            string OldLine = "";
            int IdIndex = -1;
            int VersionIndex =-1;
            int SourceIndex = -1;
            int NewVersionIndex = -1;
            bool DashesPassed = false;
            string line;
            while((line = await p.StandardOutput.ReadLineAsync()) != null)
            {
                if(!DashesPassed && line.Contains("---"))
                {
                    var HeaderPrefix = OldLine.Contains("SearchId")? "Search": "";
                    var HeaderSuffix = OldLine.Contains("SearchId")? "Header": "";
                    IdIndex = OldLine.IndexOf(HeaderPrefix+"Id");
                    VersionIndex = OldLine.IndexOf(HeaderPrefix+"Version");
                    NewVersionIndex = OldLine.IndexOf("Available"+HeaderSuffix);
                    SourceIndex = OldLine.IndexOf(HeaderPrefix+"Source");
                    DashesPassed = true;
                }
                else if (DashesPassed && IdIndex > 0 && VersionIndex > 0 && IdIndex < VersionIndex && VersionIndex < line.Length)
                {
                    string name = line[..IdIndex].Trim();
                    string id = line[IdIndex..].Trim().Split(' ')[0];
                    if (NewVersionIndex == -1 && SourceIndex != -1) NewVersionIndex = SourceIndex;
                    else if (NewVersionIndex == -1 && SourceIndex == -1) NewVersionIndex = line.Length-1;
                    string version = line[VersionIndex..NewVersionIndex].Trim();

                    ManagerSource source;
                    string sourceName = line[SourceIndex..].Trim().Split(' ')[0].Trim();
                    if(SourceIndex == -1 || SourceIndex >= line.Length)
                        source = GetLocalSource(id); // Load Winget Local Sources
                    else
                    {
                        if (SourceReference.ContainsKey(sourceName))
                            source = SourceReference[sourceName];
                        else
                        {
                            source = new ManagerSource(this, sourceName, new Uri("https://microsoft.com/winget"));
                            SourceReference.Add(source.Name, source);
                        } 
                    }
                    Packages.Add(new Package(name, id, version, source, this));
                }
                OldLine = line;
            }

            await Task.Run(p.WaitForExit);

            return Packages.ToArray();
        }

        private ManagerSource GetLocalSource(string id)
        {
            Console.WriteLine("Getting local source for " + id);

            // Check if source is android
            bool AndroidValid = true;
            foreach(char c in id)
                if(!"abcdefghijklmnopqrstuvwxyz.…".Contains(c))
                {
                    AndroidValid = false;
                    break;
                }
            if (AndroidValid && id.Count(x => x == '.') >= 2)
                return AndroidSubsystemSource;

            // Check if source is Steam
            if ((id == "Steam" || id.Contains("Steam App ")) && id.Split("Steam App ")[1].Count(x => !"1234567890".Contains(x)) == 0)
                return SteamSource;

            // Check if source is Ubisoft Connect
            if (id == "Uplay" || id.Contains("Uplay Install ")  && id.Split("Uplay Install ")[1].Count(x => !"1234567890".Contains(x)) == 0)
                return UbisoftConnectSource;
            
            // Check if source is GOG
            if (id.EndsWith("_is1") && id.Split("_is1")[0].Count(x => !"1234567890".Contains(x)) == 0)
                return GOGSource;

            // Check if source is Microsoft Store
            if (id.Count(x => x == '_') == 1 && (id.Split('_')[^1].Length == 14 | id.Split('_')[^1].Length == 13 | id.Split('_')[^1].Length <= 13 && id[^1] == '…'))
                return MicrosoftStoreSource;

            return LocalPcSource;
        }

        public override string[] GetInstallParameters(Package package, InstallationOptions options)
        {
            throw new NotImplementedException();
        }

        public override ManagerSource GetMainSource()
        {
            return new ManagerSource(this, "winget", new Uri("https://cdn.winget.microsoft.com/cache"));
        }

        public override Task<PackageDetails> GetPackageDetails_UnSafe(Package package)
        {
            throw new NotImplementedException();
        }

        public override async Task<ManagerSource[]> GetSources_UnSafe()
        {
            List<ManagerSource> sources = new List<ManagerSource>();

            Process process = new Process();
            ProcessStartInfo startInfo = new ProcessStartInfo()
            {
                FileName = Status.ExecutablePath,
                Arguments = Properties.ExecutableCallArgs + " source list",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            process.StartInfo = startInfo;
            process.Start();

            bool dashesPassed = false;
            string line;
            while ((line = await process.StandardOutput.ReadLineAsync()) != null)
            {
                try {
                    if (string.IsNullOrEmpty(line))
                        continue;

                    if (!dashesPassed) {
                        if (line.Contains("---"))
                            dashesPassed = true;
                    } else {
                        string[] parts = Regex.Replace(line.Trim(), " {2,}", " ").Split(' ');
                        sources.Add(new ManagerSource(this, parts[0].Trim(), new Uri(parts[1].Trim())));
                    }
                } catch (Exception e) {
                    Console.WriteLine(e);
                }
            }
            await process.WaitForExitAsync();
            return sources.ToArray();
        }

        public override string[] GetUninstallParameters(Package package, InstallationOptions options)
        {
            throw new NotImplementedException();
        }

        public override string[] GetUpdateParameters(Package package, InstallationOptions options)
        {
            throw new NotImplementedException();
        }

        public override async Task RefreshSources()
        {
            Process process = new Process();
            ProcessStartInfo StartInfo = new ProcessStartInfo()
            {
                FileName = Properties.ExecutableFriendlyName,
                Arguments = Properties.ExecutableCallArgs + " source update",
                RedirectStandardOutput = true,
            };
            process.StartInfo = StartInfo;
            process.Start();
            await process.WaitForExitAsync();
        }

        protected override ManagerCapabilities GetCapabilities()
        {
            return new ManagerCapabilities()
            {
                CanRunAsAdmin = true,
                CanSkipIntegrityChecks = true,
                CanRunInteractively = true,
                SupportsCustomVersions = true,
                SupportsCustomArchitectures = true,
                SupportsCustomScopes = true,
                SupportsCustomLocations = true,
                SupportsCustomSources = true,
                Sources = new ManagerSource.Capabilities()
                {
                    KnowsPackageCount = false,
                    KnowsUpdateDate = false,
                }
            };
        }

        protected override ManagerProperties GetProperties()
        {
            ManagerProperties properties = new ManagerProperties()
            {
                Name = "Winget",
                Description = bindings.Translate("Microsoft's official package manager. Full of well-known and verified packages<br>Contains: <b>General Software, Microsoft Store apps</b>"),
                IconId = "winget",
                ColorIconId = "winget_color",
                ExecutableFriendlyName = "winget.exe",
                InstallVerb = "install",
                UninstallVerb = "uninstall",
                UpdateVerb = "update",
                ExecutableCallArgs = "",
            };
            return properties;
        }

        protected override async Task<ManagerStatus> LoadManager()
        {
            var status = new ManagerStatus();
            Process process = new Process()
            {
                StartInfo = new ProcessStartInfo()
                {
                    FileName = "cmd.exe",
                    Arguments = "/C echo %PROCESSOR_ARCHITECTURE%",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                }
            };
            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync();

            if(bindings.GetSettings("UseSystemWinget"))
                status.ExecutablePath = await bindings.Which("winget.exe");
            else if(output.Contains("ARM64") | bindings.GetSettings("EnableArmWinget"))
                status.ExecutablePath = Path.Join(Directory.GetParent(Environment.ProcessPath).FullName, "wingetui/PackageEngine/Managers/winget-cli_arm64/winget.exe");
            else
                status.ExecutablePath = Path.Join(Directory.GetParent(Environment.ProcessPath).FullName, "wingetui/PackageEngine/Managers/winget-cli_x64/winget.exe");
            
            status.Found = File.Exists(status.ExecutablePath);

            if(!status.Found)
                return status;

            process = new Process()
            {
                StartInfo = new ProcessStartInfo()
                {
                    FileName = status.ExecutablePath,
                    Arguments = "--version",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                }
            };
            process.Start();
            status.Version = (await process.StandardOutput.ReadToEndAsync()).Trim();

            if (status.Found && IsEnabled())
                await RefreshSources();

            return status;
        }
    }

    public class LocalPcSource : ManagerSource
    {
        public override string IconId { get { return "localpc"; } }
        public LocalPcSource() : base(Winget.bindings.App.Winget, Winget.bindings.Translate("Local PC"), new Uri("https://microsoft.com/local-pc-source"))
        { }
        public override string ToString()
        {
            return Winget.bindings.Translate("Local PC");
        }
    }

    public class AndroidSubsystemSource : ManagerSource
    {
        public override string IconId { get { return "android"; } }
        public AndroidSubsystemSource() : base(Winget.bindings.App.Winget, Winget.bindings.Translate("Android Subsystem"), new Uri("https://microsoft.com/local-pc-source"))
        { }
        public override string ToString()
        {
            return Winget.bindings.Translate("Android Subsystem");
        }
    }
    
    public class SteamSource : ManagerSource
    {
        public override string IconId { get { return "steam"; } }
        public SteamSource() : base(Winget.bindings.App.Winget, "Steam", new Uri("https://microsoft.com/local-pc-source"))
        { }
        public override string ToString()
        {
            return "Steam";
        }
    }
    
    public class UbisoftConnectSource : ManagerSource
    {
        public override string IconId { get { return "uplay"; } }
        public UbisoftConnectSource() : base(Winget.bindings.App.Winget, "Ubisoft Connect", new Uri("https://microsoft.com/local-pc-source"))
        { }
        public override string ToString()
        {
            return "Ubisoft Connect";
        }
    }
    
    public class GOGSource : ManagerSource
    {
        public override string IconId { get { return "gog"; } }
        public GOGSource() : base(Winget.bindings.App.Winget, "GOG", new Uri("https://microsoft.com/gog-source"))
        { }
        public override string ToString()
        {
            return "GOG";
        }
    }
    
    public class MicrosoftStoreSource : ManagerSource
    {
        public override string IconId { get { return "msstore"; } }
        public MicrosoftStoreSource() : base(Winget.bindings.App.Winget, "Microsoft Store", new Uri("https://microsoft.com/microsoft-store-source"))
        { }
        public override string ToString()
        {
            return "Microsoft Store";
        }
    }

}
