using System;
using System.Collections;
using TMPro;
using UnityEngine;

namespace foriver4725.Border.Tests
{
    internal sealed class Manager : MonoBehaviour
    {
        #region

        [SerializeField] private Reference reference;
        [SerializeField] private Property property;
        [SerializeField] private TextMeshProUGUI debugTMPro;

        private Debug debug;
        private bool isFirstUpdate = true;

        private void OnEnable()
        {
            debug = new(debugTMPro);
        }

        private void Update()
        {
            if (isFirstUpdate)
            {
                isFirstUpdate = false;
                StartCoroutine(CreateBallsAsync());
            }

            debug.Update();
        }

        #endregion

        private IEnumerator CreateBallsAsync()
        {
            while (true)
            {
                MeshRenderer mr = Instantiate(reference.BallPrefab, reference.Border.GetRandomPosition().Value,
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
        [SerializeField] private GameObject ballPrefab;

        internal Border Border => border;
        internal GameObject BallPrefab => ballPrefab;

        public void Dispose()
        {
            border = null;
            ballPrefab = null;
        }
    }

    #endregion
}
