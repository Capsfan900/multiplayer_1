using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Wildstone.Game;
using Wildstone.Maps;
namespace Wildstone.Tests
{
    public sealed class PracticeSceneTests
    {
        [UnityTest] public IEnumerator AcceptedBackdropUsesMapEnvironmentAndCleanupRestoresBootstrap()
        {
            var previous=SceneManager.GetActiveScene();
            var owner=new GameObject("Backdrop scene integration fixture");
            var session=owner.AddComponent<PracticeSession>(); session.enabled=false;
            session.Catalog=Resources.Load<ArenaCatalog>("ArenaCatalog");
            AsyncOperation unload=null;
            try
            {
                Assert.That(session.Catalog,Is.Not.Null,"Prepare map content before native scene tests.");
                session.Load("atrium",false); float deadline=Time.realtimeSinceStartup+35;
                while(session.Flow.Screen==MenuScreen.Loading && Time.realtimeSinceStartup<deadline) yield return null;
                Assert.That(session.Flow.Screen,Is.EqualTo(MenuScreen.Title));
                Assert.That(SceneManager.GetActiveScene().path,Is.EqualTo(PracticeSession.MapScenePath("atrium")));
                Assert.That(RenderSettings.ambientMode,Is.EqualTo(AmbientMode.Trilight)); Assert.That(RenderSettings.fog,Is.True);
                Assert.That(RenderSettings.ambientSkyColor.r,Is.EqualTo(.66f).Within(.001f));
                Assert.That(RenderSettings.fogEndDistance,Is.EqualTo(180));
                typeof(PracticeSession).GetMethod("RestoreBootstrapScene",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(session,null);
                Assert.That(SceneManager.GetActiveScene(),Is.EqualTo(owner.scene));
            }
            finally
            {
                Object.DestroyImmediate(owner);
                if(previous.IsValid()&&previous.isLoaded) SceneManager.SetActiveScene(previous);
                var map=SceneManager.GetSceneByPath(PracticeSession.MapScenePath("atrium"));
                if(map.IsValid()&&map.isLoaded) unload=SceneManager.UnloadSceneAsync(map);
            }
            if(unload!=null) yield return unload;
        }
    }
}
