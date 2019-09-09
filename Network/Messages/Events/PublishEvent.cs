using MessagePack;

namespace SuperProxy.Network.Messages.Events
{
    [MessagePackObject]
    public class PublishEvent : IMessageEvent
    {
        [Key(0)] public string Channel { get; set; }
        [Key(1)] public PublishMessage Message { get; set; }
    }
}
