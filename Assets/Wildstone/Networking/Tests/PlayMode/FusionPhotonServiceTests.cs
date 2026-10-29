using System;
using System.Collections;
using System.Threading;
using Fusion;
using Fusion.Photon.Realtime;
using Photon.Realtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Wildstone.Combat;
using Wildstone.Maps;
using Wildstone.Networking;
using Wildstone.Session;
using Assert = NUnit.Framework.Assert;
using Object = UnityEngine.Object;

namespace Wildstone.Tests
{
    public sealed class FusionPhotonServiceTests
    {
        FusionSession session;
        CancellationTokenSource cancellation;

        [UnityTest,Explicit("Contacts the configured Photon service and creates a transient private room. Run only when explicitly authorized."),Category("LivePhoton")]
        public IEnumerator ExplicitPhotonPrivateHostCreatesAdmittedGuestLobby()
        {
            var catalog=Resources.Load<ArenaCatalog>("ArenaCatalog");
            Assert.That(catalog,Is.Not.Null,"Prepare the game content before running the live smoke test.");
            var map=catalog.Find("atrium");
            var content=new ContentVersion("1",FusionBuildIdentity.Read(),map.Id+"-"+map.Revision,"weapons-"+WeaponCatalog.Revision+"-120");
            var settings=PhotonAppSettings.Global.AppSettings.GetCopy(); settings.FixedRegion="us";
            var guest=new AuthenticationValues(Guid.NewGuid().ToString("N")) {AuthType=CustomAuthenticationType.None};
            string room=FusionMenuController.NewRoomCode();
            session=new FusionSession(content,room); cancellation=new CancellationTokenSource(TimeSpan.FromSeconds(35));
            var start=session.StartAsync(true,room,settings,guest,cancellation.Token);
            float deadline=Time.realtimeSinceStartup+40;
            while(!start.IsCompleted && Time.realtimeSinceStartup<deadline) yield return null;
            Assert.That(start.IsCompleted,Is.True,"Live Photon startup exceeded its bounded deadline.");
            Assert.That(start.IsFaulted,Is.False,"Live Photon startup faulted.");
            Assert.That(start.IsCanceled,Is.False,"Live Photon startup was cancelled.");
            Assert.That(start.Result,Is.True,"Live Photon private host startup failed.");
            Assert.That(session.Runner,Is.Not.Null); Assert.That(session.Runner.IsRunning,Is.True); Assert.That(session.Runner.IsServer,Is.True);
            Assert.That(session.Phase,Is.EqualTo(SessionPhase.Lobby)); Assert.That(session.Lobby,Is.Not.Null);
            Assert.That(session.Lobby.HostPlayer==session.Runner.LocalPlayer,Is.True,"Host must bind the actual SDK local peer.");
            int occupied=0; bool bound=false;
            for(int i=0;i<session.Lobby.Seats.Length;i++)
            {
                var seat=session.Lobby.Seats[i]; if(!seat.Occupied) continue;
                occupied++; bound|=seat.Connected && seat.Player==session.Runner.LocalPlayer;
            }
            Assert.That(occupied,Is.EqualTo(1)); Assert.That(bound,Is.True,"The host guest requires one connected SDK-bound seat.");
            session.Lobby.RequestTeam(2); session.Lobby.RequestReady(true);
            deadline=Time.realtimeSinceStartup+5;
            while(Time.realtimeSinceStartup<deadline)
            {
                var seat=session.Lobby.Seats[4];
                if(seat.Occupied && seat.Player==session.Runner.LocalPlayer && seat.Ready) break;
                yield return null;
            }
            Assert.That((bool)session.Lobby.Seats[4].Ready,Is.True,"Host team and ready commands must reach authority.");
            Assert.That(session.Phase,Is.EqualTo(SessionPhase.Lobby),"One guest cannot start a two-team match.");
        }

        [UnityTearDown] public IEnumerator AlwaysCloseTransientServiceRoom()
        {
            cancellation?.Cancel(); var owned=session?.Runner;
            try
            {
                if(session!=null)
                {
                    var stop=session.StopAsync(); float deadline=Time.realtimeSinceStartup+15;
                    while(!stop.IsCompleted && Time.realtimeSinceStartup<deadline) yield return null;
                    Assert.That(stop.IsCompleted,Is.True,"Live room cleanup exceeded its bounded deadline.");
                    Assert.That(stop.IsFaulted,Is.False,"Live room cleanup faulted.");
                    Assert.That(session.Runner,Is.Null);
                }
            }
            finally
            {
                if(owned!=null) Object.Destroy(owned.gameObject);
                cancellation?.Dispose(); cancellation=null; session=null;
            }
            yield return null;
        }
    }
}
