using System;
using UnityEngine;

namespace BenchMark
{
    internal sealed class Debug : IDisposable
    {
        private TMPro.TextMeshProUGUI debugText;

        int cnt = 0;
        float preT = 0f;

        float fps = 0f;
        float allocatedMemory = 0f;
        float unusedReservedMemory = 0f;
        float reservedMemory = 0f;
        float memoryP = 0f;

        internal Debug(TMPro.TextMeshProUGUI debugText) => this.debugText = debugText;
        public void Dispose() => debugText = null;
        public bool IsNullExist() => debugText == null;
        public void Start()
        {
            if (IsNullExist()) return;
        }
        public void Update()
        {
            if (IsNullExist()) return;

            // FPS�̌v�Z(0.5�b����)
            cnt++;
            float t = Time.realtimeSinceStartup - preT;
            if (t >= 0.5f)
            {
                fps = cnt / t;
                cnt = 0;
                preT = Time.realtimeSinceStartup;
            }

            // �g�p���������̎擾
            allocatedMemory = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong().ByteToMegabyte();
            unusedReservedMemory = UnityEngine.Profiling.Profiler.GetTotalUnusedReservedMemoryLong().ByteToMegabyte();
            reservedMemory = UnityEngine.Profiling.Profiler.GetTotalReservedMemoryLong().ByteToMegabyte();
            memoryP = allocatedMemory / reservedMemory;

            // �f�o�b�O�e�L�X�g���X�V(�����_�ȉ�2��)
            debugText.text =
                $"FPS: {fps:F2}\n" +
                $"Memory(MB): {allocatedMemory:F2}/{reservedMemory:F2} ({memoryP:P2}, {unusedReservedMemory:F2} unused)";
        }
    }

    internal static class DebugEx
    {
        internal static float ByteToMegabyte(this long n)
        {
            return (n >> 10) / 1024f;
        }
    }
}