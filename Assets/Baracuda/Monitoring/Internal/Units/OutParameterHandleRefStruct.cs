using System;
using System.Text;
using Baracuda.Monitoring.Internal.Utilities;
using Baracuda.Reflection;

namespace Baracuda.Monitoring.Internal.Units
{
#if ENABLE_IL2CPP
    /// <summary>
    /// Out parameter handle capable of handing readonly ref struct out parameters in IL2CPP Runtime.
    /// </summary>
    public class OutParameterHandleRefStruct : OutParameterHandle
    {
        private readonly IFormatData _formatData;
        private readonly StringBuilder _stringBuilder = new StringBuilder();
        private readonly Func<object, string> _valueProcessor;

        public override string GetValueAsString(object value)
        {
            return _valueProcessor(value);
        }

        public OutParameterHandleRefStruct(Type type, IFormatData formatData)
        {
            _formatData = formatData;
            _valueProcessor = CreateValueProcessor(type, formatData);
        }

        private Func<object, string> CreateValueProcessor(Type type, IFormatData formatData)
        {
            
            if (type.HasInterface<IFormattable>())
            {
                return (value) =>
                {
                    _stringBuilder.Clear();
                    _stringBuilder.Append(_formatData.Label);
                    _stringBuilder.Append(' ');
                    _stringBuilder.Append(((IFormattable)value).ToString(formatData.Format, null));
                    return _stringBuilder.ToString();
                };
            }
            else
            {
                return (value) =>
                {
                    _stringBuilder.Clear();
                    _stringBuilder.Append(_formatData.Label);
                    _stringBuilder.Append(' ');
                    _stringBuilder.Append(value);
                    return _stringBuilder.ToString();
                };
            }
        }
    }
#endif
}