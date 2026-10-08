namespace Dialogue
{
    /// <summary>会話要求の結果を表す</summary>
    public enum DialogueResult
    {
        Completed,
        Canceled,
        Rejected,
        /// <summary>セーブに表示済みとして記録されているため再生しなかった</summary>
        Skipped
    }
}
