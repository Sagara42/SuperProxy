using MessagePack;
using System;

namespace SuperProxy.Network.Messages.Events
{
    [MessagePackObject]
    public class RemoteMethodEvent : IMessageEvent
    {
        [Key(0)] public RemoteMethodType Type { get; set; }
        [Key(1)] public string CallbackGuid { get; set; }
        [Key(2)] public string MethodName { get; set; }

        [NonSerialized] [IgnoreMember] public Func<object, object[], object> Method;
        [Key(3)] public object Data { get; set; } = new object();
        [Key(4)] public bool HasAsyncResult { get; set; }
        [Key(5)] public string Channel { get; set; } = "";
    }

    public enum RemoteMethodType : byte
    {
        NONE,
        CALL,
        MOVE,
        RETURN,
        ERROR
    }
}
