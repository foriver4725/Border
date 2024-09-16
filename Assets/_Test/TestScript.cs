using System;
using System.Collections;
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

        private void OnStart()
        {
            StartCoroutine(Coroutine());
        }

        private void OnUpdate()
        {

        }

        private IEnumerator Coroutine()
        {
            while (true)
            {
                MeshRenderer mr = Instantiate(reference.SpherePrefab, reference.Border.GetRandomPosition().Value,
                    Quaternion.identity, transform).GetComponent<MeshRenderer>();
                mr.material.color = reference.Border.IsIn(mr.transform.position, property.Layer) == true ?
                    Color.blue : Color.red;
                StartCoroutine(Wait(mr.gameObject));

                yield return new WaitForSeconds(property.Interval);
            }
        }

        private IEnumerator Wait(GameObject obj)
        {
            yield return new WaitForSeconds(10);
            Destroy(obj);
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
        [SerializeField] private float interval;

        internal int Layer => layer;
        internal float Interval => interval;
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