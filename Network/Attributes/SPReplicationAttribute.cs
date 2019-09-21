using System;

namespace SuperProxy.Network.Attributes
{
    public class SPReplicationAttribute : Attribute
    {
        public string ObjectName { get; private set; }

        public SPReplicationAttribute(string name)
        {
            ObjectName = name;  
        }
    }
}
