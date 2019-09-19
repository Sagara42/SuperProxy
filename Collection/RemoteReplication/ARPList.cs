using System;
using System.Collections.Specialized;

namespace SuperProxy.Collection.RemoteReplication
{
    public abstract class ARPList<T> : IDisposable
    {
        protected SynchronizedObservableCollection<T> _observableLockedList { get; private set; }

        public void InstallARPList()
        {
            _observableLockedList = new SynchronizedObservableCollection<T>();
            _observableLockedList.CollectionChanged += OnObservableListChanged;
        }

        public void Dispose()
        {
            if (_observableLockedList != null)
                _observableLockedList.CollectionChanged -= OnObservableListChanged;
          
            _observableLockedList?.Dispose();          
        }
     
        public abstract void OnObservableListChanged(object sender, NotifyCollectionChangedEventArgs ev);
    }
}
