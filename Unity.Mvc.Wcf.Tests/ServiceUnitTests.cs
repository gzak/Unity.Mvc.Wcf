using System;
using System.Text;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.ServiceModel;
using System.ServiceModel.Description;
using Microsoft.Practices.Unity;
using System.Linq;
using System.Threading;
using System.Reflection;

namespace Unity.Mvc.Wcf.Tests
{
    /// <summary>
    /// NOTE: These unit tests require you to run VS as an administrator
    /// </summary>
    [TestClass]
    public class ServiceUnitTests
    {
        private static readonly Uri baseAddress = new Uri("http://localhost:8080/Basic");

        [TestMethod]
        public void ContainerDisposeTest()
        {
            try
            {
                using (var container = initContainer())
                {
                    foreach (var i in Enumerable.Range(0, 10))
                    {
                        using (var child = container.CreateChildContainer())
                        {
                            var client = child.Resolve<ITestService>();
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Assert.Fail("Exception seen: {0}", e.Message);
            }
        }

        [TestMethod]
        public void CallMethod()
        {
            using (var host = initHost())
            {
                using (var container = initContainer())
                {
                    foreach (int i in Enumerable.Range(0, 10))
                    {
                        using (var child = container.CreateChildContainer())
                        {
                            var client = child.Resolve<ITestService>();
                            Assert.AreEqual(client.Ping(), "Hello World");
                        }
                    }
                }
            }
        }

        [TestMethod]
        public void PropertyGetterSetter()
        {
            using (var host = initHost())
            {
                using (var container = initContainer())
                {
                    foreach (var i in Enumerable.Range(0, 10))
                    {
                        using (var child = container.CreateChildContainer())
                        {
                            var client = child.Resolve<ITestService>();
                            client.Number = i;
                            Assert.AreEqual(client.Number, i);
                        }
                    }
                }
            }
        }

        [TestMethod]
        public void LimitedPool()
        {
            using (var host = initHost())
            {
                using (var container = initContainer(new LimitedProxyPool<ITestService>(
                    1,
                    new BasicHttpBinding(),
                    new EndpointAddress(baseAddress))))
                {
                    using (var child1 = container.CreateChildContainer())
                    {
                        var blockCheck = false;
                        var client1 = child1.Resolve<ITestService>();
                        var t = new Thread(() => {
                            using (var child2 = container.CreateChildContainer())
                            {
                                var client2 = child2.Resolve<ITestService>();
                                blockCheck = true;
                            }
                        });
                        t.Start();

                        Thread.Sleep(TimeSpan.FromSeconds(1));
                        Assert.AreEqual(client1.Ping(), "Hello World");
                        Assert.IsFalse(blockCheck);

                        (client1 as IDisposable).Dispose();
                        t.Join();

                        Assert.IsTrue(blockCheck);
                    }
                }
            }
        }

        [TestMethod]
        public void PersistentPool()
        {
            using (var host = initHost())
            {
                using (var container = initContainer(new PersistentClientPool<ITestService>(
                    1,
                    new BasicHttpBinding(),
                    new EndpointAddress(baseAddress))))
                {
                    using (var child1 = container.CreateChildContainer())
                    {
                        using (var child2 = container.CreateChildContainer())
                        {
                            var client1 = child1.Resolve<ITestService>();
                            Assert.AreEqual(client1.Ping(), "Hello World");
                            (client1 as IDisposable).Dispose();

                            var client2 = child2.Resolve<ITestService>();
                            Assert.AreEqual(client2.Ping(), "Hello World");

                            Assert.AreEqual(new ClientComparer { Client = client1 },
                                            new ClientComparer { Client = client2 });
                        }
                    }
                }
            }
        }

        #region Additional test attributes
        //
        // You can use the following additional attributes as you write your tests:
        //
        // Use ClassInitialize to run code before running the first test in the class
        // [ClassInitialize()]
        // public static void MyClassInitialize(TestContext testContext) { }
        //
        // Use ClassCleanup to run code after all tests in a class have run
        // [ClassCleanup()]
        // public static void MyClassCleanup() { }
        //
        // Use TestInitialize to run code before running each test 
        // [TestInitialize()]
        // public void MyTestInitialize() { }
        //
        // Use TestCleanup to run code after each test has run
        // [TestCleanup()]
        // public void MyTestCleanup() { }
        //
        #endregion

        private static ServiceHost initHost()
        {
            var host = new ServiceHost(typeof(TestService), baseAddress);
            var smb = new ServiceMetadataBehavior { HttpGetEnabled = true };
            smb.MetadataExporter.PolicyVersion = PolicyVersion.Policy15;
            host.Description.Behaviors.Add(smb);
            host.Open();
            return host;
        }

        private static IUnityContainer initContainer()
        {
            return initContainer(new UnlimitedProxyPool<ITestService>(
                new BasicHttpBinding(),
                new EndpointAddress(baseAddress)));
        }

        private static IUnityContainer initContainer(IProxyPool<ITestService> proxyPool)
        {
            return (new UnityContainer()).RegisterWcfClientFor<ITestService>(proxyPool);
        }

        private class ClientComparer
        {
            public ITestService Client { get; set; }

            public override bool Equals(object obj)
            {
                var client = (obj as ClientComparer).Client;
                var type = client.GetType();
                var poolField = type.GetField("pool", BindingFlags.NonPublic | BindingFlags.Instance);
                var clientField = type.GetField("client", BindingFlags.NonPublic | BindingFlags.Instance);

                return poolField.GetValue(client) == poolField.GetValue(Client)
                    && clientField.GetValue(client) == clientField.GetValue(Client);
            }
        }
    }
}