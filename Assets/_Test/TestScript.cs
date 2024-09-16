using System;
using TMPro;
using UnityEngine;

using BorderSystem;

namespace Test
{
    internal sealed class TestScript : MonoBehaviour
    {
        [SerializeField] private Reference reference;
        [SerializeField] private TextMeshProUGUI debugTMPro;
        private BenchMark.Debug debug;
        bool isFirstUpdate = true;

        private void OnEnable()
        {
            debug = new(debugTMPro);
        }

        private void OnDisable()
        {
            reference.Dispose();
            debug.Dispose();

            reference = null;
            debugTMPro = null;
            debug = null;
        }

        private void Update()
        {
            if (isFirstUpdate)
            {
                isFirstUpdate = false;

                OnStart();
            }

            OnUpdate();
        }

        private void OnStart()
        {
            debug.Start();
        }

        private void OnUpdate()
        {
            debug.Update();
        }
    }

    [Serializable]
    internal sealed class Reference : IDisposable
    {
        [SerializeField] private Border border;
        [SerializeField] private GameObject testSpherePrefab;
        [SerializeField] private Material testMaterialRed;
        [SerializeField] private Material testMaterialGreen;
        [SerializeField] private Material testMaterialBlue;

        internal Border Border => border;
        internal GameObject SpherePrefab => testSpherePrefab;
        internal Material MaterialRed => testMaterialRed;
        internal Material MaterialGreen => testMaterialGreen;
        internal Material MaterialBlue => testMaterialBlue;

        public void Dispose()
        {
            testSpherePrefab = null;
            testMaterialRed = null;
            testMaterialGreen = null;
            testMaterialBlue = null;
        }
    }
}