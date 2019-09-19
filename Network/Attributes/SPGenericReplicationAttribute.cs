using System;

namespace SuperProxy.Network.Attributes
{
    public class SPGenericReplicationAttribute : Attribute
    { 
        public string ObjectName { get; private set; }

        public SPGenericReplicationAttribute(string objectName)
        {
            ObjectName = objectName;
        }
    }
}
