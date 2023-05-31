# USharpVideoQueue

A queue asset to work with MerlinVR's USharpVideo player, designed for stability and easy integration into VRChat worlds.

## Features

- Synced video queue for large instances
- Entering new videos via URL field or U# interface
- Limit queued videos per user
- Instance owner can moderate videos
- Easy to integrate with permission systems

## Setup

### Requirements

- Latest VRCSDK
- Latest release of UdonSharp
- USharpVideo v.1.0.1 (Using v.1.0.0 will lead to issues)

### Adding to Scene

- Download and open .unitypackage release file
- Drag USharpVideoQueue prefab into your Unity scene
- Set the reference to your UdonSharpVideo player in the Inspector
