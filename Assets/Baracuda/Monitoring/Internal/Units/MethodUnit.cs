// Copyright (c) 2022 Jonathan Lang

using System;
using System.Runtime.CompilerServices;
using Baracuda.Monitoring.Interface;
using Baracuda.Monitoring.Internal.Profiling;
using Baracuda.Monitoring.Internal.Utilities;

namespace Baracuda.Monitoring.Internal.Units
{
    public sealed class MethodUnit<TTarget, TValue> : MonitorUnit, IGettableValue<MethodResult<TValue>>
    {
        #region --- Properties ---

        public override IMonitorProfile Profile => _methodProfile;

        #endregion
        
        //--------------------------------------------------------------------------------------------------------------
        
        #region --- Fields ---

        private readonly MethodProfile<TTarget, TValue> _methodProfile;
        private readonly TTarget _target;
        private readonly Func<TTarget, MethodResult<TValue>> _getValue;

        private readonly StringDelegate _compiledValueProcessor;

        #endregion
        
        //--------------------------------------------------------------------------------------------------------------
        
        public MethodUnit(
            TTarget target, 
            Func<TTarget, MethodResult<TValue>> getValue,
            MethodProfile<TTarget, TValue> profile) : base(target, profile)
        {
            _target = target;
            _methodProfile = profile;
            _getValue = getValue;

            _compiledValueProcessor = () => _getValue(_target).ToString();
        }


        //--------------------------------------------------------------------------------------------------------------
        
                #region --- Update ---

        public override void Refresh()
        {
            var state = GetState();
            RaiseValueChanged(state);
        }

        #endregion
        
        //--------------------------------------------------------------------------------------------------------------
        
        #region --- Get ---

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override string GetState()
        {
            return _compiledValueProcessor();
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public MethodResult<TValue> GetValue()
        {
            return _getValue(_target);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T GetValueAs<T>()
        {
            return _getValue(_target).Value.ConvertFast<TValue, T>();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public object GetValueAsObject()
        {
            return _getValue(_target);
        }

        #endregion
    }
}