using Baracuda.Monitoring.API;
using UnityEngine;

namespace Baracuda.Monitoring.Example.Scripts
{
    public class MonitorInput : MonoBehaviour
    {
        [SerializeField] private KeyCode toggleKey = KeyCode.F3;

        private void Update()
        {
            if (Input.GetKeyDown(toggleKey))
            {
                MonitoringDisplay.ToggleDisplay();
            }
        }
    }
}