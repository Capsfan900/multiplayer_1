using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Wildstone.Game;
using Wildstone.Maps;
using Wildstone.Match;
using Wildstone.Movement;
using Wildstone.Networking;
using Wildstone.Session;
using Wildstone.UI;

namespace Wildstone.Tests
{
    public sealed class AbortedOnlineResultsTests
    {
        const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        static void Set(object target,string field,object value) => target.GetType().GetField(field,Private).SetValue(target,value);
        static void Call(object target,string method) => target.GetType().GetMethod(method,Private).Invoke(target,null);

        [UnityTest] public IEnumerator ConnectionFailureWithoutTerminalRoundKeepsTheExistingRetryBackFlow()
        {
            var owner=new GameObject("Online connection failure"); owner.SetActive(false);
            var retained=new FusionSession(new ContentVersion("1","test","atrium-1","rules"),"ROOM1");
            try
            {
                var bridge=owner.AddComponent<OnlineMenuBridge>();
                var coordinator=owner.AddComponent<FusionGameCoordinator>();
                var controller=owner.GetComponent<FusionMenuController>();
                var practice=owner.GetComponent<PracticeSession>();
                Set(controller,"menu",bridge); Set(controller,"<Session>k__BackingField",retained);
                Set(coordinator,"menu",controller); Set(coordinator,"presentation",practice); Set(coordinator,"session",retained);
                bridge.ActiveSession=true; practice.Flow.Show(MenuScreen.Online);
                Call(coordinator,"CloseOnline");
                while(!retained.Completion.IsCompleted) yield return null;
                yield return null;
                Assert.That(controller.Session,Is.Null); Assert.That(bridge.ActiveSession,Is.False);
                Assert.That(practice.Match,Is.Null); Assert.That(practice.ExternalSimulation,Is.False);
                Assert.That(practice.Flow.Screen,Is.EqualTo(MenuScreen.Online));
                Assert.That(bridge.Status,Does.Contain("could not continue"));
                Assert.That(bridge.CanReconnect,Is.False,"A failed connection closes the old session before a fresh retry.");
            }
            finally { Object.DestroyImmediate(owner); }
        }

        [UnityTest] public IEnumerator AbortedRoundClosesTransportAndKeepsAnIndependentOnlineSummaryUntilBack()
        {
            var owner=new GameObject("Aborted online summary"); owner.SetActive(false);
            var local=new GameObject("Retired network avatar",typeof(ArenaMotor));
            var camera=new GameObject("Results camera",typeof(Camera));
            var catalog=ScriptableObject.CreateInstance<ArenaCatalog>();
            var retained=new FusionSession(new ContentVersion("1","test","atrium-1","rules"),"ROOM1");
            try
            {
                var bridge=owner.AddComponent<OnlineMenuBridge>();
                var coordinator=owner.AddComponent<FusionGameCoordinator>();
                var controller=owner.GetComponent<FusionMenuController>();
                var practice=owner.GetComponent<PracticeSession>(); practice.Camera=camera.GetComponent<Camera>();
                var map=new ArenaDefinition { Id="atrium",Spawns=System.Array.Empty<ArenaSpawn>(),Pickups=System.Array.Empty<ArenaPickup>(),
                    ZoneRoute=new[]{Vector3.zero,Vector3.right*8,Vector3.forward*8} };
                catalog.Maps=new[]{map}; practice.Catalog=catalog;
                var source=new ArenaMatch(map);
                Assert.That(source.AddPlayer(1,0,1,1,Vector3.zero),Is.True);
                Assert.That(source.AddPlayer(2,1,2,1,Vector3.forward*6),Is.True);
                var state=source.Capture(); state.Phase=ArenaMatchPhase.Aborted; state.ScoreTicks[0]=1200;
                Assert.That(source.TryRestore(state),Is.True);
                Set(practice,"<Settings>k__BackingField",new LocalSettings());
                Set(practice,"<Input>k__BackingField",new PracticeInput(new LocalSettings()));
                practice.AttachNetworkMatch(source,1,new Dictionary<int,ArenaMotor>{{1,local.GetComponent<ArenaMotor>()}});
                Set(controller,"menu",bridge); Set(controller,"<Session>k__BackingField",retained);
                Set(coordinator,"menu",controller); Set(coordinator,"presentation",practice); Set(coordinator,"session",retained);
                bridge.ActiveSession=true;
                int retired=0; practice.SessionStopped+=()=>retired++;

                Call(coordinator,"CloseOnline");
                Assert.That(practice.Flow.Screen,Is.EqualTo(MenuScreen.Results),"An authoritative abort is a round outcome, not a connection failure.");
                Assert.That(controller.Session,Is.Null); Assert.That(bridge.ActiveSession,Is.False);
                Assert.That(practice.Match,Is.Not.SameAs(source));
                Assert.That(practice.Match.Phase,Is.EqualTo(ArenaMatchPhase.Aborted));
                Assert.That(practice.Match.Score(0),Is.EqualTo(10));
                Assert.That(practice.ExternalSimulation,Is.True,"Online results must not offer offline Play again.");
                Assert.That(practice.Motors.Count,Is.Zero); Assert.That(practice.View,Is.Null); Assert.That(retired,Is.EqualTo(1));
                source.Find(1).Health=1;
                Assert.That(practice.LocalPlayer.Health,Is.EqualTo(100),"Summary cannot retain mutable simulation state.");
                while(!retained.Completion.IsCompleted) yield return null;
                yield return null;
                Assert.That(practice.Flow.Screen,Is.EqualTo(MenuScreen.Results));
                Assert.That(bridge.CanReconnect,Is.False);
                Assert.That(bridge.Status,Does.Not.Contain("could not continue"));

                var menu=owner.AddComponent<ArenaMenu>(); var root=new VisualElement();
                Set(menu,"session",practice); Set(menu,"online",bridge); Set(menu,"root",root);
                Call(menu,"Render");
                Assert.That(root.Query<Label>().ToList().Exists(label=>label.text=="Round ended"),Is.True);
                Assert.That(root.Query<Button>().ToList().Exists(button=>button.text=="Play again" || button.text=="Retry"),Is.False);
                Call(menu,"Back");
                Assert.That(practice.Match,Is.Null); Assert.That(practice.Flow.Screen,Is.EqualTo(MenuScreen.Title));
            }
            finally
            {
                Object.DestroyImmediate(owner); Object.DestroyImmediate(local); Object.DestroyImmediate(camera); Object.DestroyImmediate(catalog);
            }
        }

        // A finished round stays in its session: only a running host runner may rematch, and every peer follows the lobby.
        [UnityTest] public IEnumerator FinishedRoundRematchNeedsTheHostAndPeersFollowTheLobbyInTheSameSession()
        {
            var owner=new GameObject("Finished online rematch"); owner.SetActive(false);
            var local=new GameObject("Finished network avatar",typeof(ArenaMotor));
            var camera=new GameObject("Results camera",typeof(Camera));
            var catalog=ScriptableObject.CreateInstance<ArenaCatalog>();
            var retained=new FusionSession(new ContentVersion("1","test","atrium-1","rules"),"ROOM1");
            try
            {
                var bridge=owner.AddComponent<OnlineMenuBridge>();
                var coordinator=owner.AddComponent<FusionGameCoordinator>();
                var controller=owner.GetComponent<FusionMenuController>();
                var practice=owner.GetComponent<PracticeSession>(); practice.Camera=camera.GetComponent<Camera>();
                var map=new ArenaDefinition { Id="atrium",Spawns=System.Array.Empty<ArenaSpawn>(),Pickups=System.Array.Empty<ArenaPickup>(),
                    ZoneRoute=new[]{Vector3.zero,Vector3.right*8,Vector3.forward*8} };
                catalog.Maps=new[]{map}; practice.Catalog=catalog;
                var source=new ArenaMatch(map);
                Assert.That(source.AddPlayer(1,0,1,1,Vector3.zero),Is.True);
                Assert.That(source.AddPlayer(2,1,2,1,Vector3.forward*6),Is.True);
                var state=source.Capture(); state.Phase=ArenaMatchPhase.Finished; state.Winner=0; state.ScoreTicks[0]=1200;
                Assert.That(source.TryRestore(state),Is.True);
                Set(practice,"<Settings>k__BackingField",new LocalSettings());
                Set(practice,"<Input>k__BackingField",new PracticeInput(new LocalSettings()));
                practice.AttachNetworkMatch(source,1,new Dictionary<int,ArenaMotor>{{1,local.GetComponent<ArenaMotor>()}});
                practice.Show(MenuScreen.Results);
                Set(controller,"menu",bridge); Set(controller,"<Session>k__BackingField",retained);
                Set(coordinator,"menu",controller); Set(coordinator,"presentation",practice); Set(coordinator,"session",retained);
                Set(coordinator,"loading",true); Set(coordinator,"loaded",true); Set(coordinator,"attached",true);
                bridge.ActiveSession=true;
                int retired=0; practice.SessionStopped+=()=>retired++;

                Assert.That(coordinator.Rematch(),Is.False,"Without a running host runner nothing returns the session to its lobby.");
                Assert.That(practice.Flow.Screen,Is.EqualTo(MenuScreen.Results)); Assert.That(practice.Match,Is.SameAs(source)); Assert.That(retired,Is.Zero);

                Call(coordinator,"FollowRematch");
                Assert.That(practice.Flow.Screen,Is.EqualTo(MenuScreen.Online),"Peers land in the lobby, not on the title or a failure.");
                Assert.That(practice.Match,Is.Null); Assert.That(practice.ExternalSimulation,Is.False);
                Assert.That(practice.Motors.Count,Is.Zero); Assert.That(practice.View,Is.Null); Assert.That(practice.CombatEvents,Is.Empty);
                Assert.That(retired,Is.EqualTo(1),"The finished round's presentation history is retired.");
                Assert.That(controller.Session,Is.SameAs(retained),"Following a rematch keeps the session, room and seat.");
                Assert.That(bridge.ActiveSession,Is.True); Assert.That(bridge.Status,Does.Not.Contain("could not continue"));
                Assert.That(typeof(FusionGameCoordinator).GetField("loading",Private).GetValue(coordinator),Is.False,"The next Start loads a new match.");
                yield return null;
                Assert.That(controller.Session,Is.SameAs(retained));
            }
            finally
            {
                var controller=owner.GetComponent<FusionMenuController>();
                if(controller!=null) Set(controller,"<Session>k__BackingField",null);
                Object.DestroyImmediate(owner); Object.DestroyImmediate(local); Object.DestroyImmediate(camera); Object.DestroyImmediate(catalog);
            }
        }
    }
}
