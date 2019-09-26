using SuperProxy.Network;
using SuperProxy.Network.Messages.Events;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace SuperProxy.Events
{
    public static class ReplicationEventAggregator
    {
        private static ConcurrentDictionary<SPClient, List<string>> _replicationInfos = new ConcurrentDictionary<SPClient, List<string>>();

        public static void SetReplicationInfo(SPClient client, List<string> objectNames)
        {
            if (!_replicationInfos.ContainsKey(client))
                _replicationInfos.TryAdd(client, objectNames);
        }

        public static void DispatchGenericReplicationInfo(SPClient client, ReplicationListUpdateEvent genericReplicationInfo)
        {
            var objectName = genericReplicationInfo.ObjectName;

            foreach(var ri in _replicationInfos)
            {
                if (ri.Key.Equals(client))
                    continue;

                if (ri.Value.Contains(objectName))
                    ri.Key.ReplicationListUpdate(genericReplicationInfo);
            }
        }

        public static void DispatchPrimitiveReplicationInfo(SPClient client, ReplicationPrimitiveUpdateEvent ev)
        {
            var objectName = ev.ObjectName;

            foreach (var ri in _replicationInfos)
            {
                if (ri.Key.Equals(client))
                    continue;

                if (ri.Value.Contains(objectName))
                    ri.Key.PrimitiveWillUpdate(ev.ObjectName, ev.Value);
            }
        }

        public static void ReleaseClient(SPClient client)
        {
            if (_replicationInfos.ContainsKey(client))
                _replicationInfos.Remove(client, out _);
        }
    }
}
