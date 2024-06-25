# Fluent Chat Commands

Provides a framework for creating client-side, and server-side chat commands, using a Fluent Builder pattern. Supports lazy loading of chat commands, where they can be registered early, and configured at any stage.

The main idea is to simplify the process of writing complex commands, as well as separating the construction, business logic, and registration of chat commands.

The syntax string for the command is set automatically, by the sub-commands that are added. A command with no default handler, and no sub-commands, returns its help message to the user.

## Support the Developer

If you find this dev tool useful, and you would like to show appreciation for the work I produce; please consider supporting me, and my work, using one of the methods below. Every single expression of support is most appreciated, and makes it easier to produce updates, and new features for my mods, and dev tools, moving fowards. Thank you.

 - [Join my Patreon!](https://www.patreon.com/ApacheTechSolutions?fan_landing=true)
 - [Donate via PayPal](http://bitly.com/APGDonate)
 - [Buy Me a Coffee](https://www.buymeacoffee.com/Apache)
 - [Subscribe on Twitch.TV](https://twitch.tv/ApacheGamingUK)
 - [Subscribe on YouTube](https://youtube.com/ApacheGamingUK)
 - [Purchase from my Amazon Wishlist](http://amzn.eu/7qvKTFu)
 - [Visit my website!](https://apachegaming.net/)


## Pre-requisites

This package assumes that you have [Vintage Story](https://vintagestory.at/) installed on the computer, and that you have set the following environment variables, as per the Vintage Story Modding Best Practices.

 * **%VINTAGE_STORY%**: The installation directory for Vintage Story. The location of *Vintagestory.exe*. By default this is *%APPDATA%\Vintagestory*.
 
 * **%VINTAGE_STORY_DATA%**: The data directory for Vintage Story. By default this is *%APPDATA%\VintagestoryData*. However, you can check by opening the game, going to the Mod Manager, selecting *Open Mods Folder*, and going to the parent folder.

You can also do this through the Windows Control Panel, following these steps:

- **Windows 10 and Windows 8**
    1. In *Search*, search for and then select: *System (Control Panel)*
    2. Click the *Advanced* system settings link.
    3. Click *Environment Variables*. In the section *System Variables* (for All Users), or *User Variables* (for just the Current User), find the PATH environment variable and select it. Click Edit. If the PATH environment variable does not exist, click New.
    4. In the *Edit System Variable* (or *New System Variable*) window, specify the value of the PATH environment variable. Click *OK*. Close all remaining windows by clicking OK.
    
- **Windows 7**

    1. From the desktop, right click the *Computer* icon.
    2. Choose *Properties* from the context menu.
    3. Click the *Advanced* system settings link.
    4. Click *Environment Variables*. In the section *System Variables* (for All Users), or *User Variables* (for just the Current User), find the PATH environment variable and select it. Click *Edit*. If the PATH environment variable does not exist, click New.
    5. In the *Edit System Variable* (or *New System Variable*) window, specify the value of the PATH environment variable. Click OK. Close all remaining windows by clicking OK.

Further details and troubleshooting can be found [here](https://www.computerhope.com/issues/ch000549.htm).

## Code Examples

There are lots of ways you can use FluentChat, to your own liking. Here is just a small selection of ideas to help you get started.

### Basic Example

In this example, we're setting a command called `.test`, with a basic default command handler.
```csharp  
    internal class MyMod : ModSystem
    {
        // ...
        
        public override void StartClientSide(ICoreClientAPI capi)
        {
            FluentChat.RegisterCommand("test", capi)
                .WithDescription("This is a test command.")
                .WithHandler((_,_,_) => capi.ShowChatMessage("Hello, World!"));
        }
        
        // ...
    }
```

### Basic Example with Sub-Commands

In this example, we're setting a command called `.test`, with a basic default command handler, but also setting separate sub-commands for `.test stuff` and `.test things`. The only difference between the sub-commands is that one uses a lambda expression for its handler, and the other separates it's logic into a named method.
```csharp  
    internal class MyMod : ModSystem
    {
        // ...
        
        private ICoreClientAPI _capi;
        
        public override void StartClientSide(ICoreClientAPI capi)
        {
            capi.RegisterFluentCommand("test")
                .WithDescription("This is a test command.")
                .WithHandler((_,_,_) => capi.ShowChatMessage("Hello, World!"))
                .HasSubCommand("stuff" x => x.WithHandler(StuffHandler).Build())                
                .HasSubCommand("things", x =>x.WithHandler(
                    (_,_,_) => capi.ShowChatMessage("Doing Things!")).Build());
        }
        
        private void StuffHandler(string subCommandName, int groupId, CmdArgs args)
        {
            _capi.ShowChatMessage("Doing Stuff!");
        }
        
        // ...
    }
```

### Advanced Example with Separation of Concerns

Here, we lazy load the `/test` command by immediately registering it with the API. 
```csharp
    internal class MyMod : ModSystem
    {
        // ...
        
        public override void StartServerSide(ICoreServerAPI sapi)
        {
            sapi.RegisterFluentCommand("test")
        }
        
        // ...
    }
```

We can then separate our logic into a separate class; even within a separate assembly.
```csharp  
    internal class TestChatCommand
    {
        private ICoreServerAPI _sapi;
        
        internal TestChatCommand(ICoreServerAPI sapi)
        {
            _sapi = sapi;
            var command = sapi.FluentCommand("test");
            
            // Set Description:
            command.WithDesctiption(Lang.Get("mymod:commands.test.description"));
            
            // Set Default Handler:
            command.WithHandler(DefaultHandler);
            
            // Set Required Privilege:
            command.RequiresPrivilege(Privilege.ControlServer);
            
            // Set Stuff Handler:
            command.HasSubCommand("stuff", x => x.WithHandler(StuffHandler).Build());
            
            // Set Things Handler:
            command.HasSubCommand("things", x => x.WithHandler(ThingsHandler).Build());
        }
    
        private void DefaultHandler(string subCommandName, IServerPlayer player, int groupId, CmdArgs args)
        {
            // Do stuff and things.
        }
    
        private void StuffHandler(string subCommandName, IServerPlayer player, int groupId, CmdArgs args)
        {
            // Do stuff.
        }
        
        private void ThingsHandler(string subCommandName, IServerPlayer player, int groupId, CmdArgs args)
        {
            // Do things.
        }
    }
```
