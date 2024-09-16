using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using UnityEngine;

namespace BorderSystem
{
    [ExecuteAlways]
    public sealed class Border : MonoBehaviour
    {
        [SerializeField, Header("設定項目")] private Property property;
        [SerializeField, Header("デバッグ機能")] private Debugger debugger;
        [SerializeField, Header("参照をアタッチ(ノータッチでOK)")] private Reference reference;

        private List<Transform> pinList = new();

        private void OnEnable() => BorderEx.Do(BorderEx.GetClientMode() switch
        {
            ClientMode.Editor_Editing => UpdateBorder,
            ClientMode.Editor_Playing => UpdateBorder,
            ClientMode.Build => UpdateBorder,
            _ => throw new Exception("無効な値です")
        });

        private void OnDisable() => BorderEx.Do(BorderEx.GetClientMode() switch
        {
            ClientMode.Editor_Editing => Dispose,
            ClientMode.Editor_Playing => Dispose,
            ClientMode.Build => Dispose,
            _ => throw new Exception("無効な値です")
        });

        private void Update() => BorderEx.Do(BorderEx.GetClientMode() switch
        {
            ClientMode.Editor_Editing => UpdateBorder,
            ClientMode.Editor_Playing => debugger.IsUpdateBorderEveryFrameOnRunTime ? UpdateBorder : BorderEx.Pass,
            ClientMode.Build => debugger.IsUpdateBorderEveryFrameOnRunTime ? UpdateBorder : BorderEx.Pass,
            _ => throw new Exception("無効な値です")
        });

        /// <summary>
        /// 参照を破棄する(明示的null代入)
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
        /// Borderの状態を更新する
        /// </summary>
        private void UpdateBorder()
        {
            if (reference.IsNullExist()) return;

            // アクティブ状態の設定
            bool isActive = BorderEx.GetClientMode() switch
            {
                ClientMode.Editor_Editing => property.IsShow,
                ClientMode.Editor_Playing => debugger.IsShowBorderOnEditor_Playing,
                ClientMode.Build => false,
                _ => throw new Exception("無効な値です")
            };
            reference.LineRenderer.enabled = isActive;
            foreach (Transform e in reference.PinsParentTransform) e.GetComponent<MeshRenderer>().enabled = isActive;

            int pinNum = reference.PinsParentTransform.childCount;

            // ピンのリストを更新
            pinList.Clear();
            for (int i = 0; i < pinNum; i++) pinList.Add(reference.PinsParentTransform.GetChild(i));

            // アクティブなら、マテリアルと色を設定し、線を描画する
            if (!isActive) return;
            Material mat = new(reference.Material) { color = property.Color };
            reference.LineRenderer.sharedMaterial = mat;
            reference.LineRenderer.startWidth = property.Thin;
            reference.LineRenderer.endWidth = property.Thin;
            reference.LineRenderer.positionCount = pinNum + 1;
            for (int i = 0; i < pinNum; i++) reference.LineRenderer.SetPosition(i, pinList[i].position);
            reference.LineRenderer.SetPosition(pinNum, pinList[0].position);
        }

        /// <summary>
        /// <para>範囲の中に含まれているかどうか調べる</para>
        /// <para>計算不可の場合、nullを返す</para>
        /// <para>レイヤーを指定していた場合、もしレイヤーが違うなら、falseを返す</para>
        /// <para>いずれかのピンの座標と一致していた場合、デフォルトでtrueを返す</para>
        /// </summary>
        public bool? IsIn(Vector2 pos, int? layer = null, bool isPinPositionsInclusive = true, float ofst = 0.01f)
        {
            try
            {
                if (pinList == null || pinList.Count <= 2) return null;
                if (!layer.HasValue || property.Layer != layer.Value) return false;

                float th = 0;
                for (int i = 0; i < pinList.Count; i++)
                {
                    Vector2 fromPinPos = pinList[i].position.XOZ_To_XY();
                    Vector2 toPinPos = pinList[(i < pinList.Count - 1) ? i + 1 : 0].position.XOZ_To_XY();

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
        /// <para>範囲の中に含まれているかどうか調べる(y成分は無視される)</para>
        /// <para>計算不可の場合、nullを返す</para>
        /// <para>レイヤーを指定していた場合、もしレイヤーが違うなら、falseを返す</para>
        /// <para>いずれかのピンの座標と一致していた場合、デフォルトでtrueを返す</para>
        /// </summary>
        public bool? IsIn(Vector3 pos, int? layer = null, bool isPinPositionsInclusive = true, float ofst = 0.01f)
            => IsIn(pos.XOZ_To_XY(), layer, isPinPositionsInclusive, ofst);

        /// <summary>
        /// <para>ボーダー内のランダムな座標を返す(y座標は乱数の対象外)</para>
        /// <para>計算不可の場合、nullを返す</para>
        /// <para>処理が重めなことに注意</para>
        /// <para>なお、交差している、3点が同一直線状にある、同じ座標にピンが2つある、等の特殊ケースは、考慮していない</para>
        /// </summary>
        public Vector3? GetRandomPosition(float y = 0)
        {
            try
            {
                if (pinList == null || pinList.Count <= 2) return null;

                var val0 = GetPosList(pinList.AsReadOnly());
                var val1 = DivideIntoTriangles(val0);
                var val2 = GetRandomTriangle(val1);
                var val3 = GetRandomPos(val2);
                var val4 = val3.XY_To_XOZ(y);

                return val4;
            }
            catch (Exception) { return null; }

            // Transformのコレクションから、座標のコレクションを取得((一応)重複削除 => 時計回りに変換 => 読み取り専用に変換)
            static ReadOnlyCollection<Vector2> GetPosList(ReadOnlyCollection<Transform> transforms)
            {
                List<Vector2> posList
                = transforms
                .Select(e => e.position.XOZ_To_XY())
                .Distinct()
                .ToList();

                if ((posList[1] - posList[0], posList[0] - posList[^1]).Cross() < 0)
                    posList
                        = posList
                        .AsEnumerable().Reverse()
                        .ToList();

                return posList.AsReadOnly();
            }

            // 三角形に分割する
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

                        if ((p1 - p0, p2 - p1).Cross() <= 0) continue;  // 凹はダメ
                        if (!IsEar(p0, p1, p2, remains.AsReadOnly())) continue;

                        triList.Add((p0, p1, p2));
                        remains.RemoveAt(i);
                        isFound = true;
                        break;
                    }
                    if (!isFound) break;
                }

                return triList.AsReadOnly();

                // 点abcをこの順に結んだ三角形を考える時、点pがその三角形の内部(境界を含む)にあるかどうか判定する
                static bool IsIn(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
                    => (p - a, b - a).Cross() >= 0 && (p - b, c - b).Cross() >= 0 && (p - c, a - c).Cross() >= 0;

                // 三角形abcが、listによって表現される多角形の「耳」であるかどうか、判定する
                static bool IsEar(Vector2 a, Vector2 b, Vector2 c, ReadOnlyCollection<Vector2> list)
                {
                    // 他の頂点がこの三角形の内部にあったら、アウト
                    foreach (var e in list)
                    {
                        if (e == a || e == b || e == c) continue;
                        if (IsIn(e, a, b, c)) return false;
                    }
                    return true;
                }
            }

            // ランダムな三角形を抽出
            static (Vector2 p0, Vector2 p1, Vector2 p2)
                GetRandomTriangle(ReadOnlyCollection<(Vector2 p0, Vector2 p1, Vector2 p2)> triList)
            {
                ReadOnlyCollection<(Vector2 p0, Vector2 p1, Vector2 p2, float s)> triAreaList
                    = triList
                    .Select(e => (e.p0, e.p1, e.p2, CalcArea(e.p0, e.p1, e.p2)))
                    .ToList()
                    .AsReadOnly();

                float areaSum = triAreaList.Sum(e => e.s);

                ReadOnlyCollection<(Vector2 p0, Vector2 p1, Vector2 p2, float p)> triPList
                   = triAreaList
                   .Select(e => (e.p0, e.p1, e.p2, e.s / areaSum))
                   .ToList()
                   .AsReadOnly();

                return GetRandomTri(triPList);

                // 三角形abcの面積を求める
                static float CalcArea(Vector2 a, Vector2 b, Vector2 c)
                    => Mathf.Abs((b - a, c - a).Cross()) / 2;

                // 与えられた確率に基づいて、ランダムに抽出する
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

                // pの情報を捨てる
                static (Vector2 p0, Vector2 p1, Vector2 p2) DelP((Vector2 p0, Vector2 p1, Vector2 p2, float p) triP)
                    => (triP.p0, triP.p1, triP.p2);
            }

            // 三角形内部(境界を含む)のランダムな座標を取得
            static Vector2 GetRandomPos((Vector2 p0, Vector2 p1, Vector2 p2) tri)
            {
                float s = UnityEngine.Random.value, t = UnityEngine.Random.value;
                if (s + t > 1) (s, t) = (1 - s, 1 - t);  // ここの誤差は無視する
                return tri.p0 + s * (tri.p1 - tri.p0) + t * (tri.p2 - tri.p0);
            }
        }
    }

    [Serializable]
    public sealed class Property
    {
        [SerializeField, Header("線を表示するか\n(ランタイム時は強制非表示)\nデフォルト：true")] private bool isShow = true;
        public bool IsShow => isShow;
        [SerializeField, Header("レイヤー\nデフォルト：0")] private int layer = 0;
        public int Layer => layer;
        [SerializeField, Range(0.0f, 10.0f), Header("線の太さ\nデフォルト：1.0f")] private float thin = 1.0f;
        public float Thin => thin;
        [SerializeField, Header("線の色\nデフォルト：0x83c35d")] private Color32 color = new(0x83, 0xc3, 0x5d, 0xff);
        public Color32 Color32 => color;
        public Color Color => color;
    }

    [Serializable]
    public sealed class Debugger
    {
        [SerializeField, Header("以下の全ての設定を無効にする\nデフォルト：true")]
        private bool isActive = true;

        [SerializeField, Header("エディタでプレイモード中にもBorderを表示する\nデフォルト：false")]
        private bool isShowBorderOnEditor_Playing = false;
        public bool IsShowBorderOnEditor_Playing => !isActive && isShowBorderOnEditor_Playing;
        [SerializeField, Header("ランタイム中、毎フレームBorderを更新する\nデフォルト：false")]
        private bool isUpdateBorderEveryFrameOnRunTime = false;
        public bool IsUpdateBorderEveryFrameOnRunTime => isUpdateBorderEveryFrameOnRunTime;
    }

    [Serializable]
    public sealed class Reference : IDisposable
    {
        [SerializeField, Header("ピン達の親のTransform")] private Transform pinsParentTransform;
        public Transform PinsParentTransform => pinsParentTransform;
        [SerializeField, Header("LineRenderer")] private LineRenderer lineRenderer;
        public LineRenderer LineRenderer => lineRenderer;
        [SerializeField, Header("Material")] private Material material;
        public Material Material => material;

        public void Dispose()
        {
            pinsParentTransform = null;
            lineRenderer = null;
            material = null;
        }

        public bool IsNullExist()
        {
            if (pinsParentTransform == null) return true;
            if (lineRenderer == null) return true;
            if (material == null) return true;
            return false;
        }
    }

    /// <summary>
    /// クライアントモードを取得する
    /// </summary>
    public enum ClientMode
    {
        /// <summary>
        /// エディタで実行中、かつプレイモード中でない
        /// </summary>
        Editor_Editing,

        /// <summary>
        /// エディタで実行中、かつプレイモード中
        /// </summary>
        Editor_Playing,

        /// <summary>
        /// ビルドデータで実行中
        /// </summary>
        Build
    }

    /// <summary>
    /// staticクラス
    /// </summary>
    public static class BorderEx
    {
        /// <summary>
        /// <para>3次元実数ベクトルを2次元実数ベクトルに展開する</para>
        /// <para>引数のx-zベクトル成分をx-yに展開し、y成分の情報は捨てる</para>
        /// </summary>
        public static Vector2 XOZ_To_XY(this Vector3 v) => new(v.x, v.z);

        /// <summary>
        /// <para>2次元実数ベクトルを3次元実数ベクトルに変換する</para>
        /// <para>引数のベクトル成分をx-zに展開し、引数のyの値を用いてベクトルを構築</para>
        /// </summary>
        public static Vector3 XY_To_XOZ(this Vector2 v, float y = 0) => new(v.x, y, v.y);

        /// <summary>
        /// <para>2次元実数ベクトル同士の、外積(スカラー)を求める</para>
        /// <para>正の場合、bはaの左側にある</para>
        /// </summary>
        public static float Cross(this (Vector2 a, Vector2 b) v) => v.a.x * v.b.y - v.a.y * v.b.x;

        /// <summary>
        /// Actionを実行するラッパーメソッド
        /// </summary>
        public static void Do(Action action) => action();

        /// <summary>
        /// 何もしないメソッド
        /// </summary>
        public static void Pass() { return; }

        /// <summary>
        /// ClientModeを取得する
        /// </summary>
        public static ClientMode GetClientMode()
        {
#if UNITY_EDITOR && true
            return UnityEditor.EditorApplication.isPlaying ? ClientMode.Editor_Playing : ClientMode.Editor_Editing;
#else
            return ClientMode.Build;
#endif
        }
    }
}