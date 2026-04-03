using System;
using System.Collections.Generic;
using UnityEditor;


namespace TinyGiantStudio.BetterInspector.BetterMesh
{
    /// <summary>
    /// Domain reload often fails to clean up mesh previews. Although this doesn't cause a big problem because they are cleaned up by GC later, it prints an error log. This prevents the log because this cleans it up. 
    /// </summary>
    [InitializeOnLoad]
    public static class DomainReloadCleanup
    {
        static readonly HashSet<IDisposable> disposables = new();

        static DomainReloadCleanup()
        {
            AssemblyReloadEvents.beforeAssemblyReload += DisposeAll;
        }

        public static void Register(IDisposable disposable)
        {
            disposables.Add(disposable);
        }

        public static void Unregister(IDisposable disposable)
        {
            disposables.Remove(disposable);
        }

        static void DisposeAll()
        {
            foreach (IDisposable d in disposables)
            {
                try
                {
                    d.Dispose();
                }
                catch
                {
                    // ignored
                }
            }

            disposables.Clear();
        }
    }
}