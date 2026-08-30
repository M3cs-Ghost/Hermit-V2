using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Hermit.Runtime;

namespace Hermit.Tests.PlayMode
{
    public class HermitBootstrapPlayModeTests
    {
        [UnityTest]
        public IEnumerator HermitBootstrap_SetsIsInitialized_OnAwake()
        {
            var go = new GameObject("HermitBootstrap_Test");
            go.AddComponent<HermitBootstrap>();

            yield return null;

            Assert.IsTrue(HermitBootstrap.IsInitialized);

            Object.Destroy(go);
        }
    }
}
