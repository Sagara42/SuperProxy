using SuperProxy.Network;

namespace SuperProxy.Events.Models.RemoteMethod
{
    public class RemoteMethodProvider
    {
        public string Channel { get; set; }
        public string MethodName { get; set; }
        public SPClient Client { get; set; }
    }
}
