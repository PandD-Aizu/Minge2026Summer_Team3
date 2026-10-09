using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace NavigationArrowServices
{
    /// <summary>歩行面を4方向に探索し、道の中央寄りを優先する経路を計算する</summary>
    public class NavigationArrowService
    {
        private const float GridSpacing = 0.5f;
        private const int MaxSearchNodes = 4096;
        private const float PreferredEdgeClearance = 2f;
        private const float EdgePenaltyWeight = 2f;
        private readonly Transform _playerTransform;
        private readonly NavMeshPath _path = new();
        private readonly NavMeshQueryFilter _filter = new() { agentTypeID = 0, areaMask = 1 };
        private readonly Dictionary<Vector2Int, Node> _nodes = new();
        private readonly SortedSet<(float score, float remaining, int id)> _open = new();
        private readonly List<Node> _nodeList = new();
        private readonly List<Vector3> _points = new();
        private static readonly Vector2Int[] Directions =
        {
            Vector2Int.left, Vector2Int.right, Vector2Int.down, Vector2Int.up
        };

        private sealed class Node
        {
            public Vector2Int Cell;
            public Vector3 Position;
            public float Cost = float.PositiveInfinity;
            public float Remaining;
            public float TraversalMultiplier;
            public int Id;
            public Node Parent;
            public Vector3 EntryCorner;
            public bool Closed;
        }

        public Vector3 PlayerPosition => _playerTransform.position;
        public Vector3 TargetPosition { get; private set; }
        public Vector3 RadioPosition { get; }
        public Vector3? FishingStagePosition { get; }
        public bool GuideFirstFish { get; }
        public Vector3? FirstCatchReturnPosition { get; }
        public bool CanUpdate => _playerTransform != null;

        /// <summary>案内対象と地面上の目的地を保持する</summary>
        /// <param name="playerTransform">案内対象のプレイヤー</param>
        /// <param name="targetPosition">ラジオ付近の歩行可能な座標</param>
        /// <param name="fishingStagePosition">会話後に案内するFishingStageへのワープ地点、未設定なら案内しない</param>
        /// <param name="guideFirstFish">FishingStageで最初の魚の接近地点を案内する場合はtrue</param>
        /// <param name="firstCatchReturnPosition">初回釣果後の帰還先、釣り場では帰還用ワープ、キャンプでは無人集荷場</param>
        /// <example>LifetimeScopeから生成する</example>
        public NavigationArrowService(Transform playerTransform, Vector3 targetPosition, Vector3? fishingStagePosition = null,
            bool guideFirstFish = false, Vector3? firstCatchReturnPosition = null)
        {
            _playerTransform = playerTransform;
            TargetPosition = targetPosition;
            RadioPosition = targetPosition;
            FishingStagePosition = fishingStagePosition;
            GuideFirstFish = guideFirstFish;
            FirstCatchReturnPosition = firstCatchReturnPosition;
        }

        /// <summary>案内先の目的地を変更する</summary>
        /// <param name="targetPosition">新しい目的地のワールド座標</param>
        /// <example>SetTargetPosition(radioApproach.position)</example>
        public void SetTargetPosition(Vector3 targetPosition) => TargetPosition = targetPosition;

        /// <summary>NavMesh上を縦横に歩き、端や障害物から余裕を取った経路を返す</summary>
        /// <param name="corners">成功時の経路頂点、失敗時は空配列</param>
        /// <returns>目的地まで到達できる場合はtrue</returns>
        /// <example>Presenterから一定間隔で呼ぶ</example>
        public bool TryCalculatePath(out Vector3[] corners)
        {
            corners = System.Array.Empty<Vector3>();
            if (!CanUpdate) return false;

            // 未ベイクや別の島への案内は4方向探索の前に打ち切る
            if (!NavMesh.SamplePosition(PlayerPosition, out var start, 2f, _filter)
                || !NavMesh.SamplePosition(TargetPosition, out var goal, 1f, _filter)
                || !NavMesh.CalculatePath(start.position, goal.position, _filter, _path)
                || _path.status != NavMeshPathStatus.PathComplete) return false;

            _nodes.Clear();
            _open.Clear();
            _nodeList.Clear();
            _points.Clear();

            // 目的地を格子原点とし、プレイヤーを周囲4頂点へ直角に接続する
            var relative = (start.position - goal.position) / GridSpacing;
            var cellX = Mathf.FloorToInt(relative.x);
            var cellZ = Mathf.FloorToInt(relative.z);
            for (var x = cellX; x <= cellX + 1; x++)
            for (var z = cellZ; z <= cellZ + 1; z++)
            {
                var node = GetNode(new Vector2Int(x, z), goal.position);
                if (node == null || !TryConnect(start.position, node.Position, out var elbow)) continue;
                node.EntryCorner = elbow;
                node.Cost = HorizontalDistance(start.position, node.Position) * node.TraversalMultiplier;
                _open.Add((node.Cost + node.Remaining, node.Remaining, node.Id));
            }

            // Manhattan距離を使うA*で、斜め移動を候補へ含めない
            var visited = 0;
            while (_open.Count > 0 && visited++ < MaxSearchNodes)
            {
                var key = _open.Min;
                _open.Remove(key);
                var current = _nodeList[key.id];
                current.Closed = true;
                if (current.Cell == Vector2Int.zero)
                {
                    BuildPath(current, start.position);
                    corners = _points.ToArray();
                    return corners.Length >= 2;
                }

                foreach (var direction in Directions)
                {
                    var next = GetNode(current.Cell + direction, goal.position);
                    if (next == null || next.Closed || !CanWalk(current.Position, next.Position)) continue;
                    // 端に沿って進み続ける経路より、少し回って中央へ寄る経路を優先する
                    var cost = current.Cost + GridSpacing * (current.TraversalMultiplier + next.TraversalMultiplier) * 0.5f;
                    if (cost >= next.Cost) continue;

                    _open.Remove((next.Cost + next.Remaining, next.Remaining, next.Id));
                    next.Parent = current;
                    next.Cost = cost;
                    _open.Add((cost + next.Remaining, next.Remaining, next.Id));
                }
            }
            return false;
        }

        /// <summary>格子頂点を歩行面へ投影して探索用ノードを取得する</summary>
        /// <param name="cell">目的地を原点とした格子座標</param>
        /// <param name="origin">目的地のワールド座標</param>
        /// <returns>歩ける頂点、障害物内ならnull</returns>
        /// <example>A*の隣接4頂点の取得に使う</example>
        private Node GetNode(Vector2Int cell, Vector3 origin)
        {
            if (_nodes.TryGetValue(cell, out var cached)) return cached;
            var position = origin + new Vector3(cell.x * GridSpacing, 0f, cell.y * GridSpacing);
            if (!TryProject(position, out position))
            {
                _nodes[cell] = null;
                return null;
            }

            var node = new Node
            {
                Cell = cell, Position = position, Id = _nodeList.Count,
                Remaining = (Mathf.Abs(cell.x) + Mathf.Abs(cell.y)) * GridSpacing,
                TraversalMultiplier = CalculateTraversalMultiplier(position)
            };
            _nodes[cell] = node;
            _nodeList.Add(node);
            return node;
        }

        /// <summary>歩行面の端に近いほど移動コストを増やす</summary>
        /// <param name="position">歩行面上の格子頂点</param>
        /// <returns>中央寄りでは1、端では最大3になる移動倍率</returns>
        /// <example>GetNodeで一度だけ計算し、隣接経路の評価で再利用する</example>
        private float CalculateTraversalMultiplier(Vector3 position)
        {
            // 通行禁止にはしないので、狭い道や岸際の釣り地点にも到達できる
            if (!NavMesh.FindClosestEdge(position, out var edge, _filter)) return 1f;
            var proximity = Mathf.Clamp01(1f - edge.distance / PreferredEdgeClearance);
            return 1f + EdgePenaltyWeight * proximity * proximity;
        }

        /// <summary>XZ座標を動かさずに地面の高さを取得する</summary>
        /// <param name="position">調べるワールド座標</param>
        /// <param name="projected">地面へ投影した座標</param>
        /// <returns>直下付近に歩行面がある場合はtrue</returns>
        /// <example>格子点が障害物の外へずれて斜めになることを防ぐ</example>
        private bool TryProject(Vector3 position, out Vector3 projected)
        {
            projected = position;
            if (!NavMesh.SamplePosition(position, out var hit, 1f, _filter)
                || Mathf.Abs(hit.position.x - position.x) > 0.01f
                || Mathf.Abs(hit.position.z - position.z) > 0.01f) return false;
            projected.y = hit.position.y;
            return true;
        }

        /// <summary>2地点間を曲がらずに歩けるか確認する</summary>
        /// <param name="from">始点</param>
        /// <param name="to">終点</param>
        /// <returns>NavMesh境界や障害物で遮られていなければtrue</returns>
        /// <example>格子の隣接点間を検証する</example>
        private bool CanWalk(Vector3 from, Vector3 to)
        {
            if (!NavMesh.Raycast(from, to, out var hit, _filter)) return true;

            // 到着点そのものがNavMeshの縁なら到達済みとする
            return (hit.position - to).sqrMagnitude < 0.0001f;
        }

        /// <summary>格子外のプレイヤー位置をL字で格子に接続する</summary>
        /// <param name="from">プレイヤーの足元</param>
        /// <param name="to">格子頂点</param>
        /// <param name="elbow">歩行可能な直角の曲がり角</param>
        /// <returns>縦横いずれかの順番で接続できればtrue</returns>
        /// <example>探索開始時に近隣4頂点へ適用する</example>
        private bool TryConnect(Vector3 from, Vector3 to, out Vector3 elbow)
        {
            if (TryProject(new Vector3(to.x, from.y, from.z), out elbow)
                && CanWalk(from, elbow) && CanWalk(elbow, to)) return true;
            return TryProject(new Vector3(from.x, from.y, to.z), out elbow)
                && CanWalk(from, elbow) && CanWalk(elbow, to);
        }

        /// <summary>到達ノードから経路を逆順にたどり、足元から目的地の順へ戻す</summary>
        /// <param name="goal">到達した目的地ノード</param>
        /// <param name="start">プレイヤーの足元</param>
        /// <example>A*で目的地を取り出したときに呼ぶ</example>
        private void BuildPath(Node goal, Vector3 start)
        {
            var node = goal;
            while (node != null)
            {
                _points.Add(node.Position);
                if (node.Parent == null) _points.Add(node.EntryCorner);
                node = node.Parent;
            }
            _points.Add(start);
            _points.Reverse();
        }

        /// <summary>斜め移動なしで進む場合の水平距離を返す</summary>
        /// <param name="from">始点</param>
        /// <param name="to">終点</param>
        /// <returns>X方向とZ方向の距離の合計</returns>
        /// <example>格子までの初期移動コストを計算する</example>
        private static float HorizontalDistance(Vector3 from, Vector3 to)
            => Mathf.Abs(to.x - from.x) + Mathf.Abs(to.z - from.z);
    }
}
