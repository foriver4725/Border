using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

//TODO: 全体的にnullチェックとか頑張る

namespace foriver4725.Border
{
    [ExecuteAlways]
    public sealed class Border : MonoBehaviour
    {
        [SerializeField, Header("Settings")] private Property property;
        [SerializeField, Header("Debug Functions")] private Debugger debugger;
        [SerializeField, Header("Attach References (No Need to Touch)")] private Reference reference;

        private List<Transform> pinList = new(64);

        // Used in calculations
        private List<(Vector2 p0, Vector2 p1, Vector2 p2)> getRandomPosition_divideIntoTriangles_outTriList = new(1024);

        private void OnEnable() => (BorderEx.GetClientMode() switch
        {
            ClientMode.Editor_Editing => null,
            ClientMode.Editor_Playing => UpdateBorder,
            ClientMode.Build => UpdateBorder,
            _ => null as Action
        })?.Invoke();

        private void OnDisable() => (BorderEx.GetClientMode() switch
        {
            ClientMode.Editor_Editing => null,
            ClientMode.Editor_Playing => Dispose,
            ClientMode.Build => Dispose,
            _ => null as Action
        })?.Invoke();

        private void Update() => (BorderEx.GetClientMode() switch
        {
            ClientMode.Editor_Editing => UpdateBorder,
            ClientMode.Editor_Playing => debugger.IsUpdateBorderEveryFrameOnRunTime ? UpdateBorder : null,
            ClientMode.Build => debugger.IsUpdateBorderEveryFrameOnRunTime ? UpdateBorder : null,
            _ => null as Action
        })?.Invoke();

        /// <summary>
        /// Dispose references (explicit null assignment)
        /// </summary>
        private void Dispose()
        {
            reference.Dispose();
            pinList.Clear();
            getRandomPosition_divideIntoTriangles_outTriList.Clear();

            property = null;
            debugger = null;
            reference = null;
            pinList = null;
            getRandomPosition_divideIntoTriangles_outTriList = null;
        }

        /// <summary>
        /// Update the state of Border
        /// </summary>
        private void UpdateBorder()
        {
            try
            {
                if (reference.IsNullExist())
                {
                    Debug.LogError("There are references not attached in the Inspector. " +
                        "If errors are occurring, please consider this possibility first.");
                    return;
                }

                // Set active state
                bool isActive = BorderEx.GetClientMode() switch
                {
                    ClientMode.Editor_Editing => property.IsShow,
                    ClientMode.Editor_Playing => debugger.IsShowBorderOnEditor_Playing,
                    ClientMode.Build => false,
                    _ => false,
                };
                reference.LineRenderer.enabled = isActive;
                foreach (Transform e in reference.PinsParentTransform)
                {
                    if (e.TryGetComponent(out MeshRenderer renderer) == false)
                    {
                        Debug.LogWarning($"A pin ({e.name}) is missing a MeshRenderer component. Please add one.");
                        continue;
                    }
                    renderer.enabled = isActive;
                }

                int pinNum = reference.PinsParentTransform.childCount;

                // Update pin list
                pinList.Clear();
                for (int i = 0; i < pinNum; i++)
                    pinList.Add(reference.PinsParentTransform.GetChild(i));

                // Check if pin placement is valid
                Span<Vector2> posSpan = stackalloc Vector2[pinNum];
                for (int i = 0; i < pinNum; i++)
                    posSpan[i] = pinList[i].position.ToXZ();
                string s = IsPinOK(posSpan);
                if (s != null)
                    Debug.LogWarning($"{s}. If calculations are not working correctly, consider this possibility first.");

                // If active, set material and color, and draw line
                if (!isActive) return;
                Material mat = new(reference.Shader) { color = property.Color }; //TODO
                reference.LineRenderer.sharedMaterial = mat;
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
            }
        }

        /// <summary>
        /// If posList matches any of the following, return a string explaining it; otherwise, return null<br/>
        /// - Two or more pins exist at the same coordinates<br/>
        /// - Three or more pins exist on the same straight line<br/>
        /// - The Border intersects itself<br/>
        /// </summary>
        private static string IsPinOK(ReadOnlySpan<Vector2> posList, float ofst = 0.01f)
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

                if (Mathf.Abs((p1 - p0, p2 - p1).Cross()) < ofst)
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

        /// <summary>
        /// Check if the position is inside the border<br/>
        /// Return false if calculation is not possible<br/>
        /// If a layer is specified and does not match, return false; if -1, skip the check<br/>
        /// If it matches the coordinates of any pin, return true by default<br/>
        /// *** Notes ***<br/>
        /// - Invalid if the Border intersects itself<br/>
        /// - Invalid if two or more pins exist at the same coordinates<br/>
        /// - Invalid if three or more pins exist on the same straight line<br/>
        /// </summary>
        public bool IsIn(Vector2 pos, int layer = -1, bool isPinPositionsInclusive = true, float ofst = 0.01f)
        {
            try
            {
                if (pinList == null || pinList.Count <= 2)
                {
                    Debug.LogWarning("There are not enough pins to form a Border.");
                    return false;
                }
                if (layer != -1 && property.Layer != layer) return false;

                float th = 0;
                for (int i = 0; i < pinList.Count; i++)
                {
                    Vector2 fromPinPos = pinList[i].position.ToXZ();
                    Vector2 toPinPos = pinList[(i + 1) % pinList.Count].position.ToXZ();

                    Vector2 fromVec = fromPinPos - pos;
                    Vector2 toVec = toPinPos - pos;

                    if (fromVec.sqrMagnitude < ofst) return isPinPositionsInclusive;
                    if (toVec.sqrMagnitude < ofst) return isPinPositionsInclusive;

                    float dth = Mathf.Acos(Vector2.Dot(toVec.normalized, fromVec.normalized));
                    if ((fromVec, toVec).Cross() < 0) dth *= -1;

                    th += dth;
                }

                return Mathf.Abs(th) >= ofst;
            }
            catch (Exception)
            {
                Debug.LogWarning("An error occurred during the calculation. Please check the pin placements.");
                return false;
            }
        }

        /// <summary>
        /// Check if the position is inside the border (ignores y component)<br/>
        /// Return false if calculation is not possible<br/>
        /// If a layer is specified and does not match, return false<br/>
        /// If it matches the coordinates of any pin, return true by default<br/>
        /// *** Notes ***<br/>
        /// - Invalid if the Border intersects itself<br/>
        /// - Invalid if two or more pins exist at the same coordinates<br/>
        /// - Invalid if three or more pins exist on the same straight line<br/>
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsIn(Vector3 pos, int layer = -1, bool isPinPositionsInclusive = true, float ofst = 0.01f)
            => IsIn(pos.ToXZ(), layer, isPinPositionsInclusive, ofst);

        /// <summary>
        /// Return a random position inside the border (y coordinate not randomized)<br/>
        /// Return Vector3.zero if calculation is not possible<br/>
        /// Note: This is a relatively heavy process<br/>
        /// *** Notes ***<br/>
        /// - Invalid if the Border intersects itself<br/>
        /// - Invalid if two or more pins exist at the same coordinates<br/>
        /// - Invalid if three or more pins exist on the same straight line<br/>
        /// </summary>
        public Vector3 GetRandomPosition(float y = 0, float ofst = 0.01f)
        {
            try
            {
                if (pinList == null || pinList.Count <= 2)
                {
                    Debug.LogWarning("There are not enough pins to form a Border.");
                    return Vector3.zero;
                }

                Span<Vector2> getRandomPosition_divideIntoTriangles_outCollection = stackalloc Vector2[pinList.Count];
                GetPosCollection(pinList, getRandomPosition_divideIntoTriangles_outCollection);
                DivideIntoTriangles(getRandomPosition_divideIntoTriangles_outCollection, getRandomPosition_divideIntoTriangles_outTriList);
                var tri = GetRandomTriangle(getRandomPosition_divideIntoTriangles_outTriList);
                return GetRandomPos(tri).ToX_Y(y);
            }
            catch (Exception)
            {
                Debug.LogWarning("An error occurred during the calculation. Please check the pin placements.");
                return Vector3.zero;
            }

            // Get a collection of positions from a collection of Transforms
            // The length of the returned collection is the same as that of the input collection
            void GetPosCollection(IReadOnlyList<Transform> transforms, Span<Vector2> outCollection)
            {
                if (transforms == null || transforms.Count <= 1)
                {
                    Debug.LogWarning("The input collection of Transforms is null or has insufficient elements.");
                    return;
                }

                int length = transforms.Count;
                if (outCollection.Length != length)
                {
                    Debug.LogWarning("The length of the output Span must match the number of Transforms.");
                    return;
                }

                for (int i = 0; i < length; i++)
                {
                    Transform tf = transforms[i];
                    if (tf == null)
                    {
                        Debug.LogWarning($"The Transform at index {i} is null.");
                        outCollection[i] = Vector2.zero;
                        continue;
                    }

                    outCollection[i] = tf.position.ToXZ();
                }

                // If counter-clockwise, reverse the order
                Vector2 sv = outCollection[0], ev = outCollection[1];
                Vector2 v = ev - sv;
                v = sv + v / 2 + new Vector2(v.y, -v.x) * (ofst * 10);  // A slightly right-shifted position
                if (!IsIn(v))
                    outCollection.Reverse();
            }

            // Divide into triangles
            // The returned collection should be reserved enough
            static void DivideIntoTriangles(ReadOnlySpan<Vector2> posCollection, List<(Vector2 p0, Vector2 p1, Vector2 p2)> outTriList)
            {
                if (outTriList == null)
                {
                    Debug.LogWarning("The output List for triangles is null.");
                    return;
                }

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
                static bool IsIn(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
                    => (p - a, b - a).Cross() >= 0 && (p - b, c - b).Cross() >= 0 && (p - c, a - c).Cross() >= 0;

                // Determine whether triangle abc is an "ear" of the polygon represented by collection
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                static bool IsEar(Vector2 a, Vector2 b, Vector2 c, ReadOnlySpan<Vector2> collection)
                {
                    // If any other vertex is inside this triangle, it's invalid
                    foreach (Vector2 e in collection)
                    {
                        if (e == a || e == b || e == c) continue;
                        if (IsIn(e, a, b, c)) return false;
                    }
                    return true;
                }
            }

            // Extract a random triangle
            static (Vector2 p0, Vector2 p1, Vector2 p2)
                GetRandomTriangle(IReadOnlyList<(Vector2 p0, Vector2 p1, Vector2 p2)> triList)
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
                static (Vector2 p0, Vector2 p1, Vector2 p2) GetRandomTri
                    (ReadOnlySpan<(Vector2 p0, Vector2 p1, Vector2 p2, float p)> triPSpan, float ofst = 0.01f)
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
            static Vector2 GetRandomPos((Vector2 p0, Vector2 p1, Vector2 p2) tri)
            {
                float s = UnityEngine.Random.value, t = UnityEngine.Random.value;
                if (s + t > 1) (s, t) = (1 - s, 1 - t);  // Ignore small errors here
                return tri.p0 + s * (tri.p1 - tri.p0) + t * (tri.p2 - tri.p0);
            }
        }

        /// <summary>
        /// Return a random position inside the border (y coordinate not randomized)<br/>
        /// Return Vector3.zero if calculation is not possible<br/>
        /// Note: This is not an exact uniform distribution<br/>
        /// *** Notes ***<br/>
        /// - Invalid if the Border intersects itself<br/>
        /// - Invalid if two or more pins exist at the same coordinates<br/>
        /// - Invalid if three or more pins exist on the same straight line<br/>
        /// </summary>
        public Vector3 GetRandomPositionSimply(float y = 0)
        {
            try
            {
                if (pinList == null || pinList.Count <= 2)
                {
                    Debug.LogWarning("There are not enough pins to form a Border.");
                    return Vector3.zero;
                }

                Span<Vector2> posSpan = stackalloc Vector2[pinList.Count];
                for (int i = 0; i < pinList.Count; i++)
                    posSpan[i] = pinList[i].position.ToXZ();

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
                    if (IsIn(v))
                        return v.ToX_Y(y);
                    if (++cnt >= ushort.MaxValue)
                    {
                        Debug.LogWarning("Failed to find a position inside the Border after many attempts. The Border may be too narrow or complex.");
                        return Vector3.zero;
                    }
                }
            }
            catch (Exception)
            {
                Debug.LogWarning("An error occurred during the calculation. Please check the pin placements.");
                return Vector3.zero;
            }
        }

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
        private sealed class Reference : IDisposable
        {
            [SerializeField, Header("Parent Transform of pins")] private Transform pinsParentTransform;
            [SerializeField, Header("LineRenderer")] private LineRenderer lineRenderer;
            [SerializeField, Header("Shader")] private Shader shader;

            internal Transform PinsParentTransform => pinsParentTransform;
            internal LineRenderer LineRenderer => lineRenderer;
            internal Shader Shader => shader;

            public void Dispose()
            {
                pinsParentTransform = null;
                lineRenderer = null;
                shader = null;
            }

            internal bool IsNullExist()
            {
                if (pinsParentTransform == null) return true;
                if (lineRenderer == null) return true;
                if (shader == null) return true;
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
        internal static Vector2 ToXZ(this Vector3 v) => new(v.x, v.z);
        internal static Vector3 ToX_Y(this Vector2 v, float y = 0) => new(v.x, y, v.y);

        // If positive, a is to the right of b
        internal static float Cross(this (Vector2 a, Vector2 b) v) => v.a.x * v.b.y - v.a.y * v.b.x;

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
