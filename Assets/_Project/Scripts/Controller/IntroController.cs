using System.Threading;
using Cysharp.Threading.Tasks;
using Dialogue;
using SceneLoadServices;
using UnityEngine;
using VContainer.Unity;

namespace Controller
{
    /// <summary>イントロの会話を再生し、正常終了後にキャンプへ移動する</summary>
    public class IntroController : IAsyncStartable
    {
        private readonly DialogueService _dialogueService;
        private readonly DialogueData _introDialogue;
        private readonly SceneLoadService _sceneLoader;

        /// <summary>会話データとシーン進行の依存関係を受け取る</summary>
        /// <param name="dialogueService">会話単体のサービス</param>
        /// <param name="introDialogue">イントロの会話データ</param>
        /// <param name="sceneLoader">終了後のシーン読み込み先</param>
        /// <example>IntroLifetimeScopeのEntryPoint登録から生成する</example>
        public IntroController(DialogueService dialogueService, DialogueData introDialogue, SceneLoadService sceneLoader)
        {
            _dialogueService = dialogueService;
            _introDialogue = introDialogue;
            _sceneLoader = sceneLoader;
        }

        /// <summary>この会話の正常終了を待ち、キャンプを読み込む</summary>
        /// <param name="cancellation">Scope破棄時にキャンセルされるトークン</param>
        /// <returns>会話とシーン読み込みの完了まで待つUniTask</returns>
        /// <example>VContainerの開始処理から自動実行される</example>
        public async UniTask StartAsync(CancellationToken cancellation = default)
        {
            var result = await _dialogueService.PlayAsync(_introDialogue, cancellation);
            if (result == DialogueResult.Rejected)
            {
                Debug.LogError("イントロ会話を開始できないため、DialogueDataと会話の重複起動を確認してください");
                return;
            }

            if (result == DialogueResult.Completed && !cancellation.IsCancellationRequested)
                await _sceneLoader.LoadSceneAsync("CampStage");
        }
    }
}
