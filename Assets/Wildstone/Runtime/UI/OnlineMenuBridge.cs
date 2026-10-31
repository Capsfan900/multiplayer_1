using System;
using UnityEngine;

namespace Wildstone.UI
{
    // Keeps the UI assembly independent of the optional networking assembly.
    public sealed class OnlineMenuBridge : MonoBehaviour
    {
        public bool Available, Busy, InLobby, Host, Ready, ActiveSession;
        public bool SessionLost;
        public bool Recovering;
        public int RecoverySecondsRemaining;
        public bool CanReconnect => SessionLost && !Host && !Busy && !Recovering;
        public string Status = "Online setup is unavailable. Practice is ready.", Room = "", Roster = "";
        public string Map = "atrium";
        public event Action Changed;
        public event Action<string> Join;
        public event Action HostMatch, Leave, StartMatch;
        public event Action ReconnectRequested, RematchRequested;
        public event Action<int> Team;
        public event Action<bool> SetReady;
        public void Refresh() => Changed?.Invoke();
        public void Connect(string room) => Join?.Invoke(room);
        public void HostRoom() => HostMatch?.Invoke();
        public void Exit() => Leave?.Invoke();
        public void SelectTeam(int team) => Team?.Invoke(team);
        public void ToggleReady() => SetReady?.Invoke(!Ready);
        public void StartGame() => StartMatch?.Invoke();
        public void RetryConnection() { if(CanReconnect) ReconnectRequested?.Invoke(); }
        // Only the networking host acts on this; anywhere else the request is ignored.
        public void Rematch() => RematchRequested?.Invoke();
    }
}
