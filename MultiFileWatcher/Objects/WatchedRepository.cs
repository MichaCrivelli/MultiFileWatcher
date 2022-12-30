using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MultiFileWatcher
{
    public class WatchedRepository
    {
        private readonly string _logFilePath;
        public string LogFilePath => _logFilePath;

        private string folderName;
        public string FolderName
        {
            get { return folderName; }
            set { folderName = value; }
        }

        private string localPath;
        public string LocalPath
        {
            get { return localPath; }
            set { localPath = value; }
        }

        private string[] exclusions = new string[0];
        public string[] Exclusions
        {
            get { return exclusions; }
            set { exclusions = value; }
        }

        private FileSystemWatcher watcher;
        public FileSystemWatcher Watcher
        {
            get { return watcher; }
            set { watcher = value; }
        }

        private DateTime lastRead = DateTime.MinValue;

        private ObservableCollection<string> buffer;
        public ObservableCollection<string> Buffer
        {
            get { return buffer; }
            set
            {
                buffer = value;
            }
        }

        public event EventHandler<ObservableCollection<string>> OnBufferChanged;

        public WatchedRepository(string name, string localPath, string[] exclusions, string logFolder)
        {
            this.folderName = name;
            this.localPath = localPath;
            this.Exclusions = exclusions;
            buffer = new ObservableCollection<string>();
            buffer.CollectionChanged += BufferChanged;

            if (Directory.Exists(this.localPath))
            {
                _logFilePath = Path.Combine(logFolder, name) + ".csv";
                watcher = new FileSystemWatcher(localPath);
                watcher.EnableRaisingEvents = true;
                watcher.IncludeSubdirectories = true;

                watcher.Created += OnCreated;
                watcher.Changed += OnChanged;
                watcher.Deleted += OnDeleted;
                watcher.Renamed += OnRenamed;

            }
        }

        private void BufferChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            OnBufferChanged.Invoke(this, Buffer);
        }

        private void OnRenamed(object sender, RenamedEventArgs e)
        {
            if (!IsExcluded(e.OldFullPath))
            {
                Buffer.Add($"DELETED,{DateTime.Now},{GetRelativePath(e.OldFullPath)}");
            }
            if (!IsExcluded(e.FullPath))
            {
                Buffer.Add($"CREATED,{DateTime.Now},{GetRelativePath(e.FullPath)}");
            }
        }

        private void OnDeleted(object sender, FileSystemEventArgs e)
        {
            if (!IsExcluded(e.FullPath))
            {
                Buffer.Add($"DELETED,{DateTime.Now},{GetRelativePath(e.FullPath)}");
            }
        }

        private void OnChanged(object sender, FileSystemEventArgs e)
        {
            DateTime lastWriteTime = File.GetLastWriteTime(e.FullPath);
            if (lastWriteTime > lastRead)
            {
                if (File.Exists(e.FullPath) && !IsExcluded(e.FullPath))
                {
                    Buffer.Add($"CHANGED,{DateTime.Now},{GetRelativePath(e.FullPath)}");
                    lastRead = lastWriteTime.AddSeconds(1);
                }
            }
        }

        private void OnCreated(object sender, FileSystemEventArgs e)
        {
            if (!IsExcluded(e.FullPath))
            {
                Buffer.Add($"CREATED,{DateTime.Now},{GetRelativePath(e.FullPath)}");
            }
        }

        /// <summary>
        /// Returns the relative path
        /// </summary>
        /// <param name="fullpath">the full path</param>
        /// <returns>relative path</returns>
        private string GetRelativePath(string fullpath)
        {
            return "/" + fullpath.Replace(localPath + "\\", "").Replace('\\', '/');
        }

        /// <summary>
        /// Checks if a filePath is excluded or not
        /// </summary>
        /// <param name="filePath">filePath to check</param>
        /// <returns>true if the path is excluded</returns>
        private bool IsExcluded(string filePath)
        {
            if (Exclusions.Length == 0) return false;

            string relative = filePath.Replace(localPath, "");
            string filename = Path.GetFileName(filePath);
            string extension = Path.GetExtension(filePath);

            foreach (string exclusion in Exclusions)
            {
                if (exclusion[0] == '\\')
                {
                    // full path exclusion
                    if (relative.IndexOf(exclusion) == 0)
                    {
                        return true;
                    }
                }
                else
                {
                    // check filename
                    if (filename == exclusion || extension == exclusion)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

    }
}
