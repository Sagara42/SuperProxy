using NLog;
using SuperProxy.Network;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SuperProxy.Events
{
    public static class EventAggregator
    {
        private static readonly Logger _log = LogManager.GetCurrentClassLogger();
        private static ConcurrentDictionary<string, List<SPClient>> _subscribedClients = new ConcurrentDictionary<string, List<SPClient>>();

        public static void Publish(string channel, object obj, string header, SPClient client)
        {
            ThreadPool.QueueUserWorkItem((callback) => 
            {
                Parallel.For(0, _subscribedClients[channel].Count, (i) =>
                {
                    if (_subscribedClients[channel][i].Equals(client))
                        return;

                    _subscribedClients[channel][i].Publish(channel, obj, header);
                });
            });
        }

        public static void Subscribe(string channel, SPClient client)
        {
            if (!_subscribedClients.ContainsKey(channel))
                _subscribedClients.TryAdd(channel, new List<SPClient>());

            if (!_subscribedClients[channel].Contains(client))
                _subscribedClients[channel].Add(client);

#if DEBUG
            _log.Debug($"Client {client.Socket.RemoteEndPoint.ToString()} subscribed on {channel} channel.");
#endif
        }

        public static void Unsubscribe(string channel, SPClient client)
        {
            if (_subscribedClients.ContainsKey(channel) && _subscribedClients[channel].Contains(client))
                _subscribedClients[channel].Remove(client);

#if DEBUG
            _log.Debug($"Client {client.Socket.RemoteEndPoint.ToString()} usubscribed from {channel} channel.");
#endif
        }

        public static void UnsubscribeFromAllChannels(SPClient client)
        {
            var entryChannels = _subscribedClients.ToDictionary(s => s.Key, s => s.Value);
            foreach(var channel in entryChannels)            
                if (channel.Value.Contains(client))
                    channel.Value.Remove(client);

#if DEBUG
            _log.Debug($"Client {client.Socket.RemoteEndPoint.ToString()} unsubscribed from all channel.");
#endif
        }
    }
}
