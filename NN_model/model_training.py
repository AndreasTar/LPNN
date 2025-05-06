import numpy as np
import tensorflow as tf
from tensorflow.keras import layers, models, backend as K # type: ignore
#from tensorflow.python.keras import layers, models
import tf2onnx
import onnx
import onnxruntime as ort

print("TensorFlow version:", tf.__version__)
print("ONNX version:", onnx.__version__)
print("ONNX Runtime version:", ort.__version__)
print("NumPy version:", np.__version__)


def make_model_pointnet(feature_dim: int, count):
    """
    PointNet‑style network that predicts one importance score per point.
      feature_dim: number of features per point (e.g. 4).
    """

    pts = layers.Input(shape=(None, feature_dim))  

    # Shared MLP on each point
    x = layers.Conv1D(64, 1, activation='relu')(pts)
    x = layers.Conv1D(128, 1, activation='relu')(x)
    x = layers.Conv1D(256, 1, activation='relu')(x)

    # Global feature is max over all N points
    global_feat = layers.GlobalMaxPooling1D()(x)        # (batch, 256)
    # Broadcast global feature back to each point
    #global_feat = layers.RepeatVector(count)(global_feat)


  # dynamic tile → (batch, N, 256)
    def tile_fn(inputs):
        gf, points = inputs
        N = K.shape(points)[1]
        gf = K.expand_dims(gf, 1)        # (batch, 1, 256)
        return K.tile(gf, [1, N, 1])     # (batch, N, 256)

    # here’s the key: tell Keras the output shape is (batch, N, 256)
    global_tiled = layers.Lambda(
        tile_fn,
        output_shape=lambda input_shapes: (input_shapes[1][0], input_shapes[1][1], 256)
    )([global_feat, pts])

    # Concatenate per point local and global context
    x = layers.Concatenate()([x, global_tiled])          # (batch, N, 512)

    # Per point processing
    x = layers.Conv1D(256, 1, activation='relu')(x)
    x = layers.Conv1D(128, 1, activation='relu')(x)

    # Final per point importance
    out = layers.Conv1D(1, 1, activation='sigmoid')(x)  # (batch, N, 1)

    model = models.Model(inputs=pts, outputs=out)
    model.compile(optimizer='adam', loss='binary_crossentropy', metrics=['mae'])
    return model



def main():

    #LABELS
    with open(r"C:\Users\Andreas\Desktop\UniStuff\Diploma\project\LPNN\Unity_diploma_Impl\Assets\LPNN\Results\comparisons.txt", "r") as f:
        label_lines = [line.strip() for line in f if line.strip() != '']

    labels = np.array([1.0 if l.lower() == "true" else 0.0 for l in label_lines], dtype=np.float32)
    print("Did labels")

    #FEATURES
    features = []

    with open(r"C:\Users\Andreas\Desktop\UniStuff\Diploma\project\LPNN\Unity_diploma_Impl\Assets\LPNN\Results\features.txt", "r") as f:
        block = []
        D, H, W, F = f.readline().strip().split()
        D, H, W, F = int(D), int(H), int(W), int(F)

        at_rgba = True
        for line in f:
            stripped = line.strip()
            if stripped == "": # if empty line
                at_rgba = True
                if block: # if block is not empty
                    features.append(np.array(block, dtype=np.float32).flatten())
                    block = []
            else:
                if at_rgba:
                    stripped = stripped.replace(",", ".").split()
                    stripped = [float(s) for s in stripped]
                    for rgba in stripped:
                        block.append(rgba)
                    at_rgba = False
                else:
                    stripped = stripped.replace(",", ".").split()
                    block.append([float(s) for s in stripped][0]) # since its just a single value, just do it like that


    # handle last block if no newline at end
    if block:
        features.append(np.array(block, dtype=np.float32).flatten())

    features = np.array(features, dtype=np.float32)  # shape: [N, 27]
    print("Did features")
    print(features.shape, labels.shape)

    # Reshape
    X = features[None, :, :]  # shape: [1, N, 27]
    y = labels[None, :, None] # shape: [1, N, 1]
    print("Did attributes")
    # print(X.shape, y.shape) # TODO need to add debug prints

    #MODEL
    model = make_model_pointnet(F, features.shape[0])
    model.summary()

    model.fit(X, y, batch_size=1, epochs=50)
    print("Did model")

    #SAVE

    # Convert and save
    spec = (tf.TensorSpec((None, None, F), tf.float32, name="pts"),) # 24 if features dim
    model_proto, _ = tf2onnx.convert.from_keras(
        model,
        input_signature=spec,
        opset=13,
        output_path="models/lightprobe_model.onnx"
    )
    print("Did save")

    #SANITY CHECK

    onnx_model = onnx.load("models/lightprobe_model.onnx")
    onnx.checker.check_model(onnx_model)

    ort_session = ort.InferenceSession("models/lightprobe_model.onnx")
    input_name = ort_session.get_inputs()[0].name
    output_name = ort_session.get_outputs()[0].name

    # Run a test
    result = ort_session.run([output_name], {input_name: X.astype(np.float32)})
    print(result[0].shape)  # Should be (1, N, 1)



if __name__ == "__main__":
    main()

