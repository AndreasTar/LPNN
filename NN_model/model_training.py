import numpy as np
import tensorflow as tf
from tensorflow.keras import layers, models, backend as K # type: ignore
import tf2onnx
import onnx
import onnxruntime as ort

print("TensorFlow version:", tf.__version__)
print("ONNX version:", onnx.__version__)
print("ONNX Runtime version:", ort.__version__)
print("NumPy version:", np.__version__)


def make_model_pointnet(feature_dim: int):
    """
    PointNet‑style network that predicts one importance score per point.
      feature_dim: number of features per point (e.g. 4).
    """

    pts = layers.Input(shape=(None, feature_dim))  

    # Shared MLP on each point
    x = layers.Conv1D(64, 1, activation='relu')(pts)
    x = layers.BatchNormalization()(x)
    x = layers.Conv1D(128, 1, activation='relu')(x)
    x = layers.BatchNormalization()(x)
    x = layers.Conv1D(256, 1, activation='relu')(x)
    x = layers.BatchNormalization()(x)

    # Global feature is max over all N points
    global_max = layers.GlobalMaxPooling1D()(x)
    global_avg = layers.GlobalAveragePooling1D()(x)
    global_feat = layers.Concatenate()([global_max, global_avg])  # (batch, 512)


  # dynamic tile → (batch, N, 256)
    def tile_fn(inputs):
        gf, points = inputs
        N = K.shape(points)[1]
        gf = K.expand_dims(gf, 1)    # (batch, 1, 256)
        return K.tile(gf, [1, N, 1]) # (batch, N, 256)

    # tell Keras the output shape is (batch, N, 256)
    global_tiled = layers.Lambda(
        tile_fn,
        output_shape=lambda input_shapes: (input_shapes[1][0], input_shapes[1][1], 512)
    )([global_feat, pts])

    # concatenate per point local and global context
    x = layers.Concatenate()([x, global_tiled]) # (batch, N, 512)

    # per point processing
    x = layers.Conv1D(256, 1, activation='relu')(x)
    x = layers.Dropout(0.3)(x)
    x = layers.Conv1D(128, 1, activation='relu')(x)
    x = layers.Dropout(0.3)(x)
    x = layers.Conv1D(64, 1, activation='relu')(x)

    # final per point importance
    out = layers.Conv1D(1, 1, activation='sigmoid')(x)  # (batch, N, 1)

    model = models.Model(inputs=pts, outputs=out, name="LPNN_plus_plus")
    model.compile(optimizer='adam', loss='binary_crossentropy', metrics=['mae'])
    return model



def main():
    from os import getcwd
    #LABELS
    with open(getcwd()+r"\data\comparisons.txt", "r") as f:
        label_lines = [line.strip() for line in f if line.strip() != '']

    labels = np.array([1.0 if l.lower() == "true" else 0.0 for l in label_lines], dtype=np.float32)
    print("Labels completed.")

    #FEATURES
    features = []

    with open(getcwd()+r"\data\features.txt", "r") as f:
        block = []
        F = int(f.readline().strip().split()[0]) # read first line and get number of features

        for line in f:
            if not line.isspace():
                for val in line.split():
                    if val == "": continue
                    val = val.replace(",", ".")
                    block.append(float(val))
            else:
                features.append(np.array(block, dtype=np.float32).flatten())
                block = []

    # handle last block if no newline at end
    if block:
        features.append(np.array(block, dtype=np.float32).flatten())

    features = np.array(features, dtype=np.float32)  # shape: [N, F]
    print("Features completed.")
    print(features.shape, labels.shape)

    # Reshape
    X = features[None, :, :]  # shape: [1, N, F]
    y = labels[None, :, None] # shape: [1, N, 1]
    
   
    #MODEL
    print("Starting model training...")
    model = make_model_pointnet(F)
    model.summary()

    model.fit(X, y, batch_size=1, epochs=50)
    print("Model Training completed.")

    #SAVE

    # Convert and save
    spec = (tf.TensorSpec((None, None, F), tf.float32, name="pts"),) # 24 if features dim
    model_proto, _ = tf2onnx.convert.from_keras(
        model,
        input_signature=spec,
        opset=13,
        output_path="models/lightprobe_model.onnx"
    )
    print("Saved output to models/lightprobe_model.onnx")

    #SANITY CHECK

    onnx_model = onnx.load("models/lightprobe_model.onnx")
    onnx.checker.check_model(onnx_model)

    ort_session = ort.InferenceSession("models/lightprobe_model.onnx")
    input_name = ort_session.get_inputs()[0].name
    output_name = ort_session.get_outputs()[0].name

    # Run a test
    result = ort_session.run([output_name], {input_name: X.astype(np.float32)})
    print(f"Resulting shape after test: {result[0].shape}")  # Should be (1, N, 1)



if __name__ == "__main__":
    main()

