namespace Controller
{
    /// <summary>チュートリアルの現在位置</summary>
    public enum TutorialStep
    {
        None = -1,
        TalkToRadioFirst,
        GoDayFishing,
        CatchDayFish,
        ReturnToCollectionAtNight,
        ExchangeFirstFish,
        TalkToRadioAfterFirstExchange,
        GoNightFishing,
        SeeUpperBeing,
        CatchNightFish,
        ReturnFromNightFishing,
        TalkToRadioAfterNightFishing,
        Completed
    }
}
