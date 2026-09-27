using _Project.Scripts.View;
using Cysharp.Threading.Tasks;
using MiniGame;
using UnityEngine;

public class MiniGameResultPresenter
{
    private readonly MiniGameResultView _view;

    public MiniGameResultPresenter(MiniGameResultView view)
    {
        _view = view;
    }

    public UniTask PlayResultAsync(MiniGameResult result)
    {
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
        await UniTask.Delay(500);
        HideResults();
    }

    private async UniTask PlayGoodAsync()
    {
        _view.GoodResult();
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
}
