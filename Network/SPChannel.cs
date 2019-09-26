using System.Reflection;

namespace SuperProxy.Network
{
    public abstract class SPChannel<T> : DispatchProxy
    {
        public string Channel { get; private set; }
        public T Proxy => (T) _hosted;

        private object _hosted;

        public SPChannel(string channelName)
        {
            Channel = channelName;
        }

        protected SPChannel()
        {
        }

        protected override object Invoke(MethodInfo targetMethod, object[] args)
        {
            return _hosted;//todo
        }

        protected void InitilizeDecoration(object decorated)
        {
            _hosted = decorated;
        }

        public static T GetImplicitProxy(T hosted)
        {
            object proxy = Create<T, SPChannel<T>>();

            ((SPChannel<T>)proxy).InitilizeDecoration(hosted);

            return (T)proxy;
        }
    }
}
