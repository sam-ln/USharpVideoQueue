using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UdonSharp;
using UnityEngine;
using System;
using USharpVideoQueue.Runtime;

namespace USharpVideoQueue.Tests.Editor.Utils
{
    public static class UdonSharpTestUtils
    {
        /// <summary>
        /// Simulates the RequestSerialization operation with UdonSharp.
        /// Calls OnPreSerialization on source, Copies members which have the [UdonSynced] attribute from source to target,
        /// calls OnDeserialization on target and calls OnPostDeserialization on source.
        /// </summary>
        /// <typeparam name="T">Class derived from UdonSharpBehavior</typeparam>
        /// <param name="source"></param>
        /// <param name="target"></param>
        public static void SimulateSerialization<T>(T source, T target) where T : UdonSharpBehaviour
        {
            source.OnPreSerialization();
            foreach (FieldInfo prop in typeof(VideoQueue).GetFields())
            {
                if (Attribute.IsDefined(prop, typeof(UdonSyncedAttribute)))
                {
                    if (prop.FieldType.IsArray)
                    {
                        Array sourceArray = (Array)prop.GetValue(source);
                        Array clonedArray = (Array)sourceArray.Clone();
                        prop.SetValue(target, clonedArray);
                    }
                    else
                    {
                        prop.SetValue(target, prop.GetValue(source));
                    }
                }
            }
            target.OnDeserialization();
            source.OnPostSerialization(new VRC.Udon.Common.SerializationResult(true, 10));
        }

    }
}
