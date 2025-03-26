import torch
import torch.nn as tnn
import torch.optim as toptim

class LightProbeNet(tnn.Module):
    def __init__(self):
        super(LightProbeNet, self).__init__()
        self.conv1 = tnn.Conv3d(6, 32, kernel_size=3, padding=1)  # 6 input features
        self.conv2 = tnn.Conv3d(32, 64, kernel_size=3, padding=1)
        self.conv3 = tnn.Conv3d(64, 128, kernel_size=3, padding=1)
        self.fc = tnn.Linear(128, 1)  # Outputs a single probability score per voxel

    def forward(self, x):
        x = torch.relu(self.conv1(x))
        x = torch.relu(self.conv2(x))
        x = torch.relu(self.conv3(x))
        x = x.mean(dim=[2, 3, 4])  # Global average pooling
        x = self.fc(x)
        return torch.sigmoid(x)  # Probability of placing a probe

# Example: Training loop (pseudo)
model = LightProbeNet()
optimizer = toptim.Adam(model.parameters(), lr=0.001)
criterion = tnn.BCELoss()  # Binary classification loss (place probe or not)
