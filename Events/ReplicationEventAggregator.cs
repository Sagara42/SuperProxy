using SuperProxy.Network;
using SuperProxy.Network.Messages.Events;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace SuperProxy.Events
{
    public static class ReplicationEventAggregator
    {
        private static ConcurrentDictionary<SPClient, List<string>> _replicationInfos = new ConcurrentDictionary<SPClient, List<string>>();

        public static void SetReplicationInfo(SPClient client, string channel, List<string> objectNames)
        {
            if (!_replicationInfos.ContainsKey(client))
                _replicationInfos.TryAdd(client, objectNames);
        }

        public static void DispatchGenericReplicationInfo(SPClient client, ReplicationListUpdateEvent genericReplicationInfo)
        {
            var objectName = genericReplicationInfo.ObjectName;
            var channel = genericReplicationInfo.Channel;

            foreach(var ri in _replicationInfos)
            {
                if (ri.Key.Equals(client))
                    continue;

                if (ri.Value.Contains(objectName))
                    ri.Key.ReplicationListUpdate(genericReplicationInfo);
            }
        }
    }
}
