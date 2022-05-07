using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Baracuda.Monitoring.Interface;
using UnityEngine;

namespace Baracuda.Monitoring.API
{
    public static class MonitoringUI
    {
        #region --- API ---

        /// <summary>
        /// Set the active monitoring display visible.
        /// </summary>
        public static void Show()
        {
            ShowDisplayInternal();
        }

        /// <summary>
        /// Hide the active monitoring display.
        /// </summary>
        public static void Hide()
        {
            HideDisplayInternal();
        }

        /// <summary>
        /// Toggle the visibility of the active monitoring display.
        /// This method returns a value indicating the new visibility state.
        /// </summary>
        public static bool ToggleDisplay()
        {
            return ToggleDisplayInternal();
        }

        /// <summary>
        /// Returns true if the there is an active monitoring display that is also visible.
        /// </summary>
        /// <returns></returns>
        public static bool IsVisible()
        {
            return GetIsVisibleInternal();
        }

        /// <summary>
        /// Get the current <see cref="MonitoringUIController"/>
        /// </summary>
        public static MonitoringUIController GetActiveUIController()
        {
            return GetActiveUIControllerInternal();
        }

        /// <summary>
        /// Get the current <see cref="MonitoringUIController"/> as a concrete implementation of T.
        /// </summary>
        public static T GetActiveUIController<T>() where T : MonitoringUIController
        {
            return GetActiveUIControllerInternal() as T;
        }

        #endregion

        //--------------------------------------------------------------------------------------------------------------

        #region --- Internal ---

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ShowDisplayInternal()
        {
            if (controllerInstance)
            {
                controllerInstance.ShowMonitoringUI();
            }
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void HideDisplayInternal()
        {
            if (controllerInstance)
            {
                controllerInstance.HideMonitoringUI();
            }
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool ToggleDisplayInternal()
        {
            if (controllerInstance == null)
            {
                return false;
            }

            if (controllerInstance.IsVisible())
            {
                controllerInstance.HideMonitoringUI();
            }
            else
            {
                controllerInstance.ShowMonitoringUI();
            }

            return GetIsVisibleInternal();
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool GetIsVisibleInternal()
        {
            return controllerInstance != null && controllerInstance.IsVisible();
        }
        
        /*
         * Setup   
         */

        // singleton instance managed internally
        private static MonitoringUIController controllerInstance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InitializeMonitoringDisplay()
        {
            if (MonitoringSettings.GetInstance().EnableMonitoring)
            {
                MonitoringManager.ProfilingCompleted += OnProfilingCompletedInternal;
            }
        }

        private static void OnProfilingCompletedInternal(IReadOnlyList<IMonitorUnit> staticUnits,
            IReadOnlyList<IMonitorUnit> instanceUnits)
        {
            var settings = MonitoringSettings.GetInstance();

            if (settings.UIControllerUIController == null)
            {
                return;
            }
            
            if (!Application.isPlaying)
            {
                return;
            }
            
            controllerInstance = Object.Instantiate(settings.UIControllerUIController);
            
            Object.DontDestroyOnLoad(controllerInstance.gameObject);
            controllerInstance.gameObject.hideFlags = settings.ShowRuntimeUIController ? HideFlags.None : HideFlags.HideInHierarchy;

            MonitoringManager.UnitCreated += controllerInstance.OnUnitCreated;
            MonitoringManager.UnitDisposed += controllerInstance.OnUnitDisposed;

            for (var i = 0; i < staticUnits.Count; i++)
            {
                controllerInstance.OnUnitCreated(staticUnits[i]);
            }

            for (var i = 0; i < instanceUnits.Count; i++)
            {
                controllerInstance.OnUnitCreated(instanceUnits[i]);
            }

            if (settings.OpenDisplayOnLoad)
            {
                controllerInstance.ShowMonitoringUI();
            }
            else
            {
                controllerInstance.HideMonitoringUI();
            }
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static MonitoringUIController GetActiveUIControllerInternal()
        {
            return controllerInstance;
        }

        #endregion
    }
}