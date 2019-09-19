using MessagePack;
using SuperProxy.Network.Messages.Events;

namespace SuperProxy.Network.Messages
{
    [Union(0, typeof(SubscribeEvent))]
    [Union(1, typeof(UnsubscribeEvent))]
    [Union(2, typeof(PublishEvent))]
    [Union(3, typeof(RemoteMethodEvent))]
    [Union(4, typeof(RMINotyfiEvent))]
    [Union(5, typeof(ReplicationListUpdateEvent))]
    [Union(6, typeof(ReplicationNotyfiEvent))]
    public interface IMessageEvent
    {
    }
}
