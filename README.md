# Unity.Mvc.Wcf

## What it does

The goal of this package is to minmize the cross-cutting
concern of using WCF services in your MVC controllers. Normally,
you have two options. You can add a WCF Service Reference
to your project, which is rather clunky as it adds a lot
of messy code generated files to your solution and requires
the service to be acitvely running somewhere, which may not
always be the case. In fact, many times the service hasn't
been written yet, just the contract. The other option is to
provide your own stub implementation which merely shuttles
parameters and results back and forth between the client
and the server of the service. This too is less than ideal
as it forces you to write code.

Furthermore, you can't use the service client as if it were a
pure interface. You have to always keep it in a `using` block
or at least remember to `Dispose()` the object when you're done.

With this package, all you need to do is have a contract defined
and some controllers which expect it as input. The framework will
generate a stub implementation (which is really all a Service Reference
does) on demand at runtime based on runtime configurations found in
your Web.config file (or based on programmatic configurations, if
you prefer that route). **The key is that it saves all this stub
generation until runtime so you can get on with your life and
work against the contract rather than wait for a dummy service
implementation to be hosted somewhere or manually write the stubs
yourself to decouple yourself from this scenario**.

## Installation

You're more than welcome to clone/fork a copy of this project and
build it from source yourself (contributions are always welcome),
but if you'd just like to quickly get going with it you may install
it via NuGet by running this command in the package manager console:

```
Install-Package Unity.Mvc.Wcf
```

The symbol source has also been published, so if you [configure VS](http://www.symbolsource.org/Public/Home/VisualStudio)
you should be able to step into all the source code and set breakpoints
as needed without having to download this project from GitHub explicitly.

## Examples

### The Setup

Suppose your WCF service contract looks like this...

```C#
[ServiceContract]
public interface IMyService
{
    [OperationContract] string Ping();
}
```

And your WCF service implemented the contract like below.
Let's also say it will be hosted at http://localhost:1234/MyService.svc
using basic HTTP binding...

```C#
public class MyService : IMyService
{
    public string Ping() { return "hello world"; }
}
```

Now suppose you have a controller in your MVC application
which takes an IMyService object in the constructor...

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
all of which would live somewhere in the global Application_Start() method.

### Programmatic configuration

You can register a client for the WCF service using
code constructs like BasicHttpBinding and EndpointAddress

```C#
BasicHttpBinding binding = new BasicHttpBinding();
EndpointAddress address = new EndpointAddress("http://localhost:1234/IMyService.svc");
container.RegisterWcfClientFor<IMyService>(binding, address);
```

### Web.config configuration

If you have bindings and endpoints configured in
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
can manually specify an instance of IProxyPool to manage this:

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
