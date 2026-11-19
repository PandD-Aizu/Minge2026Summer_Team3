using System;
using _Project.Scripts.Data.Fish;
using UnityEngine;

[CreateAssetMenu(fileName = "QuotaData", menuName = "Scriptable Objects/QuotaData")]
public class QuotaData : ScriptableObject
{
    public enum QuotaType
    {
        Tutorial,
        CatchFish,
        EarnMoney,
        CatchSpecialFish,
    }

    [SerializeField] private QuotaType _type;
    [SerializeField] private int _quotaAmount;
    [SerializeField] private FishDefinition _fishDefinition;
    [SerializeField] private string _quotaExplain;


    public QuotaType Type => _type;
    public int QuotaAmount => _quotaAmount;
    public string QuotaExplain => _quotaExplain;
    public FishDefinition FishDefinition => _fishDefinition;

}
