using System;
using TMPro;
using UnityEngine;

using BorderSystem;

namespace Test
{
    internal sealed class TestScript : MonoBehaviour
    {
        #region

        [SerializeField] private Reference reference;
        [SerializeField] private Property property;
        [SerializeField] private TextMeshProUGUI debugTMPro;
        private BenchMark.Debug debug;
        bool isFirstUpdate = true;

        private void OnEnable()
        {
            debug = new(debugTMPro);
        }

        private void Update()
        {
            if (isFirstUpdate)
            {
                isFirstUpdate = false;

                debug.Start();
                OnStart();
            }

            debug.Update();
            OnUpdate();
        }

        #endregion

        Transform tf;

        private void OnStart()
        {
            tf = Instantiate(reference.SpherePrefab, Vector3.zero, Quaternion.identity, transform).transform;
        }

        private void OnUpdate()
        {
            bool? b = reference.Border.IsIn(tf.position, property.Layer);
            Debug.Log(b);
        }

        private void OnDisable()
        {
            reference.Dispose();
            debug.Dispose();

            reference = null;
            property = null;
            debugTMPro = null;
            debug = null;
        }
    }

    #region

    [Serializable]
    internal sealed class Property
    {
        [SerializeField] private int layer;

        internal int Layer => layer;
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

    #endregion
}