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

        Debug.Log($"Wager: {wager}, Base Prize: {basePrize}, Min Spins: {minSpins}, Min Coin Value: {minCoinValue}, Max Coin Value: {maxCoinValue}");

        int roundedPrize = RoundToNearest(basePrize, PrizeRoundingFactor);
        List<int> spinChunks = GeneratePrizeChunks(roundedPrize, minSpins);

        List<List<string>> allFrameSplits = GenerateAllFrameSplits(spinChunks);

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

    List<List<string>> GenerateAllFrameSplits(List<int> spinChunks)
    {
        List<List<string>> allFrameSplits = new List<List<string>>();

        foreach (int spinValue in spinChunks)
        {
            List<string> rawFrames = GenerateSpinFrameSplits(spinValue);
            (var groupedFrames, int combinationCount) = GroupAdjacentFrames(rawFrames);
            allFrameSplits.Add(groupedFrames);

            string frameLog = string.Join(", ", groupedFrames.Select((value, index) => $"[{index}, \"{value}\"]"));
            Debug.Log($"Spin Value: {spinValue}, Combinations Used: {combinationCount}, Frame Splits: {frameLog}");
        }

        return allFrameSplits;
    }

    List<string> GenerateSpinFrameSplits(int spinValue)
    {
        List<int> filledFrames = new List<int>();
        int remaining = spinValue;

        int nonEmptyCount = Random.Range(MinNonEmptyFrames, MaxNonEmptyFrames);

        var coinRange = config.coinValueRanges.FirstOrDefault(c => c.wager == config.wager);
        float minCoin = coinRange?.minCoinValue ?? 0;
        float maxCoin = coinRange?.maxCoinValue ?? spinValue;

        int minCoinInt = RoundToNearest(minCoin, PrizeRoundingFactor);
        int maxCoinInt = RoundToNearest(maxCoin, PrizeRoundingFactor);

        while (true)
        {
            filledFrames.Clear();
            remaining = spinValue;

            for (int i = 0; i < nonEmptyCount - 1; i++)
            {
                int average = remaining / (nonEmptyCount - i);
                int minChunk = Mathf.Max(minCoinInt, (int)(average * MinChunkMultiplier));
                int maxChunk = Mathf.Min(maxCoinInt, (int)(average * MaxChunkMultiplier));

                int value = Random.Range(minChunk, maxChunk + 1);
                value = RoundToNearest(value, PrizeRoundingFactor);

                filledFrames.Add(value);
                remaining -= value;
            }

            int lastValue = RoundToNearest(remaining, PrizeRoundingFactor);

            if (lastValue < minCoinInt || lastValue > maxCoinInt)
                continue;

            filledFrames.Add(lastValue);

            if (filledFrames.Sum() == spinValue)
                break;
        }

        List<string> finalFrames = Enumerable.Repeat("", DefaultFrames).ToList();
        List<int> availableIndexes = Enumerable.Range(0, DefaultFrames).ToList();

        foreach (int val in filledFrames)
        {
            int randIndex = availableIndexes[Random.Range(0, availableIndexes.Count)];
            finalFrames[randIndex] = val.ToString();
            availableIndexes.Remove(randIndex);
        }

        return finalFrames;
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

    (List<string>, int) GroupAdjacentFrames(List<string> frames)
    {
        List<List<int>> groups = new List<List<int>>();
        List<int> currentGroup = new List<int>();

        for (int i = 0; i < frames.Count; i++)
        {
            if (!string.IsNullOrEmpty(frames[i]))
            {
                if (currentGroup.Count == 0 || i == currentGroup.Last() + 1)
                {
                    currentGroup.Add(i);
                }
                else
                {
                    groups.Add(new List<int>(currentGroup));
                    currentGroup.Clear();
                    currentGroup.Add(i);
                }
            }
            else
            {
                if (currentGroup.Count > 0)
                {
                    groups.Add(new List<int>(currentGroup));
                    currentGroup.Clear();
                }
            }
        }

        if (currentGroup.Count > 0)
            groups.Add(currentGroup);

        while (groups.Count > config.maxCombinations)
        {
            int minGap = int.MaxValue;
            int mergeIndex = 0;

            for (int i = 0; i < groups.Count - 1; i++)
            {
                int gap = groups[i + 1][0] - groups[i].Last();
                if (gap < minGap)
                {
                    minGap = gap;
                    mergeIndex = i;
                }
            }

            groups[mergeIndex].AddRange(groups[mergeIndex + 1]);
            groups.RemoveAt(mergeIndex + 1);
        }

        List<string> newFrames = Enumerable.Repeat("", DefaultFrames).ToList();

        foreach (var group in groups)
        {
            int groupTotal = group.Sum(i => !string.IsNullOrEmpty(frames[i]) ? int.Parse(frames[i]) : 0);
            List<int> split = GenerateCleanSplit(groupTotal, group.Count);

            for (int j = 0; j < group.Count; j++)
                newFrames[group[j]] = split[j].ToString();
        }

        return (newFrames, groups.Count);
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
