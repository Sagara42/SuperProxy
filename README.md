[![CodeFactor](https://www.codefactor.io/repository/github/sagara42/superproxy/badge)](https://www.codefactor.io/repository/github/sagara42/superproxy)

# Super Proxy
Network communication based on RMI/RPC between clients (nodes)

## Functional
Any node can host own object to provide inside functions for all another node`s
Also, node`s can subscribe on remote events

## How it works (examples)

### Server side:

```C#
var spServer = new SpServer();
spServer.Initialize("127.0.0.1", 6669, 5, 15, 3);
```

### Client(node 1 example) side:

```C#
var spClient = new SpClient(new TestHostedObject(), "someChannel"); //here we install self hosted object with shared function`s on channel
spClient.Connect("127.0.0.1", 6669);
spClient.Subscribe("testChannel" (action) =>
{
//event callback...
});

public class TestHostedObject
{
  [SpMessage] //that attribute required for all shared function`s
  public void Foo()
  {
    //foo callback...
  }
}
```

### Client(node 2 example) side:

```C#
var spClient = new SpClient(new TestHostedObject(), "someChannel"); //here we install self hosted object with shared function`s on channel
spClient.Connect("127.0.0.1", 6669);
spClient.Publish("testChannel" /*some argumets here*/); //that callback will receive all subsribed clients on 'testChannel'

spClient.RemoteCall("someChannel", "Foo");
```

### Used libraries:
NLog
MessagePack serializer

Some parts used from NLC Framework (delegate serialization)
NLC: https://github.com/ImVexed/NotLiteCode
