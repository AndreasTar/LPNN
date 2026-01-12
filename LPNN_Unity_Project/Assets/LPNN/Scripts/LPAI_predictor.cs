using UnityEngine;

using Unity.VisualScripting;
using Unity.Mathematics;
using System.Linq;
using System.Collections.Generic;

public class LightProbeAI : MonoBehaviour
{
    public Unity.InferenceEngine.ModelAsset modelAsset;
    private Unity.InferenceEngine.Model runtimeModel;
    private Unity.InferenceEngine.Worker worker;

    void Start()
    {
        runtimeModel = Unity.InferenceEngine.ModelLoader.Load(modelAsset);
        worker = new Unity.InferenceEngine.Worker(runtimeModel, Unity.InferenceEngine.BackendType.GPUCompute);
    }

    public float[] Predict(Dictionary<Vector3, float[][]> inputFeatures) // {key: [features]}
    {
        Start();

        int N = inputFeatures.Count; // Number of light probes
        float[] flattenedInput = inputFeatures.First().Value.SelectMany(x => x).ToArray(); // Flatten the first feature set to get the number of features per node
        int F = flattenedInput.Length; // Number of total values of the features per node
        Unity.InferenceEngine.Tensor<float> input = new (new Unity.InferenceEngine.TensorShape(1, N, F));
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
        Unity.InferenceEngine.Tensor<float> output = worker.PeekOutput() as Unity.InferenceEngine.Tensor<float>;  // shape: [1, N, 1]

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
