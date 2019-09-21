using SuperProxy.Collection.RemoteReplication;
using SuperProxy.Network;
using System.Collections;
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
        private bool _onUpdateSequence;

        public RpList() : base()
        {
        }

        public T this[int index] => _observableLockedList[index];
        public bool Exist(T item) => _observableLockedList.Contains(item);
        public void Add(T item) => _observableLockedList.Add(item);
        public void Remove(T item) => _observableLockedList.Remove(item);
        public void RemoveAt(int index) => _observableLockedList.RemoveAt(index);
        
        public void InstallClient(SPClient client, string objectName) 
        {
            InstallARPList();

            _client = client;
            _objectName = objectName;
        }

        public void UpdateReceive(IList newItems, IList oldItems)
        {
            _onUpdateSequence = true;

            if(oldItems.Count > 0)            
                foreach (var oldItem in oldItems)
                    Remove((T) oldItem);

            if (newItems.Count > 0)
                foreach (var newItem in newItems)
                    Add((T)newItem);

            _onUpdateSequence = false;
        }

        public override void OnObservableListChanged(object sender, NotifyCollectionChangedEventArgs ev)
        {
            if(!_onUpdateSequence)
                _client?.RemoteListWillUpdate(ev, _objectName);
        }
    }
}
