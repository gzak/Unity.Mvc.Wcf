# Unity.Mvc.Wcf



Suppose your WCF service contract looks like this...

```C#
[ServiceContract]
public interface IMyService
{
    [OperationContract] string Ping();
}
```

And your WCF service implemented the contract like this.
Let's also say it was hosted at http://localhost:1234/MyService.svc
using basic HTTP binding

```C#
public class MyService : IMyService
{
    public string Ping() { return "hello world"; }
}
```

Now suppose you have a controller in your MVC application
which takes an IMyService object in the constructor
(for dependency injection). Notice the lack of any "using"
or "Dispose" logic. This package handles the lifecycle of
the service client object for you.

```C#
public class MyController : Controller
{
    private IMyService service;

    public MyController(IMyService service)
    {
        this.service = service;
    }

    public ActionResult MyAction()
    {
        return View("MyAction", service.Ping());
    }
}
```

Using this package, you can register various "implementations"
of that interface with Unity without using a clunky service reference
or writing an IMyService client that just shuttles parameters and
results back and forth for each method manually. Below are some examples,
all of which would live somewhere in the global Application_Start() method:



You can register a client for the WCF service using
code constructs like BasicHttpBinding and EndpointAddress

```C#
BasicHttpBinding binding = new BasicHttpBinding();
EndpointAddress address = new EndpointAddress("http://localhost:1234/IMyService.svc");
container.RegisterWcfClientFor<IMyService>(binding, address);
```

or, if you have bindings and endpoints configured in
your Web.config file (which is a better practice anyway),
you can register a client for the WCF service using
those configurations like this (suppose it's called
"BasicHttpBinding_IMyService" in your Web.config file):

```C#
container.RegisterWcfClientFor<IMyService>("BasicHttpBinding_IMyService");
```

In both cases above, the behavior would have been to establish
a new connection to the service, instantiate a client, and pass it
to the controller constructor for every new request, then when the
request is over the connection would automatically be closed and
the client disposed. Under the hood, these are equivalent to calling
`container.RegisterWcfClientFor(new UnlimitedProxyPool<IMyService>(...))`.

But suppose you want to cap the number of connections to the service
(say 15 connections max), and you want your app to block until
a connection becomes available if you exceed this limit, then you
can manually specify an instance of IProxyPool to manage this

```C#
LimitedProxyPool<IMyService> limited = new LimitedProxyPool<IMyService>(15, "BasicHttpBinding_IMyService");
container.RegisterWcfClientFor(limited);
```

All implementations of IProxyPool support the same general parameter types
as the registration method in their constructors (namely, manually specifying
a Binding and EndpointAddress as in the first example or a config name like
in the second example).

The above might not be good enough for you, since it doesn't reuse
connections but rather only sets a limit on how many you can have.
If you truly want a fixed, reusable pool of connections, this manager
will be more ideal:

```C#
PersistentProxyPool<IMyService> persistent = new PersistentProxyPool<IMyService>(15, binding, address);
container.RegisterWcfClientFor(persistent);
```

And if none of these suit your needs, you're always welcome to implement
your own version of the IProxyPool interface. Enjoy!