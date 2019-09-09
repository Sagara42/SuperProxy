using MessagePack;

namespace SuperProxy.Network.Messages
{
    [MessagePackObject]
    public class PublishMessage
    {
        [Key(0)] public string Header { get; set; }
        [Key(1)] public object Data { get; set; }
    }
}
