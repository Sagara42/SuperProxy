using MessagePack;

namespace SuperProxy.Network.Messages.Events
{
    [MessagePackObject]
    public class SubscribeEvent : IMessageEvent
    {
        [Key(0)] public string Channel { get; set; }

        public SubscribeEvent()
        {

        }
    }
}
