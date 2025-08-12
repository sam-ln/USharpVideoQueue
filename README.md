# USharpVideoQueue

A synced video queue asset for [MerlinVR's USharpVideo player](https://github.com/MerlinVR/USharpVideo), designed for
stability and easy integration into VRChat worlds.

<img src="https://github.com/user-attachments/assets/c28d27b8-d57f-4c74-aed3-2ace86501ad7" width=35% height=35%> 
<img src="https://github.com/user-attachments/assets/50ee3b2d-e47f-4a07-9bfa-3f4f105f168b" width=35% height=35%>

## Features

- Synced video queue for large instances
- Entering new videos via URL field or U# interface
- Set a limit for queued videos per user
- Instance owner can remove videos and change queue positions
- Easy to integrate with permission systems
- Allows multiple displays/controls for the same queue
- Pagination for multiple pages of videos
- Reordering queued videos

## Installation

**USharpVideoQueue** can be installed using two different methods.

### Method 1: VRChat Creator Companion (recommended)

It's recommended to use the [VRChat Creator Companion (VCC)](https://vcc.docs.vrchat.com/) to install this package.
To do so, visit my VPM Repository below and click "***Add to VCC***" and add the `com.arcanescripts.usharpvideoqueue`package to your project.

If you choose this method, you need to use the USharpVideo version provided in my repository. It will be automatically installed as a dependency.

####  [📥 My VRChat Creator Companion Repository](https://sam-ln.github.io/vpm/)

### Method 2: Install manually using the .unitypackage file
This method will not use the VCC and should be used if you already have a non-VCC version of USharpVideo installed in your project
and you do not want to replace it. This version does not include Assembly Definitions (.asmdef files).
#### Requirements

- Latest version of VRChat SDK and UdonSharp (installed via [Creator Companion](https://vcc.docs.vrchat.com/))
- One version of USharpVideo must be installed. USharpVideoQueue supports these versions:
    - [USharpVideo, a fork by sam-ln (recommended)](https://github.com/sam-ln/USharpVideo)
    - [USharpVideoModernUI, a fork by DrBlackRat](https://github.com/DrBlackRat/USharpVideoModernUI) 
    - [USharpVideo by MerlinVR](https://github.com/MerlinVR/USharpVideo/)

## Setup in your project
- Depending on your install method, locate USharpVideoQueue in *Packages* or *Assets*
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

| **Event**                                  | **Trigger**                                                                                                                     | **Example Usecase**                                                                   |
|--------------------------------------------|---------------------------------------------------------------------------------------------------------------------------------|---------------------------------------------------------------------------------------|
| OnUSharpVideoQueueContentChange            | After a video is added or (automatically) removed from the Queue.                                                               | Update any displays that use queue data                                               |
| OnUSharpVideoQueuePlayingNextVideo         | When a video has finished loading and actually starts playing.                                                                  | Hide any placeholders, errors or notifications on the screen                          |
| OnUSharpVideoQueueVideoEnded               | After a video has finished playing and was removed from the queue. Does not trigger after the final video has finished playing. | Show notification for the owner of the next video that their video will start playing |
| OnUSharpVideoQueueFinalVideoEnded          | Only after the final video has ended.                                                                                           | Start playing background music when queue is empty                                    |
| OnUSharpVideoQueueSkippedError             | When a video was automatically skipped because an error occured. (..VideoEnded or ..FinalVideoEnded will trigger as well!)      | Display an error message to the users                                                 |
| OnUSharpVideoQueueCleared                  | When all entries in the queue were cleared by an elevated user                                                                  | Notify users that about the cleared queue                                             |
| OnUSharpVideoQueueCurrentVideoRemoved      | When the currently playing video has been manually removed                                                                      | Notify users about the skipped video                                                  |
| OnUSharpVideoQueueCustomURLsEnabled        | When an elevated user enabled custom URLs                                                                                       | Notify users about customs URLs being allowed now                                     |
| OnUSharpVideoQueueCustomURLsDisabled       | When an elevated user disabled custom URLs                                                                                      | Notify users about customs URLs no longer being allowed                               |
| OnUSharpVideoQueueVideoLimitPerUserChanged | When an elevated user changed the amount of allowed videos per user                                                             | Update displays of the video limit per user                                           |


