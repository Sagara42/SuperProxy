using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;

namespace SuperProxy.Network.Serialization.Delegate
{
    /// <summary>
    /// This class used parts of Not-Lite-Code framework https://github.com/ImVexed/NotLiteCode
    /// </summary>
    public static class DelegateWrapper
    {
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

        public static object ConvertProperties(this Type hostedType, Dictionary<object, object> objs)
        {
            var activated = Activator.CreateInstance(hostedType);
            var activeFields = activated.GetType().GetFields();
            var activeProps = activated.GetType().GetProperties();

            foreach (var kv in objs)
            {
                var keyName = kv.Key.ToString();
                var isField = false;

                Type property = null;

                try
                {
                    if (activeFields.Any(s => s.Name == keyName))
                    {
                        isField = true;
                        property = activeFields.First(s => s.Name == keyName).FieldType;

                        if (property.Equals(typeof(Guid)))
                            activated.GetType().GetField(keyName).SetValue(activated, Guid.Parse(kv.Value.ToString()));
                        else
                            activated.GetType().GetField(keyName).SetValue(activated, Convert.ChangeType(kv.Value, property));
                    }
                    else if (activeProps.Any(s => s.Name == keyName))
                    {
                        property = activeProps.First(s => s.Name == keyName).PropertyType;

                        if (property.Equals(typeof(Guid)))                        
                            activated.GetType().GetProperty(keyName).SetValue(activated, Guid.Parse(kv.Value.ToString()));                       
                        else
                            activated.GetType().GetProperty(keyName).SetValue(activated, Convert.ChangeType(kv.Value, property));
                    }
                }
                catch (InvalidCastException)
                {
                    if (isField)
                        activated.GetType().GetField(keyName).SetValue(activated, ConvertProperties(property, (Dictionary<object, object>)kv.Value));
                    else
                        activated.GetType().GetProperty(keyName).SetValue(activated, ConvertProperties(property, (Dictionary<object, object>)kv.Value));
                }
            }

            return activated;
        }
    }
}
