using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Fusion;
using Fusion.Photon.Realtime;
using Photon.Realtime;
using UnityEngine;
using Wildstone.Combat;
using Wildstone.Game;
using Wildstone.Session;
using Wildstone.UI;

namespace Wildstone.Networking
{
    [DefaultExecutionOrder(-100)]
    [RequireComponent(typeof(PracticeSession))]
    public sealed class FusionMenuController : MonoBehaviour
    {
        [Serializable] public sealed class Configuration
        {
            public string region="us";
        }
        public FusionSession Session { get; private set; }
        public event Action<FusionSession> SessionCreated;
        OnlineMenuBridge menu;
        readonly AuthenticationValues authentication=NewGuestAuthentication();
        Configuration configuration;
        CancellationTokenSource operation;
        int generation;
        string room;
        string failureAfterLeave;
        void Awake()
        {
            menu=GetComponent<OnlineMenuBridge>()??gameObject.AddComponent<OnlineMenuBridge>();
            try
            {
                var asset=Resources.Load<TextAsset>("WildstoneOnline");
                configuration=asset==null?new Configuration():JsonUtility.FromJson<Configuration>(asset.text);
                if(configuration==null) throw new InvalidOperationException();
                if(string.IsNullOrWhiteSpace(configuration.region)) configuration.region="us";
                if(!Guid.TryParse(PhotonAppSettings.Global.AppSettings.AppIdFusion,out var appId) || appId==Guid.Empty)
                    throw new InvalidOperationException();
                menu.Available=true; menu.Status="Host a private match or enter a room code. Guest sessions last while this game stays open.";
            }
            catch(Exception) { menu.Available=false; menu.Status="Online setup is unavailable. Practice is ready."; }
            menu.HostMatch+=Host; menu.Join+=Join; menu.Leave+=Leave;
            menu.Team+=Team; menu.SetReady+=Ready; menu.StartMatch+=StartMatch;
            menu.ReconnectRequested+=Reconnect;
            menu.RematchRequested+=Rematch;
        }
        void Rematch() { var coordinator=GetComponent<FusionGameCoordinator>(); if(coordinator!=null) coordinator.Rematch(); }
        internal static AuthenticationValues NewGuestAuthentication() => new AuthenticationValues(Guid.NewGuid().ToString("N")) {AuthType=CustomAuthenticationType.None};
        async Task Run(Func<CancellationToken,Task> action,string status)
        {
            if(!menu.Available || menu.Busy) return;
            int attempt=++generation; operation=new CancellationTokenSource();
            var source=operation; menu.Busy=true; menu.Status=status; menu.Refresh();
            try { await action(source.Token); }
            catch(OperationCanceledException) { if(attempt==generation) menu.Status="Request cancelled."; }
            catch(ArgumentException e) { if(attempt==generation) menu.Status=e.Message; }
            catch(Exception) { if(attempt==generation) menu.Status="Request failed. Check service setup and your connection, then retry."; }
            finally
            {
                source.Dispose();
                if(attempt==generation) { operation=null; menu.Busy=false; menu.Refresh(); }
            }
        }
        void Host() => Connect(true,NewRoomCode());
        void Join(string code) => Connect(false,code);
        async void Connect(bool host,string code)
        {
            if(Session!=null) return;
            await Run(async token=>
            {
                var practice=GetComponent<PracticeSession>(); var map=practice.Catalog.Find(menu.Map);
                var content=new ContentVersion("1",FusionBuildIdentity.Read(),map.Id+"-"+map.Revision,"weapons-"+WeaponCatalog.Revision+"-120");
                var created=new FusionSession(content,code); Session=created; room=code;
                menu.ActiveSession=true;
                menu.Recovering=false; menu.RecoverySecondsRemaining=0;
                menu.Host=host; menu.SessionLost=false;
                created.Changed+=SessionChanged; SessionCreated?.Invoke(created);
                var settings=PhotonAppSettings.Global.AppSettings.GetCopy(); settings.FixedRegion=configuration.region;
                try
                {
                    if(!await created.StartAsync(host,code,settings,authentication,token))
                    {
                        token.ThrowIfCancellationRequested();
                        menu.Status=created.Failure??"Connection failed. Please retry.";
                        await ReleaseSession(created);
                    }
                    else { token.ThrowIfCancellationRequested(); menu.Status="Choose a team and ready up."; }
                }
                catch { await ReleaseSession(created); throw; }
            },host?"Creating private match…":"Joining private match…");
        }
        public static string NewRoomCode() => Guid.NewGuid().ToString("N").Substring(0,8).ToUpperInvariant();
        async void Reconnect()
        {
            if(!menu.CanReconnect || Session==null) return;
            var retained=Session;
            await Run(async token=>
            {
                await retained.Completion; token.ThrowIfCancellationRequested();
                if(!ReferenceEquals(Session,retained)) return;
                SessionCreated?.Invoke(retained);
                var settings=PhotonAppSettings.Global.AppSettings.GetCopy(); settings.FixedRegion=configuration.region;
                bool connected=await retained.StartAsync(false,room,settings,authentication,token);
                token.ThrowIfCancellationRequested();
                menu.InLobby=false;
                if(connected)
                {
                    menu.SessionLost=false; menu.Status="Connected. Waiting for the host's current session state.";
                }
                else menu.Status="Reconnection failed. Retry or leave. The host may have released your seat.";
            },"Reconnecting to the previous match…");
        }
        async Task ReleaseSession(FusionSession owned)
        {
            owned.Changed-=SessionChanged;
            if(ReferenceEquals(Session,owned)) { Session=null; menu.ActiveSession=false; menu.Recovering=false; menu.RecoverySecondsRemaining=0; }
            await owned.StopAsync();
        }
        void SessionChanged() { if(menu!=null) menu.Refresh(); }
        void Team(int team) { Session?.Lobby?.RequestTeam(team); }
        void Ready(bool ready) { Session?.Lobby?.RequestReady(ready); }
        void StartMatch() { Session?.Lobby?.RequestStart(); }
        public async void Leave()
        {
            int attempt=++generation; operation?.Cancel(); operation=null;
            menu.Busy=true; menu.Status="Leaving…"; menu.Refresh();
            try { if(Session!=null) await ReleaseSession(Session); }
            finally
            {
                if(attempt==generation && this!=null)
                {
                    menu.Busy=false; menu.InLobby=false; menu.SessionLost=false; menu.Room=""; menu.Roster="";
                    menu.Status=failureAfterLeave??"Host a private match or enter a room code.";
                    failureAfterLeave=null; menu.Refresh();
                }
            }
        }
        public void LeaveWithFailure(string message) { failureAfterLeave=message; Leave(); }
        void Update()
        {
            var current=Session;
            if(current==null) return;
            if(current.Recovering && current.Failure==null)
            {
                int remaining=(int)Math.Ceiling(current.RecoverySecondsRemaining);
                if(!menu.Recovering || menu.RecoverySecondsRemaining!=remaining)
                {
                    menu.Recovering=true; menu.RecoverySecondsRemaining=remaining;
                    menu.Status=$"Recovering connection · {remaining}s remaining. Gameplay is paused."; menu.Refresh();
                }
                return;
            }
            if(menu.Recovering)
            {
                menu.Recovering=false; menu.RecoverySecondsRemaining=0;
                if(current.Failure==null)
                {
                    menu.Status="Connection restored from a checkpoint. Waiting for the current match.";
                    menu.Refresh();
                }
            }
            if(current.Failure!=null && !menu.Busy)
            {
                if(!menu.SessionLost)
                {
                    menu.SessionLost=true; menu.InLobby=false;
                    menu.Status=menu.Host?"The host session ended and recovery did not complete. Leave this match.":
                        "Connection lost. Retry the previous match or leave. The host may have released your seat.";
                    menu.Refresh();
                }
                return;
            }
            var lobby=current.Lobby;
            if(lobby==null || lobby.Object==null || !lobby.Object.IsValid) return;
            var roster=new StringBuilder(); bool ready=false;
            for(int i=0;i<8;i++)
            {
                var seat=lobby.Seats[i]; if(!seat.Occupied) continue;
                roster.Append(ArenaHud.Team(seat.Team)).Append(" · Player ").Append(i+1)
                    .Append(seat.Connected?(seat.Ready?" · Ready":" · Choosing"):" · Reconnecting").Append('\n');
                if(seat.Player==current.Runner.LocalPlayer) ready=seat.Ready;
            }
            string text=roster.ToString(); bool host=lobby.HostPlayer==current.Runner.LocalPlayer;
            if(!menu.InLobby || menu.Roster!=text || menu.Ready!=ready || menu.Host!=host)
            {
                menu.InLobby=true; menu.Roster=text; menu.Room=room; menu.Ready=ready; menu.Host=host; menu.Refresh();
            }
        }
        void OnDestroy()
        {
            ++generation; operation?.Cancel();
            if(Session!=null) { Session.Changed-=SessionChanged; _=Session.StopAsync(); }
        }
    }
}
