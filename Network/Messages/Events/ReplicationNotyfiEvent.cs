using MessagePack;
using System.Collections.Generic;

namespace SuperProxy.Network.Messages.Events
{
    [MessagePackObject]
    public class ReplicationNotyfiEvent : IMessageEvent
    {
        [Key(0)] public List<string> ObjectsToReplicate { get; set; }
    }
}
