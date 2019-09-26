using NLog;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Numerics;
using System.Reflection;
using System.Threading.Tasks;

namespace SuperProxy.Network.Serialization.Delegate
{
    /// <summary>
    /// This class used parts of Not-Lite-Code framework https://github.com/ImVexed/NotLiteCode
    /// </summary>
    public static class DelegateWrapper
    {
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();

        public static async Task<object> InvokeWrapper(this Func<object, object[], object> Method, bool HasAsyncResult, object Target, params object[] Args)
        {
            var Result = Method(Target, Args);
            if (!(Result is Task task))
                return Result;

            if (!task.IsCompleted)
                await task.ConfigureAwait(false);

            return HasAsyncResult ? task.GetType().GetProperty("Result")?.GetValue(task) : null;
        }

        public static Func<object, object[], object> CreateMethodWrapper(Type type, MethodInfo method)
        {
            var paramsExps = CreateParamsExpressions(method, out ParameterExpression argsExp);

            var targetExp = Expression.Parameter(typeof(object));
            var castTargetExp = Expression.Convert(targetExp, type);
            var invokeExp = Expression.Call(castTargetExp, method, paramsExps);

            LambdaExpression lambdaExp;

            if (method.ReturnType != typeof(void))
            {
                var resultExp = Expression.Convert(invokeExp, typeof(object));
                lambdaExp = Expression.Lambda(resultExp, targetExp, argsExp);
            }
            else
            {
                var constExp = Expression.Constant(null, typeof(object));
                var blockExp = Expression.Block(invokeExp, constExp);
                lambdaExp = Expression.Lambda(blockExp, targetExp, argsExp);
            }

            return (Func<object, object[], object>)lambdaExp.Compile();
        }

        public static Expression[] CreateParamsExpressions(MethodBase method, out ParameterExpression argsExp)
        {
            var parameters = method.GetParameters().Select(x => x.ParameterType).ToArray();

            argsExp = Expression.Parameter(typeof(object[]));
            var paramsExps = new Expression[parameters.Count()];

            for (var i = 0; i < parameters.Count(); i++)
            {
                var constExp = Expression.Constant(i, typeof(int));
                var argExp = Expression.ArrayIndex(argsExp, constExp);
                paramsExps[i] = Expression.Convert(argExp, parameters[i]);
            }

            return paramsExps;
        }

        public static object ConvertProperties(this Type hostedType, Dictionary<object, object> objs, int index = 0, bool isArr = false)
        {
            var activated = Activator.CreateInstance(hostedType);
            IList implicitArray = null;
            object linkToObject = null;
            FieldInfo[] activeFields = null;
            PropertyInfo[] activeProps = null;

            if (isArr)
            {
                implicitArray = activated as IList;
                implicitArray.Add(Activator.CreateInstance(activated.GetType().GetGenericArguments().Single()));
            }

            linkToObject = isArr ? implicitArray[index] : activated;
            activeFields = isArr ? implicitArray[index].GetType().GetFields() : activated.GetType().GetFields();
            activeProps = isArr ? implicitArray[index].GetType().GetProperties() : activated.GetType().GetProperties();

            foreach (var kv in objs)
            {
                var keyName = kv.Key.ToString();
                var valueType = kv.Value?.GetType();

                Type property = activeProps.Any(s => s.Name == keyName) ? activeProps.FirstOrDefault(s => s.Name == keyName)?.PropertyType : activeFields.FirstOrDefault(s => s.Name == keyName)?.FieldType;

                PropField propInfo = null;

                if (activeProps.Any(s => s.Name == keyName))
                    propInfo = new PropField(linkToObject.GetType().GetProperty(keyName));
                else if(activeFields.Any(s => s.Name == keyName))
                    propInfo = new PropField(linkToObject.GetType().GetField(keyName));

                if(property == null)               
                    continue;
                
                try
                {
                    if (valueType != null && valueType.IsGenericType || property.IsGenericType)
                    {
                        if (kv.Value is object[] castAsArr)
                        {
                            var list = (IList)Activator.CreateInstance(property.GetGenericTypeDefinition().MakeGenericType(property.GetGenericArguments().Single()));
                            for (int i = 0; i < castAsArr.Length; i++)
                                list.Add((ConvertProperties(property, (Dictionary<object, object>)castAsArr[i], 0, true) as IList)[0]);

                            propInfo.SetValue(linkToObject, list);
                        }
                        else if (kv.Value is Dictionary<object, object> castAsDict)
                            propInfo.SetValue(linkToObject, ConvertProperties(property, castAsDict));
                    }
                    else
                    {
                        if (property.Equals(typeof(Guid)))
                            propInfo.SetValue(linkToObject, Guid.Parse(kv.Value.ToString()));
                        else if (property.Equals(typeof(Vector3)))
                            propInfo.SetValue(linkToObject, (Vector3)kv.Value);
                        else if (property.IsEnum)
                            propInfo.SetValue(linkToObject, Enum.Parse(property, kv.Value.ToString()));
                        else
                            propInfo.SetValue(linkToObject, Convert.ChangeType(kv.Value, property));
                    }
                }
                catch (InvalidCastException ex)
                {
                    Log.Error(ex);
                }
                catch (Exception ex)
                {
                    Log.Error(ex);
                }
            }

            return activated;
        }

        public class PropField
        {
            private PropertyInfo _propInfo;
            private FieldInfo _fieldInfo;

            public PropField(PropertyInfo propInfo)
            {
                _propInfo = propInfo;
            }

            public PropField(FieldInfo fieldInfo)
            {
                _fieldInfo = fieldInfo;
            }

            public void SetValue(object obj, object value)
            {
                if (_propInfo != null)
                    _propInfo.SetValue(obj, value);
                if (_fieldInfo != null)
                    _fieldInfo.SetValue(obj, value);
            }
        }
    }
}
