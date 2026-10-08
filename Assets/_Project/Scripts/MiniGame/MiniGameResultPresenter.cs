using _Project.Scripts.Data.Fish;
using _Project.Scripts.View;
using Cysharp.Threading.Tasks;
using InventoryData;
using FMODServices;
using FMODSettings;
using MiniGame;
using UnityEngine;

public class MiniGameResultPresenter
{
    private readonly MiniGameResultView _view;
    private readonly Inventory _inventory;
    private readonly FMODSEService _se;
    private FishDefinition _currentFishDefinition;

    /// <summary>釣果の表示、所持品への追加、結果音の再生に必要な依存関係を受け取る</summary>
    /// <param name="view">釣り結果の表示先</param>
    /// <param name="inventory">釣った魚の追加先</param>
    /// <param name="se">結果の効果音を再生するサービス</param>
    /// <example>FishingSceneLifetimeScopeの登録から生成する</example>
    public MiniGameResultPresenter(MiniGameResultView view, Inventory inventory, FMODSEService se)
    {
        _view = view;
        _inventory = inventory;
        _se = se;
    }

    /// <summary>釣果をInventoryへ追加して結果UIを閉じ、追加できたかを返す</summary>
    /// <param name="result">ミニゲームの成績</param>
    /// <param name="fishDefinition">追加する魚</param>
    /// <returns>魚をInventoryへ追加した場合はtrue</returns>
    /// <example>MiniGameFlowPresenterが結果表示の終了後に進行を通知する</example>
    public async UniTask<bool> PlayResultAsync(MiniGameResult result, FishDefinition fishDefinition)
    {
        _currentFishDefinition = fishDefinition;
        switch (result)
        {
            case MiniGameResult.Great:
                return await PlayGreatAsync();
            case MiniGameResult.Good:
                return await PlayGoodAsync();
            case MiniGameResult.Miss:
                await PlayMissAsync();
                return false;
            default:
                return false;
        }
    }

    /// <summary>大成功の表示と効果音を再生し、魚を追加する</summary>
    /// <returns>魚を追加できた場合はtrue</returns>
    /// <example>Greatの判定時に呼ぶ</example>
    private async UniTask<bool> PlayGreatAsync()
    {
        _view.GreatResult();
        _se.PlayOneShot(FMODEventPath.SE_FISSHING_GREAT.Reference);
        var fishAdded = AddCurrentFish();
        await UniTask.Delay(500);
        HideResults();
        return fishAdded;
    }

    /// <summary>成功の表示と効果音を再生し、魚を追加する</summary>
    /// <returns>魚を追加できた場合はtrue</returns>
    /// <example>Goodの判定時に呼ぶ</example>
    private async UniTask<bool> PlayGoodAsync()
    {
        _view.GoodResult();
        _se.PlayOneShot(FMODEventPath.SE_FISSHING_OK.Reference);
        var fishAdded = AddCurrentFish();
        await UniTask.Delay(1000);
        HideResults();
        return fishAdded;
    }

    /// <summary>失敗の表示と効果音を再生する</summary>
    /// <returns>結果表示が終了するまでの待機</returns>
    /// <example>Missの判定時に呼ぶ</example>
    private async UniTask PlayMissAsync()
    {
        _view.MissResult();
        _se.PlayOneShot(FMODEventPath.SE_FISSHING_MISS.Reference);
        await UniTask.Delay(3000);
        HideResults();
    }

    private void Error()
    {
        Debug.Log("Error");
    }

    private void HideResults()
    {
        _view.HideResult();
    }

    private bool AddCurrentFish()
    {
        if (_currentFishDefinition == null)
        {
            Debug.LogError("釣った魚のFishDefinitionがないのでInventoryに追加できません");
            return false;
        }

        _inventory.Add(_currentFishDefinition.ItemId);
        return true;
    }
}
