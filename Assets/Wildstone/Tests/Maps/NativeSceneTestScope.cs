using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Wildstone.Tests
{
    // Native scene fixtures may replace only saved scenes or the empty batch startup.
    internal sealed class NativeSceneTestScope : IDisposable
    {
        readonly SceneSetup[] previous;
        public readonly Scene Scene;
        public NativeSceneTestScope()
        {
            for(int i=0;i<SceneManager.sceneCount;i++)
            {
                var scene=SceneManager.GetSceneAt(i);
                Assert.That(scene.isDirty,Is.False,"Save author scenes before native scene tests.");
                Assert.That(Application.isBatchMode||scene.path.Length!=0||scene.rootCount==0,Is.True,
                    "Save nonempty untitled author scenes before native scene tests. path="+scene.path+" dirty="+scene.isDirty+
                    " roots="+string.Join(", ",scene.GetRootGameObjects().Select(go=>go.name+" ["+go.hideFlags+"]")));
            }
            previous=EditorSceneManager.GetSceneManagerSetup();
            Scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        }
        public void Dispose()
        {
            if(previous.Length==0) EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            else EditorSceneManager.RestoreSceneManagerSetup(previous);
        }
    }
}
