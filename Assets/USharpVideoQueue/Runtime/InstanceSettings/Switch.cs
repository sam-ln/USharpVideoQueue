using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;


namespace USharpVideoQueue.Runtime.InstanceSettings
{
    public class Switch : UdonSharpBehaviour
    {
        public Animator Animator;

        public UdonBehaviour OnEventReceiver;
        public string OnEvent;
        public UdonBehaviour OffEventReceiver;
        public string OffEvent;

        [UdonSynced] public bool CurrentState;
        public bool AllowMasterOnly;
        public bool TriggerEventForEveryone;

        public bool SyncVisibleState;
        private bool animationState = true;

        private VRCPlayerApi localPlayer;

        void Start()
        {
            localPlayer = Networking.LocalPlayer;
            triggerEvent();
            updateAnimation();
        }

        public void _SwitchTriggered()
        {
            if (AllowMasterOnly && !localPlayer.isMaster && SyncVisibleState) return;

            if (SyncVisibleState && !Networking.IsOwner(localPlayer, gameObject))
            {
                Networking.SetOwner(localPlayer, gameObject);
            }

            CurrentState = !CurrentState;
            triggerEvent();
            updateAnimation();
            if (SyncVisibleState) RequestSerialization();
        }

        private void animateSwitchOn()
        {
            Animator.Play("Base Layer.SwitchOn");
            animationState = true;
        }

        private void animateSwitchOff()
        {
            Animator.Play("Base Layer.SwitchOff");
            animationState = false;
        }

        private void updateAnimation()
        {
            if (CurrentState != animationState)
            {
                if (CurrentState)
                {
                    animateSwitchOn();
                }
                else
                {
                    animateSwitchOff();
                }
            }
        }

        private void triggerEvent()
        {
            if (CurrentState)
            {
                OnEventReceiver.SendCustomEvent(OnEvent);
            }
            else
            {
                OffEventReceiver.SendCustomEvent(OffEvent);
            }
        }

        public override void OnDeserialization()
        {
            if (TriggerEventForEveryone)
            {
                triggerEvent();
            }

            updateAnimation();
        }
    }
}