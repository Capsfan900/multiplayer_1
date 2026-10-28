using System;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Photon.Realtime;
using Wildstone.Networking;

namespace Wildstone.Tests
{
    public class PlayFabIdentityTests
    {
        const string App = "11111111-1111-1111-1111-111111111111";

        [TestCase("")]
        [TestCase("ABC.playfabapi.com/evil")]
        [TestCase("ABC\n")]
        [TestCase("https://ABC")]
        public void InvalidTitleCannotSelectAnEndpoint(string title)
        {
            Assert.Throws<ArgumentException>(() => new PlayFabIdentity(title, App));
        }

        [Test]
        public void InvalidPhotonConfigurationIsRejected()
        {
            Assert.Throws<ArgumentException>(() => new PlayFabIdentity("AB12", ""));
            Assert.Throws<ArgumentException>(() => new PlayFabIdentity("AB12", Guid.Empty.ToString()));
        }

        [TestCase("null")]
        [TestCase("{}")]
        [TestCase("garbage")]
        [TestCase("{\"code\":400,\"status\":\"BadRequest\",\"errorMessage\":\"private credentials\"}")]
        public void InvalidResponsesFailWithoutLeakingResponse(string json)
        {
            var error = Assert.Throws<InvalidOperationException>(() => PlayFabIdentity.ParseResponse(json));
            Assert.That(error.Message, Does.Not.Contain("private credentials"));
            Assert.That(error.InnerException, Is.Null);
        }

        [TestCase("{\"code\":200,\"status\":\"OK\"}")]
        [TestCase("{\"code\":200,\"status\":\"OK\",\"data\":{}}")]
        public void MissingDataCannotProduceAuthenticatedIdentity(string json)
        {
            var data = PlayFabIdentity.ParseResponse(json);
            Assert.Throws<InvalidOperationException>(() => PlayFabIdentity.CompleteLogin(data,
                new PlayFabIdentity.ResponseData { PhotonCustomAuthenticationToken = "token" }));
            Assert.Throws<InvalidOperationException>(() => PlayFabIdentity.CompleteLogin(
                new PlayFabIdentity.ResponseData { PlayFabId = "ABC" }, data));
        }
        [Test]
        public void ProviderIdAndTokenArePairedAndProfileComesFromResponse()
        {
            var account = PlayFabIdentity.ParseResponse("{\"code\":200,\"status\":\"OK\",\"data\":{\"PlayFabId\":\"ABC123\",\"InfoResultPayload\":{\"AccountInfo\":{\"TitleInfo\":{\"DisplayName\":\"Verified Name\"}}}}}");
            var token = PlayFabIdentity.ParseResponse("{\"code\":200,\"status\":\"OK\",\"data\":{\"PhotonCustomAuthenticationToken\":\"token+value\"}}");
            var login = PlayFabIdentity.CompleteLogin(account, token);
            Assert.That(login.PlayFabId, Is.EqualTo("ABC123"));
            Assert.That(login.DisplayName, Is.EqualTo("Verified Name"));
            Assert.That(login.Authentication.AuthType, Is.EqualTo(CustomAuthenticationType.Custom));
            Assert.That(login.Authentication.AuthGetParameters, Is.EqualTo("username=ABC123&token=token%2Bvalue"));
        }

        [Test]
        public void MissingProfileStaysEmptyAndMissingTokenCannotAuthenticate()
        {
            var account = new PlayFabIdentity.ResponseData { PlayFabId = "ABC" };
            Assert.Throws<InvalidOperationException>(() => PlayFabIdentity.CompleteLogin(account, new PlayFabIdentity.ResponseData()));
            var login = PlayFabIdentity.CompleteLogin(account, new PlayFabIdentity.ResponseData { PhotonCustomAuthenticationToken = "token" });
            Assert.That(login.DisplayName, Is.Empty);
        }

        [Test]
        public void CancellationStopsAllOperationsBeforeAnyRequest()
        {
            var identity = new PlayFabIdentity("AB12", App);
            var cancellation = new CancellationToken(true);
            Assert.ThrowsAsync(Is.InstanceOf<OperationCanceledException>(), async () => await identity.LoginAsync("a@example.com", "secret", cancellation));
            Assert.ThrowsAsync(Is.InstanceOf<OperationCanceledException>(), async () => await identity.RegisterAsync("a@example.com", "secret", "Name", cancellation));
            Assert.ThrowsAsync(Is.InstanceOf<OperationCanceledException>(), async () => await identity.SendRecoveryEmailAsync("a@example.com", cancellation));
        }

        [Test]
        public void InvalidInputsFailBeforeAnyRequest()
        {
            var identity = new PlayFabIdentity("AB12", App);
            Assert.ThrowsAsync<ArgumentException>(async () => await identity.LoginAsync("", "secret"));
            Assert.ThrowsAsync<ArgumentException>(async () => await identity.RegisterAsync("a@example.com", "secret", "x"));
            Assert.ThrowsAsync<ArgumentException>(async () => await identity.SendRecoveryEmailAsync("a\r\nb"));
        }
    }
}


