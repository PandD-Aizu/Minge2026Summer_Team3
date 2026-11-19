using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace InventoryData
{
    /// <summary>装備中の設置物がフィールドの決定操作へ渡す情報</summary>
    public interface IHeldPlaceable
    {
        /// <summary>現在手に持つ設置物のPrefab　非装備時はnull</summary>
        GameObject PlacementPrefab { get; }

        /// <summary>生成と記録に成功したアイテムの装備を解除する</summary>
        /// <example>松明は手元の灯りだけを消す</example>
        void OnPlaced();
    }

    /// <summary>設置物のPrefabと姿勢をプレイ中保持し、元のステージに入り直した時に復元する</summary>
    public sealed class StagePlacements : IDisposable
    {
        private readonly Dictionary<string, List<(GameObject Prefab, Pose Pose)>> _byScene = new();

        /// <summary>ステージ読み込み後の復元を開始する</summary>
        /// <example>GameLifetimeScopeで一度だけ生成する</example>
        public StagePlacements() => SceneManager.sceneLoaded += Restore;

        /// <summary>足元の歩ける面に、底面を原点とする設置用Prefabを生成して記録する</summary>
        /// <param name="player">設置位置と所属ステージの基準</param>
        /// <param name="prefab">地面に直立させる独立したPrefabアセット</param>
        /// <returns>設置と記録が完了した場合はtrue</returns>
        /// <example>松明や標識を手放す直前に呼ぶ</example>
        public bool TryPlaceAtFeet(CharacterController player, GameObject prefab)
        {
            if (player == null || !player.enabled || prefab == null || prefab.scene.IsValid()) return false;
            var scene = player.gameObject.scene;
            if (!scene.isLoaded || string.IsNullOrEmpty(scene.path)) return false;

            // 足元の段差だけを対象にし、自身のColliderやTriggerを接地面から除く
            var bounds = player.bounds;
            float reach = player.stepOffset + player.skinWidth + 0.05f;
            var origin = new Vector3(bounds.center.x, bounds.min.y + reach, bounds.center.z);
            var hits = Physics.RaycastAll(origin, Vector3.down, reach * 2f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            float nearest = float.PositiveInfinity;
            Vector3 point = default;
            foreach (var hit in hits)
            {
                var surface = hit.collider.transform;
                if (surface.gameObject.scene != scene || surface.IsChildOf(player.transform)
                    || Vector3.Angle(hit.normal, Vector3.up) > player.slopeLimit || hit.distance >= nearest) continue;
                nearest = hit.distance;
                point = hit.point;
            }
            if (float.IsPositiveInfinity(nearest)) return false;

            var pose = new Pose(point, Quaternion.Euler(0f, player.transform.eulerAngles.y, 0f));
            Spawn(scene, prefab, pose);
            if (!_byScene.TryGetValue(scene.path, out var items))
                _byScene.Add(scene.path, items = new());
            items.Add((prefab, pose));
            return true;
        }

        /// <summary>読み込んだステージに属する設置物だけを復元する</summary>
        /// <param name="scene">新たに読み込まれたシーン</param>
        /// <param name="mode">Unityが通知するロード方式</param>
        /// <example>キャンプから釣り場へ戻った時に呼ばれる</example>
        private void Restore(Scene scene, LoadSceneMode mode)
        {
            if (!_byScene.TryGetValue(scene.path, out var items)) return;
            foreach (var item in items) Spawn(scene, item.Prefab, item.Pose);
        }

        /// <summary>指定シーンのルートに設置物を生成する</summary>
        /// <param name="scene">表示するステージ</param>
        /// <param name="prefab">設置用アセット</param>
        /// <param name="pose">地面上のワールド位置と回転</param>
        /// <example>初回設置と復元で共通して使用する</example>
        private static void Spawn(Scene scene, GameObject prefab, Pose pose)
        {
            UnityEngine.Object.Instantiate(prefab, pose.position, pose.rotation,
                new InstantiateParameters { scene = scene });
        }

        /// <summary>セッションの終了時にロード通知と設置記録を解放する</summary>
        /// <example>Rootの破棄時にVContainerが呼ぶ</example>
        public void Dispose()
        {
            SceneManager.sceneLoaded -= Restore;
            _byScene.Clear();
        }
    }
}
