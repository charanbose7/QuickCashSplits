using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class QuickCashController : MonoBehaviour
{
    public QuickCashSplitConfig config;

    // Constants for configuration
    private const float BaseWager = 100f;
    private const float BaseMaxPrize = 30000f;
    private const int DefaultFrames = 15;
    private const int PrizeRoundingFactor = 500;

    // Constants for logic thresholds
    private const float HighPrizeRatioThreshold = 0.9f;
    private const int HighPrizeMinSpins = 5;
    private const float MinChunkMultiplier = 0.8f;
    private const float MaxChunkMultiplier = 1.2f;
    private const int MinNonEmptyFrames = 5;
    private const int MaxNonEmptyFrames = 11;

    void Start()
    {
        if (config == null)
        {
            Debug.LogError("QuickCashSplitConfig is not assigned!");
            return;
        }

        if (config.coinValueRanges == null || config.coinValueRanges.Length == 0)
        {
            Debug.LogError("Coin value ranges are not configured in QuickCashSplitConfig!");
            return;
        }

        CalculateSplit(config.wager, config.basePrize);
    }

    void CalculateSplit(int wager, float basePrize)
    {
        int minSpins = CalculateMinSpins(wager, basePrize);
        (float minCoinValue, float maxCoinValue) = GetCoinValueRange(wager, basePrize, minSpins);

        int roundedPrize = RoundToNearest(basePrize, PrizeRoundingFactor);
        int minCoinInt = RoundToNearest(minCoinValue, PrizeRoundingFactor);
        int maxCoinInt = RoundToNearest(maxCoinValue, PrizeRoundingFactor);

        Debug.Log($"Wager: {wager}, Base Prize: {basePrize}, Min Spins: {minSpins}, Min Coin Value: {minCoinValue}, Max Coin Value: {maxCoinValue}");

        List<int> spinChunks = GeneratePrizeChunks(roundedPrize, minSpins);
        List<List<string>> allFrameSplits = GenerateAllFrameSplits(spinChunks, minCoinInt, maxCoinInt);

        RunValidationTests(spinChunks, allFrameSplits, roundedPrize);
    }

    int CalculateMinSpins(int wager, float basePrize)
    {
        float scalingFactor = wager / BaseWager;
        float scaledMaxPrize = BaseMaxPrize * scalingFactor;
        float prizeRatio = basePrize / scaledMaxPrize;

        if (prizeRatio >= HighPrizeRatioThreshold)
        {
            return HighPrizeMinSpins;
        }

        int minSpins = Mathf.FloorToInt(config.maxSpins * prizeRatio);
        return Mathf.Clamp(minSpins, 1, config.maxSpins - 1);
    }

    (float minCoinValue, float maxCoinValue) GetCoinValueRange(int wager, float basePrize, int minSpins)
    {
        var coinRange = config.coinValueRanges.FirstOrDefault(c => c.wager == wager);
        if (coinRange != null)
        {
            return (coinRange.minCoinValue, coinRange.maxCoinValue);
        }

        Debug.LogWarning("No coin value range found for this wager! Defaulting to basePrize/minSpins");
        return (basePrize / minSpins, basePrize);
    }

    List<int> GeneratePrizeChunks(int totalPrize, int chunks)
    {
        totalPrize = RoundToNearest(totalPrize, PrizeRoundingFactor);
        List<int> values = new List<int>();
        int remaining = totalPrize;

        while (true)
        {
            values.Clear();
            remaining = totalPrize;

            for (int i = 0; i < chunks - 1; i++)
            {
                int maxChunk = (int)(remaining / (chunks - i) * MaxChunkMultiplier);
                int minChunk = (int)(remaining / (chunks - i) * MinChunkMultiplier);

                int value = Random.Range(minChunk, maxChunk + 1);
                value = RoundToNearest(value, PrizeRoundingFactor);

                values.Add(value);
                remaining -= value;
            }

            int finalChunk = RoundToNearest(remaining, PrizeRoundingFactor);
            values.Add(finalChunk);

            if (values.Sum() == totalPrize)
                break;
        }

        return values;
    }

    List<List<string>> GenerateAllFrameSplits(List<int> spinChunks, int minCoinInt, int maxCoinInt)
    {
        List<List<string>> allFrameSplits = new List<List<string>>();

        foreach (int spinValue in spinChunks)
        {
            List<string> rawFrames = GenerateSpinFrameSplits(spinValue, minCoinInt, maxCoinInt);
            int combinationCount = GroupAdjacentFrames(rawFrames);
            allFrameSplits.Add(rawFrames);

            string frameLog = string.Join(", ", rawFrames.Select((value, index) => $"[{index}, \"{value}\"]"));
            Debug.Log($"Spin Value: {spinValue}, Combinations Used: {combinationCount}, Frame Splits: {frameLog}");
        }

        return allFrameSplits;
    }

    private List<string> GenerateSpinFrameSplits(int spinValue, int minCoinInt, int maxCoinInt)
    {
        List<string> result;

        do
        {
            List<int> filled = new List<int>();
            int remaining = spinValue;

            int maxFill = Mathf.Min(DefaultFrames, spinValue / minCoinInt);
            int fillCount = Random.Range(3, maxFill + 1); // At least 3 for possible valid group

            for (int i = 0; i < fillCount - 1; i++)
            {
                int avg = remaining / (fillCount - i);
                int minVal = Mathf.Max(minCoinInt, RoundToNearest(avg * MinChunkMultiplier, PrizeRoundingFactor));
                int maxVal = Mathf.Min(maxCoinInt, RoundToNearest(avg * MaxChunkMultiplier, PrizeRoundingFactor));
                int val = RoundToNearest(Random.Range(minVal, maxVal + 1), PrizeRoundingFactor);

                filled.Add(val);
                remaining -= val;
            }

            int last = RoundToNearest(remaining, PrizeRoundingFactor);
            filled.Add(last);

            result = Enumerable.Repeat("", DefaultFrames).ToList();
            var indices = Enumerable.Range(0, DefaultFrames).OrderBy(_ => Random.value).Take(filled.Count).ToList();
            for (int i = 0; i < filled.Count; i++)
                result[indices[i]] = filled[i].ToString();

        } while (GroupAdjacentFrames(result) == 0); // Ensure at least one valid group

        return result;
    }

    private int GroupAdjacentFrames(List<string> frames)
    {
        int comboCount = 0;
        int currentStreak = 0;

        for (int i = 0; i < frames.Count; i++)
        {
            if (!string.IsNullOrEmpty(frames[i]))
            {
                currentStreak++;
            }
            else
            {
                if (currentStreak >= 3)
                    comboCount++;

                currentStreak = 0;
            }
        }

        if (currentStreak >= 3)
            comboCount++;

        return comboCount;
    }

    void RunValidationTests(List<int> spinChunks, List<List<string>> allFrameSplits, float basePrize)
    {
        Debug.Log("Running validation tests...");

        int spinTotal = spinChunks.Sum();
        Debug.Log(spinTotal == Mathf.RoundToInt(basePrize)
            ? "✅ Spin total == Base prize: Passed"
            : $"❌ Spin total != Base prize: FAILED ({spinTotal} != {basePrize})");

        for (int i = 0; i < spinChunks.Count; i++)
        {
            int expectedSpin = spinChunks[i];
            int actualSpin = allFrameSplits[i]
                .Where(s => !string.IsNullOrEmpty(s))
                .Select(int.Parse)
                .Sum();

            Debug.Log(actualSpin == expectedSpin
                ? $"✅ Frame total == Spin {i + 1}: Passed"
                : $"❌ Frame total != Spin {i + 1}: FAILED ({actualSpin} != {expectedSpin})");
        }

        Debug.Log("Validation done.");
    }

    List<int> GenerateCleanSplit(int total, int parts)
    {
        List<int> result = new List<int>();
        int remaining = total;

        for (int i = 0; i < parts - 1; i++)
        {
            int avg = remaining / (parts - i);
            int val = RoundToNearest(avg, PrizeRoundingFactor);
            result.Add(val);
            remaining -= val;
        }

        result.Add(RoundToNearest(remaining, PrizeRoundingFactor));
        return result;
    }

    int RoundToNearest(float value, int factor)
    {
        return Mathf.RoundToInt(value / factor) * factor;
    }
}
