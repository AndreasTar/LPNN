import torch
import torch.nn as tnn
import torch.optim as toptim
import numpy as np
import tensorflow as tf
from keras import layers, models
import tf2onnx
import onnx
import onnxruntime as ort


def build_lightprobe_model(input_shape):
    inputs = layers.Input(shape=input_shape)  # (D, H, W, 24)

    # Encoder
    x1 = layers.Conv3D(32, 3, padding='same', activation='relu')(inputs)
    x1 = layers.BatchNormalization()(x1)

    x2 = layers.Conv3D(64, 3, strides=2, padding='same', activation='relu')(x1)
    x2 = layers.BatchNormalization()(x2)
    x2 = layers.Dropout(0.2)(x2)

    x3 = layers.Conv3D(128, 3, strides=2, padding='same', activation='relu')(x2)
    x3 = layers.BatchNormalization()(x3)
    x3 = layers.Dropout(0.3)(x3)

    # Bottleneck
    x = layers.Conv3D(256, 3, padding='same', activation='relu')(x3)

    # Decoder with skip connections
    x = layers.Conv3DTranspose(128, 3, strides=2, padding='same', activation='relu')(x)
    x = layers.Concatenate()([x, x2])
    x = layers.Conv3D(128, 3, padding='same', activation='relu')(x)

    x = layers.Conv3DTranspose(64, 3, strides=2, padding='same', activation='relu')(x)
    x = layers.Concatenate()([x, x1])
    x = layers.Conv3D(64, 3, padding='same', activation='relu')(x)

    # Final output layer
    x = layers.Conv3D(1, 1, activation='sigmoid')(x)  # Output: importance per voxel

    model = models.Model(inputs, x)
    model.compile(optimizer='adam', loss='binary_crossentropy', metrics=['mae'])

    return model



def main():

    #LABELS
    with open("C:\Users\Andreas\Desktop\UniStuff\Diploma\project\LPNN\Unity_diploma_Impl\Assets\LPNN\Results\comparisons.txt", "r") as f:
        label_lines = [line.strip() for line in f if line.strip() != '']

    labels = np.array([1.0 if l.lower() == "true" else 0.0 for l in label_lines], dtype=np.float32)

    #FEATURES
    features = []

    with open("C:\Users\Andreas\Desktop\UniStuff\Diploma\project\LPNN\Unity_diploma_Impl\Assets\LPNN\Results\evals.txt", "r") as f:
        block = []
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

    #ATTRIBUTES
    # Determine voxel grid shape (manually for now)
    D, H, W = 11, 3, 9  # Example shape — you should know your real grid size

    # Make sure the number of samples matches D*H*W
    assert features.shape[0] == D * H * W
    assert labels.shape[0] == D * H * W

    # Reshape
    X = features.reshape(D, H, W, -1)  # shape: [D, H, W, 24]
    y = labels.reshape(D, H, W)       # shape: [D, H, W]

    #MODEL
    input_shape = (D, H, W, 24)
    model = build_lightprobe_model(input_shape)

    # Add batch dimension
    X = np.expand_dims(X, axis=0)       # (1, D, H, W, 24)
    y = np.expand_dims(y, axis=(0, -1)) # (1, D, H, W, 1)

    model.fit(X, y, epochs=50)


    #SAVE

    # Convert and save
    spec = (tf.TensorSpec((None, D, H, W, 24), tf.float32, name="input"),)
    model_proto, _ = tf2onnx.convert.from_keras(
        model,
        input_signature=spec,
        opset=11,
        output_path="models/lightprobe_model.onnx"
    )

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

