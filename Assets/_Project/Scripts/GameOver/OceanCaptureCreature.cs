using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>既存のタコ画像と、カメラへ伸びる四本の立体触手を演出専用に生成する</summary>
public sealed class OceanCaptureCreature : IDisposable
{
    private const int Segments = 28;
    private const int Sides = 10;
    private readonly GameObject _root;
    private readonly Transform _head;
    private readonly Material _headMaterial;
    private readonly Material _armMaterial;
    private readonly Material _surfaceMaterial;
    private readonly Mesh[] _arms = new Mesh[4];
    private readonly Vector3[] _vertices = new Vector3[(Segments + 1) * (Sides + 1)];
    private readonly Vector3 _waterPoint;
    private readonly Vector3 _forward;
    private readonly Vector3 _right;
    private readonly float _size;
    private Transform _scare;
    private Material _scareMaterial;

    /// <summary>接近時だけ使う高精細な顔を元の目の位置に重ねる</summary>
    /// <param name="texture">中央に目がある専用画像</param>
    /// <example>生成直後に一度だけ呼ぶ</example>
    public void PrepareScareFace(Texture texture)
    {
        if (texture == null) return;
        _scareMaterial = new Material(_headMaterial) { name = "Closeup Octopus Face" };
        _scareMaterial.SetTexture("_BaseMap", texture);
        _scareMaterial.SetFloat("_Scare", 1f);
        _scareMaterial.SetFloat("_Opacity", 0f);
        _scare = CreateRenderer("Closeup Face", _head.GetComponent<MeshFilter>().sharedMesh, _scareMaterial).transform;
        _scare.localScale = Vector3.one * _size;
    }

    /// <summary>静かな水面に目だけを淡く見せる</summary>
    /// <param name="progress">静止時間の進行度</param>
    /// <param name="eye">カメラ位置</param>
    /// <example>出現直前の各フレームで呼ぶ</example>
    public void ShowOmen(float progress, Vector3 eye)
    {
        _head.position = _waterPoint - Vector3.up * (_size * 0.27f - 0.035f);
        _head.rotation = Quaternion.LookRotation((_head.position - eye).normalized, Vector3.up);
        _headMaterial.SetFloat("_Omen", 1f);
        _headMaterial.SetFloat("_Opacity", Mathf.Sin(progress * Mathf.PI) * 0.28f);
    }

    /// <summary>近接用の顔へ切り替え、視線を中央に寄せて瞳孔を細くする</summary>
    /// <param name="blend">専用の顔の表示量</param>
    /// <param name="focus">目の動きの進行度</param>
    /// <example>引き寄せ中と目の前で停止している間に呼ぶ</example>
    public void ShowScareFace(float blend, float focus)
    {
        if (_scare == null) return;
        _scare.SetPositionAndRotation(EyePosition - FaceForward * 0.025f, _head.rotation);
        _scareMaterial.SetFloat("_Opacity", blend);
        _scareMaterial.SetFloat("_Focus", focus);
        _headMaterial.SetFloat("_Opacity", 1f - blend);
    }

    /// <summary>元画像の上部中央にある目のワールド座標</summary>
    public Vector3 EyePosition => _head.TransformPoint(new Vector3(0f, 0.27f, 0f));

    /// <summary>頭部の面へ正面から近づく方向</summary>
    public Vector3 FaceForward => _head.forward;

    /// <summary>元の敵の物理判定を動かさず、表示専用の頭部と触手を作る</summary>
    /// <param name="enemy">元画像とメッシュを持つ敵</param>
    /// <param name="shader">タコと触手の描画シェーダー</param>
    /// <param name="surfaceShader">海面の裏側を描くシェーダー</param>
    /// <param name="waterPoint">出現先の水面座標</param>
    /// <param name="forward">カメラから海へ向かう水平な方向</param>
    /// <param name="size">タコの表示幅、単位はメートル</param>
    /// <example>暗転中に生成し、Revealで透明から実体へ変える</example>
    public OceanCaptureCreature(VisionEnemyView enemy, Shader shader, Shader surfaceShader,
        Vector3 waterPoint, Vector3 forward, float size)
    {
        // テクスチャのある描画物だけを使い、敵の発見範囲メッシュは複製しない
        MeshFilter source = null;
        Material sourceMaterial = null;
        foreach (var renderer in enemy.GetComponentsInChildren<MeshRenderer>(true))
        {
            if (renderer.sharedMaterial == null || renderer.sharedMaterial.mainTexture == null) continue;
            source = renderer.GetComponent<MeshFilter>();
            sourceMaterial = renderer.sharedMaterial;
            if (source != null) break;
        }
        if (source == null || shader == null || surfaceShader == null)
            throw new InvalidOperationException("落水演出の敵メッシュまたはシェーダーが未設定");

        _waterPoint = waterPoint;
        _forward = forward;
        _right = Vector3.Cross(Vector3.up, forward).normalized;
        _size = size;
        _root = new GameObject("Ocean Capture Creature");
        SceneManager.MoveGameObjectToScene(_root, enemy.gameObject.scene);
        _headMaterial = new Material(shader) { name = "Capture Octopus" };
        _headMaterial.SetTexture("_BaseMap", sourceMaterial.mainTexture);
        _headMaterial.SetFloat("_WaterHeight", waterPoint.y);
        _armMaterial = new Material(shader) { name = "Capture Tentacles" };
        _armMaterial.SetFloat("_Tentacle", 1f);
        _armMaterial.SetFloat("_WaterHeight", waterPoint.y);
        _head = CreateRenderer("Octopus", source.sharedMesh, _headMaterial).transform;
        _head.localScale = Vector3.one * size;

        // 波打つ裏面は水中からだけ見え、水上の既存の海面はそのまま使う
        _surfaceMaterial = new Material(surfaceShader);
        var underside = GameObject.CreatePrimitive(PrimitiveType.Quad);
        UnityEngine.Object.Destroy(underside.GetComponent<Collider>());
        underside.name = "Water Underside";
        underside.transform.SetParent(_root.transform, false);
        underside.transform.SetPositionAndRotation(waterPoint - Vector3.up * 0.025f, Quaternion.Euler(90f, 0f, 0f));
        underside.transform.localScale = new Vector3(60f, 60f, 1f);
        underside.GetComponent<MeshRenderer>().sharedMaterial = _surfaceMaterial;

        for (var i = 0; i < _arms.Length; i++)
        {
            var mesh = CreateArmMesh();
            _arms[i] = mesh;
            CreateRenderer("Tentacle " + i, mesh, _armMaterial);
        }
        Reveal(0f, waterPoint - forward * 3f + Vector3.up * 1.6f);
    }

    /// <summary>専用ルートの下へ物理判定を持たない描画物を追加する</summary>
    /// <param name="name">オブジェクト名</param>
    /// <param name="mesh">表示メッシュ</param>
    /// <param name="material">共有する演出マテリアル</param>
    /// <returns>生成した描画オブジェクト</returns>
    /// <example>頭部と各触手の生成時に呼ぶ</example>
    private GameObject CreateRenderer(string name, Mesh mesh, Material material)
    {
        var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(_root.transform, false);
        go.GetComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = go.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        return go;
    }

    /// <summary>触手の固定トポロジーと吸盤用UVを一度だけ生成する</summary>
    /// <returns>頂点だけを更新して再利用するメッシュ</returns>
    /// <example>各触手の初期化時に呼ぶ</example>
    private Mesh CreateArmMesh()
    {
        var mesh = new Mesh { name = "Capture Tentacle Mesh" };
        mesh.MarkDynamic();
        var uv = new Vector2[_vertices.Length];
        var indices = new int[Segments * Sides * 6];
        for (var ring = 0; ring <= Segments; ring++)
            for (var side = 0; side <= Sides; side++)
                uv[ring * (Sides + 1) + side] = new Vector2(side / (float)Sides, ring / (float)Segments);
        var index = 0;
        for (var ring = 0; ring < Segments; ring++)
            for (var side = 0; side < Sides; side++)
            {
                var a = ring * (Sides + 1) + side;
                var b = a + Sides + 1;
                indices[index++] = a; indices[index++] = b; indices[index++] = a + 1;
                indices[index++] = a + 1; indices[index++] = b; indices[index++] = b + 1;
            }
        mesh.vertices = _vertices;
        mesh.uv = uv;
        mesh.triangles = indices;
        return mesh;
    }

    /// <summary>タコを水中から浮かび上がらせ、透明度を徐々に戻す</summary>
    /// <param name="progress">出現の進行度、0から1</param>
    /// <param name="eye">一人称カメラの位置</param>
    /// <example>出現アニメーションの各フレームで呼ぶ</example>
    public void Reveal(float progress, Vector3 eye)
    {
        _headMaterial.SetFloat("_Omen", 0f);
        var p = Mathf.SmoothStep(0f, 1f, progress);
        _head.position = _waterPoint + Vector3.up * Mathf.Lerp(-_size * 0.48f, _size * 0.28f, p);
        // Quadの表面を観客へ向け、水面を覗き込んでも頭部を読める角度にする
        _head.rotation = Quaternion.LookRotation((_head.position - eye).normalized, Vector3.up);
        _headMaterial.SetFloat("_Opacity", p);
        _armMaterial.SetFloat("_Opacity", 0f);
    }

    /// <summary>水中から画面の左右へ触手を伸ばし、落下中も視界の縁をつかむ</summary>
    /// <param name="progress">伸びる進行度、0から1</param>
    /// <param name="eye">現在のカメラ座標</param>
    /// <param name="rotation">現在のカメラの向き</param>
    /// <example>掴みと落下の両方でUpdateReachを呼ぶ</example>
    public void UpdateReach(float progress, Vector3 eye, Quaternion rotation)
    {
        _armMaterial.SetFloat("_Opacity", Mathf.Clamp01(progress * 4f));
        var viewRight = rotation * Vector3.right;
        var viewUp = rotation * Vector3.up;
        var viewForward = rotation * Vector3.forward;
        for (var arm = 0; arm < _arms.Length; arm++)
        {
            var sign = arm % 2 == 0 ? -1f : 1f;
            var upper = arm >= 2;
            var start = _waterPoint + _right * sign * (upper ? 0.6f : 1f) - Vector3.up * 0.1f;
            var target = eye + viewForward * (upper ? 0.5f : 0.4f)
                + viewRight * sign * (upper ? 0.22f : 0.35f) + viewUp * (upper ? 0.19f : -0.18f);
            var end = Vector3.Lerp(start + Vector3.up * 0.3f, target, Mathf.SmoothStep(0f, 1f, progress));
            var control1 = start - _forward * 0.7f + Vector3.up * (upper ? 1.8f : 0.7f) + _right * sign * 0.9f;
            var control2 = end + viewRight * sign * 0.8f + viewForward * 0.7f + viewUp * (upper ? 0.4f : -0.25f);
            for (var ring = 0; ring <= Segments; ring++)
            {
                var t = ring / (float)Segments;
                var u = 1f - t;
                var center = u * u * u * start + 3f * u * u * t * control1 + 3f * u * t * t * control2 + t * t * t * end;
                var tangent = (3f * u * u * (control1 - start) + 6f * u * t * (control2 - control1) + 3f * t * t * (end - control2)).normalized;
                var normal = Vector3.Cross(tangent, _right).normalized;
                if (normal.sqrMagnitude < 0.01f) normal = Vector3.Cross(tangent, Vector3.up).normalized;
                var binormal = Vector3.Cross(tangent, normal);
                var radius = Mathf.Lerp(upper ? 0.13f : 0.18f, 0f, Mathf.Pow(t, 1.4f));
                for (var side = 0; side <= Sides; side++)
                {
                    var angle = side / (float)Sides * Mathf.PI * 2f;
                    _vertices[ring * (Sides + 1) + side] = center + radius * (normal * Mathf.Cos(angle) + binormal * Mathf.Sin(angle));
                }
            }
            _arms[arm].vertices = _vertices;
            _arms[arm].RecalculateNormals();
            _arms[arm].RecalculateBounds();
        }
    }

    /// <summary>水膜に包まれる間にタコを水の濁りへ隠す</summary>
    /// <param name="immersion">水面を越えた割合</param>
    /// <param name="elapsed">入水後の秒数</param>
    /// <example>水中で視線を上へ向ける前に触手を視界から消す</example>
    public void Submerge(float immersion, float elapsed)
    {
        _headMaterial.SetFloat("_Opacity", 1f - Mathf.Clamp01(immersion));
        if (_scareMaterial != null) _scareMaterial.SetFloat("_Opacity", 1f - Mathf.Clamp01(immersion));
        _armMaterial.SetFloat("_Opacity", 1f - Mathf.SmoothStep(0f, 1f, elapsed / 0.4f));
    }

    /// <summary>元アセットを残し、演出専用の描画物とメッシュを解放する</summary>
    /// <example>演出終了や中断のどちらでも呼ぶ</example>
    public void Dispose()
    {
        if (_root != null) UnityEngine.Object.Destroy(_root);
        foreach (var mesh in _arms) if (mesh != null) UnityEngine.Object.Destroy(mesh);
        if (_headMaterial != null) UnityEngine.Object.Destroy(_headMaterial);
        if (_scareMaterial != null) UnityEngine.Object.Destroy(_scareMaterial);
        if (_armMaterial != null) UnityEngine.Object.Destroy(_armMaterial);
        if (_surfaceMaterial != null) UnityEngine.Object.Destroy(_surfaceMaterial);
    }
}
