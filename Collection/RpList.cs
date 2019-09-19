using SuperProxy.Collection.RemoteReplication;
using SuperProxy.Network;
using System.Collections.Specialized;

namespace SuperProxy.Collection
{
    /// <summary>
    /// Remote proxy list, use it only inside of HostedObjects with SPGenericReplicationAttribute
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class RpList<T> : ARPList<T>
    {
        private SPClient _client;
        private string _objectName;

        public RpList() : base()
        {
        }

        public T this[int index] => _observableLockedList[index];
        public void Add(T item) => _observableLockedList.Add(item);
        public void Remove(T item) => _observableLockedList.Remove(item);
        public void RemoveAt(int index) => _observableLockedList.RemoveAt(index);

        public void InstallClient(SPClient client, string objectName) 
        {
            InstallARPList();

            _client = client;
            _objectName = objectName;
        }

        public override void OnObservableListChanged(object sender, NotifyCollectionChangedEventArgs ev) => _client?.RemoteListWillUpdate(ev, _objectName);
    }
}
