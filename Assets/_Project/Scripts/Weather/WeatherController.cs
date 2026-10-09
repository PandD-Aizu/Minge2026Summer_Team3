using System.Collections.Generic;
using UnityEngine;

namespace Minge2026.Weather
{
    /// <summary>ステージの雨、濡れ、雨上がり、落雷とFMOD音をまとめて制御する</summary>
    [DisallowMultipleComponent]
    public sealed class WeatherController : MonoBehaviour
    {
        [SerializeField] private WeatherSettings _settings;
        [SerializeField] private Transform _focus;
        [SerializeField] private Bounds _groundBounds;
        [SerializeField] private Transform _groundRoot;
        [SerializeField] private Material[] _groundMaterials;
        [SerializeField] private Material _rainMaterial;
        [SerializeField] private Material _lightningMaterial;
        [SerializeField] private Material _puddleMaterial;
        [SerializeField] private WeatherAtmosphere _atmosphere;
        private WeatherCycle _cycle;
        private WeatherPrecipitation _precipitation;
        private WeatherGroundSurface _ground;
        private WeatherRainAudio _audio;
        private System.Random _random;
        private float _lightningRemaining;

        public WeatherCycle Cycle => _cycle;
        public WeatherSettings Settings => _settings;

        /// <summary>保存済みの参照を使い、シーン専用の天候表現を初期化する</summary>
        /// <example>シーンを再生すると晴天から抽選が始まる</example>
        private void Awake()
        {
            if (_settings == null)
            {
                Debug.LogError("Weather Settingsが未設定", this);
                enabled = false;
                return;
            }

            _random = new System.Random();
            _cycle = new WeatherCycle(_settings, _random);
            _lightningRemaining = Mathf.Max(1f, _settings.LightningCheckInterval);
            _precipitation = GetComponent<WeatherPrecipitation>() ?? gameObject.AddComponent<WeatherPrecipitation>();
            _precipitation.Configure(_rainMaterial, _lightningMaterial, _groundBounds);
            _ground = GetComponent<WeatherGroundSurface>() ?? gameObject.AddComponent<WeatherGroundSurface>();
            _ground.Configure(CollectGround(), _puddleMaterial, _groundBounds);
            _audio = GetComponent<WeatherRainAudio>() ?? gameObject.AddComponent<WeatherRainAudio>();
            _audio.Initialize(_settings.RainEventPath);
        }

        /// <summary>開始時に一度だけ対象素材の地面を収集し、数千タイルの個別参照の保存を避ける</summary>
        /// <returns>設定された地表高に接する地面Renderer</returns>
        /// <example>CollectGround()の結果を濡れ処理へ渡す</example>
        private Renderer[] CollectGround()
        {
            if (_groundRoot == null || _groundMaterials == null) return System.Array.Empty<Renderer>();
            var materials = new HashSet<Material>(_groundMaterials);
            var result = new List<Renderer>();
            foreach (MeshRenderer renderer in _groundRoot.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (materials.Contains(renderer.sharedMaterial) && renderer.GetComponent<Collider>() != null &&
                    Mathf.Abs(renderer.bounds.max.y - _groundBounds.max.y) < 0.2f && renderer.bounds.size.y > 0.1f)
                    result.Add(renderer);
            }
            return result.ToArray();
        }

        /// <summary>ポーズに従って天候を進め、雨天中だけ低確率の落雷を抽選する</summary>
        /// <example>8秒ごとに8パーセントの確率で落雷する</example>
        private void Update()
        {
            if (_cycle == null) return;
            float dt = Time.deltaTime;
            _cycle.Tick(dt);
            _precipitation.SetIntensity(_cycle.RainIntensity);
            _precipitation.Tick(dt, _focus != null ? _focus : transform);
            _ground.SetWetness(_cycle.Wetness, _cycle.Puddles, _cycle.RainIntensity);
            _audio.SetRainIntensity(_cycle.RainIntensity);
            if (_atmosphere != null) _atmosphere.SetIntensity(_cycle.RainIntensity);

            if (_cycle.Phase != WeatherPhase.Raining)
            {
                _lightningRemaining = Mathf.Max(1f, _settings.LightningCheckInterval);
                return;
            }

            _lightningRemaining -= dt;
            if (_lightningRemaining > 0f) return;
            _lightningRemaining = Mathf.Max(1f, _settings.LightningCheckInterval);
            if (_random.NextDouble() < Mathf.Clamp01(_settings.LightningProbability)) TriggerLightning();
        }

        /// <summary>再生中の確認用に雨を開始する</summary>
        /// <example>Inspectorの雨を降らせるボタンから呼ぶ</example>
        public void StartRain() => _cycle?.StartRain();

        /// <summary>再生中の確認用に雨を止める</summary>
        /// <example>Inspectorの雨を止めるボタンで水たまりの生成を確認する</example>
        public void StopRain() => _cycle?.StopRain();

        /// <summary>プレイヤーから離れた背景側へ演出のみの雷を落とす</summary>
        /// <example>Inspectorの落雷ボタンで閃光を確認する</example>
        public void TriggerLightning()
        {
            if (_precipitation == null || _random == null) return;
            Vector3 focus = _focus != null ? _focus.position : _groundBounds.center;
            float offset = (_random.Next(2) == 0 ? -1f : 1f) * Mathf.Lerp(8f, 15f, (float)_random.NextDouble());
            Vector3 point = new Vector3(Mathf.Clamp(focus.x + offset, _groundBounds.min.x, _groundBounds.max.x),
                _groundBounds.max.y, Mathf.Clamp(focus.z + 8f, _groundBounds.min.z, _groundBounds.max.z));
            _precipitation.Strike(point);
            _audio.TriggerThunder();
        }

        /// <summary>コンポーネント単独で停止した場合も残った雨音と照明を停止する</summary>
        /// <example>Inspectorのチェックを外すと天候の描画と音も停止する</example>
        private void OnDisable()
        {
            if (_precipitation != null) _precipitation.enabled = false;
            if (_ground != null) _ground.enabled = false;
            if (_audio != null) _audio.enabled = false;
            if (_atmosphere != null) _atmosphere.enabled = false;
        }

        /// <summary>再有効化時は保持している天候から再開する</summary>
        /// <example>一時停止した天候コンポーネントのチェックを戻す</example>
        private void OnEnable()
        {
            if (_precipitation != null) _precipitation.enabled = true;
            if (_ground != null) _ground.enabled = true;
            if (_audio != null) _audio.enabled = true;
            if (_atmosphere != null) _atmosphere.enabled = true;
        }
    }
}
