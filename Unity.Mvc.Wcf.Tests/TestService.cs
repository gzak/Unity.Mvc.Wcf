using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ServiceModel;
using System.ServiceModel.Description;

namespace Unity.Mvc.Wcf.Tests
{
    [ServiceContract]
    public interface ITestService
    {
        [OperationContract] string Ping();
        int Number { [OperationContract] get; [OperationContract] set; }
    }

    [ServiceBehavior(InstanceContextMode = InstanceContextMode.Single)]
    public class TestService : ITestService
    {
        public string Ping() { return "Hello World"; }
        public int Number { get; set; }
    }
}