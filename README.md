# USharpVideoQueue

A queue asset for [MerlinVR's USharpVideo player](https://github.com/MerlinVR/USharpVideo), designed for stability and easy integration into VRChat worlds.

<img src="https://github.com/sam-ln/USharpVideoQueue/assets/82455742/ea3b0a97-4f1a-47a2-9327-93e72341dec2" width=35% height=35%>

## Features

- Synced video queue for large instances
- Entering new videos via URL field or U# interface
- Set a limit for queued videos per user
- Instance owner can moderate videos
- Easy to integrate with permission systems

## Setup

### Requirements

- Latest VRCSDK
- Latest release of UdonSharp
- USharpVideo v.1.0.1 (Using v.1.0.0 will cause problems due to unfixed bugs)

### Adding to Scene

- Download and open .unitypackage release file and add everything to your project
- Drag USharpVideoQueue prefab into your Unity scene
- Open up the Queue in your Inspector window and drag your USharpVideoPlayer into the field "Video Player"

<img src="https://github.com/sam-ln/USharpVideoQueue/assets/82455742/f68ca4ff-ef0b-4b22-b315-9e1b0dde27e7" width=80% height=80%>
