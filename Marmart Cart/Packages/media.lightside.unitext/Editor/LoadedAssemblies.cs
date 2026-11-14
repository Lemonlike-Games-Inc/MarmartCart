using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
#if UNITY_6000_4_OR_NEWER
using UnityEngine.Assemblies;
#endif

namespace LightSide
{
    /// <summary>
    /// The assemblies Unity holds loaded right now, and the file each came from. Unity reloads
    /// code through a collectible load context and may load an assembly from memory, so
    /// <c>AppDomain</c> enumeration can still list unloaded assemblies and
    /// <c>Assembly.Location</c> can be empty; editor code takes both answers from here.
    /// </summary>
    internal static class LoadedAssemblies
    {
        public static IReadOnlyList<Assembly> All()
        {
#if UNITY_6000_4_OR_NEWER
            return CurrentAssemblies.GetLoadedAssemblies();
#else
            return AppDomain.CurrentDomain.GetAssemblies();
#endif
        }

        /// <summary>
        /// Where the assembly was loaded from; null or empty when it has no file. A dynamic
        /// assembly has none and is excluded by the caller.
        /// </summary>
        public static string PathOf(Assembly assembly)
        {
#if UNITY_6000_4_OR_NEWER
            return assembly.GetLoadedAssemblyPath();
#else
            return assembly.Location;
#endif
        }
    }
}
