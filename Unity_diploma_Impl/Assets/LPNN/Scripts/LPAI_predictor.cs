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

    public float[] Predict(Dictionary<Vector3, float[][]> inputFeatures) // {key: [features]}
    {
        Start();

        int N = inputFeatures.Count; // Number of light probes
        float[] flattenedInput = inputFeatures.First().Value.SelectMany(x => x).ToArray(); // Flatten the first feature set to get the number of features per node
        int F = flattenedInput.Length; // Number of total values of the features per node
        Tensor<float> input = new (new TensorShape(1, N, F));
        print("Input shape: " + input.shape.ToString());

        int i = 0;
        inputFeatures.Keys.ToList().ForEach((key) =>
        {
            float[] flat = inputFeatures[key].SelectMany(x => x).ToArray();
            // Fill the tensor with the features
            for (int j = 0; j < F; j++)
            {
                input[0,i,j] = flat[j];
            }

            i++;
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
