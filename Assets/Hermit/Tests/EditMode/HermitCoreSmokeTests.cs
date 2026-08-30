using NUnit.Framework;
using UnityEngine;
using Hermit.Core;
using Hermit.Networking;

namespace Hermit.Tests.EditMode
{
    public class HermitCoreSmokeTests
    {
        [Test]
        public void HermitEnvironment_HasExactlyThreeValues()
        {
            var values = System.Enum.GetValues(typeof(HermitEnvironment));

            Assert.AreEqual(3, values.Length);
        }

        [Test]
        public void EnvironmentConfig_DefaultsToDevelopmentWithNoSecrets()
        {
            var config = ScriptableObject.CreateInstance<EnvironmentConfig>();

            Assert.AreEqual(HermitEnvironment.Development, config.Environment);
            Assert.IsEmpty(config.SupabaseUrl);
            Assert.IsEmpty(config.SupabaseAnonKey);

            UnityEngine.Object.DestroyImmediate(config);
        }

        [Test]
        public void HermitSession_DefaultsAreEmpty()
        {
            var session = new HermitSession();

            Assert.IsNull(session.AccessToken);
            Assert.IsNull(session.RefreshToken);
            Assert.AreEqual(0, session.ExpiresAtUnixSeconds);
        }
    }
}
