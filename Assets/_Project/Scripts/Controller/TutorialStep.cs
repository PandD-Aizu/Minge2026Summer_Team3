namespace Controller
{
    /// <summary>チュートリアルの現在位置</summary>
    public enum TutorialStep
    {
        None = -1,
        TalkToRadioFirst = 0,
        CatchDayFish = 2,
        ReturnToCollectionAtNight = 3,
        // 既存のInspector設定に保存されたEnum値をずらさずに段階を追加する
        TalkToRadioBeforeFirstExchange = 12,
        ExchangeFirstFish = 4,
        TalkToRadioAfterFirstExchange = 5,
        GoNightFishing = 6,
        SeeUpperBeing = 7,
        CatchNightFish = 8,
        ReturnFromNightFishing = 9,
        TalkToRadioAfterNightFishing = 10,
        Completed = 11
    }
}
