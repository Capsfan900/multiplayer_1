using Fusion;
using NUnit.Framework;
using Wildstone.Networking;
using Wildstone.Session;
using Assert = NUnit.Framework.Assert;

namespace Wildstone.Tests
{
    public sealed class FusionLobbyAdmissionTests
    {
        static ContentVersion Content => new ContentVersion("p", "b", "map", "r");
        static byte[] Credential(byte value) { var result = new byte[32]; result[0] = value; return result; }
        static SessionState Lobby()
        {
            var rules = new SessionState(Content, "test");
            Assert.That(rules.BeginConnect(0), Is.True);
            Assert.That(rules.EnterLobby(rules.AttemptGeneration, Content, 0), Is.True);
            return rules;
        }
        [Test] public void DuplicateIdentityCannotTakeOverAndRetiredPeerCannotDisconnectReconnect()
        {
            var rules = Lobby(); var admission = new FusionLobbyAdmission(rules, Content);
            var original = PlayerRef.FromIndex(0); var duplicate = PlayerRef.FromIndex(1); var reconnected = PlayerRef.FromIndex(2);
            Assert.That(admission.Admit(original, "verified", Credential(1), 0), Is.True);
            Assert.That(admission.Admit(duplicate, "verified", Credential(1), 1), Is.False);
            Assert.That(admission.Disconnect(original, 2), Is.True);
            Assert.That(admission.Admit(duplicate, "verified", Credential(2), 3), Is.False, "A spoofed anonymous ID without its reconnect secret cannot take a held seat.");
            Assert.That(admission.Admit(reconnected, "verified", Credential(1), 4), Is.True);
            Assert.That(admission.Disconnect(original, 5), Is.False);
            Assert.That(rules.CaptureSeats(5).Seats[0].Connected, Is.True);
            Assert.That(admission.TryGetBinding(original, out _), Is.False);
            Assert.That(admission.TryGetBinding(reconnected, out var seat), Is.True);
            Assert.That(seat.ConnectionGeneration, Is.GreaterThan(1));
            Assert.That(rules.SetReady("verified", 1, true), Is.False);
        }
        [Test] public void DuplicateSdkJoinCallbackIsIdempotentOnlyForTheCurrentBoundPeer()
        {
            var rules=Lobby(); var admission=new FusionLobbyAdmission(rules,Content); var player=PlayerRef.FromIndex(0);
            Assert.That(admission.Admit(player,"verified",Credential(1),0),Is.True);
            admission.TryGetBinding(player,out var first);
            Assert.That(rules.SetReady("verified",first.ConnectionGeneration,true),Is.True);
            Assert.That(admission.Admit(player,"verified",Credential(1),1),Is.True);
            Assert.That(admission.Admit(player,"different-account",Credential(1),1),Is.False);
            Assert.That(admission.Admit(player,"verified",Credential(2),1),Is.False);
            Assert.That(admission.Admit(PlayerRef.FromIndex(1),"verified",Credential(1),1),Is.False);
            var snapshot=rules.CaptureSeats(1);
            Assert.That(snapshot.Seats.Length,Is.EqualTo(1)); Assert.That(snapshot.Seats[0].Ready,Is.True);
            Assert.That(snapshot.LastConnectionGeneration,Is.EqualTo(first.ConnectionGeneration));
            Assert.That(admission.Disconnect(player,2),Is.True);
            Assert.That(admission.Admit(PlayerRef.FromIndex(1),"verified",Credential(1),3),Is.True);
            Assert.That(admission.Admit(player,"verified",Credential(1),4),Is.False,"Retired SDK callback cannot take over the rebound account.");
        }
        [Test] public void ClientChosenPhotonIdAloneCannotTakeAnotherGuestsHeldSeat()
        {
            const string guestId="11111111111141118111111111111111";
            var rules=Lobby(); var admission=new FusionLobbyAdmission(rules,Content);
            Assert.That(admission.Admit(PlayerRef.FromIndex(0),guestId,Credential(1),0),Is.True);
            Assert.That(admission.Admit(PlayerRef.FromIndex(1),guestId,Credential(2),1),Is.False);
            Assert.That(admission.Disconnect(PlayerRef.FromIndex(0),2),Is.True);
            Assert.That(admission.Admit(PlayerRef.FromIndex(1),guestId,Credential(2),3),Is.False,"Knowing a client-chosen Photon ID is not possession of its seat.");
            Assert.That(admission.Admit(PlayerRef.FromIndex(2),guestId,Credential(1),4),Is.True);
            Assert.That(rules.CaptureSeats(4).Seats.Length,Is.EqualTo(1));
        }
        [Test] public void EightSeatsAreSerializedAndGenerationsIncreaseAcrossDifferentAccounts()
        {
            var rules = Lobby(); var admission = new FusionLobbyAdmission(rules, Content);
            long previous = 0;
            for (int i = 0; i < SeatTable.Capacity; i++)
            {
                var player = PlayerRef.FromIndex(i);
                Assert.That(admission.Admit(player, "verified-" + i, Credential((byte)i), 0), Is.True);
                Assert.That(admission.TryGetBinding(player, out var seat), Is.True);
                Assert.That(seat.ConnectionGeneration, Is.GreaterThan(previous)); previous = seat.ConnectionGeneration;
            }
            Assert.That(admission.Admit(PlayerRef.FromIndex(9), "ninth", Credential(9), 0), Is.False);
            var snapshot = rules.CaptureSeats(0);
            Assert.That(snapshot.Seats.Length, Is.EqualTo(8));
            for (int team = 0; team < 4; team++)
                Assert.That(System.Array.FindAll(snapshot.Seats, s => s.Team == team).Length, Is.EqualTo(2));
        }
        [Test] public void ReconnectHoldExpiresAndMalformedBindingsFailClosed()
        {
            var rules = Lobby(); var admission = new FusionLobbyAdmission(rules, Content); var player = PlayerRef.FromIndex(0);
            Assert.That(admission.Admit(PlayerRef.None, "verified", Credential(1), 0), Is.False);
            Assert.That(admission.Admit(player, "verified", new byte[31], 0), Is.False);
            Assert.That(admission.Admit(player, "bad\nidentity", Credential(1), 0), Is.False);
            Assert.That(admission.Admit(player, "verified", Credential(1), 0), Is.True);
            Assert.That(admission.Disconnect(player, 1), Is.True);
            Assert.That(admission.Admit(PlayerRef.FromIndex(1), "verified", Credential(2), 31), Is.True,
                "Expired seat is a fresh anonymous lobby admission.");
            Assert.That(rules.CaptureSeats(31).Seats[0].ConnectionGeneration, Is.GreaterThan(1));
        }
        // Every refusal names a fixed reason (logged by the host when it removes the peer); success clears it.
        [Test] public void RefusedAdmissionsNameAFixedReason()
        {
            var rules=Lobby(); var admission=new FusionLobbyAdmission(rules,Content);
            Assert.That(admission.Admit(PlayerRef.None,"a",Credential(1),0),Is.False); Assert.That(admission.Rejection,Is.EqualTo("invalid-peer"));
            Assert.That(admission.Admit(PlayerRef.FromIndex(0),"a",Credential(1),0),Is.True); Assert.That(admission.Rejection,Is.Null);
            Assert.That(admission.Admit(PlayerRef.FromIndex(0),"other",Credential(1),0),Is.False); Assert.That(admission.Rejection,Is.EqualTo("rebind-mismatch"));
            Assert.That(admission.Admit(PlayerRef.FromIndex(1),"a",Credential(1),0),Is.False); Assert.That(admission.Rejection,Is.EqualTo("seat-already-connected"));
            Assert.That(admission.Disconnect(PlayerRef.FromIndex(0),1),Is.True);
            Assert.That(admission.Admit(PlayerRef.FromIndex(1),"a",Credential(2),2),Is.False); Assert.That(admission.Rejection,Is.EqualTo("seat-credential-mismatch"));
            for(int i=1;i<SeatTable.Capacity;i++) Assert.That(admission.Admit(PlayerRef.FromIndex(i+1),"p"+i,Credential((byte)(i+1)),3),Is.True);
            Assert.That(admission.Admit(PlayerRef.FromIndex(20),"ninth",Credential(20),3),Is.False); Assert.That(admission.Rejection,Is.EqualTo("teams-full"));
            var loading=Lobby(); var late=new FusionLobbyAdmission(loading,Content);
            late.Admit(PlayerRef.FromIndex(0),"a",Credential(1),0); late.Admit(PlayerRef.FromIndex(1),"b",Credential(2),0);
            foreach(var seat in loading.CaptureSeats(0).Seats) loading.SetReady(seat.VerifiedSubject,seat.ConnectionGeneration,true);
            Assert.That(loading.StartLoading(0),Is.True);
            Assert.That(late.Admit(PlayerRef.FromIndex(2),"c",Credential(3),0),Is.False); Assert.That(late.Rejection,Is.EqualTo("phase-Loading"));
        }
        static FusionLobbyAdmission Running(out SessionState rules)
        {
            rules=Lobby(); var admission=new FusionLobbyAdmission(rules,Content);
            admission.Admit(PlayerRef.FromIndex(0),"a",Credential(1),0);
            admission.Admit(PlayerRef.FromIndex(1),"b",Credential(2),0);
            var seats=rules.CaptureSeats(0).Seats;
            foreach(var seat in seats) rules.SetReady(seat.VerifiedSubject,seat.ConnectionGeneration,true);
            Assert.That(rules.StartLoading(0),Is.True);
            foreach(var seat in seats) rules.SetLoaded(seat.VerifiedSubject,seat.ConnectionGeneration);
            Assert.That(rules.FinishLoading(0),Is.True); rules.Tick(.15);
            Assert.That(rules.Phase,Is.EqualTo(SessionPhase.Running)); return admission;
        }
        [Test] public void LateJoinAndReconnectRequireFreshLoadedAcknowledgement()
        {
            var admission=Running(out var rules); var late=PlayerRef.FromIndex(2);
            Assert.That(admission.Admit(late,"late",Credential(3),.2),Is.True);
            admission.TryGetBinding(late,out var binding);
            var seat=System.Array.Find(rules.CaptureSeats(.2).Seats,s=>s.VerifiedSubject=="late");
            Assert.That(seat.Ready,Is.False);
            Assert.That(rules.SetReady("late",binding.ConnectionGeneration,true),Is.False,"Lobby ready command cannot acknowledge a running map.");
            Assert.That(rules.SetLoaded("late",binding.ConnectionGeneration-1),Is.False);
            Assert.That(rules.SetLoaded("late",binding.ConnectionGeneration),Is.True);
            Assert.That(admission.Disconnect(late,.3),Is.True);
            Assert.That(admission.Admit(PlayerRef.FromIndex(3),"late",Credential(3),.4),Is.True);
            var reconnected=System.Array.Find(rules.CaptureSeats(.4).Seats,s=>s.VerifiedSubject=="late");
            Assert.That(reconnected.Ready,Is.False,"Reconnect cannot retain an old map acknowledgement.");
            Assert.That(reconnected.ConnectionGeneration,Is.GreaterThan(binding.ConnectionGeneration));
            Assert.That(rules.SetLoaded("late",binding.ConnectionGeneration),Is.False);
            Assert.That(rules.SetLoaded("late",reconnected.ConnectionGeneration),Is.True);
        }
        [Test] public void ExpiredRunningSeatCanServeAnotherAnonymousPeerWithNewGeneration()
        {
            var admission=Running(out var rules);
            for(int i=2;i<8;i++) Assert.That(admission.Admit(PlayerRef.FromIndex(i),"account-"+i,Credential((byte)i),.2),Is.True);
            var original=System.Array.Find(rules.CaptureSeats(.2).Seats,s=>s.VerifiedSubject=="a");
            Assert.That(admission.Disconnect(PlayerRef.FromIndex(0),.3),Is.True);
            Assert.That(admission.Admit(PlayerRef.FromIndex(9),"replacement",Credential(9),30.3),Is.True);
            var replacement=System.Array.Find(rules.CaptureSeats(30.3).Seats,s=>s.VerifiedSubject=="replacement");
            Assert.That(replacement.Team,Is.EqualTo(original.Team)); Assert.That(replacement.Slot,Is.EqualTo(original.Slot));
            Assert.That(replacement.ConnectionGeneration,Is.GreaterThan(8)); Assert.That(replacement.Ready,Is.False);
            Assert.That(admission.Disconnect(PlayerRef.FromIndex(0),30.4),Is.False);
            Assert.That(rules.CaptureSeats(30.4).Seats.Length,Is.EqualTo(8));
        }
        [Test] public void MigratedAdmissionRestoresOnlyCredentialHashesAndRequiresSdkPeerBindingAgain()
        {
            var original=Running(out var rules);
            var checkpoint=rules.CaptureCheckpoint(10,.2);
            var wire=new FusionRecoverySeat[checkpoint.Seats.Seats.Length];
            for(int i=0;i<wire.Length;i++)
            {
                var seat=checkpoint.Seats.Seats[i];
                Assert.That(original.TryGetCredentialHash(seat.VerifiedSubject,out var hash),Is.True);
                wire[i]=FusionRecoverySeat.FromSeat(seat,hash);
            }
            Assert.That(rules.BeginMigration(10,120,.3),Is.True);
            Assert.That(rules.RestoreCheckpoint(checkpoint,.3),Is.True);
            var restored=new FusionLobbyAdmission(rules,Content);
            Assert.That(restored.RestoreCredentials(new[]{wire[0]},.3),Is.False,"Incomplete credential state cannot partly install.");
            Assert.That(restored.RestoreCredentials(wire,.3),Is.True);
            Assert.That(restored.TryGetBinding(PlayerRef.FromIndex(0),out _),Is.False,"Retired runner peers cannot survive migration.");
            Assert.That(restored.Admit(PlayerRef.FromIndex(3),null,Credential(1),.4),Is.False);
            Assert.That(restored.Admit(PlayerRef.FromIndex(3),"a",Credential(9),.4),Is.False);
            Assert.That(restored.Admit(PlayerRef.FromIndex(3),"a",Credential(1),.4),Is.True);
            Assert.That(restored.IsFreshAdmission(PlayerRef.FromIndex(3)),Is.False);
            Assert.That(restored.TryGetBinding(PlayerRef.FromIndex(3),out var rebound),Is.True);
            Assert.That(rebound.ConnectionGeneration,Is.GreaterThan(checkpoint.Seats.LastConnectionGeneration));
            Assert.That(rebound.Ready,Is.False);
            Assert.That(rules.SetLoaded("a",rebound.ConnectionGeneration),Is.True,"Resumed clients acknowledge content while warmup is gated.");
            rules.Tick(.6);
            Assert.That(rules.Phase,Is.EqualTo(SessionPhase.Warmup),"One rebound team cannot resume authority.");
            Assert.That(restored.Admit(PlayerRef.FromIndex(4),"b",Credential(2),.6),Is.True);
            restored.TryGetBinding(PlayerRef.FromIndex(4),out var other);
            rules.Tick(.8); Assert.That(rules.Phase,Is.EqualTo(SessionPhase.Warmup),"All connected clients must acknowledge content before recovery resumes.");
            Assert.That(rules.SetLoaded("b",other.ConnectionGeneration),Is.True);
            rules.Tick(.8); Assert.That(rules.Phase,Is.EqualTo(SessionPhase.Running));
        }
        [Test] public void ReboundLifeExpiresOnOriginalDeadlineWhileSimulationIsPaused()
        {
            var admission=Running(out var rules);
            Assert.That(admission.Disconnect(PlayerRef.FromIndex(0),.2),Is.True);
            Assert.That(admission.Admit(PlayerRef.FromIndex(2),"a",Credential(1),.3),Is.True);
            Assert.That(admission.RequiresFreshLife(PlayerRef.FromIndex(2),5.199),Is.False);
            Assert.That(admission.Admit(PlayerRef.FromIndex(2),"a",Credential(1),4),Is.True,"Repeated SDK callback cannot extend vulnerability.");
            Assert.That(admission.RequiresFreshLife(PlayerRef.FromIndex(2),5.2),Is.True);
            Assert.That(admission.Disconnect(PlayerRef.FromIndex(2),4.1),Is.True);
            Assert.That(admission.Admit(PlayerRef.FromIndex(3),"a",Credential(1),4.2),Is.True);
            Assert.That(admission.RequiresFreshLife(PlayerRef.FromIndex(3),5.2),Is.True,"Socket retry cannot restart the original vulnerability grace.");
            Assert.That(admission.RequiresFreshLife(PlayerRef.FromIndex(3),20),Is.True,"Loading may pause model ticks, never the original host-clock deadline.");
            admission.MarkControlRestored(PlayerRef.FromIndex(3));
            Assert.That(admission.Disconnect(PlayerRef.FromIndex(3),20),Is.True);
            Assert.That(admission.Admit(PlayerRef.FromIndex(4),"a",Credential(1),20.1),Is.True);
            Assert.That(admission.RequiresFreshLife(PlayerRef.FromIndex(4),24.99),Is.False,"A later actual disconnect gets its own five-second grace after control was restored.");
            Assert.That(admission.RequiresFreshLife(PlayerRef.FromIndex(4),25),Is.True);
            Assert.That(admission.RequiresFreshLife(PlayerRef.FromIndex(1),20),Is.True,"A fresh admission still requires starter life.");
        }
        [Test] public void PendingReconnectMigrationRetainsTheFirstVulnerabilityDeadline()
        {
            var admission=Running(out var rules);
            Assert.That(admission.Disconnect(PlayerRef.FromIndex(0),.2),Is.True);
            Assert.That(admission.Admit(PlayerRef.FromIndex(2),"a",Credential(1),.3),Is.True);
            var checkpoint=rules.CaptureCheckpoint(10,.4);
            var wire=new FusionRecoverySeat[checkpoint.Seats.Seats.Length];
            for(int i=0;i<wire.Length;i++)
            {
                var seat=checkpoint.Seats.Seats[i]; admission.TryGetCredentialHash(seat.VerifiedSubject,out var hash);
                wire[i]=FusionRecoverySeat.FromSeat(seat,hash,vulnerableUntil:admission.GetVulnerableUntil(seat.VerifiedSubject));
            }
            Assert.That(rules.BeginMigration(10,120,.5),Is.True); Assert.That(rules.RestoreCheckpoint(checkpoint,.5),Is.True);
            var restored=new FusionLobbyAdmission(rules,Content);
            Assert.That(restored.RestoreCredentials(wire,.5),Is.True);
            Assert.That(restored.GetVulnerableUntil("a"),Is.EqualTo(5.2));
            Assert.That(restored.Admit(PlayerRef.FromIndex(3),"a",Credential(1),1),Is.True);
            Assert.That(restored.RequiresFreshLife(PlayerRef.FromIndex(3),5.199),Is.False);
            Assert.That(restored.RequiresFreshLife(PlayerRef.FromIndex(3),5.2),Is.True,"Migration cannot restart grace from its later DisconnectedAt.");
            Assert.That(restored.GetVulnerableUntil("a"),Is.EqualTo(5.2));
        }
        [Test] public void ContentTokenAloneCannotAuthorizeAdmissionAndEveryVersionIsPinned()
        {
            var token = new byte[64]; var hash = FusionSession.ContentToken(Content);
            System.Array.Copy(hash, token, hash.Length); System.Array.Copy(Credential(7), 0, token, 32, 32);
            Assert.That(FusionSession.TryReadCredential(hash, Content, out _), Is.False);
            Assert.That(FusionSession.TryReadCredential(token, Content, out var credential), Is.True);
            CollectionAssert.AreEqual(Credential(7), credential);
            Assert.That(FusionSession.TryReadCredential(token, new ContentVersion("p", "different", "map", "r"), out _), Is.False);
            var rules = Lobby(); var admission = new FusionLobbyAdmission(rules, Content);
            Assert.That(admission.Admit(PlayerRef.FromIndex(0), null, credential, 0), Is.False,
                "A valid content token and credential still require an SDK-bound anonymous peer ID.");
        }
        [Test] public void TeamAndReadyCommandsRequireCurrentBindingAndStartRequiresTwoReadyTeams()
        {
            var rules = Lobby(); var admission = new FusionLobbyAdmission(rules, Content);
            var a = PlayerRef.FromIndex(0); var b = PlayerRef.FromIndex(1);
            Assert.That(admission.Admit(a, "a", Credential(1), 0), Is.True);
            Assert.That(admission.Admit(b, "b", Credential(2), 0), Is.True);
            admission.TryGetBinding(a, out var sa); admission.TryGetBinding(b, out var sb);
            Assert.That(rules.StartLoading(0), Is.False);
            Assert.That(rules.SetReady("a", sb.ConnectionGeneration, true), Is.False);
            Assert.That(rules.SetReady("a", sa.ConnectionGeneration, true), Is.True);
            Assert.That(rules.SetReady("b", sb.ConnectionGeneration, true), Is.True);
            Assert.That(rules.SelectTeam("b", sb.ConnectionGeneration, 0), Is.True);
            Assert.That(rules.StartLoading(0), Is.False);
            Assert.That(rules.SelectTeam("b", sb.ConnectionGeneration, 1), Is.True);
            Assert.That(rules.StartLoading(0), Is.False, "Team change clears readiness.");
            Assert.That(rules.SetReady("b", sb.ConnectionGeneration, true), Is.True);
            Assert.That(rules.StartLoading(0), Is.True);
            Assert.That(rules.SelectTeam("a", sa.ConnectionGeneration, 2), Is.False);
        }
    }
}
