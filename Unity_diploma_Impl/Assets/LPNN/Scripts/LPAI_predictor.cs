using UnityEngine;
using Unity.Sentis;
using Unity.VisualScripting;
using Unity.Mathematics;
using System.Linq;

public class LightProbeAI : MonoBehaviour
{
    public ModelAsset modelAsset;
    private Model runtimeModel;
    private Worker worker;

    // Example voxel grid size. Adjust these to your scene's grid dimensions.
    public int depth = 11;  // D
    public int height = 3;  // H
    public int width = 9;   // W
    public int channels = 24; // C

    void Start()
    {
        runtimeModel = ModelLoader.Load(modelAsset);
        worker = new Worker(runtimeModel, BackendType.GPUCompute);
    }

    public float[] Predict(float[,,,,] inputFeatures)
    {
        Start();

        float[] channels = new float[
            inputFeatures.GetLength(0) * inputFeatures.GetLength(1) * inputFeatures.GetLength(2) * inputFeatures.GetLength(3) * inputFeatures.GetLength(4)
        ];
        
        Debug.Log(
            $"Features shape: {inputFeatures.GetLength(0)}, {inputFeatures.GetLength(1)}, {inputFeatures.GetLength(2)}, {inputFeatures.GetLength(3)}, {inputFeatures.GetLength(4)}"
        );
        for (int i = 0; i < inputFeatures.GetLength(0); i++)
        {
            for (int j = 0; j < inputFeatures.GetLength(1); j++)
            {
                for (int k = 0; k < inputFeatures.GetLength(2); k++)
                {
                    for (int l = 0; l < inputFeatures.GetLength(3); l++)
                    {
                        for (int m = 0; m < inputFeatures.GetLength(4); m++)
                        {
                            channels.Append(inputFeatures[i, j, k, l, m]);
                        }
                    }
                }
            }
        }

        Debug.Log("Channels shape: " + channels.Length);

        // Assume inputFeatures is [1, D, H, W, C]
        Tensor<float> input = new (
            new TensorShape(1, inputFeatures.GetLength(1), inputFeatures.GetLength(2), inputFeatures.GetLength(3), inputFeatures.GetLength(4)),
            channels
        );

        worker.Schedule(input);
        Tensor<float> output = worker.PeekOutput() as Tensor<float>;  // shape: [1, D, H, W, 1]

        float[] importance = output.DownloadToArray();

        input.Dispose();
        output.Dispose();

        return importance;
    }

    void OnDestroy()
    {
        worker?.Dispose();
    }
}
