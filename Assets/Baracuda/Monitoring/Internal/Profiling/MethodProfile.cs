// Copyright (c) 2022 Jonathan Lang

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Baracuda.Monitoring.Internal.Units;
using Baracuda.Monitoring.Internal.Utilities;
using Baracuda.Reflection;
using UnityEngine;

namespace Baracuda.Monitoring.Internal.Profiling
{
    public readonly struct MethodResult<TValue>
    {
        public readonly TValue Value;
        private readonly string _toStringValue;

        public MethodResult(TValue value, string toStringValue)
        {
            Value = value;
            _toStringValue = toStringValue;
        }

        public override string ToString()
        {
            return _toStringValue;
        }
    }
    
    public sealed class MethodProfile<TTarget, TValue> : MonitorProfile
    {
        private readonly Func<TTarget, MethodResult<TValue>> _getValueDelegate;

        private MethodProfile(
            MethodInfo methodInfo,
            MonitorAttribute attribute,
            MonitorProfileCtorArgs args) : base(methodInfo, attribute, typeof(TTarget), typeof(TValue),
            UnitType.Method, args)
        {
            var valueProcessor = ValueProcessorFactory.CreateProcessorForType<TValue>(FormatData);
            var parameter = CreateParameterArray(methodInfo, attribute);
            _getValueDelegate = CreateGetDelegate(methodInfo, parameter, valueProcessor, FormatData);
        }

        internal override MonitorUnit CreateUnit(object target)
        {
            return new MethodUnit<TTarget, TValue>((TTarget)target, _getValueDelegate, this);
        }

        //--------------------------------------------------------------------------------------------------------------

        private static Func<TTarget, MethodResult<TValue>> CreateGetDelegate(MethodInfo methodInfo, object[] parameter, Func<TValue, string> valueProcessor, in IFormatData format)
        {
            var sb = new StringBuilder();
            var parameterInfos = methodInfo.GetParameters();
            var parameterHandles = CreateParameterHandles(parameterInfos, format);
            
            if (methodInfo.ReturnType == typeof(void))
            {
                var @void = new VoidValue().ConvertFast<VoidValue, TValue>();
                
                return target =>
                {
                    sb.Clear();
                    methodInfo.Invoke(target, parameter);
                    sb.Append(valueProcessor(@void));
                    foreach (var pair in parameterHandles)
                    {
                        sb.Append('\n');
                        var key = pair.Key;
                        var handle = pair.Value;
                        sb.Append(handle.GetValueAsString(parameter[key]));
                    }
                    return new MethodResult<TValue>(@void, sb.ToString());
                };
            }
            else
            {
                return target =>
                {
                    sb.Clear();
                    var result = methodInfo.Invoke(target, parameter).ConvertFast<object, TValue>();
                    sb.Append(valueProcessor(result));
                    foreach (var pair in parameterHandles)
                    {
                        sb.Append('\n');
                        var key = pair.Key;
                        var handle = pair.Value;
                        sb.Append(handle.GetValueAsString(parameter[key]));
                    }
                    return new MethodResult<TValue>(result, sb.ToString());
                };
            }
        }

        private static Dictionary<int, OutParameterHandle> CreateParameterHandles(IReadOnlyList<ParameterInfo> parameterInfos, IFormatData format)
        {
            var handles = new Dictionary<int, OutParameterHandle>(parameterInfos.Count);
            for (var i = 0; i < parameterInfos.Count; i++)
            {
                var current = parameterInfos[i];
                if (current.IsOut)
                {
                    var outArgName = $"  {"out".Colorize(new Color(1f, 0.27f, 0.53f))} {current.Name}";
                    var parameterFormat = new FormatData(
                        format.Format,
                        format.ShowIndexer,
                        outArgName,
                        format.FontSize,
                        format.Position,
                        format.AllowGrouping,
                        format.Group,
                        Mathf.Max(format.ElementIndent * 2, 4)
                    );
                    var handle = OutParameterHandle.CreateForType(current.ParameterType, parameterFormat);
                    handles.Add(i, handle);
                }
            }
            return handles;
        }

        private static object[] CreateParameterArray(MethodInfo methodInfo, MonitorAttribute attribute)
        {
            var parameterInfos = methodInfo.GetParameters();
            var paramArray = new object[parameterInfos.Length];
            var monitorMethodAttribute = attribute as MonitorMethodAttribute;
            
            for (var i = 0; i < parameterInfos.Length; i++)
            {
                var current = parameterInfos[i];
                var currentType = current.ParameterType;
                if (monitorMethodAttribute?.Args?.Length > i && !current.IsOut)
                {
                    paramArray[i] = Convert.ChangeType(monitorMethodAttribute.Args[i] ?? currentType.GetDefault(), currentType);
                }
                else
                {
                    var defaultValue = current.HasDefaultValue? current.DefaultValue : currentType.GetDefault();
                    paramArray[i] = defaultValue;
                }
            }

            return paramArray;
        }
    }
}