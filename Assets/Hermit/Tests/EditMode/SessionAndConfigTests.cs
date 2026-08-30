using NUnit.Framework;
using UnityEngine;
using Hermit.Core;
using Hermit.Networking;

namespace Hermit.Tests.EditMode
{
    public class SessionAndConfigTests
    {
        [Test]
        public void SessionExpiry_ValidWhenFarInFuture()
        {
            var session = new HermitSession { ExpiresAtUnixSeconds = 10_000 };
            Assert.IsTrue(SessionExpiry.IsStillValid(session, nowUnixSeconds: 1_000));
        }

        [Test]
        public void SessionExpiry_InvalidWhenAlreadyPast()
        {
            var session = new HermitSession { ExpiresAtUnixSeconds = 1_000 };
            Assert.IsFalse(SessionExpiry.IsStillValid(session, nowUnixSeconds: 10_000));
        }

        [Test]
        public void SessionExpiry_InvalidWithinSafetyMargin()
        {
            // Expires 10s from "now" — inside the default 30s safety margin,
            // so a refresh should be triggered before it actually lapses.
            var session = new HermitSession { ExpiresAtUnixSeconds = 1_010 };
            Assert.IsFalse(SessionExpiry.IsStillValid(session, nowUnixSeconds: 1_000, safetyMarginSeconds: 30));
        }

        [Test]
        public void LocalFileSessionStore_RoundTripsSession()
        {
            var store = new LocalFileSessionStore();
            store.Clear();

            var session = new HermitSession
            {
                UserId = "user-123",
                AccessToken = "access-abc",
                RefreshToken = "refresh-xyz",
                ExpiresAtUnixSeconds = 42
            };

            store.Save(session);
            Assert.IsTrue(store.HasStoredSession);

            var loaded = store.Load();
            Assert.IsTrue(loaded.HasValue);
            Assert.AreEqual(session.UserId, loaded.Value.UserId);
            Assert.AreEqual(session.AccessToken, loaded.Value.AccessToken);
            Assert.AreEqual(session.RefreshToken, loaded.Value.RefreshToken);
            Assert.AreEqual(session.ExpiresAtUnixSeconds, loaded.Value.ExpiresAtUnixSeconds);

            store.Clear();
            Assert.IsFalse(store.HasStoredSession);
        }

        [Test]
        public void LocalFileSessionStore_LoadReturnsNull_WhenNothingStored()
        {
            var store = new LocalFileSessionStore();
            store.Clear();

            Assert.IsFalse(store.HasStoredSession);
            Assert.IsNull(store.Load());
        }

        [Test]
        public void HermitResult_Ok_CarriesValue()
        {
            var ok = HermitResult<int>.Ok(5);

            Assert.IsTrue(ok.Success);
            Assert.AreEqual(5, ok.Value);
        }

        [Test]
        public void HermitResult_Fail_CarriesError()
        {
            var error = new HermitError(HermitErrorKind.Network, "friendly", "technical");
            var fail = HermitResult<int>.Fail(error);

            Assert.IsFalse(fail.Success);
            Assert.AreEqual(HermitErrorKind.Network, fail.Error.Kind);
            Assert.AreEqual("friendly", fail.Error.UserMessage);
            Assert.AreEqual("technical", fail.Error.TechnicalDetail);
        }

        [Test]
        public void EnvironmentConfig_IsConfigured_FalseWhenEmpty()
        {
            var config = ScriptableObject.CreateInstance<EnvironmentConfig>();

            Assert.IsFalse(config.IsConfigured);

            Object.DestroyImmediate(config);
        }
    }
}
