using _Project.Scripts.Data.Fish;
using _Project.Scripts.View;
using Cysharp.Threading.Tasks;
using InventoryData;
using MiniGame;
using UnityEngine;

public class MiniGameResultPresenter
{
    private readonly MiniGameResultView _view;
    private readonly Inventory _inventory;
    private FishDefinition _currentFishDefinition;

    public MiniGameResultPresenter(MiniGameResultView view, Inventory inventory)
    {
        _view = view;
        _inventory = inventory;
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

    private async UniTask<bool> PlayGreatAsync()
    {
        _view.GreatResult();
        var fishAdded = AddCurrentFish();
        await UniTask.Delay(500);
        HideResults();
        return fishAdded;
    }

    private async UniTask<bool> PlayGoodAsync()
    {
        _view.GoodResult();
        var fishAdded = AddCurrentFish();
        await UniTask.Delay(1000);
        HideResults();
        return fishAdded;
    }

    private async UniTask PlayMissAsync()
    {
        _view.MissResult();
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
