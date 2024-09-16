using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using UnityEngine;

namespace BorderSystem
{
    [ExecuteInEditMode]
    public sealed class Border : MonoBehaviour
    {
        [SerializeField, Header("�ݒ荀��")] private Property property;
        [SerializeField, Header("�f�o�b�O�@�\")] private Debugger debugger;
        [SerializeField, Header("�Q�Ƃ��A�^�b�`(�m�[�^�b�`��OK)")] private Reference reference;

        private List<Transform> pinList = new();

        private void OnEnable() => BorderEx.Do(BorderEx.GetClientMode() switch
        {
            ClientMode.Editor_Editing => BorderEx.Pass,
            ClientMode.Editor_Playing => UpdateBorder,
            ClientMode.Build => UpdateBorder,
            _ => throw new Exception("�����Ȓl�ł�")
        });

        private void OnDisable() => BorderEx.Do(BorderEx.GetClientMode() switch
        {
            ClientMode.Editor_Editing => BorderEx.Pass,
            ClientMode.Editor_Playing => Dispose,
            ClientMode.Build => Dispose,
            _ => throw new Exception("�����Ȓl�ł�")
        });

        private void Update() => BorderEx.Do(BorderEx.GetClientMode() switch
        {
            ClientMode.Editor_Editing => UpdateBorder,
            ClientMode.Editor_Playing => BorderEx.Pass,
            ClientMode.Build => BorderEx.Pass,
            _ => throw new Exception("�����Ȓl�ł�")
        });

        /// <summary>
        /// �Q�Ƃ�j������(�����Inull���)
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
        /// Border�̏�Ԃ��X�V����
        /// </summary>
        private void UpdateBorder()
        {
            if (reference.IsNullExist()) return;

            // �A�N�e�B�u��Ԃ̐ݒ�
            bool isActive = BorderEx.GetClientMode() switch
            {
                ClientMode.Editor_Editing => property.IsShow,
                ClientMode.Editor_Playing => debugger.IsShowBorderOnEditor_Playing,
                ClientMode.Build => false,
                _ => throw new Exception("�����Ȓl�ł�")
            };
            reference.LineRenderer.enabled = isActive;
            foreach (Transform e in reference.PinsParentTransform) e.GetComponent<MeshRenderer>().enabled = isActive;

            int pinNum = reference.PinsParentTransform.childCount;

            // �s���̃��X�g���X�V
            pinList.Clear();
            for (int i = 0; i < pinNum; i++) pinList.Add(reference.PinsParentTransform.GetChild(i));

            // �A�N�e�B�u�Ȃ�A�}�e���A���ƐF��ݒ肵�A����`�悷��
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
        /// <para>�͈͂̒��Ɋ܂܂�Ă��邩�ǂ������ׂ�</para>
        /// <para>�v�Z�s�̏ꍇ�Anull��Ԃ�</para>
        /// <para>���C���[���w�肵�Ă����ꍇ�A�������C���[���Ⴄ�Ȃ�Afalse��Ԃ�</para>
        /// <para>�����ꂩ�̃s���̍��W�ƈ�v���Ă����ꍇ�A�f�t�H���g��true��Ԃ�</para>
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
        /// <para>�͈͂̒��Ɋ܂܂�Ă��邩�ǂ������ׂ�(y�����͖��������)</para>
        /// <para>�v�Z�s�̏ꍇ�Anull��Ԃ�</para>
        /// <para>���C���[���w�肵�Ă����ꍇ�A�������C���[���Ⴄ�Ȃ�Afalse��Ԃ�</para>
        /// <para>�����ꂩ�̃s���̍��W�ƈ�v���Ă����ꍇ�A�f�t�H���g��true��Ԃ�</para>
        /// </summary>
        public bool? IsIn(Vector3 pos, int? layer = null, bool isPinPositionsInclusive = true, float ofst = 0.01f)
            => IsIn(pos.XOZ_To_XY(), layer, isPinPositionsInclusive, ofst);

        /// <summary>
        /// <para>�{�[�_�[���̃����_���ȍ��W��Ԃ�(y���W�͗����̑ΏۊO)</para>
        /// <para>�v�Z�s�̏ꍇ�Anull��Ԃ�</para>
        /// <para>�������d�߂Ȃ��Ƃɒ���</para>
        /// <para>�Ȃ��A�������Ă���A�������W�Ƀs����2���铙�̓���P�[�X�́A�l�����Ă��Ȃ�</para>
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

            // Transform�̃R���N�V��������A���W�̃R���N�V�������擾((�ꉞ)�d���폜 => �����v���ɕϊ� => �ǂݎ���p�ɕϊ�)
            static ReadOnlyCollection<Vector2> GetPosList(ReadOnlyCollection<Transform> transforms)
            {
                List<Vector2> posList
                = transforms
                .Select(e => e.position.XOZ_To_XY())
                .Distinct()
                .ToList();

                if ((posList[1] - posList[0], posList[^1] - posList[0]).Cross() > 0)
                    posList
                        = posList
                        .AsEnumerable().Reverse()
                        .ToList();

                return posList.AsReadOnly();
            }

            // �O�p�`�ɕ�������
            static ReadOnlyCollection<(Vector2 p0, Vector2 p1, Vector2 p2)>
                DivideIntoTriangles(ReadOnlyCollection<Vector2> posList)
            {
                List<(Vector2 p0, Vector2 p1, Vector2 p2)> triList = new();

                List<Vector2> remains = new(posList);

                while (remains.Count >= 3)
                {
                    for (int i = 0; i < remains.Count; i++)
                    {
                        Vector2 p0 = remains[(i - 1 + remains.Count) % remains.Count];
                        Vector2 p1 = remains[i];
                        Vector2 p2 = remains[(i + 1) % remains.Count];

                        if (!IsEar(p0, p1, p2, remains.AsReadOnly())) continue;

                        triList.Add((p0, p1, p2));
                        remains.RemoveAt(i);
                        break;
                    }
                }

                return triList.AsReadOnly();

                // �_abc�����̏��Ɍ��񂾎O�p�`���l���鎞�A�_p�����̎O�p�`�̓���(���E���܂�)�ɂ��邩�ǂ������肷��
                static bool IsIn(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
                    => (p - a, b - a).Cross() >= 0 && (p - b, c - b).Cross() >= 0 && (p - c, a - c).Cross() >= 0;

                // �O�p�`abc���Alist�ɂ���ĕ\������鑽�p�`�́u���v�ł��邩�ǂ����A���肷��
                static bool IsEar(Vector2 a, Vector2 b, Vector2 c, ReadOnlyCollection<Vector2> list)
                {
                    // ���̒��_�����̎O�p�`�̓����ɂ�������A�A�E�g
                    foreach (var e in list)
                    {
                        if (e == a || e == b || e == c) continue;
                        if (IsIn(e, a, b, c)) return false;
                    }
                    return true;
                }
            }

            // �����_���ȎO�p�`�𒊏o
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

                // �O�p�`abc�̖ʐς����߂�
                static float CalcArea(Vector2 a, Vector2 b, Vector2 c)
                    => Mathf.Abs((b - a, c - a).Cross()) / 2;

                // �^����ꂽ�m���Ɋ�Â��āA�����_���ɒ��o����
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

                // p�̏����̂Ă�
                static (Vector2 p0, Vector2 p1, Vector2 p2) DelP((Vector2 p0, Vector2 p1, Vector2 p2, float p) triP)
                    => (triP.p0, triP.p1, triP.p2);
            }

            // �O�p�`����(���E���܂�)�̃����_���ȍ��W���擾
            static Vector2 GetRandomPos((Vector2 p0, Vector2 p1, Vector2 p2) tri)
            {
                float s = UnityEngine.Random.value, t = UnityEngine.Random.value;
                if (s + t > 1) (s, t) = (1 - s, 1 - t);  // �����̌덷�͖�������
                return tri.p0 + s * (tri.p1 - tri.p0) + t * (tri.p2 - tri.p0);
            }
        }
    }

    [Serializable]
    public sealed class Property
    {
        [SerializeField, Header("����\�����邩\n(�����^�C�����͋�����\��)\n�f�t�H���g�Ftrue")] private bool isShow = true;
        public bool IsShow => isShow;
        [SerializeField, Header("���C���[\n�f�t�H���g�F0")] private int layer = 0;
        public int Layer => layer;
        [SerializeField, Range(0.0f, 10.0f), Header("���̑���\n�f�t�H���g�F1.0f")] private float thin = 1.0f;
        public float Thin => thin;
        [SerializeField, Header("���̐F\n�f�t�H���g�F0x83c35d")] private Color32 color = new(0x83, 0xc3, 0x5d, 0xff);
        public Color32 Color32 => color;
        public Color Color => color;
    }

    [Serializable]
    public sealed class Debugger
    {
        [SerializeField, Header("�ȉ��̑S�Ă̐ݒ�𖳌��ɂ���\n�f�t�H���g�Ftrue")]
        private bool isActive = true;

        [SerializeField, Header("�G�f�B�^�Ńv���C���[�h���ɂ�Border��\������\n�f�t�H���g�Ffalse")]
        private bool isShowBorderOnEditor_Playing = false;
        public bool IsShowBorderOnEditor_Playing => !isActive && isShowBorderOnEditor_Playing;
    }

    [Serializable]
    public sealed class Reference : IDisposable
    {
        [SerializeField, Header("�s���B�̐e��Transform")] private Transform pinsParentTransform;
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
    /// �N���C�A���g���[�h���擾����
    /// </summary>
    public enum ClientMode
    {
        /// <summary>
        /// �G�f�B�^�Ŏ��s���A���v���C���[�h���łȂ�
        /// </summary>
        Editor_Editing,

        /// <summary>
        /// �G�f�B�^�Ŏ��s���A���v���C���[�h��
        /// </summary>
        Editor_Playing,

        /// <summary>
        /// �r���h�f�[�^�Ŏ��s��
        /// </summary>
        Build
    }

    /// <summary>
    /// static�N���X
    /// </summary>
    public static class BorderEx
    {
        /// <summary>
        /// <para>3���������x�N�g����2���������x�N�g���ɓW�J����</para>
        /// <para>������x-z�x�N�g��������x-y�ɓW�J���Ay�����̏��͎̂Ă�</para>
        /// </summary>
        public static Vector2 XOZ_To_XY(this Vector3 v) => new(v.x, v.z);

        /// <summary>
        /// <para>2���������x�N�g����3���������x�N�g���ɕϊ�����</para>
        /// <para>�����̃x�N�g��������x-z�ɓW�J���A������y�̒l��p���ăx�N�g�����\�z</para>
        /// </summary>
        public static Vector3 XY_To_XOZ(this Vector2 v, float y = 0) => new(v.x, y, v.y);

        /// <summary>
        /// 2���������x�N�g�����m�́A�O��(�X�J���[��)�����߂�
        /// </summary>
        public static float Cross(this (Vector2 a, Vector2 b) v) => v.a.x * v.b.y - v.a.y * v.b.x;

        /// <summary>
        /// Action�����s���郉�b�p�[���\�b�h
        /// </summary>
        public static void Do(Action action) => action();

        /// <summary>
        /// �������Ȃ����\�b�h
        /// </summary>
        public static void Pass() { return; }

        /// <summary>
        /// ClientMode���擾����
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