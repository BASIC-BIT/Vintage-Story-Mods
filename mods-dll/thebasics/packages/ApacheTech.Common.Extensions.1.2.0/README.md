# Common Extension Methods

Extension Methods for many different classes within .NET.

This is an inexaustive list of extension methods included within the package.

## Collections

|Command|Description|
|---|---|
| `myCollection.FirstOrNull<T>()` | Returns the first element of a sequence, or null if the sequence contains no elements. |
| `myCollection.FirstOrNull<T>(Func<T, bool> predicate)` | Returns the first element of the sequence that satisfies a condition or null if no such element is found. |
| `myCollection.AddIfNotPresent<T>(T item)` | Adds an item to the "ICollection", if it not already present in the collection. |
| `myConcurrentBag.AddIfNotPresent<T>(T item)` | Adds an item to the "ConcurrentBag", if it not already present in the collection. |

## Enums

|Command|Description|
|---|---|
| `myEnum.GetDescription()` | Gets the description for the enum member, decorated with a "DescriptionAttribute". |
| `myEnum.Count()` | Gets the number of values within this enumeration. |

## Strings

|Command|Description|
|---|---|
| `myString.IfNullOrWhitespace(string defaultString)` | Returns a default string, if a specified string is "null", empty, or consists only of white-space characters. |
| `myString.IfNullOrEmpty(string defaultString)` | Returns a default string, if a specified string is "null", or empty. |
| `myString.StartsWithAny(IEnumerable<string> values)` | Determines whether the beginning of this string instance matches any of the specified strings. |
| `myString.StartsWithAny(params string[] values)` | Determines whether the beginning of this string instance matches any of the specified strings. |
| `myString.ContainsAny(IEnumerable<string> values)` | Determines whether the string instance contains any of the specified strings. |
| `myString.ContainsAny(params string[] values)` | Determines whether the string instance contains any of the specified strings. |
| `myString.ToOrdinal(int value)` | Converts a number to its ordinal string representation. |

## Objects

|Command|Description|
|---|---|
| `myObject.DynamicProperties()` | Gets a dynamic collection of properties associated with an object instance, with a lifetime scoped to the lifetime of the object. |
| `myObject.To<T>()` | Dynamically casts the object instance to a specified type. |
| `myObject.With<T>(Action<T> work)` | A close approximation to Visual Basic's "With" keyword, that allows batch setting of Properties, without needing to initialise the object. |

## Reflection

|Command|Description|
|---|---|
| `myAssembly.InstantiateAllTypes<T>(params object[] constructorArgs)` | Scans an assembly for all instantiable classes of a specified type, and forms an array of instances. |
| `myAssembly.GetTypesWithAttribute<T>()` | Gets the derived types of a specified Attribute, within the assembly. |
| `myType.GetDerivedTypesFromAssembly(Assembly assembly)` | Gets the derived types of a specified Attribute, within a given assembly. |
| `myMember.HasCustomAttribute<T>()` | Determines whether the specified member is decorated with the given custom attribute. |
| `myProperty.TryGetDefaultValue(out object defaultValue)` | Tries the get default value of a property, via its "PropertyInfo". |
| `myProperty.TryGetDefaultValue<T>(out T defaultValue)` | Tries the get default value of a property, via its "PropertyInfo". |

## Tasks

|Command|Description|
|---|---|
| `myTask.OnCompletedSuccessfully<TResult, TNewResult>()` | Executes an action when a task has been completed successfully. |

## Functional Monadic Extensions

|Command|Description|
|---|---|
| `Identity<T>` | Wraps the value in a monad, allowing functional style operations to be executed. |
| `Maybe<T>` | Wraps the value in a monad, allowing functional style operations to be executed, and degrades gracefully upon failure at any step. |
| `Either<T>` | Wraps the value in a monad, allowing functional style operations to be executed, degrades gracefully at any step, and tracks any exceptions thrown. |