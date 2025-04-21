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
    public int maxCombinations = 3; // Maximum possible combinations
    public int desiredCombinations = 2; // Target number of combinations (defaults to 2 as in the example)
    public int maxAdjacentElements = 3;

    public CoinValueRange[] coinValueRanges;
}