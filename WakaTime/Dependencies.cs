﻿using System.IO;
using System.IO.Compression;
using System.Net;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using System;
using System.Collections.Generic;

namespace WakaTime
{
    public class Dependencies
    {
        private const string CurrentPythonVersion = "3.5.2";
        private static string PythonBinaryLocation { get; set; }
        private static string PythonDownloadUrl
        {
            get
            {
                var arch = ProcessorArchitectureHelper.Is64BitOperatingSystem ? "amd64" : "win32";
                return string.Format("https://www.python.org/ftp/python/{0}/python-{0}-embed-{1}.zip", CurrentPythonVersion, arch);
            }
        }
        public static string AppDataDirectory
        {
            get
            {
                var roamingFolder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                var appFolder = Path.Combine(roamingFolder, "WakaTime");

                // Create folder if it does not exist
                if (!Directory.Exists(appFolder))
                    Directory.CreateDirectory(appFolder);

                return appFolder;
            }
        }
        internal static string CliLocation => Path.Combine(AppDataDirectory, Constants.CliFolder);

        public static void DownloadAndInstallCli()
        {
            Logger.Debug("Downloading wakatime-cli...");
            var destinationDir = AppDataDirectory;
            var localZipFile = Path.Combine(destinationDir, "wakatime-cli.zip");

            // Download wakatime-cli
            var client = GetWebClient();
            client.DownloadFile(Constants.CliUrl, localZipFile);
            Logger.Debug("Finished downloading wakatime-cli.");

            // Remove old folder if it exists
            RecursiveDelete(Path.Combine(destinationDir, "legacy-python-cli-master"));

            // Extract wakatime-cli zip file
            Logger.Debug($"Extracting wakatime-cli to: {destinationDir}");
            ZipFile.ExtractToDirectory(localZipFile, destinationDir);
            Logger.Debug("Finished extracting wakatime-cli.");

            try
            {
                File.Delete(localZipFile);
            }
            catch { /* ignored */ }
        }

        public static void DownloadAndInstallPython()
        {
            Logger.Debug("Downloading python...");
            var url = PythonDownloadUrl;
            var destinationDir = AppDataDirectory;
            var localZipFile = Path.Combine(destinationDir, "python.zip");
            var extractToDir = Path.Combine(destinationDir, "python");

            // Download python
            var client = GetWebClient();
            client.DownloadFile(url, localZipFile);
            Logger.Debug("Finished downloading python.");

            // Remove old python folder if it exists
            RecursiveDelete(extractToDir);

            // Extract python cli zip file
            Logger.Debug($"Extracting python to: {extractToDir}");
            ZipFile.ExtractToDirectory(localZipFile, extractToDir);
            Logger.Debug("Finished extracting python.");

            try
            {
                File.Delete(localZipFile);
            }
            catch { /* ignored */ }
        }

        internal static bool IsPythonInstalled()
        {
            return GetPython() != null;
        }

        internal static string GetPython()
        {
            if (PythonBinaryLocation == null)
                PythonBinaryLocation = GetEmbeddedPythonPath();

            if (PythonBinaryLocation == null)
                PythonBinaryLocation = GetPythonPathFromMicrosoftRegistry();

            if (PythonBinaryLocation == null)
                PythonBinaryLocation = GetPythonPathFromFixedPath();

            return PythonBinaryLocation;
        }

        private static WebClient GetWebClient()
        {
            if (!ServicePointManager.SecurityProtocol.HasFlag(SecurityProtocolType.Tls12))
                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;

            var proxy = WakaTimePackage.GetProxy();

            return new WebClient { Proxy = proxy };
        }

        internal static string GetPythonPathFromMicrosoftRegistry()
        {
            try
            {
                var regex = new Regex(@"""([^""]*)\\([^""\\]+(?:\.[^"".\\]+))""");
                var pythonKey = Registry.ClassesRoot.OpenSubKey(@"Python.File\shell\open\command");
                if (pythonKey == null)
                    return null;

                var python = pythonKey.GetValue(null).ToString();
                var match = regex.Match(python);

                if (!match.Success) return null;

                var directory = match.Groups[1].Value;
                var fullPath = Path.Combine(directory, "pythonw");
                var process = new RunProcess(fullPath, "--version");

                process.Run();

                if (!process.Success)
                    return null;

                Logger.Debug($"Python found from Microsoft Registry: {fullPath}");

                return fullPath;
            }
            catch (Exception ex)
            {
                Logger.Error("GetPathFromMicrosoftRegistry:", ex);
                return null;
            }
        }

        internal static string GetPythonPathFromFixedPath()
        {
            var locations = new List<string>();
            for (var i = 26; i <= 50; i++)
            {
                locations.Add(Path.Combine("\\python" + i, "pythonw"));
                locations.Add(Path.Combine("\\Python" + i, "pythonw"));
            }

            foreach (var location in locations)
            {
                try
                {
                    var process = new RunProcess(location, "--version");
                    process.Run();

                    if (!process.Success) continue;
                }
                catch { /*ignored*/ }

                Logger.Debug($"Python found by Fixed Path: {location}");

                return location;
            }

            return null;
        }

        internal static string GetEmbeddedPythonPath()
        {
            var path = Path.Combine(AppDataDirectory, "python", "pythonw");
            try
            {
                var process = new RunProcess(path, "--version");
                process.Run();

                if (!process.Success)
                    return null;

                Logger.Debug($"Python found from embedded location: {path}");

                return path;
            }
            catch (Exception ex)
            {
                Logger.Error("GetEmbeddedPath:", ex);
                return null;
            }
        }

        internal static bool DoesCliExist()
        {
            return File.Exists(CliLocation);
        }

        internal static bool IsCliUpToDate()
        {
            var process = new RunProcess(Dependencies.GetPython(), CliLocation, "--version");
            process.Run();

            if (process.Success)
            {
                var currentVersion = process.Error.Trim();
                Logger.Info($"Current wakatime-cli version is {currentVersion}");

                Logger.Info("Checking for updates to wakatime-cli...");
                var latestVersion = Constants.LatestWakaTimeCliVersion();

                if (currentVersion.Equals(latestVersion))
                {
                    Logger.Info("wakatime-cli is up to date.");
                    return true;
                }

                Logger.Info($"Found an updated wakatime-cli v{latestVersion}");
            }
            return false;
        }

        internal static void RecursiveDelete(string folder)
        {
            try
            {
                Directory.Delete(folder, true);
            }
            catch { /* ignored */ }
            try
            {
                File.Delete(folder);
            }
            catch { /* ignored */ }
        }
    }
}
