using System.Collections.Concurrent;
using Discord.Interactions;

namespace DougaBot.Modules.TopLevel;

public sealed partial class TopLevel : InteractionModuleBase<SocketInteractionContext>
{
    private static readonly ConcurrentDictionary<ulong, SemaphoreSlim> QueueLocks = new();

    public class QueueLock : IDisposable
    {
        private SemaphoreSlim Lock { get; }

        public QueueLock(SemaphoreSlim @lock) => Lock = @lock;

        public void Dispose()
        {
            // Release the lock
            Lock.Release();
            // Dispose the SemaphoreSlim object
            Lock.Dispose();
            // Suppress finalization of this object
            GC.SuppressFinalize(this);
        }
    }

    private static string RemuxVideo => "qt>mp4/mov>mp4/mkv>mp4/webm>mp4/opus>aac";
}