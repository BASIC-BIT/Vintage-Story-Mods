# Harmony Reflection Extensions

This package of extension methods gives an elegant way to use reflection on all kinds of objects within C#. Below, is a list of methods that are added at an object level, allowing simple and easy access to `internal` and `private` members.

All that is required is that `Lib.Harmony` should be included within your project. This package does not supply Harmony. Once installed, add the following using statement to your class file.

```csharp
using ApacheTech.Common.Extensions.Harmony;
```

## Fields

| Method | Description |
| --- | --- |
| **GetField&lt;T&gt;** | Gets a field within the calling instanced object. This can be an internal or private field within another assembly. |
| **GetFields&lt;T&gt;** | Gets an array of fields within the calling instanced object, of a specified Type. These can be an internal or private fields within another assembly. |
| **SetField** | Sets a field within the calling instanced object. This can be an internal or private field within another assembly. |

## Properties

| Method | Description |
| --- | --- |
| **GetProperty&lt;T&gt;** | Gets a property within the calling instanced object. This can be an internal or private property within another assembly. |
| **SetProperty** | Sets a property within the calling instanced object. This can be an internal or private property within another assembly. |

## Methods

| Method | Description |
| --- | --- |
| **CallMethod** |  (2 methods) Calls a method within an instance of an object, via reflection. This can be an internal or private method within another assembly. |
| **CallMethod&lt;T&gt;** | Calls a method within an instance of an object, via reflection. This can be an internal or private method within another assembly. |
| **GetMethod** | Gets the <see cref="MethodInfo"/> for a method within an instance of a class, via reflection. This can be an internal or private method within another assembly. |

## Types

| Method | Description |
| --- | --- |
| **GetClassType** | Gets the type of the class within an assembly, via reflection. |
| **CreateInstance** | Creates the instance of a specified Type, using Harmony AccessTools. Be aware that this will ignore all Service Providers, and attempt to directly instantiate a class. |

## Objects

| Method | Description |
| --- | --- |
| **DeepClone&lt;T&gt;** | Makes a deep copy of any object. |