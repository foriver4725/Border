using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using UnityEngine;

namespace foriver4725.Border
{
    [ExecuteAlways]
    public sealed class Border : MonoBehaviour
    {
        [SerializeField, Header("Settings")] private Property property;
        [SerializeField, Header("Debug Functions")] private Debugger debugger;
        [SerializeField, Header("Attach References (No Need to Touch)")] private Reference reference;

        private List<Transform> pinList = new(64);

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

            property = null;
            debugger = null;
            reference = null;
            pinList = null;
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
                foreach (Transform e in reference.PinsParentTransform) e.GetComponent<MeshRenderer>().enabled = isActive;

                int pinNum = reference.PinsParentTransform.childCount;

                // Update pin list
                pinList.Clear();
                for (int i = 0; i < pinNum; i++) pinList.Add(reference.PinsParentTransform.GetChild(i));

                // Check if pin placement is valid
                var posList = pinList.Select(e => e.position.XZ()).ToList();
                string s = IsPinOK(posList.AsReadOnly());
                if (s != null) Debug.LogWarning($"{s}. If calculations are not working correctly, consider this possibility first.");

                // If active, set material and color, and draw line
                if (!isActive) return;
                Material mat = new(reference.Shader) { color = property.Color };
                reference.LineRenderer.sharedMaterial = mat;
                reference.LineRenderer.startWidth = property.Thin;
                reference.LineRenderer.endWidth = property.Thin;
                reference.LineRenderer.positionCount = pinNum + 1;
                for (int i = 0; i < pinNum; i++) reference.LineRenderer.SetPosition(i, pinList[i].position);
                reference.LineRenderer.SetPosition(pinNum, pinList[0].position);
            }
            catch (Exception e) { Debug.LogError($"An error was thrown: {e}"); }
        }

        /// <summary>
        /// <para>If posList matches any of the following, return a string explaining it; otherwise, return null</para>
        /// <para>・Two or more pins exist at the same coordinates</para>
        /// <para>・Three or more pins exist on the same straight line</para>
        /// <para>・The Border intersects itself</para>
        /// </summary>
        private string IsPinOK(ReadOnlyCollection<Vector2> posList, float ofst = 0.01f)
        {
            // Two or more pins at the same coordinates?
            for (int i = 0; i < posList.Count; i++)
            {
                for (int j = 0; j < posList.Count; j++)
                {
                    if (i == j) continue;

                    if (posList[i] == posList[j]) return "Two or more pins exist at the same coordinates";
                }
            }

            // Three or more pins on the same straight line?
            for (int i = 0; i < posList.Count; i++)
            {
                Vector2 p0 = posList[(i - 1 + posList.Count) % posList.Count];
                Vector2 p1 = posList[i];
                Vector2 p2 = posList[(i + 1) % posList.Count];

                if (Mathf.Abs((p1 - p0, p2 - p1).Cross()) < ofst)
                {
                    return "Three or more pins exist on the same straight line";
                }
            }

            // Border intersects itself?
            for (int i = 0; i < posList.Count; i++)
            {
                for (int j = 0; j < posList.Count; j++)
                {
                    if (i == j) continue;

                    Vector2 p0 = posList[i], p1 = posList[(i + 1) % posList.Count];
                    Vector2 q0 = posList[j], q1 = posList[(j + 1) % posList.Count];

                    float c0 = (p1 - p0, q0 - p0).Cross();
                    float c1 = (p1 - p0, q1 - p0).Cross();
                    float c2 = (q1 - q0, p0 - q0).Cross();
                    float c3 = (q1 - q0, p1 - q0).Cross();

                    if (c0 * c1 < 0 && c2 * c3 < 0) return "There are intersecting parts in the Border";
                }
            }

            return null;
        }

        /// <summary>
        /// <para>Check if the position is inside the border</para>
        /// <para>Return null if calculation is not possible</para>
        /// <para>If a layer is specified and does not match, return false</para>
        /// <para>If it matches the coordinates of any pin, return true by default</para>
        /// <para>*** Notes ***</para>
        /// <para>※ Invalid if the Border intersects itself</para>
        /// <para>※ Invalid if two or more pins exist at the same coordinates</para>
        /// <para>※ Invalid if three or more pins exist on the same straight line</para>
        /// </summary>
        public bool? IsIn(Vector2 pos, int? layer = null, bool isPinPositionsInclusive = true, float ofst = 0.01f)
        {
            try
            {
                if (pinList == null || pinList.Count <= 2) return null;
                if (layer.HasValue && property.Layer != layer.Value) return false;

                float th = 0;
                for (int i = 0; i < pinList.Count; i++)
                {
                    Vector2 fromPinPos = pinList[i].position.XZ();
                    Vector2 toPinPos = pinList[(i + 1) % pinList.Count].position.XZ();

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
            catch (Exception) { return null; }
        }

        /// <summary>
        /// <para>Check if the position is inside the border (ignores y component)</para>
        /// <para>Return null if calculation is not possible</para>
        /// <para>If a layer is specified and does not match, return false</para>
        /// <para>If it matches the coordinates of any pin, return true by default</para>
        /// <para>*** Notes ***</para>
        /// <para>※ Invalid if the Border intersects itself</para>
        /// <para>※ Invalid if two or more pins exist at the same coordinates</para>
        /// <para>※ Invalid if three or more pins exist on the same straight line</para>
        /// </summary>
        public bool? IsIn(Vector3 pos, int? layer = null, bool isPinPositionsInclusive = true, float ofst = 0.01f)
            => IsIn(pos.XZ(), layer, isPinPositionsInclusive, ofst);

        /// <summary>
        /// <para>Return a random position inside the border (y coordinate not randomized)</para>
        /// <para>Return null if calculation is not possible</para>
        /// <para>Note: This is a relatively heavy process</para>
        /// <para>*** Notes ***</para>
        /// <para>※ Invalid if the Border intersects itself</para>
        /// <para>※ Invalid if two or more pins exist at the same coordinates</para>
        /// <para>※ Invalid if three or more pins exist on the same straight line</para>
        /// </summary>
        public Vector3? GetRandomPosition(float y = 0, float ofst = 0.01f)
        {
            try
            {
                if (pinList == null || pinList.Count <= 2) return null;

                var val0 = GetPosList(pinList.AsReadOnly());
                var val1 = DivideIntoTriangles(val0);
                var val2 = GetRandomTriangle(val1);
                var val3 = GetRandomPos(val2);
                var val4 = val3.X_Y(y);

                return val4;
            }
            catch (Exception) { return null; }

            // Get a collection of positions from a collection of Transforms
            ReadOnlyCollection<Vector2> GetPosList(ReadOnlyCollection<Transform> transforms)
            {
                var posList = transforms.Select(e => e.position.XZ()).ToList().AsReadOnly();

                // If counter-clockwise, reverse the order
                Vector2 sv = posList[0], ev = posList[1];
                Vector2 v = ev - sv;
                v = sv + v / 2 + new Vector2(v.y, -v.x) * (ofst * 10);  // A slightly right-shifted position
                if (IsIn(v) != true) posList = posList.AsEnumerable().Reverse().ToList().AsReadOnly();

                return posList;
            }

            // Divide into triangles
            static ReadOnlyCollection<(Vector2 p0, Vector2 p1, Vector2 p2)>
                DivideIntoTriangles(ReadOnlyCollection<Vector2> posList)
            {
                List<(Vector2 p0, Vector2 p1, Vector2 p2)> triList = new();

                List<Vector2> remains = new(posList);

                while (remains.Count >= 3)
                {
                    bool isFound = false;
                    for (int i = 0; i < remains.Count; i++)
                    {
                        Vector2 p0 = remains[(i - 1 + remains.Count) % remains.Count];
                        Vector2 p1 = remains[i];
                        Vector2 p2 = remains[(i + 1) % remains.Count];

                        if ((p1 - p0, p2 - p1).Cross() >= 0) continue;  // Concave is not allowed
                        if (!IsEar(p0, p1, p2, remains.AsReadOnly())) continue;

                        triList.Add((p0, p1, p2));
                        remains.RemoveAt(i);
                        isFound = true;
                        break;
                    }
                    if (!isFound) break;
                }

                return triList.AsReadOnly();

                // When considering a triangle formed by connecting points a, b, c in this order,
                // check whether point p is inside (including the boundary) of the triangle
                static bool IsIn(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
                    => (p - a, b - a).Cross() >= 0 && (p - b, c - b).Cross() >= 0 && (p - c, a - c).Cross() >= 0;

                // Determine whether triangle abc is an "ear" of the polygon represented by list
                static bool IsEar(Vector2 a, Vector2 b, Vector2 c, ReadOnlyCollection<Vector2> list)
                {
                    // If any other vertex is inside this triangle, it's invalid
                    foreach (var e in list)
                    {
                        if (e == a || e == b || e == c) continue;
                        if (IsIn(e, a, b, c)) return false;
                    }
                    return true;
                }
            }

            // Extract a random triangle
            static (Vector2 p0, Vector2 p1, Vector2 p2)
                GetRandomTriangle(ReadOnlyCollection<(Vector2 p0, Vector2 p1, Vector2 p2)> triList)
            {
                ReadOnlyCollection<(Vector2 p0, Vector2 p1, Vector2 p2, float s)> triAreaList
                    = triList.Select(e => (e.p0, e.p1, e.p2, CalcArea(e.p0, e.p1, e.p2))).ToList().AsReadOnly();

                float areaSum = triAreaList.Sum(e => e.s);

                ReadOnlyCollection<(Vector2 p0, Vector2 p1, Vector2 p2, float p)> triPList
                   = triAreaList.Select(e => (e.p0, e.p1, e.p2, e.s / areaSum)).ToList().AsReadOnly();

                return GetRandomTri(triPList);

                // Calculate the area of triangle abc
                static float CalcArea(Vector2 a, Vector2 b, Vector2 c)
                    => Mathf.Abs((b - a, c - a).Cross()) / 2;

                // Randomly select based on the given probability
                static (Vector2 p0, Vector2 p1, Vector2 p2) GetRandomTri
                    (ReadOnlyCollection<(Vector2 p0, Vector2 p1, Vector2 p2, float p)> triPList, float ofst = 0.01f)
                {
                    float p = UnityEngine.Random.value;

                    float cnt = 0.0f;
                    foreach (var e in triPList)
                    {
                        float sp = cnt;
                        float ep = cnt + e.p;
                        if (sp <= p && p < ep) return DelP(e);
                        cnt += e.p;
                    }

                    return DelP(triPList[^1]);
                }

                // Discard the probability information
                static (Vector2 p0, Vector2 p1, Vector2 p2) DelP((Vector2 p0, Vector2 p1, Vector2 p2, float p) triP)
                    => (triP.p0, triP.p1, triP.p2);
            }

            // Get a random position inside a triangle (including boundaries)
            static Vector2 GetRandomPos((Vector2 p0, Vector2 p1, Vector2 p2) tri)
            {
                float s = UnityEngine.Random.value, t = UnityEngine.Random.value;
                if (s + t > 1) (s, t) = (1 - s, 1 - t);  // Ignore small errors here
                return tri.p0 + s * (tri.p1 - tri.p0) + t * (tri.p2 - tri.p0);
            }
        }

        /// <summary>
        /// <para>Return a random position inside the border (y coordinate not randomized)</para>
        /// <para>Return null if calculation is not possible</para>
        /// <para>Note: This is not an exact uniform distribution</para>
        /// <para>*** Notes ***</para>
        /// <para>※ Invalid if the Border intersects itself</para>
        /// <para>※ Invalid if two or more pins exist at the same coordinates</para>
        /// <para>※ Invalid if three or more pins exist on the same straight line</para>
        /// </summary>
        public Vector3? GetRandomPositionSimply(float y = 0)
        {
            try
            {
                if (pinList == null || pinList.Count <= 2) return null;

                List<Vector2> posList = pinList.Select(e => e.position.XZ()).ToList();

                float sx = posList.Min(e => e.x), ex = posList.Max(e => e.x);
                float sy = posList.Min(e => e.y), ey = posList.Max(e => e.y);

                int cnt = 0;
                while (true)
                {
                    Vector2 v = new(UnityEngine.Random.Range(sx, ex), UnityEngine.Random.Range(sy, ey));
                    if (IsIn(v) == true) return v.X_Y(y);
                    if (++cnt >= ushort.MaxValue) throw new Exception();
                }
            }
            catch (Exception) { return null; }
        }

        [Serializable]
        private sealed class Property
        {
            [SerializeField, Header("Show line?\n(Runtime is forced hidden)\nDefault: true")] private bool isShow = true;
            [SerializeField, Header("Layer\nDefault: 0")] private int layer = 0;
            [SerializeField, Range(0.0f, 10.0f), Header("Line thickness\nDefault: 1.0f")] private float thin = 1.0f;
            [SerializeField, Header("Line color\nDefault: 0x83c35d")] private Color32 color = new(0x83, 0xc3, 0x5d, 0xff);

            internal bool IsShow => isShow;
            internal int Layer => layer;
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
        internal static Vector2 XZ(this Vector3 v) => new(v.x, v.z);
        internal static Vector3 X_Y(this Vector2 v, float y = 0) => new(v.x, y, v.y);

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
