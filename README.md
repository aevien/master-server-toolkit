![Master Server Toolkit Logo](https://master-toolkit.com/media/th4iz2gx/msf_logo.jpg)

# What is Master Server Toolkit?
This is an independent, free and at the same time powerful solution to many of the problems you face when developing online multiplayer games and applications. Among those problems are:

### User registration and authorization

Every multiplayer game, keeps the accounts of players. And for this purpose, there are many services that in one way or another help the developer in solving this issue. MST solves this problem right out of the box. Using the built-in authorization module, you can quickly launch the ability to work with user accounts. Moreover, you can easily integrate MST with remote user registration and authorization services using the API provided by them.

### Lists of game servers like in Counter Strike, PUBG

Very often in multiplayer games, developers are faced with the problem of displaying active servers in the list, so that users can select the desired one and connect to it. MST solves this issue with just a few lines of code! Moreover, MST has a built-in system that helps you run game servers/rooms anywhere in the world.

### Managing of user data

Interaction with user data such as profiles, friends, clans, game currency, and more is also an integral part of multiplayer games. With built-in, flexible tools, you can easily solve this issue and achieve the necessary results. MST is able to perform instant synchronization of user data in real time at any change.

### Interaction with databases

Handling user information is one task, but storing this information is another. MST has the functionality to work with databases without connecting to external data storage sources. However, if you already use sources such as Amazon DynamoDB, GameSparks, or Azure Playlab, you can easily interact with their API using the built-in MST tools.

### Game chats and chat channels

It is quite often, an integral part of multiplayer games is a game chat. MST will also help resolve this issue. With the ready-made module, you can easily create your game chats and chat channels with the unlimited number of users.

### Cross-platform support

The MST client side runs on platforms such as Windows, Linux, MacOS, Android, iOS, and WebGL. The server part is supported by Windows, Linux, and Android platforms.

### Extending the functionality according to your needs

Not all of the features we can provide you with are described here. There is a separate solution for each individual problem, but most of them can be solved using MST. But if the functionality provided in MST is not enough for you, its modular system allows you to easily write extensions for your needs. With the help of modules, you can create services that will work together and still be located in different parts of the world.

### MST Networking API

It's a layer of abstraction on top of networking technologies / protocols, that simplifies communication between servers and clients. It's designed to be fast, convenient and very extendable - you can fine tune structure of your messages, change communication protocols, and none of your networking code would have to change.

The MS package provides demo scenes showing how to quickly and easily create a prototype of your multiplayer game or SOFTWARE, and the Discord channel will allow you to get help faster than on the forums.

### Is It For You?
1. As mentioned earlier, the purpose of the framework is to create servers, services, and microservices that will work in different parts of the world and perform the same task. With the built-in network API, you can create your own game server without having to use uNet, Mirror Networking, Forge Remastered, or other systems for creating online multiplayer games.
1. Servers and services that are created using the current framework can work locally, but you need to use a VPS or dedicated server for production.
1. I recommend you have your servers on a remote machine, not on a local machine. You can either have some pre-defined game servers running 24/7, or you can have a Spawner server "Spawn" a game server on clients request (this seamlessly imitates client hosting).
1. The concept of a framework already indicates the readiness of those who will use it to program using the API. The interface components used in the examples are only designed to demonstrate a particular tool. There is no need to use the interface at all to solve your problems. For example: you don't need an interface to get a list of game servers. You just need to make a request to get a list and use the response for your purposes.
1. There is no need to use everything the framework is providing you with. Use just the tools you need.
