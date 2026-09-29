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

    public UniTask PlayResultAsync(MiniGameResult result, FishDefinition fishDefinition)
    {
        _currentFishDefinition = fishDefinition;
        return result switch
        {
            MiniGameResult.Great => PlayGreatAsync(),
            MiniGameResult.Good => PlayGoodAsync(),
            MiniGameResult.Miss => PlayMissAsync(),
            _ => UniTask.CompletedTask
        };
    }

    private async UniTask PlayGreatAsync()
    {
        _view.GreatResult();
        AddCurrentFish();
        await UniTask.Delay(500);
        HideResults();
    }

    private async UniTask PlayGoodAsync()
    {
        _view.GoodResult();
        AddCurrentFish();
        await UniTask.Delay(1000);
        HideResults();
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

    private void AddCurrentFish()
    {
        if (_currentFishDefinition == null)
        {
            Debug.LogError("釣った魚のFishDefinitionがないのでInventoryに追加できません");
            return;
        }

        _inventory.Add(_currentFishDefinition.ItemId);
    }
}
