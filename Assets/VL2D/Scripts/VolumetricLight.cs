using VL2D.Common;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace VL2D
{
    /// <summary>
    /// 自作の2Dボリュームライト表現を行う
    /// </summary>
    [RequireComponent(typeof(MeshRenderer))] // メッシュの描画を行うスクリプト
    [RequireComponent(typeof(MeshFilter))] // このオブジェクトのメッシュを取り扱うためのスクリプト
    [ExecuteInEditMode]
    public class VolumetricLight : MonoBehaviour
    {
        /// <summary>
        /// メッシュ角度
        /// </summary>
        [SerializeField]
        [Range(0, 360)]
        int degree = 360;

        /// <summary>
        /// メッシュ半径
        /// </summary>
        [SerializeField]
        [Min(0f)]
        float radius = 10f;

        /// <summary>
        /// 基本の扇形生成時の刻み
        /// 数が少ない = ハイポリゴンで綺麗な扇形が生成できるが高負荷
        /// </summary>
        [Range(1, 60)]
        [SerializeField]
        int splitDegree = 20;

        /// <summary>
        /// 頂点カラー
        /// </summary>
        [SerializeField]
        Color color = Color.white;

        /// <summary>
        /// ソートレイヤーの名前 Start時のみ参照
        /// MEMO: int指定にした方がいいかも？
        /// </summary>
        [SerializeField]
        string sortingLayerName = "Back";

        /// <summary>
        /// 2D上の表示優先度 Start時のみ参照
        /// </summary>
        [SerializeField]
        int sortingOrder = 1;

        /// <summary>
        /// 自分自身のコライダー(判定時にdisable化する)
        /// </summary>
        Collider2D selfCollider;

        /// <summary>
        /// メッシュを自動的に更新させるか
        /// </summary>
        [SerializeField]
        bool autoUpdateMesh = true;

        /// <summary>
        /// 2D上の正面
        /// </summary>
        Vector3 Forward { get { return transform.right; } }

        /// <summary>
        /// メッシュフィルタ
        /// </summary>
        MeshFilter meshFilter;

        /// <summary>
        /// 初期化処理
        /// </summary>
        private void Start()
        {
            var renderer = GetComponent<MeshRenderer>();
            if (renderer == null)
                return;

            renderer.sortingLayerName = sortingLayerName;
            renderer.sortingOrder = sortingOrder;

            meshFilter = GetComponent<MeshFilter>();

            var collider = GetComponent<Collider2D>();
            SetSelfCollider(collider);
        }

        /// <summary>
        /// LataUpdate
        /// </summary>
        private void LateUpdate()
        {
            if (!autoUpdateMesh)
                return;

            UpdateMesh();
        }

        /// <summary>
        /// 自身のコライダーを設定
        /// MEMO: 現状は単一コライダーのみ対応
        /// </summary>
        /// <param name="collider">コライダー2D</param>
        public void SetSelfCollider(Collider2D collider)
        {
            selfCollider = collider;
        }

        /// <summary>
        /// メッシュの形を更新
        /// </summary>
        public void UpdateMesh()
        {
            UpdateMesh(degree, radius);
        }

        /// <summary>
        /// メッシュの形を更新
        /// </summary>
        /// <param name="degree">角度</param>
        /// <param name="radius">半径</param>
        public void UpdateMesh(int degree, float radius)
        {
            ///// 【リスト6.1: ベースとなるメッシュの頂点座標を計算】に当たる部分 /////
            // 基本形である扇形の外側部分の頂点を計算し、レイキャスト対象として追加
            var zeroPosition = new Vector3(transform.position.x, transform.position.y, 0f);
            var startDirection = Quaternion.Euler(0f, 0f, degree / 2f) * Forward;
            var currentDegree = 0;
            var baseVerticies = new List<Vector3>();
            while (true)
            {
                // 刻む角度分ずつ向きをずらして、視界半径分先の頂点位置を計算
                var direction = Quaternion.Euler(0f, 0f, -currentDegree) * startDirection;
                baseVerticies.Add(direction * radius + zeroPosition); // zeroPosition 座標を加えてワールド座標として作成している点に注意。Raycast用の座標なので、これ自体は最終的なメッシュ頂点の座標にはなりません

                // 最終角度の計算が済んだら終了
                if (currentDegree >= degree)
                    break;

                currentDegree = Mathf.Min(currentDegree + splitDegree, degree);
            }

            ///// 【リスト6.5: ボックスコライダーのワールド頂点座標を取得】に当たる部分
            // 視界範囲の障害物の頂点をレイキャスト対象として追加
            var colliders = FindObjectsOfType<Collider2D>();
            var colliderVerticies = new List<Vector3>();
            foreach (var col in colliders)
            {
                // 自身はスキップ
                if (col.gameObject == gameObject)
                    continue;

                // ボックスコライダーの場合
                if (col is BoxCollider2D boxCollider)
                {
                    colliderVerticies.AddRange(GetWorldVerticiesFromBoxCollider(boxCollider, radius, degree));
                }

                // ポリゴンコライダーの場合
                // MEMO: 一応ポリゴンコライダー版を作ってはありますが、Unity2021.3.6f1 での動作は以前のバージョンよりもちょっと不安定かもしれません
                if (col is PolygonCollider2D polygonCollider)
                {
                    colliderVerticies.AddRange(GetWorldVerticiesFromPolygonCollider(polygonCollider, radius, degree));
                }
            }

            ///// 【長い壁に衝突した場合】の項の部分 /////
            // 円周部が何かにぶつかっている場合はそこの頂点をレイキャスト対象として追加
            var circleHitVerticies = new List<Vector3>();
            var layerMask = LayerMask.GetMask("Map", "Character");
            for (int i = 0; i < baseVerticies.Count - 1; i++)
            {
                var p1 = baseVerticies[i];
                var p2 = baseVerticies[i + 1];
                var ray = (p2 - p1).normalized;
                var length = (p2 - p1).magnitude;

                // レイキャストを行う
                var result = Physics2D.Raycast(p1, ray, length, layerMask);
                if (result.collider != null)
                {
                    circleHitVerticies.Add(result.point);
                }

                // 逆向き
                var invResult = Physics2D.Raycast(p2, -ray, length, layerMask);
                if (invResult.collider != null)
                {
                    circleHitVerticies.Add(invResult.point);
                }
            }

            // 必要あれば自分自身のコライダーを一時的に非アクティブ化する
            var preActiveCollider = false;
            if (selfCollider != null)
            {
                preActiveCollider = selfCollider.enabled;
                selfCollider.enabled = false;
            }

            ///// 【リスト6.4: ベースの頂点へ向けてレイキャスト】に当たる部分 (+ 外周部レイキャスト頂点)
            // 基本のメッシュ頂点 + 円周部レイキャストで追加したメッシュ頂点 へ向けて原点からレイキャスト
            var allVerticies = new List<Vector3>();
            var inverseAngle = Mathf.Atan2(Forward.y, Forward.x) * Mathf.Rad2Deg; // メッシュを右向きに戻すための角度
            foreach (var target in baseVerticies.Concat(circleHitVerticies).Distinct())
            {
                var ray = (target - zeroPosition).normalized;
                var result = Physics2D.Raycast(zeroPosition, ray, radius, layerMask);
                allVerticies.Add(Quaternion.Euler(0f, 0f, -inverseAngle) * (ray * (result.collider == null ? radius : result.distance)));
            }

            ///// 【リスト6.6: コライダーの頂点座標へ2度レイキャスト処理】に当たる部分
            // 周辺のコライダー頂点へ向けて原点から2回ずつ僅かに角度をずらしながらレイキャスト
            const float AdditionalCheckDifferenceAngle = 0.1f;
            foreach (var target in colliderVerticies)
            {
                // 基本のレイ
                var ray = (target - zeroPosition).normalized;

                // 左側レイキャスト
                // MEMO: 生成したレイがライト角度の範囲外である場合はメッシュが狂ってしまう場合がある為除外
                var lRay = Quaternion.Euler(0f, 0f, AdditionalCheckDifferenceAngle) * ray;
                if (CommonUtility.IsInDotDegree(Forward, lRay, degree * 0.5f))
                {
                    var lResult = Physics2D.Raycast(zeroPosition, lRay, radius, layerMask);
                    allVerticies.Add(Quaternion.Euler(0f, 0f, -inverseAngle) * (lRay * (lResult.collider == null ? radius : lResult.distance)));
                }

                // 右側レイキャスト
                var rRay = Quaternion.Euler(0f, 0f, -AdditionalCheckDifferenceAngle) * ray;
                if (CommonUtility.IsInDotDegree(Forward, rRay, degree * 0.5f))
                {
                    var rResult = Physics2D.Raycast(zeroPosition, rRay, radius, layerMask);
                    allVerticies.Add(Quaternion.Euler(0f, 0f, -inverseAngle) * (rRay * (rResult.collider == null ? radius : rResult.distance)));
                }
            }

            // Z座標を潰しておく(各頂点生成時に事前に行っても構いませんが、面倒くさいのでここでまとめて行います)
            for (int i = 0; i < allVerticies.Count; i++)
            {
                allVerticies[i] = new Vector3(allVerticies[i].x, allVerticies[i].y, 0f);
            }

            // 一時的に非アクティブ化していた場合自身のコライダーを入れなおす
            if (preActiveCollider)
            {
                selfCollider.enabled = true;
            }

            ///// 【リスト6.7: 生成した頂点座標を並べなおし】に当たる部分
            // 基準角度からの成す角によって並べ替え ついでに原点追加
            var orderBaseDirection = Quaternion.Euler(0f, 0f, degree / 2f) * Vector3.right;
            allVerticies = allVerticies.OrderBy(v => Mathf.Repeat(Vector3.SignedAngle(orderBaseDirection, v.normalized, -Vector3.forward), 360)).ToList();
            allVerticies.Add(Vector3.zero);

            ///// 【リスト6.2: UVを計算】に当たる部分
            // UVを計算
            var allUvs = new List<Vector2>();
            foreach (var vertex in allVerticies)
            {
                allUvs.Add((vertex / radius) * 0.5f + new Vector3(0.5f, 0.5f));
            }

            ///// 【リスト6.3: インデックスを計算】に当たる部分
            // インデックスを計算
            var indexes = new List<int>();
            for (int i = 0; i < allVerticies.Count - 2; i++)
            {
                // 原点 -> 頂点N -> 頂点N+1
                indexes.Add(allVerticies.Count - 1);
                indexes.Add(i);
                indexes.Add(i + 1);
            }

            // 360度の場合は最後の繋ぎ部分のインデックスを手動で追加生成しておく
            if (degree == 360)
            {
                indexes.Add(allVerticies.Count - 1);
                indexes.Add(allVerticies.Count - 2);
                indexes.Add(0);
            }

            // メッシュを設定
            var mesh = new Mesh();
            mesh.vertices = allVerticies.ToArray();
            mesh.uv = allUvs.ToArray();
            mesh.triangles = indexes.ToArray();
            mesh.colors = Enumerable.Repeat(color, mesh.vertices.Length).ToArray();
#if DEBUG
            // デバッグ中は名前を分かりやすくしておく
            mesh.name = gameObject.name + "_VolumetricLight2DMesh";
#endif

            mesh.RecalculateNormals();

            // リークするので前回分を消す
            if (meshFilter.sharedMesh != null)
            {
#if DEBUG
                if (EditorApplication.isPlaying)
                    Destroy(meshFilter.sharedMesh);
                else
                    DestroyImmediate(meshFilter.sharedMesh);
#else
                Destroy(meshFilter.sharedMesh);
#endif
            }

            meshFilter.sharedMesh = mesh;
        }

        /// <summary>
        /// ボックスコライダーから頂点リストを取得
        /// </summary>
        /// <param name="boxCollider"></param>
        /// <param name="radius"></param>
        /// <param name="degree"></param>
        /// <returns></returns>
        List<Vector3> GetWorldVerticiesFromBoxCollider(BoxCollider2D boxCollider, float radius, float degree)
        {
            var verticies = new List<Vector3>(4);

            verticies.Add(new Vector3(-boxCollider.size.x, boxCollider.size.y, 0f) * 0.5f);
            verticies.Add(new Vector3(boxCollider.size.x, boxCollider.size.y, 0f) * 0.5f);
            verticies.Add(new Vector3(-boxCollider.size.x, -boxCollider.size.y, 0f) * 0.5f);
            verticies.Add(new Vector3(boxCollider.size.x, -boxCollider.size.y, 0f) * 0.5f);

            var zeroPosition = new Vector3(transform.position.x, transform.position.y, 0f);

            for (int i = 0; i < verticies.Count; i++)
            {
                verticies[i] += new Vector3(boxCollider.offset.x, boxCollider.offset.y, 0f);
                verticies[i] = boxCollider.transform.TransformPoint(verticies[i]);
            }

            verticies = verticies.Where(v => ((v - zeroPosition).magnitude <= radius) && CommonUtility.IsInDotDegree(Forward, (v - zeroPosition).normalized, degree * 0.5f)).ToList();

            return verticies;
        }

        /// <summary>
        /// ポリゴンコライダーから頂点リストを取得
        /// </summary>
        /// <param name="polygonCollider"></param>
        /// <param name="radius"></param>
        /// <param name="degree"></param>
        /// <returns></returns>
        List<Vector3> GetWorldVerticiesFromPolygonCollider(PolygonCollider2D polygonCollider, float radius, float degree)
        {
            var verticies = new List<Vector3>();
            var zeroPosition = new Vector3(transform.position.x, transform.position.y, 0f);

            for (int i = 0; i < polygonCollider.pathCount; i++)
            {
                // パス毎に調査
                foreach (var vertex2D in polygonCollider.GetPath(i))
                {
                    // スケール分サイズを変更しておく
                    var vertex = new Vector3(vertex2D.x * polygonCollider.transform.localScale.x, vertex2D.y * polygonCollider.transform.localScale.y);

                    // 範囲外なら探索不要
                    var magnitude = (vertex - zeroPosition).magnitude;
                    if (magnitude > radius)
                        continue;

                    //// 頂点までへの角度が範囲外なら探索不要
                    var vertexDirection = (vertex - zeroPosition).normalized;
                    if (!CommonUtility.IsInDotDegree(Forward, vertexDirection, degree * 0.5f))
                        continue;

                    //// 前回との差分があまりに小さければカット
                    var ver = vertexDirection * magnitude + zeroPosition;
                    if (verticies.Any() && (verticies.Last() - ver).magnitude < 0.1f)
                        continue;

                    verticies.Add(vertex);
                }
            }

            return verticies;
        }

        /// <summary>
        /// 非実行時のプレビュー用
        /// </summary>
        private void OnGUI()
        {
            if (EditorApplication.isPlaying)
            {
                return;
            }

            Start();
            UpdateMesh();
        }
    }
}
