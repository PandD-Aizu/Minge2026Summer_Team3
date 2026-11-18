using System;
using UnityEngine;

[CreateAssetMenu(fileName = "QuotaData", menuName = "Scriptable Objects/QuotaData")]
public class QuotaData : ScriptableObject
{
    [SerializeField] [Range(1, 4)] private int _quotaNumber;
    [SerializeField] private int _quotaAmount;

    [Header("作る側の把握用でゲームで直接使用する説明ではない")]
    [SerializeField] private string _quotaExplain;


    public int QuotaNumber => _quotaNumber;
    public int QuotaAmount => _quotaAmount;
    public string QuotaExplain => _quotaExplain;

}
