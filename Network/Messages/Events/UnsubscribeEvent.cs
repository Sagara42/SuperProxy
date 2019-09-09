using MessagePack;
using System;

namespace SuperProxy.Network.Messages.Events
{
    [MessagePackObject]
    public class UnsubscribeEvent : IMessageEvent
    {
        [Key(0)] public string Channel { get; set; }
        [Key(1)] public bool FromAll { get; set; }

        public UnsubscribeEvent()
        {
        }
    }
}
