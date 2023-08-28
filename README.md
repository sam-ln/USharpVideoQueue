# USharpVideoQueue

A synced video queue asset for [MerlinVR's USharpVideo player](https://github.com/MerlinVR/USharpVideo), designed for stability and easy integration into VRChat worlds.

<img src="https://github.com/sam-ln/USharpVideoQueue/assets/82455742/52590ed0-72a8-4ffb-80e3-1effb0e063a4" width=35% height=35%>


## Features

- Synced video queue for large instances
- Entering new videos via URL field or U# interface
- Set a limit for queued videos per user
- Instance owner can moderate videos
- Easy to integrate with permission systems
- Allows multiple displays/controls for the same queue
- Pagination for multiple pages of videos


## Setup

### Requirements

- Latest VRCSDK
- Latest release of UdonSharp
- USharpVideo v.1.0.1 (Using v.1.0.0 will cause problems due to unfixed bugs)

### Adding to Scene

- Download and open .unitypackage release file and add everything to your project
- Drag USharpVideoQueue prefab into your Unity scene
- Open up the Queue in your Inspector window and drag your USharpVideoPlayer into the field "Video Player"

<img src="https://github.com/sam-ln/USharpVideoQueue/assets/82455742/1160ae86-ae24-46ad-86ba-45253017c61a" width=80% height=80%>



## Integration
This section is only relevant if you want to integrate this queue with your own U# scripts.

### Events

You can react to any events emitted by the Queue by registering as a callback listener in your own U# Behaviour
and implementing a receiving method: 
```c# 
using UdonSharp;
using UnityEngine;
using USharpVideoQueue.Runtime;

public class YourBehaviour : UdonSharpBehaviour
{
    public VideoQueue VideoQueue;
    
    void Start()
    {
        VideoQueue.RegisterCallbackReceiver(this);
    }

    public void OnUSharpVideoQueueContentChange()
    {
        Debug.Log("Received ContentChangeEvent!");
    }
}
 ```

| **Event**                          | **Trigger**                                                                                                                              | **Example Usecase**                                                                   |
|------------------------------------|------------------------------------------------------------------------------------------------------------------------------------------|---------------------------------------------------------------------------------------|
| OnUSharpVideoQueueContentChange    | Triggers after a video is added or (automatically) removed from the Queue.                                                               | Update any displays that use queue data                                               |
| OnUSharpVideoQueuePlayingNextVideo | Triggers when a video has finished loading and actually starts playing.                                                                  | Hide any placeholders, errors or notifications on the screen                          |
| OnUSharpVideoQueueVideoEnded       | Triggers after a video has finished playing and was removed from the queue. Does not trigger after the final video has finished playing. | Show notification for the owner of the next video that their video will start playing |
| OnUSharpVideoQueueFinalVideoEnded  | Triggers only after the final video has ended.                                                                                           | Start playing background music when queue is empty                                    |
| OnUSharpVideoQueueSkippedError     | Triggers when a video was automatically skipped because an error occured. (..VideoEnded or ..FinalVideoEnded will trigger as well!)      | Display an error message to the users                                                 |
