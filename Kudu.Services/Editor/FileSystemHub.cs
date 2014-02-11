using System;
using Kudu.Contracts.Tracing;
using Kudu.Core;
using Microsoft.AspNet.SignalR;
using System.Collections.Concurrent;
using System.IO;
using System.Threading.Tasks;

namespace Kudu.Services.Editor
{
    public class FileSystemHub : Hub
    {
        protected static readonly ConcurrentDictionary<string, SlowDirectoryWatcher> _fileWatchers =
            new ConcurrentDictionary<string, SlowDirectoryWatcher>();

        private readonly IEnvironment _environment;
        private readonly ITracer _tracer;
        public FileSystemHub(IEnvironment environment, ITracer tracer)
        {
            _environment = environment;
            _tracer = tracer;
        }

        public async Task Register(string path)
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
                await Groups.Add(Context.ConnectionId, path);
            }
        }

        public async Task Deregister(string path)
        {
            using (
                _tracer.Step(String.Format("Deregistering connectionId {0} for FileSystemWatcher on path: {1}",
                        Context.ConnectionId, path)))
            {
                await Groups.Remove(Context.ConnectionId, path);
            }
        }

        public override async Task OnDisconnected()
        {
            using (
                _tracer.Step(String.Format("Client with connectionId {0} disconnected. Disposing FileSystemWatcher",
                    Context.ConnectionId)))
            {
                SlowDirectoryWatcher slowDirectoryWatcher;
                if (_fileWatchers.TryRemove(Context.ConnectionId, out slowDirectoryWatcher))
                {
                    slowDirectoryWatcher.Dispose();
                }
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
                return slowDirectoryWatcher;
            }
        }

        protected class SlowDirectoryWatcher : FileSystemWatcher
        {
            public delegate void SlowFileSystemEventHandler(string path);
            public event SlowFileSystemEventHandler GeneralDirectoryChanged;
            private readonly object _sync = new object();
            public TimeSpan FireDelay { get; set; }
            private DateTime _lastFired;
            
            public SlowDirectoryWatcher(string path)
                : base(path)
            {
                Changed += HandleChange;
                Created += HandleChange;
                Deleted += HandleChange;
                Renamed += HandleRename;
                _lastFired = DateTime.MinValue;
                FireDelay = TimeSpan.FromMilliseconds(500);
            }

            private void HandleSlowChange(string fullPath)
            {
                lock (_sync)
                {
                    if (_lastFired.Add(FireDelay) < DateTime.UtcNow)
                    {
                        GeneralDirectoryChanged.Invoke(System.IO.Path.GetDirectoryName(fullPath));
                        _lastFired = DateTime.UtcNow;
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