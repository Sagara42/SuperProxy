using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SuperProxy.Network.Serialization
{
    public static class SerializeExtension
    {
        public static T ConvertData<T>(this Dictionary<object, object> dict)
        {
            var type = typeof(T);
            var obj = Activator.CreateInstance(type);
            var props = obj.GetType().GetProperties();
            var fields = obj.GetType().GetFields();

            foreach (var kv in dict)
            {
                var prop = props.FirstOrDefault(s => s.Name == kv.Key.ToString());
                if (prop != null)
                    type.GetProperty(kv.Key.ToString()).SetValue(obj, Convert.ChangeType(kv.Value, prop.PropertyType));
                else
                {
                    var field = fields.FirstOrDefault(s => s.Name == kv.Key.ToString());
                    if(field != null)
                        type.GetField(kv.Key.ToString()).SetValue(obj, Convert.ChangeType(kv.Value, field.FieldType));
                    
                }
            }

            return (T)obj;
        }
    }
}
