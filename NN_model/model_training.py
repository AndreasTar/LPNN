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



def build_lightprobe_model(input_shape: tuple):
    # inputs = layers.Input(shape=input_shape)  # (D, H, W, 24)

    # # Encoder
    # x1 = layers.Conv3D(32, 3, padding='same', activation='relu')(inputs)
    # x1 = layers.BatchNormalization()(x1)

    # x2 = layers.Conv3D(64, 3, strides=2, padding='same', activation='relu')(x1)
    # x2 = layers.BatchNormalization()(x2)
    # x2 = layers.Dropout(0.2)(x2)

    # x3 = layers.Conv3D(128, 3, strides=2, padding='same', activation='relu')(x2)
    # x3 = layers.BatchNormalization()(x3)
    # x3 = layers.Dropout(0.3)(x3)

    # # Bottleneck
    # x = layers.Conv3D(256, 3, padding='same', activation='relu')(x3)

    # # Decoder with skip connections
    # x = layers.Conv3DTranspose(128, 3, strides=2, padding='same', activation='relu')(x)
    # x_cropped = center_crop_to_match(x, x2)
    # x = layers.Concatenate()([x_cropped, x2])
    # x = layers.Conv3D(128, 3, padding='same', activation='relu')(x)

    # x = layers.Conv3DTranspose(64, 3, strides=2, padding='same', activation='relu')(x)
    # x_cropped = center_crop_to_match(x, x1)
    # x = layers.Concatenate()([x_cropped, x1])
    # x = layers.Conv3D(64, 3, padding='same', activation='relu')(x)

    # # Final output layer
    # x = layers.Conv3D(1, 1, activation='sigmoid')(x)  # Output: importance per voxel

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
    print(features)
    print(features.shape, labels.shape)

    # Make sure the number of samples matches D*H*W
    assert features.shape[0] == D * H * W
    assert labels.shape[0] == D * H * W

    # Reshape
    X = features.reshape(D, H, W, -1)  # shape: [D, H, W, 24]
    y = labels.reshape(D, H, W)       # shape: [D, H, W]
    print("Did attributes")

    #MODEL
    input_shape = (D, H, W, 24)
    model = build_lightprobe_model(input_shape)

    # Add batch dimension
    X = np.expand_dims(X, axis=0)       # (1, D, H, W, 24)
    y = np.expand_dims(y, axis=(0, -1)) # (1, D, H, W, 1)

    model.fit(X, y, epochs=50)
    print("Did model")

    #SAVE

    # Convert and save
    spec = (tf.TensorSpec((None, None, None, None, 24), tf.float32, name="input"),)
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

