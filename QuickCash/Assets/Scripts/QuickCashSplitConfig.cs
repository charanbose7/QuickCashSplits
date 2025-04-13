using UnityEngine;

[CreateAssetMenu(fileName = "QuickCashSplitConfig", menuName = "ScriptableObjects/QuickCashSplitConfig", order = 1)]
public class QuickCashSplitConfig : ScriptableObject
{
    [System.Serializable]
    public class CoinValueRange
    {
        public int wager;
        public int minCoinValue;
        public int maxCoinValue;
    }

    public float basePrize;
    public int maxSpins;
    public int wager;
    public int maxCombinations;

    public CoinValueRange[] coinValueRanges;
}
