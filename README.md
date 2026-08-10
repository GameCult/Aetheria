# Aetheria
Aetheria is an ambitious (AA quality) open source tactical action role-playing game with a sprawling sci-fi setting that combines stunning visuals with unprecedented simulation depth while maintaining accessibility.

All mechanics are developed to be engine-agnostic, while Unity was chosen as a renderer and editor environment for its accessibility and extensibility. Many custom features have been developed, such as a database frontend with configurable backing store for managing game content, a novel visual input rebinding system, and a volumetric cloud renderer with groundbreaking fidelity, any of which can easily add value to other open source gamedev projects. We'd love to see other teams building on our foundation to deliver their own vision and contribute to the ecosystem we're creating.

It is our ambition to forge a new paradigm of open source game development, as to our knowledge there are no other open source projects of this scope building up a foundation from scratch. Game development is notoriously averse to this model and nobody wants to share their shiny toys for fear of losing their competitive advantage. By building and sharing immersive and compelling experiences like this, we hope to change that.

## Contact Us

If you want to chat, please join [our Discord server](https://discord.gg/trbteNj) for detailed development updates and discussions about the game design, narrative, art direction, and general topics. Shitposting welcome!

### Trailer (outdated)
[![Trailer](https://img.youtube.com/vi/6hg1w2vcwDc/0.jpg)](https://www.youtube.com/watch?v=6hg1w2vcwDc)
### Screenshots
<img src="https://i.ibb.co/3h1xrRw/main.png" style="zoom:50%;" />
<img src="https://i.ibb.co/HzVd8kv/view.jpg" style="zoom:50%;" />
<img src="https://i.ibb.co/z5vRz7M/map.jpg" style="zoom:50%;" />
<img src="https://i.ibb.co/BnYxXVC/laser.jpg" style="zoom:50%;" />
<img src="https://i.ibb.co/Fq4bNVK/flamethrower.jpg" style="zoom:50%;" />
<img src="https://i.ibb.co/QFzxGHH/lightning.jpg" style="zoom:50%;" />

## Table of Contents

1. [Game Design](#Game-Design)
2. [Previous Work](#Previous-Work)
3. [Current Work](#Current-Work)
4. [Architecture](#Architecture)
    - [Project Structure](#Project-Structure)
    - [Third Party Libraries](#Third-Party-Libraries)
    - [Programming Paradigms](#Programming-Paradigms)
    - [Data Structures](#Data-Structures)
5. [Contributing](#Contributing)
    - [Getting the Files](#Getting-the-Files)
    - [Choosing a Task](#Choosing-a-Task)
    - [Database Editor Tools](#Database-Editor-Tools)
      - [Connecting to RethinkDB](#Connecting-to-RethinkDB)
      - [Editing Items](#Editing-Items)
    - [Testing Locally](#Testing-Locally)
    - [Debug Console](#Debug-Console)
6. [Galaxy Editor](#Galaxy-Editor)
    - [Map Layer Data](#Map-Layer-Data)
    - [Star Tools](#Star-Tools)
7. [Contact Us](#Contact-Us)



## Game Design

The ARPG game design document is available [here](https://docs.google.com/document/d/1iULu1WsbuQoUM3c87XkGseb1P-8R5xlruoiyg03TsSE/edit?usp=sharing), while the RTS gameplay is documented [here](https://docs.google.com/document/d/1U3uGFqQboAiFJ_Y-nUOGpyixbXUHRbc5DiCuB59GM4w/edit?usp=sharing). There's also a document explaining how some of the shaders work [here](https://docs.google.com/document/d/1AFycvCtW6hA1jkKq1ZmYd3k6_uEWaaCqcZ4fYj4vU6A/edit?usp=sharing).

The eventual goal is to essentially create two games which both take place in the same persistent universe, allowing players with vastly different preferences to struggle together for the survival of mankind. Each instance of the game lasts until the inevitable destruction of the entire population at the hands of aliens, after which the universe resets. Each loop is designed to last up to a couple of months, during which the hostility of the aliens steadily increases until the players are unable to hold back the tide. As players gain proficiency with the systems, the length of the time loop may increase, allowing us to organically inject new content into the timeline.

## Previous Work

The concept for Aetheria goes back many years, during which I have steadily acquired my current skill with the primary objective of becoming competent enough to realize my vision. Previously I have built prototypes of the ARPG gameplay, [here's a video of the most recent one](https://www.youtube.com/watch?v=PNwVGtvefCg). While it included stations, AI opponents, multiple ships and a complex loadout system which simulates heat transfer between all of the ship's hardpoints with temperature affecting the performance of each item differently, the world was rather static and empty.

As a result of lessons learned, we then focused on the economy system, and built a client-server architecture for the networked simulation of a persistent universe. We created an RTS client, allowing players to take the role of a corporation, where they can define roles for their population, gather resources, build infrastructure, research new technology and produce items in order to make as much money as possible.

## Current Work

At the moment we are focused on implementing and polishing the ARPG gameplay as a standalone title, first as a rogue-lite combat demo and then adding a story-driven campaign mode.

## Architecture

### Project Structure

There are two solutions in this repository. One is a Unity project containing the desktop client for Aetheria, and the other is a .NET Core application intended to run on Linux cloud servers. The bulk of the game's code is shared between them, located at [Assets/Scripts/ServerShared](Assets/Scripts/ServerShared), defining a common protocol, world simulation code and data serialization format.

### Third Party Libraries

Client-Server communication is implemented using [LiteNetLib](https://github.com/RevenantX/LiteNetLib), a semi-reliable UDP transport library which we use to transmit [MessagePack](https://github.com/neuecc/MessagePack-CSharp) over the wire.

Aetheria uses [RethinkDB](https://rethinkdb.com/) for data persistence. To make this possible, all persistent data is marked with attributes for both MessagePack and [JSON.Net](https://www.newtonsoft.com/json) serialization. During operation, the client does not communicate with the database server directly, only the game server does that; the game server caches data relevant to the game and sends it to the clients.

### Programming Paradigms

The codebase makes heavy use of C#'s [Language Integrated Queries (LINQ)](https://docs.microsoft.com/en-us/dotnet/csharp/programming-guide/concepts/linq/), allowing for the concise representation of operations that modify or filter collections (though they do generate some garbage so must be avoided within the update loop). Asynchronous stream processing is often performed using the [functional reactive programming](http://reactivex.io/) paradigm, which is achieved using [Microsoft's Reactive Extensions](https://github.com/dotnet/reactive) on the server and [Reactive Extensions for Unity](https://github.com/neuecc/UniRx). Combining Observables with LINQ allows for extremely powerful expressions of the programmer's intent.

### Data Structures

All of the persistent state classes inherit from the DatabaseEntry class, which uses a GUID as each entry's primary key. Whenever a reference to a database entry must be held, it should be stored as a GUID and when needed, retrieved directly from the DatabaseCache held by the appropriate manager, usually ItemManager for references to ItemData. The ItemManager contains GetData helper methods for retrieving the ItemData for the various subclasses of ItemInstance.

#### Equipment

Items in the game which can be equipped are defined as subclasses of EquippableItemData, including the HullData class which defines a space ship, station or turret, and the GearData class which defines anything that can be equipped onto a Hull.

#### Behaviors

Equippable Items can hold any number of Behaviors, which define the functionality of that item in game. Everything from radiating heat into space to moving a ship, firing a weapon or boosting the stats of another item is defined as a Behavior.

#### Performance Stats

While some stats are fixed, others can vary according to the condition the item is in. Such stats are PerformanceStats. These can vary depending on the item's remaining durability, the current temperature of the item, and the quality with which it was crafted.

#### Blueprints

In order to make an item craftable in-game, that item needs to be associated with one or more Blueprints. A Blueprint defines the ingredients (or components) necessary to build an item. In addition, specific ingredients can be associated with particular PerformanceStats for the resulting item's Behaviors, allowing a single item to be crafted in various ways, with its final stats varying in accordance with the supply chain and quality control of the manufacturer.

## Contributing

### Contributor Agreement

By pushing to this repository or submitting a pull request, you are implicitly providing us (GameCult) permission to relicense your work as we see fit. This means that your contribution will automatically be under the same license as this repository, but also grants us the right to release your contribution under a different license should we see fit. This agreement exists mostly because we have witnessed the difficulties some open source projects have had when they did not have such a contributor agreement in place.

### Getting the Files

In order to checkout the project, you need a git client (Github's zip download will not work!). You also need to have installed [Git LFS (Large File Storage)](https://git-lfs.github.com/). This is necessary because assets in gamedev projects can get rather large, and Git is essentially a text versioning system that does not by itself support that use case well. After installing LFS you'll need a Git client. I recommend [Github Desktop](https://desktop.github.com/), which has a nice simplified workflow and integrates with the site. For more advanced users, there's nothing wrong with using the command line or a more comprehensive client like [GitKraken](https://www.gitkraken.com/), but beginners beware that it's easy to shoot yourself in the foot that way.

When you have synced with the repository, you can open the project using Unity. The project uses Unity 2020.3.2f1 at the moment, and while it may work with newer or older versions, that cannot be guaranteed. You can open the project by opening the root of this repository either directly with the Unity Editor, or using [Unity Hub](https://public-cdn.cloud.unity3d.com/hub/prod/UnityHubSetup.exe), which will also take care of downloading the correct version of the Editor.

### Choosing a Task

We are organizing according to an [Agile development](https://en.wikipedia.org/wiki/Agile_software_development) schedule, with the progress of each sprint being tracked on its own board in the [Github Projects tab](https://github.com/rwvens/Aetheria-Economy/projects). If you wish to take on a task from the board, please contact us to become an official contributor so that the task can be assigned to you directly. Some issues are not on the sprint schedule, those are ideal for developers who want to jump in but are shy about joining. We use the [good first issue](https://github.com/rwvens/Aetheria-Economy/labels/good%20first%20issue) label for issues that don't require heavy knowledge of the codebase.

You don't have to be a programmer to contribute, either! We have issue labels for and very much welcome contributions from [writers](https://github.com/rwvens/Aetheria-Economy/labels/worldbuilding) and [game designers](https://github.com/rwvens/Aetheria-Economy/labels/game%20design).

### Database Editor Tools

In order to facilitate the creation and maintenance of game data, there is a Unity editor utility which communicates directly with RethinkDB. You can access the tools by selecting Window/Aetheria Database Tools in Unity's menu. This will cause two windows to appear, the Database List View and the Database Inspector.

#### Connecting to RethinkDB

At the top of the list view there is a text field where you can enter the URL of the database server. When you click connect, the editor will download and cache all of the items in the game, as well as subscribe to the changefeed. The list should now populate with items. For access to our database servers and therefore live game data, please contact us; it would be dangerous to make our actual database URL public!

#### Editing Items

You can unfold the categories of items in the list view to see what items exist. If you select an item, the Database Inspector will populate with all of the available fields of that item. Any changes you make in the Inspector will automatically be pushed to RethinkDB. If you've connected to the production database, this will update the stats of in-game items in real-time!

### Testing Locally

Testing the game entirely offline doesn't require running the economy server, but you still need to download the database contents. In the database list view, click the "Connect" button. Once the tools are finished syncing, which can take a while (there's a progress bar), you can click "Save" to create a local backup of the entire database. If you enter Play mode in the "ARPG" scene, the game will use that local copy instead of requiring a connection to the master server.

Note that this process needs to be repeated every time the data model changes or if you wish to test the game with updated database contents.

### Debug Console

Pressing the tilde key (`) while running the game allows you to access the console. Here you can view the debug log as well as entering commands which aid in testing various game mechanics. Console commands are registered with the console controller. Our current convention is to perform command registration inside ActionGameManager.cs:Start().

#### Commands

### Galaxy Editor

When you select a galaxy asset in the Unity scene hierarchy, a custom editor opens in the inspector which enables the procedural generation of a new galaxy. There you can find some variables pertaining to the galaxy as a whole, such as the number and twist of the spiral arms. 

#### Map Layer Data

Below that is an editor for map layer data which allows the creation of a density map defining the value of some variable as it varies over the space containing the galaxy, which can be previewed at the top of the inspector. By default the star density map layer is displayed, which controls the distribution of stars. Any number of map layers can be created, defining variables such as the radius of zones and the presence of life and resources to be mined.

#### Star Tools

After the map layer data section is a foldout containing tools which allow you to generate stars according to the star density map. Stars are placed by accumulating density while walking over a space-filling Hilbert curve, maintaining some minimum distance between stars. This isn't as good as a proper sampling algorithm like Poisson disk sampling or Mitchell’s best-candidate algorithm, but it gets the job done ([please feel free to contribute a better sampling algorithm!](https://github.com/rwvens/Aetheria-Economy/issues/15)).

After generating stars, you can generate the links between them, which performs a Delaunay Tessellation, and then remove some proportion of links until the desired sparsity is reached. The algorithm for filtering star links is also not ideal, [there's an issue for fixing that, too!](https://github.com/rwvens/Aetheria-Economy/issues/25)

## License

The majority of this repository is under the Mozilla Public License and therefore available for anyone to use. Note that the MPL is per-file and therefore the license only applies to files which contain the MPL header. If you believe a file has been created by us and is missing the header, please let us know (we do forget sometimes).
