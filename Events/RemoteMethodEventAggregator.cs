using NLog;
using SuperProxy.Events.Models.RemoteMethod;
using SuperProxy.Network;
using SuperProxy.Network.Messages.Events;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace SuperProxy.Events
{
    public class RemoteMethodEventAggregator
    {
        private static readonly Logger _log = LogManager.GetCurrentClassLogger();
        private static ConcurrentDictionary<string, List<RemoteMethodProvider>> _subscribersInfo = new ConcurrentDictionary<string, List<RemoteMethodProvider>>();
        private static ConcurrentDictionary<string, SPClient> _callbacks = new ConcurrentDictionary<string, SPClient>();

        public static void Publish(RemoteMethodEvent remoteEvent, SPClient client)
        {
            if(remoteEvent.Type == RemoteMethodType.RETURN)
            {
                if (_callbacks.ContainsKey(remoteEvent.CallbackGuid))
                {
                    if (_callbacks.TryRemove(remoteEvent.CallbackGuid, out var cl))
                        cl.SentRemoteMethodCall(remoteEvent);
                }
                return;
            }

            ThreadPool.QueueUserWorkItem((callback) =>
            {
                var subscribed = _subscribersInfo[remoteEvent.Channel].FirstOrDefault(s => !s.Client.Equals(client) && s.Client.ReadyForRMI && s.MethodName == remoteEvent.MethodName);
                if (subscribed != null)
                {
                    subscribed.Client.SentRemoteMethodCall(remoteEvent);

                    _callbacks.TryAdd(remoteEvent.CallbackGuid, client);
                }
            });
        }

        public static void Subscribe(SPClient client, RMINotyfiEvent notifyEvent)
        {
            if (!client.ReadyForRMI)
                client.ReadyForRMI = true;

            var count = 0;
            foreach(var methodName in notifyEvent.MethodNames)
            {
                Subscribe(notifyEvent.Channel, new RemoteMethodProvider()
                {
                     Channel = notifyEvent.Channel,
                     Client = client,
                     MethodName = methodName
                });
                count++;
            }

#if DEBUG
            _log.Debug($"Client {client.Socket.RemoteEndPoint.ToString()} subscribed {count} methods to channel {notifyEvent.Channel}");
#endif
        }

        public static void Subscribe(string channel, RemoteMethodProvider methodProvider)
        {
            if (!_subscribersInfo.ContainsKey(channel))
                _subscribersInfo.TryAdd(channel, new List<RemoteMethodProvider>());

            if (!_subscribersInfo[channel].Contains(methodProvider))
                _subscribersInfo[channel].Add(methodProvider);
        }

        public static void Unsubscribe(string channel, RemoteMethodProvider methodProvider)
        {
            methodProvider.Client.ReadyForRMI = false;

            if (_subscribersInfo.ContainsKey(channel) && _subscribersInfo[channel].Contains(methodProvider))
                _subscribersInfo[channel].Remove(methodProvider);

#if DEBUG
            _log.Debug($"Client {methodProvider.Client.Socket.RemoteEndPoint.ToString()} unsubscribed from RemoteEvent: {channel} channel.");
#endif
        }

        public static void UnsubscribeFromAllChannels(SPClient client)
        {
            client.ReadyForRMI = false;
            if(_subscribersInfo.Any(s => s.Value.Exists(e => e.Client == client)))
            {
                var toRemove = _subscribersInfo.Where(s => s.Value.Where(e => e.Client == client).Any()).ToList();
                if (toRemove.Any())
                    for (int i = 0; i < toRemove.Count; i++)
                        _subscribersInfo.Remove(toRemove[i].Key, out _);
            }
        }
    }
}
