using System;
using UnityEngine;
using UnityEngine.Profiling;

namespace foriver4725.Border.Tests
{
    internal sealed class Analyzer : MonoBehaviour
    {
        [SerializeField] private Border border;
        [SerializeField] private Transform target;

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                Vector3 pos = target.position;
                ReadOnlySpan<byte> layers = stackalloc byte[] { 0, 4, 16 };

                // warmup
                for (int i = 0; i < 8; i++)
                    _ = border.DoesContain(pos, layers);
                for (int i = 0; i < 8; i++)
                    _ = border.GetRandomPositionSimply();
                for (int i = 0; i < 8; i++)
                    _ = border.GetRandomPositionAccurately();

                Profiler.BeginSample("### Border DoContains ###");
                {
                    for (int i = 0; i < 1e6; i++)
                        _ = border.DoesContain(pos, layers);
                }
                Profiler.EndSample();

                Profiler.BeginSample("### Border GetRandomPositionSimply ###");
                {
                    for (int i = 0; i < 1e6; i++)
                        _ = border.GetRandomPositionSimply();
                }
                Profiler.EndSample();

                Profiler.BeginSample("### Border GetRandomPositionAccurately ###");
                {
                    for (int i = 0; i < 1e6; i++)
                        _ = border.GetRandomPositionAccurately();
                }
                Profiler.EndSample();
            }
        }
    }
}
