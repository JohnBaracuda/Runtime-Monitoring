using System;
using UnityEngine.Scripting;

namespace Baracuda.Monitoring.Attributes
{
    /// <summary>
    /// Disable monitoring for the target assembly or class.
    /// </summary>
    [AttributeUsage(AttributeTargets.Assembly | AttributeTargets.Class | AttributeTargets.Struct)]
    [Preserve]
    public class DisableMonitoringAttribute : Attribute
    {
    }
}
