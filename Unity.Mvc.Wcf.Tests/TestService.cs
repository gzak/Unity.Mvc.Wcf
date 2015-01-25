using System.ServiceModel;
using System.Threading.Tasks;

namespace Unity.Mvc.Wcf.Tests
{
    [ServiceContract]
    public interface ITestService1
    {
        [OperationContract] Task<string> Ping();
    }

    [ServiceContract]
    public interface ITestService2
    {
        int Number { [OperationContract] get; [OperationContract] set; }
    }

    [ServiceContract]
    public interface ITestService<T> : ITestService1, ITestService2
    {
        [OperationContract] Task<T> Generic();
    }

    [ServiceBehavior(InstanceContextMode = InstanceContextMode.Single)]
    public class TestService<T> : ITestService<T>
    {
        public Task<T> Generic() { return Task.FromResult(default(T)); }
        public Task<string> Ping() { return Task.FromResult("Hello World"); }
        public int Number { get; set; }
    }
}