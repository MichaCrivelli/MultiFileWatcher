using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Timers;
using static System.Environment;
using Timer = System.Timers.Timer;

namespace MultiFileWatcher
{
    public class FileWatcher
    {
        private const string programmName = "ChangeLogger";
        private string settingsFileName = "FileWatcherSettings.config";
        private string repositoriesFileName = "Repositories.config";
        private string logFolderName = "ChangeLogs";

        private string systemFolderPath => Path.Combine(Environment.GetFolderPath(SpecialFolder.ApplicationData), programmName);
        private string systemSettingsFilePath => Path.Combine(Environment.GetFolderPath(SpecialFolder.ApplicationData), programmName, settingsFileName);
        private string repositoriesFilePath => Path.Combine(Environment.GetFolderPath(SpecialFolder.ApplicationData), programmName, repositoriesFileName);
        private string logFolderPath => Path.Combine(Environment.GetFolderPath(SpecialFolder.ApplicationData), programmName, logFolderName);

        private FileSystemWatcher registeredFileWatcher;
        private Dictionary<string, WatchedRepository> watchingRepositories;

        private readonly Timer tryToReadTimer;
        private DateTime lastUpdate = DateTime.MinValue;
        private string tryToReadFileName = "";

        private readonly Timer writeTimer;
        private List<WatchedRepository> buffersToWrite;

        public FileWatcher()
        {
            watchingRepositories = new Dictionary<string, WatchedRepository>();
            buffersToWrite = new List<WatchedRepository>();

            CreateConfigWatcher();
            CheckSystemFolder();

            tryToReadTimer = new Timer(1000) { AutoReset = true };
            tryToReadTimer.Elapsed += TryRead;

            writeTimer = new Timer(100) { AutoReset = true };
            writeTimer.Elapsed += WriteBuffers;
        }

        /// <summary>
        /// Checking if the systemfolder and settingsfile exist. Otherwise creates it
        /// </summary>
        private void CheckSystemFolder()
        {
            bool reInit = false;
            if (!Directory.Exists(systemFolderPath))
            {
                Directory.CreateDirectory(systemFolderPath);
                Console.WriteLine($"SystemFolder created at {systemFolderPath}");
                reInit = true;
            }
            if (!File.Exists(repositoriesFilePath))
            {
                File.WriteAllLines(repositoriesFilePath, GenerateRepositoriesFile());
                Console.WriteLine($"Repositories file created at {repositoriesFilePath}");
            }
            if (!Directory.Exists(logFolderPath)) Directory.CreateDirectory(logFolderPath);
            if (reInit)
            {
                CreateConfigWatcher();
                Console.WriteLine("Reinitialized config watcher");
            }
            Console.WriteLine($"SystemFolder is fine");
        }

        /// <summary>
        /// Replaces the current config filewatcher with a new instance
        /// </summary>
        private void CreateConfigWatcher()
        {
            if (registeredFileWatcher != null)
            {
                registeredFileWatcher.Dispose();
            }
            registeredFileWatcher = new FileSystemWatcher(systemFolderPath, "*.config");
            registeredFileWatcher.EnableRaisingEvents = true;
            registeredFileWatcher.Changed += OnChange;
        }

        /// <summary>
        /// Generates the lines of the Repositories.config file with all the active FileWatchers
        /// </summary>
        /// <returns>array with all lines to write</returns>
        private string[] GenerateRepositoriesFile()
        {
            List<string> lines = new List<string>
            {
                "# Each line defines a FileWatcher for a Folder",
                "# All FileWatchers are updated when this file changes",
                "# Lines that start with # are ignored by the service",
                "",
                "# A repository is defined like this:",
                "# Name | FileWatcherState | Excluded files | Path to the watched folder",
                "",
                "# Example",
                "# Repo | WATCHING,PAUSED | .txt:\\TestFolder:unwantedFile.md | D:\\User\\ImportantFolder"
            };
            foreach (string key in watchingRepositories.Keys)
            {
                WatchedRepository repo = watchingRepositories[key];
                string[] data = new string[]
                {
                    repo.FolderName,
                    repo.Watcher.EnableRaisingEvents ? "WATCHING" : "PAUSED",
                    string.Join(":",repo.Exclusions),
                    repo.LocalPath
                };
                lines.Add(string.Join(" | ",data));
            }

            return lines.ToArray();
        }

        /// <summary>
        /// Runs when the emergencyTimer elapses. When successfully read it stops the timer
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void TryRead(object sender, ElapsedEventArgs e)
        {
            Console.WriteLine("");
            Console.WriteLine($"Trying to read the repositories file");
            bool success = ReadRegisteredRepos();
            if (success) tryToReadTimer.Stop();
        }

        /// <summary>
        /// When a .config file changes
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnChange(object sender, FileSystemEventArgs e)
        {
            if (e.Name == repositoriesFileName)
            {
                DateTime lastWriteTime = File.GetLastWriteTime(e.FullPath);
                if (lastWriteTime > lastUpdate.AddSeconds(1))
                {
                    if (!ReadRegisteredRepos())
                    {
                        tryToReadTimer.Start();
                    }
                    lastUpdate = lastWriteTime;
                }
            }
        }

        /// <summary>
        /// Service Start function
        /// </summary>
        public void Start()
        {
            Console.WriteLine("FileWatcher started");
            ReadRegisteredRepos();
        }

        /// <summary>
        /// Reads the Registered Folders and adds/removes watched Folders <br/>
        /// Lines starting with # are ignored <br/>
        /// Example : Reponame | FileWatcherAction | LocalRootPath | RemoteRootPath
        /// </summary>
        /// <returns>nothing</returns>
        private bool ReadRegisteredRepos()
        {
            if (File.Exists(repositoriesFilePath))
            {
                try
                {
                    Console.WriteLine("Reading the Repositories file");

                    List<string> pausedFolders = new List<string>();
                    List<string> FolderNamesInConfig = new List<string>();

                    string[] folderStrings = File.ReadAllLines(repositoriesFilePath);

                    // handle commands and add new fileWatchers
                    foreach (string repoString in folderStrings)
                    {
                        if (repoString.Length > 0 && repoString[0] != '#')
                        {
                            string[] repoData = repoString.Split(new string[] { " | " },StringSplitOptions.None); // 0: RepoName, 1: FileWatcherAction, 2: Exclusions, 3: LocalPath
                            if (repoData.Length >= 4)
                            {
                                Console.WriteLine("");
                                Console.WriteLine($"Reading Repository {repoData[0]}");
                                Console.WriteLine(repoString);
                                FolderNamesInConfig.Add(repoData[0]);

                                if ((repoData[1] == "PAUSED" || repoData[1] == "WATCHING") && Directory.Exists(repoData[3]))
                                {
                                    string[] exclusions = repoData[2].Split(new char[] { ':' },StringSplitOptions.RemoveEmptyEntries);
                                    
                                    // Create SystemFileWatcher if not existing
                                    if (!watchingRepositories.ContainsKey(repoData[0]))
                                    {
                                        WatchedRepository newWatcher = new WatchedRepository(repoData[0], repoData[3], exclusions, logFolderPath);
                                        watchingRepositories[repoData[0]] = newWatcher;
                                        newWatcher.OnBufferChanged += WatcherBufferChanged;
                                        Console.WriteLine($"Created Watcher for folder {repoData[3]}");
                                    }

                                    // Pausing existing SystemFileWatcher if necessary
                                    if (repoData[1] == "PAUSED")
                                    {
                                        watchingRepositories[repoData[0]].Watcher.EnableRaisingEvents = false;
                                        Console.WriteLine($"Paused FileWatcher");
                                    }
                                    else
                                    {
                                        watchingRepositories[repoData[0]].Watcher.EnableRaisingEvents = true;
                                        Console.WriteLine($"Enabled FileWatcher");
                                    }

                                    // Update exclusions on existing FileWatcher
                                    watchingRepositories[repoData[0]].Exclusions = exclusions;

                                }
                                else
                                {
                                    Console.WriteLine("Unkown FileWatcher Action or Folder. Must be WATCHING or PAUSED");
                                }
                            }
                            else
                            {
                                Console.WriteLine($"Line doesen't have enough arguments: {repoString}. Check that the seperator is ' | ' with whitespace");
                            }

                        }
                    }

                    // remove fileWatchers that are not in the cofig file
                    Console.WriteLine("");
                    foreach (string key in watchingRepositories.Keys)
                    {
                        if (!FolderNamesInConfig.Contains(key))
                        {
                            Console.WriteLine($"Removed FileWatcher for repo {key}");
                            watchingRepositories[key].Watcher.Dispose();
                            watchingRepositories.Remove(key);
                        }
                    }
                    return true;
                }
                catch
                {
                    Console.WriteLine("Could not read the file");
                    return false;
                }
            }
            else
            {
                Console.WriteLine($"{repositoriesFilePath} does not exist!");
                return false;
            }
        }

        /// <summary>
        /// Eventhanlder when a buffer in a WatchedFolder changes
        /// </summary>
        /// <param name="sender">WatchedFolder instance</param>
        /// <param name="e">ObservableCollection<string> buffer</param>
        private void WatcherBufferChanged(object sender, ObservableCollection<string> e)
        {
            WatchedRepository instance = (WatchedRepository)sender;
            if (e.Count > 0)
            {
                if (!TryWriteBuffer(instance))
                {
                    if (!buffersToWrite.Contains(instance))
                    {
                        buffersToWrite.Add(instance);
                        writeTimer.Start();
                    }
                }
            }
        }

        /// <summary>
        /// Tries to write the buffer of a WatchedFolder
        /// </summary>
        /// <param name="instance">WatchedFolder instance</param>
        /// <returns>true if successfully written else false</returns>
        private bool TryWriteBuffer(WatchedRepository instance)
        {
            try
            {
                CheckSystemFolder();
                string[] lines = instance.Buffer.ToArray();
                File.AppendAllLines(instance.LogFilePath, lines);
                instance.Buffer.Clear();
                return true;
            }
            catch
            {
                return false;
            }

        }

        /// <summary>
        /// Try write buffers from all WatchedFolders in the buffersToWrite. Run by timer
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void WriteBuffers(object sender, ElapsedEventArgs e)
        {
            if (buffersToWrite.Count > 0)
            {
                bool stopTimer = true;
                foreach (WatchedRepository folder in buffersToWrite)
                {
                    if (!TryWriteBuffer(folder)) stopTimer = false;
                }
                if (stopTimer) writeTimer.Stop();
            }
            else
            {
                writeTimer?.Stop();
            }
        }
    }
}
