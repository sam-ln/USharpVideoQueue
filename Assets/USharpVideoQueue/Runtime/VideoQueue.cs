
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using UdonSharp.Video;

namespace USharpVideoQueue.Runtime
{
    public class VideoQueue : UdonSharpBehaviour
    {
  
        internal bool Initialized;
        internal void Start()
        {
            Initialized = true;
            Debug.Log("Started");
        }

        internal void DummyMethod() {
            
        }
    }
}
