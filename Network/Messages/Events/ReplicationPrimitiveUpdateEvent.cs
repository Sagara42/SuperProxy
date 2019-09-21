using MessagePack;


namespace SuperProxy.Network.Messages.Events
{
    [MessagePackObject]
    public class ReplicationPrimitiveUpdateEvent : IMessageEvent
    {
        [Key(0)] public string ObjectName { get; set; }
        [Key(1)] public object Value { get; set; }
    }
}
