using MessagePack;

namespace SuperProxy.Network.Messages.Events
{
    [MessagePackObject]
    public class RMINotyfiEvent : IMessageEvent
    {
        [Key(0)] public string Channel { get; set; }
        [Key(1)]  public string[] MethodNames { get; set; }
    }
}
