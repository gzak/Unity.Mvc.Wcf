using System;
using System.Text;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.ServiceModel;
using System.ServiceModel.Description;
using Microsoft.Practices.Unity;
using System.Linq;

namespace Unity.Mvc.Wcf.Tests
{
    /// <summary>
    /// Summary description for UnitTest1
    /// </summary>
    [TestClass]
    public class ServiceUnitTests
    {
        private static readonly Uri baseAddress = new Uri("http://localhost:8080/Basic");

        public ServiceUnitTests()
        {
            //
            // TODO: Add constructor logic here
            //
        }

        private TestContext testContextInstance;

        /// <summary>
        ///Gets or sets the test context which provides
        ///information about and functionality for the current test run.
        ///</summary>
        public TestContext TestContext
        {
            get
            {
                return testContextInstance;
            }
            set
            {
                testContextInstance = value;
            }
        }

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
                            (client as IDisposable).Dispose();
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
                            using (client as IDisposable)
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
                            using (client as IDisposable)
                            {
                                client.Number = i;
                                Assert.AreEqual(client.Number, i);
                            }
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
            return (new UnityContainer())
                .RegisterWcfClientFor<ITestService>(
                    new BasicHttpBinding(),
                    new EndpointAddress(baseAddress));
        }
    }
}