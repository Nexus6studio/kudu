using Kudu.Contracts.Tracing;
using Kudu.Services.Editor;
using Microsoft.AspNet.SignalR;
using Microsoft.AspNet.SignalR.Hubs;
using Moq;
using System;
using System.Threading.Tasks;
using Xunit;
using Xunit.Extensions;

namespace Kudu.Core.Test
{
    public class FileSystemHubFacts
    {
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task FileSystemHubRegisterTests(bool update)
        {
            // Setup
            var env = new Mock<IEnvironment>();
            var tracer = new Mock<ITracer>();
            var request = new Mock<IRequest>();

            var context = new HubCallerContext(request.Object, Guid.NewGuid().ToString());
            var groups = new Mock<IGroupManager>();
            var clients = new Mock<IHubCallerConnectionContext>();

            using (
                var fileSystemHubTest = new FileSystemHubTest(env.Object, tracer.Object, context, groups.Object,
                    clients.Object))
            {
                // Test
                await fileSystemHubTest.TestRegister(@"C:\");

                // Assert
                Assert.Equal(1, fileSystemHubTest.FileWatchersCount);

                if (update)
                {
                    // Test
                    await fileSystemHubTest.TestRegister(@"C:\");

                    // Assert
                    Assert.Equal(1, fileSystemHubTest.FileWatchersCount);
                }

                // Test
                await fileSystemHubTest.TestDisconnect();

                // Assert
                Assert.Equal(0, fileSystemHubTest.FileWatchersCount);
            }
        }

        public class FileSystemHubTest : FileSystemHub, IDisposable
        {
            public FileSystemHubTest(IEnvironment environment, ITracer tracer, HubCallerContext context,
                IGroupManager group, IHubCallerConnectionContext clients) : base(environment, tracer)
            {
                Context = context;
                Groups = group;
                Clients = clients;
            }

            public int FileWatchersCount
            {
                get { return _fileWatchers.Count; }
            }

            public Task TestRegister(string path)
            {
                return Register(path);
            }

            public Task TestDisconnect()
            {
                return OnDisconnected();
            }

            public new void Dispose()
            {
                _fileWatchers.Clear();
            }
        }
    }
}
