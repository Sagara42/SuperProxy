using SuperProxy.Network.Messages.Events;
using System.Threading;

namespace SuperProxy.Network.Events
{
    public class RemoteEventWait
    {
        public EventWaitHandle Event = new EventWaitHandle(false, EventResetMode.ManualReset);
        public RemoteMethodEvent Result;
    }
}
