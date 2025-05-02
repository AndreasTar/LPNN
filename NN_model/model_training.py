import math
import torch
import torch.nn as tnn
import torch.optim as toptim
import numpy as np
import tensorflow as tf
import keras
from keras import layers, models, ops
import tf2onnx
import onnx
import onnxruntime as ort

print("TensorFlow version:", tf.__version__)
print("Keras version:", keras.__version__)
print("PyTorch version:", torch.__version__)
print("ONNX version:", onnx.__version__)
print("ONNX Runtime version:", ort.__version__)
print("NumPy version:", np.__version__)



def make_model_3dcnn(input_shape: tuple):
    """
    Build a fully convolutional 3D CNN model that outputs a per-voxel importance score (0–1).
    - input_channels: Number of channels per voxel (e.g. 4 for RGBA).
    """
    input_tensor = layers.Input(shape=(None, None, None, input_shape[3]))  # [D, H, W, C]

    x = input_tensor

    # Encoder
    x = layers.Conv3D(32, kernel_size=3, padding='same', activation='relu')(x)
    x = layers.Conv3D(64, kernel_size=3, padding='same', activation='relu')(x)
    x = layers.Conv3D(128, kernel_size=3, padding='same', activation='relu')(x)

    # Bottleneck
    x = layers.Conv3D(128, kernel_size=1, padding='same', activation='relu')(x)

    # Decoder-ish layers (upsample not needed, we keep the same size)
    x = layers.Conv3D(64, kernel_size=3, padding='same', activation='relu')(x)
    x = layers.Conv3D(32, kernel_size=3, padding='same', activation='relu')(x)

    # Final 1×1×1 conv layer to produce per-voxel prediction
    output = layers.Conv3D(1, kernel_size=1, padding='same', activation='sigmoid')(x)

    model = models.Model(input_tensor, outputs=output)
    model.compile(optimizer='adam', loss='binary_crossentropy')

    return model

def make_model_pointnet(feature_dim: int, count: int):
    """
    PointNet‑style network that predicts one importance score per point.
      feature_dim: number of features per voxel (e.g. 24).
    """

    pts = layers.Input(shape=(None, feature_dim))  

    # Shared MLP on each point
    x = layers.Conv1D(64, 1, activation='relu')(pts)
    x = layers.Conv1D(128, 1, activation='relu')(x)
    x = layers.Conv1D(256, 1, activation='relu')(x)

    # Global feature is max over all N points
    global_feat = layers.GlobalMaxPooling1D()(x)        # (batch, 256)
    # Broadcast global feature back to each point
    global_feat = layers.RepeatVector(count)(global_feat)  # (batch, N, 256)

    # Concatenate per point local and global context
    x = layers.Concatenate()([x, global_feat])          # (batch, N, 512)

    # Per point processing
    x = layers.Conv1D(256, 1, activation='relu')(x)
    x = layers.Conv1D(128, 1, activation='relu')(x)

    # Final per point importance
    out = layers.Conv1D(1, 1, activation='sigmoid')(x)  # (batch, N, 1)

    model = models.Model(inputs=pts, outputs=out)
    model.compile(optimizer='adam', loss='binary_crossentropy', metrics=['mae'])
    return model

def center_crop_to_match(source, target):
    """
    Crops `source` tensor so its spatial shape matches `target`.
    Both are expected to be 5D tensors: (batch, depth, height, width, channels)
    """
    s = source.shape
    t = target.shape
    # crop = (s[1:4] - t[1:4]) // 2  # Get crop sizes for D, H, W
    crop = tuple(map(lambda x, y: math.ceil(abs(x - y)/2), s[1:4], t[1:4]))

    # Calculate how much to crop from the start and end of each dimension
    crop_d_start = crop[0] // 2
    crop_d_end = crop[0] - crop_d_start
    crop_h_start = crop[1] // 2
    crop_h_end = crop[1] - crop_h_start
    crop_w_start = crop[2] // 2
    crop_w_end = crop[2] - crop_w_start

    crop = ((crop_d_start, crop_d_end), (crop_h_start, crop_h_end), (crop_w_start, crop_w_end))
    print(f"Cropping: {crop}\nfor {s}, {t}")

    res = keras.layers.Cropping3D(cropping=crop)(source)

    print(f"result: {res.shape}")
    return res



def main():

    #LABELS
    with open(r"C:\Users\Andreas\Desktop\UniStuff\Diploma\project\LPNN\Unity_diploma_Impl\Assets\LPNN\Results\comparisons.txt", "r") as f:
        label_lines = [line.strip() for line in f if line.strip() != '']

    labels = np.array([1.0 if l.lower() == "true" else 0.0 for l in label_lines], dtype=np.float32)
    print("Did labels")

    #FEATURES
    features = []

    with open(r"C:\Users\Andreas\Desktop\UniStuff\Diploma\project\LPNN\Unity_diploma_Impl\Assets\LPNN\Results\evals.txt", "r") as f:
        block = []
        D, H ,W = f.readline().strip().split()
        D, H, W = int(D), int(H), int(W)

        for line in f:
            stripped = line.strip()
            if stripped == "":
                if block:
                    features.append(np.array(block, dtype=np.float32).flatten())  # 6 x 4 = 24 features
                    block = []
            else:
                rgba = list(map(float, stripped.split()))
                block.append(rgba)

    # handle last block if no newline at end
    if block:
        features.append(np.array(block, dtype=np.float32).flatten())

    features = np.array(features, dtype=np.float32)  # shape: [N, 24]
    print("Did features")
    print(features.shape, labels.shape)

    # Reshape
    X = features[None, :, :]  # shape: [1, N, 24]
    y = labels[None, :, None] # shape: [1, N, 1]
    print("Did attributes")

    #MODEL
    input_shape = (D, H, W, 24)
    model = make_model_pointnet(24, features.shape[0])
    model.summary()

    model.fit(X, y, batch_size=1, epochs=50)
    print("Did model")

    #SAVE

    # Convert and save
    spec = (tf.TensorSpec((None, None, 24), tf.float32, name="pts"),) # 24 if features dim
    model_proto, _ = tf2onnx.convert.from_keras(
        model,
        input_signature=spec,
        opset=15,
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
    print(result[0].shape)  # Should be (1, D, H, W, 1)



if __name__ == "__main__":
    main()

