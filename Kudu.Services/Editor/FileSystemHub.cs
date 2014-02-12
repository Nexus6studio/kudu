using System;
using Kudu.Contracts.Tracing;
using Kudu.Core;
using Microsoft.AspNet.SignalR;
using System.Collections.Concurrent;
using System.IO;
using System.Threading.Tasks;
using System.Linq;

namespace Kudu.Services.Editor
{
    public class FileSystemHub : Hub
    {
        public const int MaxFileSystemWatchers = 5;

        protected static readonly ConcurrentDictionary<string, SlowDirectoryWatcher> _fileWatchers =
            new ConcurrentDictionary<string, SlowDirectoryWatcher>();

        private readonly IEnvironment _environment;
        private readonly ITracer _tracer;

        public FileSystemHub(IEnvironment environment, ITracer tracer)
        {
            _environment = environment;
            _tracer = tracer;
        }

        public void Register(string path)
        {
            path = path ?? _environment.RootPath;
            using (
                _tracer.Step(
                    String.Format("Registering connectionId {0} for FileSystemWatcher on path: {1}",
                        Context.ConnectionId, path)))
            {
                SlowDirectoryWatcher watcher;
                if (_fileWatchers.TryGetValue(Context.ConnectionId, out watcher))
                {
                    watcher.Path = path;
                }
                else
                {
                    _fileWatchers.TryAdd(Context.ConnectionId, GetFileSystemWatcher(path));
                }
            }
        }

        public override async Task OnDisconnected()
        {
            using (
                _tracer.Step(String.Format("Client with connectionId {0} disconnected.",
                    Context.ConnectionId)))
            {
                RemoveFileSystemWatcher(Context.ConnectionId, _tracer);
                await base.OnDisconnected();
            }
        }

        private async void NotifyGroup(string path)
        {
            await Clients.Caller.fileExplorerChanged();
        }

        private SlowDirectoryWatcher GetFileSystemWatcher(string path)
        {
            using (_tracer.Step(String.Format("Creating FileSystemWatcher on path {0}", path)))
            {
                var slowDirectoryWatcher = new SlowDirectoryWatcher(path)
                {
                    EnableRaisingEvents = true,
                    IncludeSubdirectories = false
                };
                slowDirectoryWatcher.GeneralDirectoryChanged += NotifyGroup;
                EnsureFileSystemWatchersMax();
                return slowDirectoryWatcher;
            }
        }

        private static void RemoveFileSystemWatcher(string key, ITracer tracer)
        {
            using (tracer.Step(String.Format("Disposing FileSystemWatcher for connectionId {0}", key)))
            {
                SlowDirectoryWatcher temp;
                if (_fileWatchers.TryRemove(key, out temp))
                {
                    temp.Dispose();
                }
            }
        }

        private void EnsureFileSystemWatchersMax()
        {
            while (_fileWatchers.Count >= MaxFileSystemWatchers)
            {
                var toRemove = _fileWatchers.OrderBy(p => p.Value.LastFired).LastOrDefault();
                if (String.IsNullOrEmpty(toRemove.Key))
                {
                    break;
                }
                RemoveFileSystemWatcher(toRemove.Key, _tracer);
            }
        }

        protected class SlowDirectoryWatcher : FileSystemWatcher
        {
            public delegate void SlowFileSystemEventHandler(string path);

            public event SlowFileSystemEventHandler GeneralDirectoryChanged;
            private readonly object _sync = new object();
            public TimeSpan FireDelay { get; set; }
            public DateTime LastFired { get; private set; }

            public SlowDirectoryWatcher(string path)
                : base(path)
            {
                Changed += HandleChange;
                Created += HandleChange;
                Deleted += HandleChange;
                Renamed += HandleRename;
                LastFired = DateTime.MinValue;
                FireDelay = TimeSpan.FromMilliseconds(500);
            }

            private void HandleSlowChange(string fullPath)
            {
                if (LastFired.Add(FireDelay) < DateTime.UtcNow)
                {
                    lock (_sync)
                    {
                        GeneralDirectoryChanged.Invoke(System.IO.Path.GetDirectoryName(fullPath));
                        LastFired = DateTime.UtcNow;
                    }
                }
            }

            private void HandleChange(object sender, FileSystemEventArgs args)
            {
                HandleSlowChange(args.FullPath);
            }

            private void HandleRename(object sender, RenamedEventArgs args)
            {
                HandleSlowChange(args.FullPath);
            }

            protected override void Dispose(bool disposing)
            {
                GeneralDirectoryChanged = null;
                base.Dispose(disposing);
            }
        }
    }
}