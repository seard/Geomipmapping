using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VarianceCompressor : MonoBehaviour
{
    public Texture2D HeightMap;
    public int TreeDepth = 5;

    public float[,] VarianceArray;
    public Texture2D varianceTexture;

	void Awake ()
    {
        // Exp
        int expTree = 1;
        for(int i = 0; i < TreeDepth; i++)
        {
            expTree *= 4;
        }

        int varianceTextureSize = (int)Mathf.Sqrt(expTree); // Width of texture
        Debug.Log("Variance texture size = " + varianceTextureSize);

        int stepSize = HeightMap.width / varianceTextureSize;
        Debug.Log("Step size = " + stepSize);

        VarianceArray = new float[varianceTextureSize, varianceTextureSize];

        for (int i = 0; i < varianceTextureSize; i++)
        {
            for(int j = 0; j < varianceTextureSize; j++)
            {
                // The value to pick out
                float maximum = 0;
                float minimum = 1;
                float result = 0;

                // Iterate over all pixels that will be thrown, and find a value to describe the sum of the total
                for (int x = 0; x < stepSize; x++)
                {
                    for (int y = 0; y < stepSize; y++)
                    {
                        // Get the height value at this pixel
                        float heightValue = HeightMap.GetPixel((i * stepSize) + x, (j * stepSize) + y).r;

                        // Compare and set max/min
                        if (heightValue > maximum) maximum = heightValue;
                        if (heightValue < minimum) minimum = heightValue;
                    }
                }

                result = (maximum - minimum);

                VarianceArray[i, varianceTextureSize - j - 1] = result; // Flip the X coordinate because of how we later iterate over the texture (it's flipped)
                varianceTexture.SetPixel(i, varianceTextureSize - j - 1, new Color(result, result, result, result));
            }
        }

        varianceTexture.Apply();
	}
}
