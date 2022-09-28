using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Playables;
using UnityEngine.Serialization;

namespace Cinemachine
{
#if !(CINEMACHINE_PHYSICS || CINEMACHINE_PHYSICS_2D)
    /// <summary>
    /// A multi-purpose script which causes an action to occur when
    /// a trigger collider is entered and exited.  This is only available when physics is present.
    /// </summary>
    [AddComponentMenu("")] // Hide in menu
    public class CinemachineTriggerAction : MonoBehaviour {}
#else
    /// <summary>
    /// A multi-purpose script which causes an action to occur when
    /// a trigger collider is entered and exited.
    /// </summary>
    [SaveDuringPlay]
    [AddComponentMenu("Cinemachine/Helpers/Cinemachine Trigger Action")]
    [HelpURL(Documentation.BaseURL + "api/Cinemachine.CinemachineTriggerAction.html")]
    public class CinemachineTriggerAction : MonoBehaviour
    {
        /// <summary>Only triggers generated by objects on these layers will be considered.</summary>
        [Header("Trigger Object Filter")]
        [Tooltip("Only triggers generated by objects on these layers will be considered")]
        [FormerlySerializedAs("m_LayerMask")]
        public LayerMask LayerMask = 1;

        /// <summary>If set, only triggers generated by objects with this tag will be considered</summary>
        [TagField]
        [Tooltip("If set, only triggers generated by objects with this tag will be considered")]
        [FormerlySerializedAs("m_WithTag")]
        public string WithTag = string.Empty;

        /// <summary>Triggers generated by objects with this tag will be ignored</summary>
        [TagField]
        [Tooltip("Triggers generated by objects with this tag will be ignored")]
        [FormerlySerializedAs("m_WithoutTag")]
        public string WithoutTag = string.Empty;

        /// <summary>Skip this many trigger entries before taking action</summary>
        [NoSaveDuringPlay]
        [Tooltip("Skip this many trigger entries before taking action")]
        [FormerlySerializedAs("m_SkipFirst")]
        public int SkipFirst = 0;

        /// <summary>Repeat the action for all subsequent trigger entries</summary>
        [Tooltip("Repeat the action for all subsequent trigger entries")]
        [FormerlySerializedAs("m_Repeating")]
        public bool Repeating = true;

        /// <summary>What action to take when an eligible object enters the collider or trigger zone</summary>
        [Tooltip("What action to take when an eligible object enters the collider or trigger zone")]
        [FormerlySerializedAs("m_OnObjectEnter")]
        public ActionSettings OnObjectEnter = new(ActionSettings.ActionModes.Custom);

        /// <summary>What action to take when an eligible object exits the collider or trigger zone</summary>
        [Tooltip("What action to take when an eligible object exits the collider or trigger zone")]
        [FormerlySerializedAs("m_OnObjectExit")]
        public ActionSettings OnObjectExit = new(ActionSettings.ActionModes.Custom);

        HashSet<GameObject> m_ActiveTriggerObjects = new();
        
        /// <summary>Defines what action to take on trigger enter/exit</summary>
        [Serializable]
        public struct ActionSettings
        {
            /// <summary>What action to take</summary>
            public enum ActionModes
            {
                /// <summary>Use the event only</summary>
                Custom,
                /// <summary>Boost priority of virtual camera target</summary>
                PriorityBoost,
                /// <summary>Activate the target GameObject</summary>
                Activate,
                /// <summary>Decativate target GameObject</summary>
                Deactivate,
                /// <summary>Enable a component</summary>
                Enable,
                /// <summary>Disable a component</summary>
                Disable,
#if CINEMACHINE_TIMELINE
                /// <summary>Start animation on target</summary>
                Play,
                /// <summary>Stop animation on target</summary>
                Stop
#endif
            }

            /// <summary>Serializable parameterless game event</summary>
            [Serializable] public class TriggerEvent : UnityEvent {}

            /// <summary>What action to take</summary>
            [Tooltip("What action to take")]
            [FormerlySerializedAs("m_Action")]
            public ActionModes Action;

            /// <summary>The target object on which to operate.  If null, then the current behaviour/GameObject will be used</summary>
            [Tooltip("The target object on which to operate.  If null, then the current behaviour/GameObject will be used")]
            [FormerlySerializedAs("m_Target")]
            public UnityEngine.Object Target;

            /// <summary>If PriorityBoost, this amount will be added to the virtual camera's priority</summary>
            [Tooltip("If PriorityBoost, this amount will be added to the virtual camera's priority")]
            [FormerlySerializedAs("m_BoostAmount")]
            public int BoostAmount;

            /// <summary>If playing a timeline, start at this time</summary>
            [Tooltip("If playing a timeline, start at this time")]
            [FormerlySerializedAs("m_StartTime")]
            public float StartTime;

            /// <summary>How to interpret the start time</summary>
            public enum TimeModes
            {
                /// <summary>Offset after the start of the timeline</summary>
                FromStart, 
                /// <summary>Offset before the end of the timeline</summary>
                FromEnd, 
                /// <summary>Offset before the current timeline time</summary>
                BeforeNow, 
                /// <summary>Offset after the current timeline time</summary>
                AfterNow 
            };

            /// <summary>How to interpret the start time</summary>
            [Tooltip("How to interpret the start time")]
            [FormerlySerializedAs("m_Mode")]
            public TimeModes Mode;

            /// <summary>This event will be invoked</summary>
            [Tooltip("This event will be invoked")]
            [FormerlySerializedAs("m_Event")]
            public TriggerEvent Event;

            /// <summary>Standard Constructor</summary>
            /// <param name="action">Action to set</param>
            public ActionSettings(ActionModes action)
            {
                Action = action;
                Target = null;
                BoostAmount = 0;
                StartTime = 0;
                Mode = TimeModes.FromStart;
                Event = new TriggerEvent();
            }

            /// <summary>Invoke the action.  Depending on the mode, different action will
            /// be performed.  The embedded event will always be invoked, in addition to the
            /// action specified by the Mode.</summary>
            public void Invoke()
            {
                UnityEngine.Object currentTarget = Target;
                if (currentTarget != null)
                {
                    var targetGameObject = currentTarget as GameObject;
                    var targetBehaviour = currentTarget as Behaviour;
                    if (targetBehaviour != null)
                        targetGameObject = targetBehaviour.gameObject;

                    switch (Action)
                    {
                        case ActionModes.Custom:
                            break;
                        case ActionModes.PriorityBoost:
                            {
                                var vcam = targetGameObject.GetComponent<CinemachineVirtualCameraBase>();
                                if (vcam != null)
                                {
                                    vcam.Priority += BoostAmount;
                                    vcam.Prioritize();
                                }
                                break;
                            }
                        case ActionModes.Activate:
                            if (targetGameObject != null)
                            {
                                targetGameObject.SetActive(true);
                                var vcam = targetGameObject.GetComponent<CinemachineVirtualCameraBase>();
                                if (vcam != null)
                                    vcam.Prioritize();
                            }
                            break;
                        case ActionModes.Deactivate:
                            if (targetGameObject != null)
                                targetGameObject.SetActive(false);
                            break;
                        case ActionModes.Enable:
                            {
                                if (targetBehaviour != null)
                                    targetBehaviour.enabled = true;
                                break;
                            }
                        case ActionModes.Disable:
                            {
                                if (targetBehaviour != null)
                                    targetBehaviour.enabled = false;
                                break;
                            }
#if CINEMACHINE_TIMELINE
                        case ActionModes.Play:
                            {
                                var playable = targetGameObject.GetComponent<PlayableDirector>();
                                if (playable != null)
                                {
                                    double startTime = 0;
                                    double duration = playable.duration;
                                    double current = playable.time;
                                    switch (Mode)
                                    {
                                        default:
                                        case TimeModes.FromStart:
                                            startTime += StartTime;
                                            break;
                                        case TimeModes.FromEnd:
                                            startTime = duration - StartTime;
                                            break;
                                        case TimeModes.BeforeNow:
                                            startTime = current - StartTime;
                                            break;
                                        case TimeModes.AfterNow:
                                            startTime = current + StartTime;
                                            break;
                                    }
                                    playable.time = startTime;
                                    playable.Play();
                                }
                                else
                                {
                                    var ani = targetGameObject.GetComponent<Animation>();
                                    if (ani != null)
                                        ani.Play();
                                }
                                break;
                            }
                        case ActionModes.Stop:
                            {
                                var playable = targetGameObject.GetComponent<PlayableDirector>();
                                if (playable != null)
                                    playable.Stop();
                                else
                                {
                                    var ani = targetGameObject.GetComponent<Animation>();
                                    if (ani != null)
                                        ani.Stop();
                                }
                                break;
                            }
#endif
                    }
                }
                Event.Invoke();
            }
        }

        private bool Filter(GameObject other)
        {
            if (!enabled)
                return false;
            if (((1 << other.layer) & LayerMask) == 0)
                return false;
            if (WithTag.Length != 0 && !other.CompareTag(WithTag))
                return false;
            if (WithoutTag.Length != 0 && other.CompareTag(WithoutTag))
                return false;

            return true;
        }

        void InternalDoTriggerEnter(GameObject other)
        {
            if (!Filter(other))
                return;
            --SkipFirst;
            if (SkipFirst > -1)
                return;
            if (!Repeating && SkipFirst != -1)
                return;

            m_ActiveTriggerObjects.Add(other);
            OnObjectEnter.Invoke();
        }

        void InternalDoTriggerExit(GameObject other)
        {
            if (!m_ActiveTriggerObjects.Contains(other))
                return;
            m_ActiveTriggerObjects.Remove(other);
            if (enabled)
                OnObjectExit.Invoke();
        }

#if CINEMACHINE_PHYSICS
        void OnTriggerEnter(Collider other) => InternalDoTriggerEnter(other.gameObject);
        void OnTriggerExit(Collider other) => InternalDoTriggerExit(other.gameObject);
        void OnCollisionEnter(Collision other) => InternalDoTriggerEnter(other.gameObject);
        void OnCollisionExit(Collision other) => InternalDoTriggerExit(other.gameObject);
#endif
#if CINEMACHINE_PHYSICS_2D
        void OnTriggerEnter2D(Collider2D other) => InternalDoTriggerEnter(other.gameObject);
        void OnTriggerExit2D(Collider2D other) => InternalDoTriggerExit(other.gameObject);
        void OnCollisionEnter2D(Collision2D other) => InternalDoTriggerEnter(other.gameObject);
        void OnCollisionExit2D(Collision2D other) => InternalDoTriggerExit(other.gameObject);
#endif
        void OnEnable() {} // For the Enabled checkbox
    }
#endif
}
