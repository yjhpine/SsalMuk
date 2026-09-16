using System;
namespace SsalMuk.Core
{
    public interface IRunCommands { bool TryQueueChoice(Guid runId, long offerId, int slot); }
}
