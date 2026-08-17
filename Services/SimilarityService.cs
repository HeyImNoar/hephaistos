namespace Hephaistos.Services;

public static class SimilarityService
{
    public static double CosineSimilarity(
        float[] vectorA,
        float[] vectorB
    )
    {
        if (vectorA.Length != vectorB.Length)
            throw new ArgumentException(
                "Les deux vecteurs doivent avoir la même taille."
            );

        double dotProduct = 0;
        double magnitudeA = 0;
        double magnitudeB = 0;

        for (int i = 0; i < vectorA.Length; i++)
        {
            dotProduct += vectorA[i] * vectorB[i];
            magnitudeA += vectorA[i] * vectorA[i];
            magnitudeB += vectorB[i] * vectorB[i];
        }

        if (magnitudeA == 0 || magnitudeB == 0)
            return 0;

        return dotProduct /
               (Math.Sqrt(magnitudeA) * Math.Sqrt(magnitudeB));
    }
}