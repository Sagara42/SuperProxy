﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;

namespace SuperProxy.Collection
{
    [Serializable, ComVisible(false)]
    public class SynchronizedObservableCollection<T> : IDisposable, IList<T>, IList, IReadOnlyList<T>, INotifyCollectionChanged, INotifyPropertyChanged
    {
        [NonSerialized] private object _syncRoot;
        private readonly SimpleMonitor _monitor = new SimpleMonitor();
        private readonly ReaderWriterLockSlim _itemsLock = new ReaderWriterLockSlim();
        private readonly SynchronizationContext _context;
        private readonly IList<T> _items;

        public SynchronizedObservableCollection()
        {
            _context = new SynchronizationContext();
            _items = new List<T>();
        }

        event PropertyChangedEventHandler INotifyPropertyChanged.PropertyChanged
        {
            add { PropertyChanged += value; }
            remove { PropertyChanged -= value; }
        }

        public event NotifyCollectionChangedEventHandler CollectionChanged;
        protected event PropertyChangedEventHandler PropertyChanged;

        bool IList.IsFixedSize
        {
            get
            {
                var list = _items as IList;
                if (list != null)
                {
                    return list.IsFixedSize;
                }

                return _items.IsReadOnly;
            }
        }

        bool ICollection<T>.IsReadOnly
        {
            get { return _items.IsReadOnly; }
        }

        bool IList.IsReadOnly
        {
            get { return _items.IsReadOnly; }
        }

        bool ICollection.IsSynchronized
        {
            get { return true; }
        }

        object ICollection.SyncRoot
        {
            get
            {
                if (_syncRoot == null)
                {
                    _itemsLock.EnterReadLock();

                    try
                    {
                        if (_items is ICollection c)
                            _syncRoot = c.SyncRoot;
                        else
                            Interlocked.CompareExchange<object>(ref _syncRoot, new object(), null);

                    }
                    finally
                    {
                        _itemsLock.ExitReadLock();
                    }
                }

                return _syncRoot;
            }
        }

        public int Count
        {
            get
            {
                _itemsLock.EnterReadLock();

                try
                {
                    return _items.Count;
                }
                finally
                {
                    _itemsLock.ExitReadLock();
                }
            }
        }

        public T this[int index]
        {
            get
            {
                _itemsLock.EnterReadLock();

                try
                {
                    CheckIndex(index);

                    return _items[index];
                }
                finally
                {
                    _itemsLock.ExitReadLock();
                }
            }
            set
            {
                T oldValue;

                _itemsLock.EnterWriteLock();

                try
                {
                    CheckIsReadOnly();
                    CheckIndex(index);
                    CheckReentrancy();

                    oldValue = this[index];

                    _items[index] = value;

                }
                finally
                {
                    _itemsLock.ExitWriteLock();
                }

                OnPropertyChanged("Item[]");
                OnCollectionChanged(NotifyCollectionChangedAction.Replace, oldValue, value, index);
            }
        }

        object IList.this[int index]
        {
            get { return this[index]; }
            set
            {
                try
                {
                    this[index] = (T)value;
                }
                catch (InvalidCastException)
                {
                    throw new ArgumentException("'value' is the wrong type");
                }
            }
        }

        private IDisposable BlockReentrancy()
        {
            _monitor.Enter();

            return _monitor;
        }

        private void CheckIndex(int index)
        {
            if (index < 0 || index >= _items.Count)            
                throw new ArgumentOutOfRangeException();           
        }

        private void CheckIsReadOnly()
        {
            if (_items.IsReadOnly)            
                throw new NotSupportedException("Collection is readonly");           
        }

        private void CheckReentrancy()
        {
            if (_monitor.Busy && CollectionChanged != null && CollectionChanged.GetInvocationList().Length > 1)            
                throw new InvalidOperationException("SynchronizedObservableCollection reentrancy not allowed");           
        }

        private static bool IsCompatibleObject(object value) => ((value is T) || (value == null && default(T) == null));

        private void OnPropertyChanged(string propertyName) => OnPropertyChanged(new PropertyChangedEventArgs(propertyName));
        private void OnCollectionChanged(NotifyCollectionChangedAction action, object item, int index) => OnCollectionChanged(new NotifyCollectionChangedEventArgs(action, item, index));
        private void OnCollectionChanged(NotifyCollectionChangedAction action, object item, int index, int oldIndex) => OnCollectionChanged(new NotifyCollectionChangedEventArgs(action, item, index, oldIndex));
        private void OnCollectionChanged(NotifyCollectionChangedAction action, object oldItem, object newItem, int index) => OnCollectionChanged(new NotifyCollectionChangedEventArgs(action, newItem, oldItem, index));

        private void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
        {
            var collectionChanged = CollectionChanged;
            if (collectionChanged == null)            
                return;
            

            using (BlockReentrancy())            
                _context.Send(state => collectionChanged(this, e), null);           
        }

        private void OnCollectionReset() => OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));

        private void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            var propertyChanged = PropertyChanged;
            if (propertyChanged == null)
            {
                return;
            }

            _context.Send(state => propertyChanged(this, e), null);
        }

        public IEnumerator<T> GetEnumerator()
        {
            _itemsLock.EnterReadLock();

            try
            {
                return _items.ToList().GetEnumerator();
            }
            finally
            {
                _itemsLock.ExitReadLock();
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            _itemsLock.EnterReadLock();

            try
            {
                return ((IEnumerable)_items.ToList()).GetEnumerator();
            }
            finally
            {
                _itemsLock.ExitReadLock();
            }
        }

        public int IndexOf(T item)
        {
            _itemsLock.EnterReadLock();

            try
            {
                return _items.IndexOf(item);
            }
            finally
            {
                _itemsLock.ExitReadLock();
            }
        }

        int IList.IndexOf(object value)
        {
            if (IsCompatibleObject(value))
            {
                _itemsLock.EnterReadLock();

                try
                {
                    return _items.IndexOf((T)value);
                }
                finally
                {
                    _itemsLock.ExitReadLock();
                }
            }

            return -1;
        }

        public void Insert(int index, T item)
        {
            _itemsLock.EnterWriteLock();

            try
            {
                CheckIsReadOnly();
                CheckIndex(index);
                CheckReentrancy();

                _items.Insert(index, item);
            }
            finally
            {
                _itemsLock.ExitWriteLock();
            }

            OnPropertyChanged("Count");
            OnPropertyChanged("Item[]");
            OnCollectionChanged(NotifyCollectionChangedAction.Add, item, index);
        }

        void IList.Insert(int index, object value)
        {
            try
            {
                Insert(index, (T)value);
            }
            catch (InvalidCastException)
            {
                throw new ArgumentException("'value' is the wrong type");
            }
        }

        public void Add(T item)
        {
            _itemsLock.EnterWriteLock();

            var index = _items.Count;

            try
            {
                CheckIsReadOnly();
                CheckReentrancy();

                _items.Insert(index, item);
            }
            finally
            {
                _itemsLock.ExitWriteLock();
            }

            OnPropertyChanged("Count");
            OnPropertyChanged("Item[]");
            OnCollectionChanged(NotifyCollectionChangedAction.Add, item, index);
        }

        int IList.Add(object value)
        {
            _itemsLock.EnterWriteLock();

            var index = _items.Count;
            T item;

            try
            {
                CheckIsReadOnly();
                CheckReentrancy();

                item = (T)value;

                _items.Insert(index, item);
            }
            catch (InvalidCastException)
            {
                throw new ArgumentException("'value' is the wrong type");
            }
            finally
            {
                _itemsLock.ExitWriteLock();
            }

            OnPropertyChanged("Count");
            OnPropertyChanged("Item[]");
            OnCollectionChanged(NotifyCollectionChangedAction.Add, item, index);

            return index;
        }

        public void Clear()
        {
            _itemsLock.EnterWriteLock();

            try
            {
                CheckIsReadOnly();
                CheckReentrancy();

                _items.Clear();
            }
            finally
            {
                _itemsLock.ExitWriteLock();
            }

            OnPropertyChanged("Count");
            OnPropertyChanged("Item[]");
            OnCollectionReset();
        }

        public void CopyTo(T[] array, int index)
        {
            _itemsLock.EnterReadLock();

            try
            {
                _items.CopyTo(array, index);
            }
            finally
            {
                _itemsLock.ExitReadLock();
            }
        }

        void ICollection.CopyTo(Array array, int index)
        {
            _itemsLock.EnterReadLock();

            try
            {
                if (array == null)               
                    throw new ArgumentNullException("array", "'array' cannot be null");
                
                if (array.Rank != 1)                
                    throw new ArgumentException("Multidimension arrays are not supported", "array");
                
                if (array.GetLowerBound(0) != 0)               
                    throw new ArgumentException("Non-zero lower bound arrays are not supported", "array");
                
                if (index < 0)                
                    throw new ArgumentOutOfRangeException("index", "'index' is out of range");
                
                if (array.Length - index < _items.Count)                
                    throw new ArgumentException("Array is too small");
                

                var tArray = array as T[];
                if (tArray != null)
                {
                    _items.CopyTo(tArray, index);
                }
                else
                {
                    var targetType = array.GetType().GetElementType();
                    var sourceType = typeof(T);
                    if (!(targetType.IsAssignableFrom(sourceType) || sourceType.IsAssignableFrom(targetType)))                    
                        throw new ArrayTypeMismatchException("Invalid array type");
                    
                    var objects = array as object[];
                    if (objects == null)                   
                        throw new ArrayTypeMismatchException("Invalid array type");
                    
                    var count = _items.Count;
                    try
                    {
                        for (var i = 0; i < count; i++)                       
                            objects[index++] = _items[i];                        
                    }
                    catch (ArrayTypeMismatchException)
                    {
                        throw new ArrayTypeMismatchException("Invalid array type");
                    }
                }
            }
            finally
            {
                _itemsLock.ExitReadLock();
            }
        }

        public bool Contains(T item)
        {
            _itemsLock.EnterReadLock();

            try
            {
                return _items.Contains(item);
            }
            finally
            {
                _itemsLock.ExitReadLock();
            }
        }

        bool IList.Contains(object value)
        {
            if (IsCompatibleObject(value))
            {
                _itemsLock.EnterReadLock();

                try
                {
                    return _items.Contains((T)value);
                }
                finally
                {
                    _itemsLock.ExitReadLock();
                }
            }

            return false;
        }

        public void Dispose()
        {
            _itemsLock.Dispose();
        }

        public bool Remove(T item)
        {
            int index;
            T value;

            _itemsLock.EnterUpgradeableReadLock();

            try
            {
                CheckIsReadOnly();

                index = _items.IndexOf(item);
                if (index < 0)                
                    return false;
                
                _itemsLock.EnterWriteLock();

                try
                {
                    CheckReentrancy();

                    value = _items[index];

                    _items.RemoveAt(index);
                }
                finally
                {
                    _itemsLock.ExitWriteLock();
                }
            }
            finally
            {
                _itemsLock.ExitUpgradeableReadLock();
            }

            OnPropertyChanged("Count");
            OnPropertyChanged("Item[]");
            OnCollectionChanged(NotifyCollectionChangedAction.Remove, value, index);

            return true;
        }

        void IList.Remove(object value)
        {
            if (IsCompatibleObject(value))            
                Remove((T)value);           
        }

        public void RemoveAt(int index)
        {
            T value;

            _itemsLock.EnterWriteLock();

            try
            {
                CheckIsReadOnly();
                CheckIndex(index);
                CheckReentrancy();

                value = _items[index];

                _items.RemoveAt(index);
            }
            finally
            {
                _itemsLock.ExitWriteLock();
            }

            OnPropertyChanged("Count");
            OnPropertyChanged("Item[]");
            OnCollectionChanged(NotifyCollectionChangedAction.Remove, value, index);
        }
    }

    public class SimpleMonitor : IDisposable
    {
        private int _busyCount;
        public bool Busy => _busyCount > 0;
        public void Enter() => ++_busyCount;
        public void Dispose() => --_busyCount;
    }
}