using _Project.Scripts.Core;
using R3;
using TMPro;
using UnityEngine;
using VContainer;

public class GameTimeView : MonoBehaviour
{
    private GameProgress _gameProgress;
    [SerializeField] private RectTransform _transformClockHand;
    [SerializeField] private TextMeshProUGUI _dayText;
    [SerializeField] private TextMeshProUGUI _weekText;


    /// <summary>進行データを受け取り、現在時刻と時刻変更を針へ反映する 戻り値なし</summary>
    /// <param name="gameProgress">ゲーム内の経過秒数を保持する進行データ</param>
    /// <example>CampStageLifetimeScopeへの登録で自動的に購読を開始する</example>
    [Inject]
    public void Construct(GameProgress gameProgress)
    {
        _gameProgress = gameProgress;

        _gameProgress.GameSeconds
            .Subscribe(ChangeClockHand)
            .AddTo(this);
        _gameProgress.CurrentDay
            .Subscribe(UpdateDayText)
            .AddTo(this);
    }

   // 時計の秒針を現実世界で12分で一周、ゲーム内時間で24時間で一周動かす
    private void ChangeClockHand(float second)
    {
        // 時刻を0時以上24時未満へ収める
        float gameTimeHour = Mathf.Repeat(second / 3600f, 24f);

        // 太陽があるほうを12時、月がある方を0時で0度として、1時間につき15度進める
        float angle = 180f - gameTimeHour * 15f;

        _transformClockHand.localRotation = Quaternion.Euler(0f, 0f, angle);
    }

    private void UpdateDayText(int currentDay)
    {
        _dayText.text = "Day." + currentDay.ToString();
    }

    private void UpdateWeekText(int currentWeek)
    {
        _weekText.text = "Week." + currentWeek.ToString();
    }
}
