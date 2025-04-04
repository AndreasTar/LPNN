import torch
import torch.nn as tnn
import torch.optim as toptim
import numpy as np
import tensorflow as tf
from tensorflow.keras import layers, models





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



if __name__ == "__main__":
    main()

