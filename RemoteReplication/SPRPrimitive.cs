using SuperProxy.Network;

namespace SuperProxy.RemoteReplication
{
    public class SPRPrimitive<T>
    {
        private SPClient _client;
        private string _objectName;
        private bool _onUpdateSequence;
        private object _localPrimitive;
        private object _primitiveLock = new object();

        public SPRPrimitive()
        {
        }

        public T Value
        {
            get
            {
                return (T) _localPrimitive;
            }
            set
            {
                _localPrimitive = value;

                ValueChanged(value);
            }
        }

        public void InstallClient(SPClient client, string objectName)
        {
            _client = client;
            _objectName = objectName;
            _localPrimitive = typeof(T);
        }

        private void ValueChanged(T value)
        {
            if (!_onUpdateSequence)
            {
                _localPrimitive = value;

                _client.PrimitiveWillUpdate(_objectName, _localPrimitive);
            }
        }

        public void UpdateReceive(object value)
        {
            _onUpdateSequence = true;

            lock (_primitiveLock)
                _localPrimitive = (T)value;

            _onUpdateSequence = false;
        }
    }
}
