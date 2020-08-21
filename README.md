
# What is Master Server Toolkit?
This is a framework that allows you to create game servers and services for your game inside Unity. This allows you to avoid using third-party services such as Playful, PAN, or Smartfox server. This framework does not claim to be a substitute for all these systems. No way! It offers to use free systems that you will pay for from others. Here are some of the free services that you are most likely to use in your game:

* Authentication module - is responsible for registration and authorization of the players in your game.
* Profiles module - is responsible for working with the profiles of your players.
* Game servers module - allows you to register running game servers in the system, so that users can see them in the list.
* Game servers spawner module - allows you to remotely launch game servers that may be located in different parts of the world.
* Chat module - allows you to create game chats and private chat channels.

Servers and modules communicate to each other via Networking API

# What is integrated networking API?
It's a layer of abstraction on top of networking technologies / protocols, that simplifies communication between servers and clients.
It allows you to easily start any number of socket servers (think "Photon Server", just without monthly subscriptions, CCU limits and with Linux support), which you can use to create any kind of server you want.
It's decoupled from master server, so you're free to write any kind of socket-based server you need.

# Is It For You?
As mentioned earlier, the purpose of the framework is to create servers, services, and microservices that will work in different parts of the world and perform the same task. With the built-in network API, you can create your own game server without having to use uNet, Mirror Networking, Forge Remastered, or other systems for creating online multiplayer games.
Servers and services that are created using the current framework can work locally, but you need to use a VPS or dedicated server for production.
We recommend you have your servers on a remote machine, not on a local machine. You can either have some pre-defined game servers running 24/7, or you can have a Spawner server "Spawn" a game server on clients request (this seamlessly imitates client hosting).
The concept of a framework already indicates the readiness of those who will use it to program using the API. The interface components used in the examples are only designed to demonstrate a particular tool. There is no need to use the interface at all to solve your problems. For example: you don't need an interface to get a list of game servers. You just need to make a request to get a list and use the response for your purposes.
There is no need to use everything in the framework. Use just the tools you need.