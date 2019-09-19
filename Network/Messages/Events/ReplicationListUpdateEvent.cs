using MessagePack;
using System.Collections;

namespace SuperProxy.Network.Messages.Events
{
    [MessagePackObject]
    public class ReplicationListUpdateEvent : IMessageEvent
    {
        [Key(0)] public IList NewItems { get; set; }
        [Key(1)] public IList OldItems { get; set; }
        [Key(2)] public string Channel { get; set; }
        [Key(3)] public string ObjectName { get; set; }
    }
}
