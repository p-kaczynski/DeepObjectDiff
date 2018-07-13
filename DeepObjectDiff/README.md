# DeepObjectDiff
## Object equivalence comparer

### Status
This is still in early stage, use at your own risk.

### Compatability
This project is targeted for:
* .NET Framework 4.6.1 or higher
* .NET Standard 2.0 or higher
* .NET Core 2.0 or higher

and does not contain any other references besides the SDKs.

### License
This project is provided under The Unlicense - meaning you are free to do whatever you want with it. Attribution, a thank-you note, or a bottle of scotch will be appreciated but are not required.

Of course it comes without any warranty, it might not work, or you might get fired for using it, and it is none of my concern.

### Purpose
The library provides an `ObjectComparer` which allows, in a configurable way, to determine if two objects are equivalent.

In case of primitives or strings it is a trivial task, achievable by simple `object1 == object2` comparison.
Also, if you are only interested in whether two objects are literaly same object (`ReferenceEquals(object1, object2)`),
or if you want to compare two objects which implement `IEquatable<T>` interface.

However what are your options if you have objects like this:

```csharp
public class ComplexObject {
    public string Name { get;set; }
    public IList<Uri> Links { get; set; }
    public ComplexObject[] NestedObjects { get; set; }
    public AnotherVeryComplexClass MoreHeadache { get; set; }
    // ...and so on
}
```

Well, if you control the class, you can implement `IEquatable<ComplexObject>`, that will however take you a decent while. You can, with same amount of code implement an `IEqualityComparer<ComplexObject>`,
both of which will unfortunately mean that you have to remember about every property, write logic to deal with collections, run recursion for nested objects, and finally do the whole thing again for other classes
that your class includes as properties.

Good news! I did it for you. `ObjectComparer` will run a variety of checks, go through all properties, recurse where necessary, apply configurable logic to collections and not only will it give you a `bool` result - 
it will also provide you with a list of differences between objects you compare, like a `diff` utility. Great for unit testing - you can see where things went wrong.

### Usage
The basic comparison with default option can be performed by calling:
```csharp
var result = ObjectComparer.Compare(model, model, out _);
```
If you would like to obtain the list of found differences just provide an `out` parameter:
```csharp
var result = ObjectComparer.Compare(model, model, out var objectDifferenceArray);
```

### Behaviour customization
The default behaviour can be customized by passing in a fourth, optional parameter of type `CompareOptions`.

* `bool UseMultithreading` _default: false_ - currently not implemented. This will cause `ObjectComparer` to use multiple threads where possible to speed-up the traversal of the object graph

* `bool VerifyListOrder` _default: true_ - by default it ensures that collections that implement `IList<T>` interface will be compared in order of the elements. If set to _false_ it will just ensure that the same elements are contained
        
* `bool EnumerateEnumerables` _default: true_ - by default it will enumerate properties of `IEnumerable<T>` type (or implementing such, if no higher-level checks were triggered). If your property contains data that cannot 
be enumerated multiple times, you might want to set that to _false_. It will however not detect any differences between the enumerations.

* `bool StopAtFirstDifference` _default: true_ - by default, to provide quick result, the comparison will stop at the first difference encountered. It however means that the provided array of `ObjectDifference` will not be exhaustive.
If you primarly care about the differences, set this to _false_.

* `ICollection<string> PathsToIgnore` _default: string[0]_ - if you want to exclude specific paths, set them here. They should be in format `/PropertyName/SubPropertyName`, where `/` is the object passed to the `Compare` method.

* `ICollection<Type> TypesToIgnore` _default: Type[0]_ - if you want to exclude specific types from being examined, set them here.

* `StringComparison DefaultStringComparison` _default: StringComparison.Ordinal_ - type of comparison to use for strings. It will by default apply to all encoutnered strings.

* `Use<T>(IEqualityComparer<T> comparer)` _(chainable)_ - allows to provide `ObjectComparer` with your preferred way of comparing objects of specific type. Note, that any object inheritance is not take into consideration here.
This is a preferred way of defining some specific behaviour. For example if one of the objects in the object graph is problematic (e.g. contains cyclic reference to itself), does not implement `IEquatable<T>` interface, but can be
simply compared using default `Equals` method, you can call `Use(EqualityComparer<T>.Default)`, which will provide `ObjectComparer` with a basic, default comparer to use, instead of traversing down properties of object of this type
to determine equivalence.

### Limitations/TODO/Wishlist
* No support for non-generic collections
* Multithreading is not implemented
* Globalization for strings?
* Greater control over properties - public vs non-public, only settable etc., preferably per-type
* Ability to compare by fields not properties
* Add ReferenceEquals checks to `IEnumerable<T>` if `CompareOptions.VerifyListOrder` is set to _false_
* Some considerations were taken to keep performance at acceptable levels, but by its nature this is not going to be blazingly fast - worst case scenario it will perform an unholy number of comparisons, as it dwelves into properties of objects
in collections which are in a property of an object in a collection... you get the point. With `StopAtFirstDifference` set to _false_ it will perform a number of comparisons that is a multiple of a number of value type entities in your object.

### Contributing
Just fork and drop a Pull Request, I'll gladly review it. You are also welcome to create issue tickets with any queries, requests, bug reports and complaints, I will try to help within reason.