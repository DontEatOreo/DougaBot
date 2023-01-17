using System.Collections.Concurrent;
using Discord.Interactions;
using DougaBot.PreConditions;

namespace DougaBot.Modules.CompressGroup;

[RateLimit(60, RateLimitAttribute.RateLimitType.Guild), Group("compress", "Group of commands for compression")]
public sealed partial class CompressGroup : InteractionModuleBase<SocketInteractionContext>
{
    private static readonly ConcurrentDictionary<ulong, SemaphoreSlim> QueueLocks = new();

    private static readonly RateLimitAttribute RateLimitAttribute = new();

    private static string RemuxVideo => "qt>mp4/mov>mp4/mkv>mp4/webm>mp4/opus>aac";

    public class QueueLock : IDisposable
    {
        private readonly SemaphoreSlim _queueLock;
        public QueueLock(SemaphoreSlim queueLock) => _queueLock = queueLock;

        public void Dispose()
        {
            // Release the lock
            _queueLock.Release();
            // Dispose the SemaphoreSlim object
            _queueLock.Dispose();
            // Suppress finalization of this object
            GC.SuppressFinalize(this);
        }
    }
}