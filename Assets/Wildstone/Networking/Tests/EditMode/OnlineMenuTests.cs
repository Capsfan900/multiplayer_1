using NUnit.Framework;
using UnityEngine;
using Wildstone.Game;
using Wildstone.Networking;
using Wildstone.UI;
using Photon.Realtime;
using System.Reflection;
using UnityEngine.UIElements;
using Assert = NUnit.Framework.Assert;

namespace Wildstone.Tests
{
    public sealed class OnlineMenuTests
    {
        [Test] public void RecoveryProjectionUsesTheExistingDeadlineAndDoesNotOfferAnotherReconnect()
        {
            var owner=new GameObject("recovery deadline presentation"); owner.SetActive(false);
            var retained=new FusionSession(new Wildstone.Session.ContentVersion("1","test","atrium-1","rules"),"ROOM1");
            const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
            try
            {
                var controller=owner.AddComponent<FusionMenuController>(); var bridge=owner.AddComponent<OnlineMenuBridge>();
                bridge.SessionLost=true;
                typeof(FusionMenuController).GetField("menu",flags).SetValue(controller,bridge);
                typeof(FusionMenuController).GetField("<Session>k__BackingField",flags).SetValue(controller,retained);
                typeof(FusionSession).GetField("recovering",flags).SetValue(retained,true);
                double deadline=Time.realtimeSinceStartupAsDouble+20;
                typeof(FusionSession).GetField("recoveryDeadline",flags).SetValue(retained,deadline);
                typeof(FusionMenuController).GetMethod("Update",flags).Invoke(controller,null);
                Assert.That(bridge.Recovering,Is.True); Assert.That(bridge.RecoverySecondsRemaining,Is.InRange(19,20));
                Assert.That(bridge.CanReconnect,Is.False); StringAssert.Contains("remaining",bridge.Status);
                Assert.That(typeof(FusionSession).GetField("recoveryDeadline",flags).GetValue(retained),Is.EqualTo(deadline));
                typeof(FusionSession).GetField("recoveryDeadline",flags).SetValue(retained,Time.realtimeSinceStartupAsDouble-1);
                typeof(FusionMenuController).GetMethod("Update",flags).Invoke(controller,null);
                Assert.That(bridge.RecoverySecondsRemaining,Is.Zero);
                typeof(FusionSession).GetField("recovering",flags).SetValue(retained,false);
                typeof(FusionMenuController).GetMethod("Update",flags).Invoke(controller,null);
                Assert.That(bridge.Recovering,Is.False); Assert.That(retained.RecoverySecondsRemaining,Is.Zero);
                typeof(FusionMenuController).GetField("<Session>k__BackingField",flags).SetValue(controller,null);
            }
            finally
            {
                var controller=owner.GetComponent<FusionMenuController>();
                if(controller!=null) typeof(FusionMenuController).GetField("<Session>k__BackingField",flags).SetValue(controller,null);
                Object.DestroyImmediate(owner);
            }
        }
        [TestCase(true,true)] [TestCase(true,false)] [TestCase(false,true)] [TestCase(false,false)]
        public void LoadingCancelAndFailureBackExitAnOnlineSessionBeforeExternalSimulationAttaches(bool active,bool cancel)
        {
            var owner=new GameObject("unattached online cancellation"); owner.SetActive(false);
            try
            {
                var bridge=owner.AddComponent<OnlineMenuBridge>(); bridge.ActiveSession=active;
                var menu=owner.AddComponent<ArenaMenu>(); var practice=owner.GetComponent<PracticeSession>();
                const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
                typeof(ArenaMenu).GetField("session",flags).SetValue(menu,practice);
                typeof(ArenaMenu).GetField("online",flags).SetValue(menu,bridge);
                int exits=0; bridge.Leave+=()=>{exits++;bridge.ActiveSession=false;};
                int stale=practice.Flow.BeginLoad();
                if(!cancel) practice.Flow.Show(MenuScreen.Failure);
                Assert.That(practice.ExternalSimulation,Is.False,"Online loading has no attached gameplay presentation yet.");
                typeof(ArenaMenu).GetMethod(cancel?"CancelArenaLoad":"LeaveArena",flags).Invoke(menu,null);
                Assert.That(exits,Is.EqualTo(active?1:0)); Assert.That(practice.Flow.Screen,Is.EqualTo(MenuScreen.Title));
                if(cancel) Assert.That(practice.Flow.CompleteLoad(stale,true),Is.False,"Cancelled completion must not capture gameplay input.");
            }
            finally { Object.DestroyImmediate(owner); }
        }
        [TestCase(true)] [TestCase(false)]
        public void OnlineLoadingFailureNeverOffersAnOfflineRetry(bool active)
        {
            var owner=new GameObject("online failure retry"); owner.SetActive(false);
            try
            {
                var bridge=owner.AddComponent<OnlineMenuBridge>(); bridge.ActiveSession=active;
                var menu=owner.AddComponent<ArenaMenu>(); var practice=owner.GetComponent<PracticeSession>();
                var root=new VisualElement(); const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
                typeof(ArenaMenu).GetField("session",flags).SetValue(menu,practice);
                typeof(ArenaMenu).GetField("online",flags).SetValue(menu,bridge);
                typeof(ArenaMenu).GetField("root",flags).SetValue(menu,root);
                practice.Flow.Show(MenuScreen.Failure); typeof(ArenaMenu).GetMethod("Render",flags).Invoke(menu,null);
                bool retry=root.Query<Button>().ToList().Exists(button=>button.text=="Retry");
                Assert.That(retry,Is.EqualTo(!active));
            }
            finally { Object.DestroyImmediate(owner); }
        }
        [TestCase(false,false,false,false)]
        [TestCase(true,true,false,false)]
        [TestCase(true,false,true,false)]
        [TestCase(true,false,false,true)]
        public void ReconnectRequiresLostRemoteSessionAndIdleTransport(bool lost,bool host,bool busy,bool expected)
        {
            var owner=new GameObject("reconnect menu test");
            try
            {
                var bridge=owner.AddComponent<OnlineMenuBridge>();
                bridge.SessionLost=lost; bridge.Host=host; bridge.Busy=busy;
                int retries=0; bridge.ReconnectRequested+=()=>retries++;
                bridge.RetryConnection();
                Assert.That(bridge.CanReconnect,Is.EqualTo(expected)); Assert.That(retries,Is.EqualTo(expected?1:0));
            }
            finally { Object.DestroyImmediate(owner); }
        }
        [Test] public void GeneratedRoomsMeetBothUiAndTransportCodeFormat()
        {
            for(int i=0;i<100;i++)
            {
                string room=FusionMenuController.NewRoomCode();
                Assert.That(MenuFlow.ValidJoinCode(room),Is.True);
                Assert.That(room,Does.Match("^[A-Z0-9]{8}$"));
            }
        }
        [Test] public void GuestIdentityIsRandomAndDoesNotRequestCustomAuthentication()
        {
            var first=FusionMenuController.NewGuestAuthentication(); var second=FusionMenuController.NewGuestAuthentication();
            Assert.That(first.UserId,Does.Match("^[0-9a-f]{32}$")); Assert.That(second.UserId,Is.Not.EqualTo(first.UserId));
            Assert.That(first.AuthType,Is.EqualTo(CustomAuthenticationType.None)); Assert.That(first.AuthGetParameters,Is.Null.Or.Empty);
            Assert.That(first.AuthPostData,Is.Null);
        }
        [Test] public void BridgeForwardsHostJoinAndLobbyActionsWithoutAnAccountStep()
        {
            var owner=new GameObject("menu bridge test");
            try
            {
                var bridge=owner.AddComponent<OnlineMenuBridge>();
                int hosts=0,team=-1; bool ready=false; string joined=null;
                bridge.HostMatch+=()=>hosts++; bridge.Join+=code=>joined=code;
                bridge.Team+=value=>team=value; bridge.SetReady+=value=>ready=value;
                bridge.HostRoom(); bridge.Connect("ROOM1");
                bridge.SelectTeam(3); bridge.ToggleReady();
                Assert.That(hosts,Is.EqualTo(1)); Assert.That(joined,Is.EqualTo("ROOM1")); Assert.That(team,Is.EqualTo(3)); Assert.That(ready,Is.True);
            }
            finally { Object.DestroyImmediate(owner); }
        }
    }
}
