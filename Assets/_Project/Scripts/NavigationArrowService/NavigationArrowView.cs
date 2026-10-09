using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace View
{
    /// <summary>経路上の点々から上昇する光の粒を描く</summary>
    public class NavigationArrowView : MonoBehaviour
    {
        [SerializeField, HideInInspector] private Transform _arrowTransform;
        [SerializeField] private Material _pathMaterial;
        [SerializeField, Min(0.1f)] private float _pointSpacing = 0.6f;
        [SerializeField, Min(0.01f)] private float _surfaceOffset = 0.08f;
        [SerializeField, Min(0.03f)] private float _emissionInterval = 0.12f;
        [SerializeField, Range(0f, 75f), Tooltip("上方向を中心に光が広がる角度")]
        private float _spreadAngle = 35f;
        private readonly List<Vector3> _emitters = new();
        private ParticleSystem _particles;
        private float _nextEmissionTime;

        public bool CanUpdate => isActiveAndEnabled && _pathMaterial != null;

        /// <summary>旧シーンに残る矢印を無効化する</summary>
        /// <example>シーン読み込み時にUnityが呼ぶ</example>
        private void Awake()
        {
            if (_arrowTransform != null) _arrowTransform.gameObject.SetActive(false);
        }

        /// <summary>経路を等間隔の発生点へ分割する</summary>
        /// <param name="corners">縦横のみで結んだ地面上の経路頂点</param>
        /// <example>PresenterからShowPath(corners)を呼ぶ</example>
        public void ShowPath(Vector3[] corners)
        {
            if (!CanUpdate || corners == null || corners.Length < 2)
            {
                Hide();
                return;
            }
            if (_particles == null) CreateParticles();

            // 長い経路でも発生点数を抑え、曲がり角を飛び越す斜め補間は行わない
            _emitters.Clear();
            var length = 0f;
            for (var i = 1; i < corners.Length; i++) length += Vector3.Distance(corners[i - 1], corners[i]);
            var spacing = Mathf.Max(_pointSpacing, length / 127f);
            var remaining = 0f;
            for (var i = 1; i < corners.Length; i++)
            {
                var from = corners[i - 1];
                var to = corners[i];
                var segmentLength = Vector3.Distance(from, to);
                if (segmentLength < 0.0001f) continue;
                while (remaining <= segmentLength)
                {
                    _emitters.Add(Vector3.Lerp(from, to, remaining / segmentLength) + Vector3.up * _surfaceOffset);
                    remaining += spacing;
                }
                remaining -= segmentLength;
            }
            if (!_particles.isPlaying) _particles.Play();
        }

        /// <summary>一定間隔で全発生点から上向きに光を放出する</summary>
        /// <example>UnityがLateUpdateで呼び、経路計算とは独立して発生を続ける</example>
        private void LateUpdate()
        {
            if (_particles == null || _emitters.Count == 0 || Time.time < _nextEmissionTime) return;
            _nextEmissionTime = Time.time + _emissionInterval;
            EmitParticles();
        }

        /// <summary>地面の各点から上方向の円錐内へランダムに拡散する粒を生成する</summary>
        /// <example>一定時間ごとにLateUpdateから呼ぶ</example>
        private void EmitParticles()
        {
            foreach (var position in _emitters)
            {
                // 上昇速度を保ち、水平速度を円内から選んで全方向へ広げる
                var upwardSpeed = Random.Range(0.35f, 0.8f);
                var spread = Random.insideUnitCircle * upwardSpeed * Mathf.Tan(_spreadAngle * Mathf.Deg2Rad);
                var emission = new ParticleSystem.EmitParams
                {
                    position = position,
                    velocity = new Vector3(spread.x, upwardSpeed, spread.y),
                    startLifetime = Random.Range(0.7f, 1.25f),
                    startSize = Random.Range(0.08f, 0.17f),
                    startColor = new Color(1f, Random.Range(0.7f, 0.95f), 0.35f, 1f)
                };
                _particles.Emit(emission, 1);
            }
        }

        /// <summary>共有マテリアルを使う単一のパーティクルシステムを生成する</summary>
        /// <example>ShowPathの初回だけ呼ぶ</example>
        private void CreateParticles()
        {
            var effect = new GameObject("Navigation Twinkle Particles");
            effect.transform.SetParent(transform, false);
            _particles = effect.AddComponent<ParticleSystem>();
            _particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            // 座標と速度をワールド空間で扱い、親の回転によらず上方向へ拡散させる
            var main = _particles.main;
            main.playOnAwake = false;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 2048;
            main.startSpeed = 0f;
            main.gravityModifier = 0f;
            var emission = _particles.emission;
            emission.enabled = false;
            var shape = _particles.shape;
            shape.enabled = false;

            // 粒は現れてから小さくなりながら消える
            var color = _particles.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0.3f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0f, 1f) });
            color.color = gradient;
            var size = _particles.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.25f));

            var renderer = effect.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = _pathMaterial;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        /// <summary>発生と残っている粒を同時に消す</summary>
        /// <example>会話完了または経路探索失敗時に呼ぶ</example>
        public void Hide()
        {
            _emitters.Clear();
            _nextEmissionTime = 0f;
            if (_particles != null && !_particles.isStopped)
                _particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        /// <summary>コンポーネントを無効にしたときに案内も消す</summary>
        /// <example>シーン切り替え時にUnityが呼ぶ</example>
        private void OnDisable() => Hide();

        /// <summary>コンポーネント単体の削除時も生成した描画オブジェクトを破棄する</summary>
        /// <example>UnityがViewの破棄時に呼ぶ</example>
        private void OnDestroy()
        {
            if (_particles != null) Destroy(_particles.gameObject);
        }
    }
}
