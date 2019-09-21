using System;
using System.ComponentModel;

namespace SuperProxy.Network.Attributes
{
    [Description("Feature in development, may work incorrect")]
    public class SPGenericReplicationAttribute : Attribute
    { 
        public string ObjectName { get; private set; }

        /// <summary>
        /// Feature in development, may work incorrect
        /// </summary>
        /// <param name="objectName"></param>
        public SPGenericReplicationAttribute(string objectName)
        {
            ObjectName = objectName;
        }
    }
}
