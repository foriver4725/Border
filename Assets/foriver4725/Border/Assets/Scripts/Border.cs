using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace foriver4725.Border
{
    [ExecuteAlways]
    public sealed class Border : MonoBehaviour, IBorder
    {
        [SerializeField, Header("Settings")] private Property property;
        [SerializeField, Header("Debug Functions")] private Debugger debugger;
        [SerializeField, Header("Attach References (No Need to Touch)")] private Reference reference;

        private List<Transform> pinList = null;
        private MaterialPropertyBlock mpb = null;

        // Used in calculations
        private List<(Vector2 p0, Vector2 p1, Vector2 p2)> getRandomPosition_divideIntoTriangles_outTriList = null;
        private static readonly int ShaderColorID = Shader.PropertyToID("_Color");

        // Constants
        private static readonly float Tolerance = 0.01f;

        private void OnEnable()
        {
            pinList = new(64);
            mpb = new();
            getRandomPosition_divideIntoTriangles_outTriList = new(1024);

            UpdateBorder();
        }

        private void Update() => (BorderEx.GetClientMode() switch
        {
            ClientMode.Editor_Editing => UpdateBorder,
            ClientMode.Editor_Playing => debugger.IsUpdateBorderEveryFrameOnRunTime ? UpdateBorder : null,
            ClientMode.Build => debugger.IsUpdateBorderEveryFrameOnRunTime ? UpdateBorder : null,
            _ => null as Action<bool>,
        })?.Invoke(false);

        private void OnDisable()
        {
            UpdateBorder(doForciblyDisable: true);

            pinList.Clear();
            getRandomPosition_divideIntoTriangles_outTriList.Clear();
        }

        private void UpdateBorder(bool doForciblyDisable = false)
        {
            try
            {
                if (reference.DoesNullExists())
                {
                    Debug.LogError("There are references not attached in the Inspector. " +
                        "If errors are occurring, please consider this possibility first.");
                    return;
                }

                // Set active state
                bool isActive = !doForciblyDisable && BorderEx.GetClientMode() switch
                {
                    ClientMode.Editor_Editing => property.IsShow,
                    ClientMode.Editor_Playing => debugger.IsShowBorderOnEditor_Playing,
                    ClientMode.Build => false,
                    _ => false,
                };
                reference.LineRenderer.enabled = isActive;
                for (int i = 0; i < reference.PinsParentTransform.childCount; i++)
                {
                    Transform e = reference.PinsParentTransform.GetChild(i);
                    if (e == null)
                    {
                        Debug.LogWarning($"A pin at index {i} is null. Please remove it.");
                        continue;
                    }

                    if (e.TryGetComponent(out MeshRenderer renderer) == false)
                    {
                        Debug.LogWarning($"A pin ({e.name}) is missing a MeshRenderer component. Please add one.");
                        continue;
                    }

                    renderer.enabled = isActive;
                }

                int pinNum = reference.PinsParentTransform.childCount;
                if (pinNum < 3)
                {
                    if (isActive)
                        Debug.LogWarning("There are not enough pins to form a Border. At least 3 pins are required.");
                    reference.LineRenderer.positionCount = 0;
                    return;
                }

                // Update pin list
                pinList.Clear();
                for (int i = 0; i < pinNum; i++)
                    pinList.Add(reference.PinsParentTransform.GetChild(i));

                // Check if pin placement is valid
                Span<Vector2> posSpan = stackalloc Vector2[pinNum];
                for (int i = 0; i < pinNum; i++)
                    posSpan[i] = pinList[i].position.XZ();
                string s = IsPinOK(posSpan);
                if (string.IsNullOrEmpty(s) == false)
                    Debug.LogWarning($"{s}. If calculations are not working correctly, consider this possibility first.");

                // If active, set material and color, and draw line
                if (!isActive) return;
                {
                    reference.LineRenderer.GetPropertyBlock(mpb);
                    mpb.SetColor(ShaderColorID, property.Color);
                    reference.LineRenderer.SetPropertyBlock(mpb);
                    reference.LineRenderer.startColor = property.Color;
                    reference.LineRenderer.endColor = property.Color;
                }
                reference.LineRenderer.startWidth = property.Thin;
                reference.LineRenderer.endWidth = property.Thin;
                reference.LineRenderer.positionCount = pinNum + 1;
                for (int i = 0; i < pinNum; i++)
                    reference.LineRenderer.SetPosition(i, pinList[i].position);
                reference.LineRenderer.SetPosition(pinNum, pinList[0].position);
            }
            catch (Exception e)
            {
                Debug.LogError($"An error was thrown: {e}");
                return;
            }
        }

        private static string IsPinOK(ReadOnlySpan<Vector2> posList)
        {
            int length = posList.Length;

            // Two or more pins at the same coordinates?
            for (int i = 0; i < length; i++)
                for (int j = 0; j < length; j++)
                {
                    if (i == j) continue;
                    if (posList[i] == posList[j])
                        return "Two or more pins exist at the same coordinates";
                }

            // Three or more pins on the same straight line?
            for (int i = 0; i < length; i++)
            {
                Vector2 p0 = posList[(i - 1 + length) % length];
                Vector2 p1 = posList[i];
                Vector2 p2 = posList[(i + 1) % length];

                if (Mathf.Abs((p1 - p0, p2 - p1).Cross()) < Tolerance)
                    return "Three or more pins exist on the same straight line";
            }

            // Border intersects itself?
            for (int i = 0; i < length; i++)
                for (int j = 0; j < length; j++)
                {
                    if (i == j) continue;

                    Vector2 p0 = posList[i], p1 = posList[(i + 1) % length];
                    Vector2 q0 = posList[j], q1 = posList[(j + 1) % length];

                    float c0 = (p1 - p0, q0 - p0).Cross();
                    float c1 = (p1 - p0, q1 - p0).Cross();
                    float c2 = (q1 - q0, p0 - q0).Cross();
                    float c3 = (q1 - q0, p1 - q0).Cross();

                    if (c0 * c1 < 0 && c2 * c3 < 0)
                        return "There are intersecting parts in the Border";
                }

            return null;
        }

        public bool DoesContain(Vector2 pos, ReadOnlySpan<byte> layers, out bool outResult)
        {
            try
            {
                if (layers.Length > 0)
                {
                    byte myLayer = property.Layer;
                    bool hasValidLayer = false;
                    foreach (int l in layers)
                    {
                        if (myLayer == l)
                        {
                            hasValidLayer = true;
                            break;
                        }
                    }
                    if (!hasValidLayer)
                    {
                        outResult = false;
                        return true;
                    }
                }

                float th = 0;
                for (int i = 0; i < pinList.Count; i++)
                {
                    Vector2 fromPinPos = pinList[i].position.XZ();
                    Vector2 toPinPos = pinList[(i + 1) % pinList.Count].position.XZ();

                    Vector2 fromVec = fromPinPos - pos;
                    Vector2 toVec = toPinPos - pos;

                    if (fromVec.sqrMagnitude < Tolerance || toVec.sqrMagnitude < Tolerance)
                    {
                        outResult = true;
                        return true;
                    }

                    float dth = Mathf.Acos(Vector2.Dot(toVec.normalized, fromVec.normalized));
                    if ((fromVec, toVec).Cross() < 0) dth *= -1;

                    th += dth;
                }

                outResult = Mathf.Abs(th) >= Tolerance;
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"An error was thrown: {e}");
                outResult = false;
                return false;
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool DoesContain(Vector2 pos, out bool outResult) => DoesContain(pos, stackalloc byte[0], out outResult);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool DoesContain(Vector2 pos, byte layer, out bool outResult) => DoesContain(pos, stackalloc byte[1] { layer }, out outResult);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool DoesContain(Vector2 pos, IReadOnlyList<byte> layers, out bool outResult)
        {
            Span<byte> layerSpan = stackalloc byte[layers.Count];
            for (int i = 0; i < layers.Count; i++)
                layerSpan[i] = layers[i];
            return DoesContain(pos, layerSpan, out outResult);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool DoesContain(Vector2 pos) => DoesContain(pos, out bool result) ? result : false;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool DoesContain(Vector2 pos, byte layer) => DoesContain(pos, layer, out bool result) ? result : false;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool DoesContain(Vector2 pos, ReadOnlySpan<byte> layers) => DoesContain(pos, layers, out bool result) ? result : false;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool DoesContain(Vector2 pos, IReadOnlyList<byte> layers) => DoesContain(pos, layers, out bool result) ? result : false;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool DoesContain(Vector3 pos, ReadOnlySpan<byte> layers, out bool outResult) => DoesContain(pos.XZ(), layers, out outResult);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool DoesContain(Vector3 pos, out bool outResult) => DoesContain(pos, stackalloc byte[0], out outResult);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool DoesContain(Vector3 pos, byte layer, out bool outResult) => DoesContain(pos, stackalloc byte[1] { layer }, out outResult);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool DoesContain(Vector3 pos, IReadOnlyList<byte> layers, out bool outResult)
        {
            Span<byte> layerSpan = stackalloc byte[layers.Count];
            for (int i = 0; i < layers.Count; i++)
                layerSpan[i] = layers[i];
            return DoesContain(pos, layerSpan, out outResult);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool DoesContain(Vector3 pos) => DoesContain(pos, out bool result) ? result : false;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool DoesContain(Vector3 pos, byte layer) => DoesContain(pos, layer, out bool result) ? result : false;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool DoesContain(Vector3 pos, ReadOnlySpan<byte> layers) => DoesContain(pos, layers, out bool result) ? result : false;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool DoesContain(Vector3 pos, IReadOnlyList<byte> layers) => DoesContain(pos, layers, out bool result) ? result : false;

        public bool GetRandomPositionSimply(float y, out Vector3 outResult)
        {
            try
            {
                Span<Vector2> posSpan = stackalloc Vector2[pinList.Count];
                for (int i = 0; i < pinList.Count; i++)
                    posSpan[i] = pinList[i].position.XZ();

                float sx = float.MaxValue, ex = float.MinValue;
                float sy = float.MaxValue, ey = float.MinValue;
                foreach (Vector2 pos in posSpan)
                {
                    sx = Mathf.Min(sx, pos.x); ex = Mathf.Max(ex, pos.x);
                    sy = Mathf.Min(sy, pos.y); ey = Mathf.Max(ey, pos.y);
                }

                int cnt = 0;
                while (true)
                {
                    Vector2 v = new(UnityEngine.Random.Range(sx, ex), UnityEngine.Random.Range(sy, ey));
                    if (DoesContain(v))
                    {
                        outResult = v.X_Y(y);
                        return true;
                    }
                    if (++cnt >= ushort.MaxValue)
                    {
                        Debug.LogError("The calculation is taking too long");
                        outResult = Vector3.zero;
                        return false;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"An error was thrown: {e}");
                outResult = Vector3.zero;
                return false;
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool GetRandomPositionSimply(out Vector2 outResult)
        {
            bool r = GetRandomPositionSimply(0, out Vector3 v);
            outResult = v.XZ();
            return r;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector2 GetRandomPositionSimply() => GetRandomPositionSimply(out Vector2 result) ? result : Vector2.zero;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector3 GetRandomPositionSimply(float y) => GetRandomPositionSimply(y, out Vector3 result) ? result : Vector3.zero;

        public bool GetRandomPositionAccurately(float y, out Vector3 outResult)
        {
            try
            {
                Span<Vector2> getRandomPosition_divideIntoTriangles_outCollection = stackalloc Vector2[pinList.Count];
                GetPosCollection(pinList, getRandomPosition_divideIntoTriangles_outCollection);
                DivideIntoTriangles(getRandomPosition_divideIntoTriangles_outCollection, getRandomPosition_divideIntoTriangles_outTriList);
                (Vector2 p0, Vector2 p1, Vector2 p2) = GetRandomTriangle(getRandomPosition_divideIntoTriangles_outTriList);
                outResult = GetRandomPos(p0, p1, p2).X_Y(y);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"An error was thrown: {e}");
                outResult = Vector3.zero;
                return false;
            }

            // Get a collection of positions from a collection of Transforms
            // The length of the returned collection should be the same as that of the input collection
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            void GetPosCollection(IReadOnlyList<Transform> transforms, Span<Vector2> outCollection)
            {
                for (int i = 0; i < transforms.Count; i++)
                    outCollection[i] = transforms[i].position.XZ();

                // If counter-clockwise, reverse the order
                Vector2 sv = outCollection[0], ev = outCollection[1];
                Vector2 v = ev - sv;
                v = sv + v / 2 + new Vector2(v.y, -v.x) * (Tolerance * 10);  // A slightly right-shifted position
                if (!DoesContain(v))
                    outCollection.Reverse();
            }

            // Divide into triangles
            // The returned collection should be reserved enough
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            static void DivideIntoTriangles(ReadOnlySpan<Vector2> posCollection, List<(Vector2 p0, Vector2 p1, Vector2 p2)> outTriList)
            {
                outTriList.Clear();

                Span<Vector2> remains = stackalloc Vector2[posCollection.Length];
                posCollection.CopyTo(remains);

                while (remains.Length >= 3)
                {
                    int length = remains.Length;

                    bool isFound = false;
                    for (int i = 0; i < length; i++)
                    {
                        Vector2 p0 = remains[(i - 1 + length) % length];
                        Vector2 p1 = remains[i];
                        Vector2 p2 = remains[(i + 1) % length];

                        if ((p1 - p0, p2 - p1).Cross() >= 0) continue;  // Concave is not allowed
                        if (!IsEar(p0, p1, p2, remains)) continue;

                        outTriList.Add((p0, p1, p2));
                        {
                            // remains.RemoveAt(i);
                            Span<Vector2> newRemains = stackalloc Vector2[length - 1];
                            for (int j = 0, k = 0; j < length; j++)
                            {
                                if (j == i) continue;
                                newRemains[k++] = remains[j];
                            }
                            remains = newRemains;
                        }
                        isFound = true;
                        break;
                    }
                    if (!isFound) break;
                }

                // When considering a triangle formed by connecting points a, b, c in this order,
                // check whether point p is inside (including the boundary) of the triangle
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                static bool IsInside(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
                    => (p - a, b - a).Cross() >= 0 && (p - b, c - b).Cross() >= 0 && (p - c, a - c).Cross() >= 0;

                // Determine whether triangle abc is an "ear" of the polygon represented by collection
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                static bool IsEar(Vector2 a, Vector2 b, Vector2 c, ReadOnlySpan<Vector2> collection)
                {
                    // If any other vertex is inside this triangle, it's invalid
                    foreach (Vector2 e in collection)
                    {
                        if (e == a || e == b || e == c) continue;
                        if (IsInside(e, a, b, c)) return false;
                    }
                    return true;
                }
            }

            // Extract a random triangle
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            static (Vector2 p0, Vector2 p1, Vector2 p2) GetRandomTriangle(IReadOnlyList<(Vector2 p0, Vector2 p1, Vector2 p2)> triList)
            {
                Span<(Vector2 p0, Vector2 p1, Vector2 p2, float s)> triAreaSpan = stackalloc (Vector2, Vector2, Vector2, float)[triList.Count];
                for (int i = 0; i < triList.Count; i++)
                {
                    var (p0, p1, p2) = triList[i];
                    triAreaSpan[i] = (p0, p1, p2, CalcArea(p0, p1, p2));
                }

                float areaSum = 0;
                foreach (var (p0, p1, p2, s) in triAreaSpan)
                    areaSum += s;

                Span<(Vector2 p0, Vector2 p1, Vector2 p2, float p)> triPSpan = stackalloc (Vector2, Vector2, Vector2, float)[triAreaSpan.Length];
                for (int i = 0; i < triAreaSpan.Length; i++)
                {
                    var (p0, p1, p2, s) = triAreaSpan[i];
                    triPSpan[i] = (p0, p1, p2, s / areaSum);
                }

                return GetRandomTri(triPSpan);

                // Calculate the area of triangle abc
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                static float CalcArea(Vector2 a, Vector2 b, Vector2 c)
                    => Mathf.Abs((b - a, c - a).Cross()) * 0.5f;

                // Randomly select based on the given probability
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                static (Vector2 p0, Vector2 p1, Vector2 p2) GetRandomTri(ReadOnlySpan<(Vector2 p0, Vector2 p1, Vector2 p2, float p)> triPSpan)
                {
                    float p = UnityEngine.Random.value;

                    float cnt = 0.0f;
                    foreach (var e in triPSpan)
                    {
                        float sp = cnt;
                        float ep = cnt + e.p;
                        if (sp <= p && p < ep) return DelP(e);
                        cnt += e.p;
                    }

                    return DelP(triPSpan[^1]);
                }

                // Discard the probability information
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                static (Vector2 p0, Vector2 p1, Vector2 p2) DelP((Vector2 p0, Vector2 p1, Vector2 p2, float p) triP)
                    => (triP.p0, triP.p1, triP.p2);
            }

            // Get a random position inside a triangle (including boundaries)
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            static Vector2 GetRandomPos(Vector2 p0, Vector2 p1, Vector2 p2)
            {
                float s = UnityEngine.Random.value, t = UnityEngine.Random.value;
                if (s + t > 1) (s, t) = (1 - s, 1 - t);  // Ignore small errors here
                return p0 + s * (p1 - p0) + t * (p2 - p0);
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool GetRandomPositionAccurately(out Vector2 outResult)
        {
            bool r = GetRandomPositionAccurately(0, out Vector3 v);
            outResult = v.XZ();
            return r;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector2 GetRandomPositionAccurately() => GetRandomPositionAccurately(out Vector2 result) ? result : Vector2.zero;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector3 GetRandomPositionAccurately(float y) => GetRandomPositionAccurately(y, out Vector3 result) ? result : Vector3.zero;

        [Serializable]
        private sealed class Property
        {
            [SerializeField, Header("Show line?\n(Runtime is forced hidden)\nDefault: true")] private bool isShow = true;
            [SerializeField, Header("Layer\nDefault: 0")] private byte layer = 0;
            [SerializeField, Range(0.0f, 10.0f), Header("Line thickness\nDefault: 1.0f")] private float thin = 1.0f;
            [SerializeField, Header("Line color\nDefault: 0x83c35d")] private Color32 color = new(0x83, 0xc3, 0x5d, 0xff);

            internal bool IsShow => isShow;
            internal byte Layer => layer;
            internal float Thin => thin;
            internal Color32 Color32 => color;
            internal Color Color => color;
        }

        [Serializable]
        private sealed class Debugger
        {
            [SerializeField, Header("Disable all settings below\nDefault: true")] private bool isActive = true;
            [SerializeField, Header("Show Border in play mode (Editor)\nDefault: false")] private bool isShowBorderOnEditor_Playing = false;
            [SerializeField, Header("Update Border every frame during runtime\nDefault: false")] private bool isUpdateBorderEveryFrameOnRunTime = false;

            internal bool IsShowBorderOnEditor_Playing => !isActive && isShowBorderOnEditor_Playing;
            internal bool IsUpdateBorderEveryFrameOnRunTime => isUpdateBorderEveryFrameOnRunTime;
        }

        [Serializable]
        private sealed class Reference
        {
            [SerializeField, Header("Parent Transform of pins")] private Transform pinsParentTransform;
            [SerializeField, Header("LineRenderer")] private LineRenderer lineRenderer;

            internal Transform PinsParentTransform => pinsParentTransform;
            internal LineRenderer LineRenderer => lineRenderer;

            internal bool DoesNullExists()
            {
                if (pinsParentTransform == null) return true;
                if (lineRenderer == null) return true;
                return false;
            }
        }
    }

    internal enum ClientMode : byte
    {
        Editor_Editing,
        Editor_Playing,
        Build,
    }

    internal static class BorderEx
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Vector2 XZ(this Vector3 v) => new(v.x, v.z);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Vector3 X_Y(this Vector2 v, float y = 0) => new(v.x, y, v.y);

        // If positive, a is to the right of b
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static float Cross(this (Vector2 a, Vector2 b) v) => v.a.x * v.b.y - v.a.y * v.b.x;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ClientMode GetClientMode()
        {
#if UNITY_EDITOR && true
            return UnityEditor.EditorApplication.isPlaying ? ClientMode.Editor_Playing : ClientMode.Editor_Editing;
#else
            return ClientMode.Build;
#endif
        }
    }
}
