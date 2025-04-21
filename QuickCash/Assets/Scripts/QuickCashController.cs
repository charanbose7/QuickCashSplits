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
    private const int MinAdjacentForCombination = 3;

    // Color combinations
    public enum CoinColor { Red, Green, Blue }

    [System.Serializable]
    public class CoinPosition
    {
        public int index;
        public int value;
        public CoinColor color;

        public CoinPosition(int index, int value, CoinColor color)
        {
            this.index = index;
            this.value = value;
            this.color = color;
        }

        public override string ToString()
        {
            return $"[{index}:${value}:{color}]";
        }
    }

    [System.Serializable]
    public class Combination
    {
        public List<CoinPosition> coins = new List<CoinPosition>();
        public CoinColor color;
        public int totalValue;

        public override string ToString()
        {
            return $"{color} Combination: ${totalValue} from {string.Join(", ", coins)}";
        }
    }

    [System.Serializable]
    public class SpinData
    {
        public int spinValue;
        public List<Combination> combinations = new List<Combination>();

        public override string ToString()
        {
            return $"Spin: ${spinValue} with {combinations.Count} combinations";
        }
    }

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

        // First, divide the total prize into spin chunks (from original script)
        List<int> spinChunks = GeneratePrizeChunks(roundedPrize, minSpins);

        // Create list to hold all spin data
        List<SpinData> allSpins = new List<SpinData>();

        // For each spin chunk, create combinations
        foreach (int spinValue in spinChunks)
        {
            SpinData spinData = new SpinData { spinValue = spinValue };

            // Determine how many combinations for this spin (1-3)
            int combinationCount = Random.Range(1, Mathf.Min(config.maxCombinations, 4));

            // Split this spin's prize into combination chunks
            List<int> combinationTotals = SplitSpinIntoCombinations(spinValue, combinationCount);

            // Generate combinations based on the prize distribution
            List<Combination> combinations = GenerateCombinations(combinationTotals, minCoinInt, maxCoinInt);

            spinData.combinations = combinations;
            allSpins.Add(spinData);
        }

        // Log the results for all spins
        LogSpinResults(allSpins, roundedPrize);
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

    List<int> SplitSpinIntoCombinations(int spinTotal, int combinationCount)
    {
        // Split spin value into chunks for each combination
        return GenerateCleanSplit(spinTotal, combinationCount);
    }

    List<Combination> GenerateCombinations(List<int> combinationTotals, int minCoinValue, int maxCoinValue)
    {
        List<Combination> result = new List<Combination>();
        CoinColor[] colorValues = (CoinColor[])System.Enum.GetValues(typeof(CoinColor));

        // Track all used positions
        HashSet<int> usedPositions = new HashSet<int>();

        // For each combination total, create a set of adjacent coins with the same color
        for (int i = 0; i < combinationTotals.Count; i++)
        {
            int total = combinationTotals[i];

            // Pick a random color for this combination (avoid reusing if possible)
            CoinColor color = GetUniqueColor(result, colorValues);

            // Decide how many coins in this combination (at least MinAdjacentForCombination)
            // Maximum is constrained by remaining available positions, maximum value constraint, and maxAdjacentElements
            int maxCoinsBasedOnValue = Mathf.Max(MinAdjacentForCombination,
                                              Mathf.CeilToInt((float)total / maxCoinValue));
            int maxCoinsBasedOnSpace = DefaultFrames - usedPositions.Count;

            // Apply maxAdjacentElements constraint if set in config
            int maxAllowedElements = config.maxAdjacentElements > 0 ? config.maxAdjacentElements : int.MaxValue;

            int maxCoins = Mathf.Min(maxCoinsBasedOnSpace, maxCoinsBasedOnValue, maxAllowedElements);

            // Ensure we have at least MinAdjacentForCombination
            int coinCount = Random.Range(MinAdjacentForCombination,
                                        Mathf.Min(maxCoins, 7) + 1);

            // Split the total value for this combination among these coins
            List<int> values = SplitCombinationTotal(total, coinCount, minCoinValue, maxCoinValue);

            // Create adjacent positions for this combination
            List<int> positions = CreateAdjacentPositions(coinCount, usedPositions);

            // Add all positions to used set
            foreach (int pos in positions)
                usedPositions.Add(pos);

            // Create the combination
            Combination combination = new Combination
            {
                color = color,
                totalValue = total
            };

            // Create coin positions with values and color
            for (int j = 0; j < coinCount; j++)
            {
                combination.coins.Add(new CoinPosition(positions[j], values[j], color));
            }

            result.Add(combination);
        }

        return result;
    }

    CoinColor GetUniqueColor(List<Combination> existingCombinations, CoinColor[] availableColors)
    {
        // If we haven't used all colors yet, try to pick a new one
        HashSet<CoinColor> usedColors = new HashSet<CoinColor>(existingCombinations.Select(c => c.color));

        if (usedColors.Count < availableColors.Length)
        {
            List<CoinColor> unusedColors = availableColors.Where(c => !usedColors.Contains(c)).ToList();
            return unusedColors[Random.Range(0, unusedColors.Count)];
        }

        // Otherwise, pick a random color
        return availableColors[Random.Range(0, availableColors.Length)];
    }

    List<int> CreateAdjacentPositions(int count, HashSet<int> usedPositions)
    {
        List<int> availablePositions = Enumerable.Range(0, DefaultFrames)
                                     .Where(i => !usedPositions.Contains(i))
                                     .ToList();

        // If we don't have enough positions, return an error
        if (availablePositions.Count < count)
        {
            Debug.LogError($"Not enough available positions! Need {count}, have {availablePositions.Count}");
            return availablePositions.Take(count).ToList();
        }

        // Create adjacency map based on a grid (3x5 grid for 15 positions)
        Dictionary<int, List<int>> adjacencyMap = CreateAdjacencyMap();

        // Try several starting positions to find good clusters
        int maxTrials = 10;
        List<int> bestResult = null;
        int bestClusterSize = 0;

        for (int trial = 0; trial < maxTrials; trial++)
        {
            // Start with a random position
            int startIndex = Random.Range(0, availablePositions.Count);
            int startPos = availablePositions[startIndex];

            // BFS to find connected components
            List<int> result = new List<int> { startPos };
            Queue<int> frontier = new Queue<int>();
            HashSet<int> visited = new HashSet<int> { startPos };

            frontier.Enqueue(startPos);

            while (frontier.Count > 0 && result.Count < count)
            {
                int current = frontier.Dequeue();

                // Check all neighbors
                foreach (int neighbor in adjacencyMap[current])
                {
                    if (!visited.Contains(neighbor) && !usedPositions.Contains(neighbor))
                    {
                        visited.Add(neighbor);

                        // Add this position to our result
                        if (result.Count < count)
                        {
                            result.Add(neighbor);
                            frontier.Enqueue(neighbor);
                        }
                    }
                }
            }

            // Keep track of the best result
            if (result.Count > bestClusterSize)
            {
                bestClusterSize = result.Count;
                bestResult = result;

                // If we found a perfect cluster, break early
                if (bestClusterSize == count)
                    break;
            }
        }

        List<int> positions = bestResult ?? new List<int>();

        // If we couldn't find enough adjacent positions, add some random ones
        if (positions.Count < count)
        {
            Debug.LogWarning($"Could only find {positions.Count} adjacent positions, needed {count}");

            // Add some random positions to meet the count requirement
            List<int> remainingPositions = availablePositions
                .Where(p => !positions.Contains(p))
                .OrderBy(_ => Random.value)
                .Take(count - positions.Count)
                .ToList();

            positions.AddRange(remainingPositions);
        }

        return positions;
    }

    Dictionary<int, List<int>> CreateAdjacencyMap()
    {
        Dictionary<int, List<int>> adjacencyMap = new Dictionary<int, List<int>>();

        // Create a grid layout (3x5 grid for 15 positions)
        for (int i = 0; i < DefaultFrames; i++)
        {
            int row = i / 5;
            int col = i % 5;

            List<int> neighbors = new List<int>();

            // Check adjacent cells (up, down, left, right)
            if (row > 0) neighbors.Add(i - 5);  // Up
            if (row < 2) neighbors.Add(i + 5);  // Down
            if (col > 0) neighbors.Add(i - 1);  // Left
            if (col < 4) neighbors.Add(i + 1);  // Right

            adjacencyMap[i] = neighbors;
        }

        return adjacencyMap;
    }

    List<int> SplitCombinationTotal(int total, int parts, int minValue, int maxValue)
    {
        // Make sure all values are properly rounded
        total = RoundToNearest(total, PrizeRoundingFactor);
        List<int> values = new List<int>();
        int remaining = total;

        // Generate parts-1 values
        for (int i = 0; i < parts - 1; i++)
        {
            int maxChunk = Mathf.Min(maxValue, (int)(remaining / (parts - i) * MaxChunkMultiplier));
            int minChunk = Mathf.Max(minValue, (int)(remaining / (parts - i) * MinChunkMultiplier));

            // Handle edge case where minChunk > maxChunk
            if (minChunk > maxChunk)
                minChunk = maxChunk;

            int value = Random.Range(minChunk, maxChunk + 1);
            value = RoundToNearest(value, PrizeRoundingFactor);

            values.Add(value);
            remaining -= value;
        }

        // Add the final chunk
        int finalChunk = RoundToNearest(remaining, PrizeRoundingFactor);
        values.Add(finalChunk);

        return values;
    }

    void LogSpinResults(List<SpinData> allSpins, int targetPrize)
    {
        Debug.Log($"Generated {allSpins.Count} spins:");

        int totalPrize = 0;

        foreach (var spin in allSpins)
        {
            Debug.Log($"\nSpin value: ${spin.spinValue}");
            int spinTotal = 0;

            foreach (var combo in spin.combinations)
            {
                Debug.Log(combo.ToString());
                spinTotal += combo.totalValue;

                // Log which elements are paying in this combination
                string payingElements = string.Join(", ", combo.coins.Select(c => $"${c.value} at position {c.index}"));
                Debug.Log($"{combo.color} combination is paying: {payingElements}");

                // Log if the number of adjacent elements is within configuration limit
                int adjacentCount = combo.coins.Count;
                if (config.maxAdjacentElements > 0 && adjacentCount > config.maxAdjacentElements)
                {
                    Debug.LogWarning($"⚠️ Combination has {adjacentCount} adjacent elements, " +
                        $"which exceeds the maximum of {config.maxAdjacentElements}");
                }
                else
                {
                    Debug.Log($"✅ Combination has {adjacentCount} adjacent elements " +
                        $"{(config.maxAdjacentElements > 0 ? $"(max: {config.maxAdjacentElements})" : "")}");
                }
            }

            Debug.Log(spinTotal == spin.spinValue
                ? $"✅ Combinations total matches spin value: {spinTotal} == {spin.spinValue}"
                : $"❌ Combinations total doesn't match spin value: {spinTotal} != {spin.spinValue}");

            totalPrize += spin.spinValue;

            // Create a visual representation of this spin (board with all combinations)
            List<CoinPosition> allCoinsInSpin = spin.combinations.SelectMany(c => c.coins).ToList();
            VisualizeBoard(allCoinsInSpin, $"Spin ${spin.spinValue} Board");

            // Visualize each combination individually
            foreach (var combo in spin.combinations)
            {
                VisualizeBoard(combo.coins, $"{combo.color} Combination (${combo.totalValue}) - {combo.coins.Count} elements");
            }
        }

        // Validate overall total
        Debug.Log(totalPrize == targetPrize
            ? $"✅ Total prize value matches target: {totalPrize} == {targetPrize}"
            : $"❌ Total prize value doesn't match target: {totalPrize} != {targetPrize}");
    }

    void VisualizeBoard(List<CoinPosition> coins, string title)
    {
        // Create a full board representation for detailed visualization
        string[,] boardVisual = new string[3, 5];
        for (int row = 0; row < 3; row++)
        {
            for (int col = 0; col < 5; col++)
            {
                boardVisual[row, col] = "    ";  // Empty space
            }
        }

        // Fill in the board with coin values and colors
        foreach (var coin in coins)
        {
            int row = coin.index / 5;
            int col = coin.index % 5;

            string colorPrefix;
            switch (coin.color)
            {
                case CoinColor.Red: colorPrefix = "R"; break;
                case CoinColor.Green: colorPrefix = "G"; break;
                case CoinColor.Blue: colorPrefix = "B"; break;
                default: colorPrefix = "?"; break;
            }

            boardVisual[row, col] = $"{colorPrefix}${coin.value}";
        }

        // Print the complete board with all 15 positions
        Debug.Log($"{title}:");
        string boardStr = "";
        for (int row = 0; row < 3; row++)
        {
            for (int col = 0; col < 5; col++)
            {
                boardStr += $"[{boardVisual[row, col]}]";
            }
            boardStr += "\n";
        }
        Debug.Log(boardStr);
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