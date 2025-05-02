using UnityEngine;
using Unity.Sentis;
using Unity.VisualScripting;
using Unity.Mathematics;
using System.Linq;
using System.Collections.Generic;

public class LightProbeAI : MonoBehaviour
{
    public ModelAsset modelAsset;
    private Model runtimeModel;
    private Worker worker;

    // Example voxel grid size. Adjust these to your scene's grid dimensions.
    public int depth = 10;  // D
    public int height = 5;  // H
    public int width = 10;   // W
    public int channels = 24; // C

    void Start()
    {
        runtimeModel = ModelLoader.Load(modelAsset);
        worker = new Worker(runtimeModel, BackendType.GPUCompute);
    }

    public void SetDims(Vector3Int dims)
    {
        depth = dims.x;
        height = dims.y;
        width = dims.z;
    }

    public float[] Predict(Dictionary<Vector3Int, float[]> inputFeatures) // {key: [features]}
    {
        Start();

        int N = inputFeatures.Count; // Number of light probes
        int F = inputFeatures.First().Value.Length; // Number of features per light probe
        Tensor<float> input = new (new TensorShape(1, N, F));



        inputFeatures.Keys.ToList().ForEach((key) =>
        {
            // Convert the Vector3Int key to a 1D index for the tensor
            int index = key.x * height * width + key.y * width + key.z;
            // Fill the tensor with the features
            for (int j = 0; j < F; j++)
            {
                input[0, index, j] = inputFeatures[key][j];
            }
        });

        worker.Schedule(input);
        Tensor<float> output = worker.PeekOutput() as Tensor<float>;  // shape: [1, N, 1]

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
