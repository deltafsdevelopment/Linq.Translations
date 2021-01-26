# Microsoft.Linq.Translations (aka Expressives)

Author: Damieng Guard

Created: June 2009

Original repository: https://github.com/damieng/Linq.Translations 

Site: https://damieng.com/blog/2009/06/24/client-side-properties-and-any-remote-linq-provider 


Code customised by Delta Financial Systems for Platinum~Pro project

Customisations by: A G Smith

Modified: 13 Jan 2016 - onwards 

Forked repository: https://platinum.deltafs.net/svn/trunk/ThirdParty/Microsoft.Linq.Translations/ 

## Overview

Extension that enables calculated fields on entities to be expressed as Lambda expressions which can be converted by the Linq-to-entities query processor to SQL and run at the database level.

By default any calculated fields added to an entity will not be able to be used directly in a LINQ-to-Entities (L2E) query, this is because the query parser will not be able to convert the calculated field's definition into an expression that can be used by the data source provider - e.g. a SQL query. In order to get the calculated field into a L2E query it needs to be expressed as a Lambda expression tree that can be parsed by the query parser and converted into an equivalent SQL statement. There is an additional restriction in that only a subset of all functions supported in .NET lambdas can be used, this subset is the "Canonical functions" that are present in all languages. To make defining expressions for calculated fields slightly easier we use a third party library called *Microsoft.Linq.Translations*. This enables us to use somewhat more condensed syntax to define expressions.

### 1. Defining translations for calculated properties
* `using Microsoft.Linq.Translations`
* Declare a private shared variable in the entity you want the calculated field on, with a lambda expression that defines the logic to use to compute the value of the property
  ```
  private static CompiledExpression<[EntityType], [ReturnType]> exp[PropertyName]  = 
     DefaultTranslationOf<[EntityType]>.Property(p => p.[PropertyName])
         .Is(o => [Lambda expression defining the calculation])); 
  ```
* Note that you can only use Canonical Functions in the expression, and the expression must be a single line, multi-line lambdas are not supported
* To ensure the logic defining the field is only defined in one place the actual implementation of the property must be replaced as follows:
  ```
  public [ReturnType] [PropertyName] {
     get {
         return exp[PropertyName].Evaluate(this);
     }
  }
  ```
* __Do not put any other code inside the getter__. Code inside the getter will only be run when property is accessed in-memory e.g. by a Linq-to-objects (L2O) query
* Generally in order to use the expression as part of a L2E query you must append the `WithTranslations` extension to the query
  ```
  var qry = (from o in context.[EntitySet] 
            where o.[PropertyName] = [Value] 
            select o).WithTranslations();
  ```
## 2. Defining translations for parameterless methods

In addition to properties we have enhanced the library to support defining translations for *parameterless* methods. So typically the `ToString()` method is not L2E compliant, but by defining a translation expression for it we can use it in a L2E query. The approach is almost identical to that for properties:

* Declare a private shared variable in the class you want the calculated field on, with a lambda expression that defines the logic to use to compute the value of the property
  ```
  private static CompiledExpression<[EntityType], [ReturnType]> exp[MethodName]  = 
    DefaultTranslationOf<[EntityType]>.Method(p => p.[MethodName]())
      .Is(o => [Lambda expression defining the method implementation])); 
  ```
* To ensure the logic defining the method is only defined in one place the actual implementation of the method must be replaced as follows:
  ```
  public override [ReturnType] [MethodName] {
    get {
      return exp[MethodName].Evaluate(this);
    }
  }
  ```
## 3. Defining translations for converting enums to strings

Often we want to transform an enum to its string version in an L2E compliant way. As enums are `ValueType` and derive from a special sealed class called `Enum` we cannot "override" and apply a translation for the `ToString()` method as we would in a conventional class (see 2. above). To allow this type of translation we have further modified Microsoft.Linq.Translations so that you can place the translation in a "helper class" which by convention appears in the ''same namespace as the enum and is named `[EnumName]Expressions`:

```
namespace Deltafs.Platinum.Common
{
  public enum [EnumName]
  {
    NotSpecified = 0,
    Value1 = 1,
    Value2 = 2,
    Value3 = 3
  }

  public static class [EnumName]Expressions
  {
    public static CompiledExpression<[EnumName], String> exp[EnumName] =
      DefaultTranslationOf<[EnumName]>.Property(p => p.ToString())
        .Is(o => [MyCustomEnumNameFormatter](o));
  }
}
```
### How it works
Microsoft.Linq.Translations will use the supplied custom formatter to dynamically create an L2E compliant expression for converting the enum into a string.

## 4. Defining base translations

Additionally it is sometimes useful to be able to place a translation for a given a property or method ''outside'' of the class the property or method is defined in (e.g. if this resides in an assembly not under your control). To facilitate this we have modified Microsoft.Linq.Translations so you can place translations in any class and have them included by applying the `[BaseTranslation]` attribute to the class. 

This is particularly useful for applying a global enum formatting function (as the Enum class is sealed and in mscorlib):

```
namespace Deltafs.Platinum.Model
{
  [BaseTranslations]
  public static class BaseTranslations
  {
    private static CompiledExpression<Enum, String> expCalculatedEnumAttribute =
      DefaultTranslationOf<Enum>.Property(p => p.ToString())
        .Is((o) => Formatting.GetCaptionOrName(o));

  }
}
```

### How it works
Microsoft.Linq.Translations when applying `WithTranslations` will search all assemblies in the AppDomain and initialise all static exported types that carry the `BaseTranslation` attribute.

## 5. Polymorphic overrides

The original version of the library did not support overrides on calculated fields. This makes sense as the compiled expressions are static in their declaring type. We have modified the library to give the *illusion* of usual polymorphic override semantics.

```
public class Base 
{
  [Key] public Int32 PK { get; set; }
  
  private static CompiledExpression<Base, String> expDisplayName  = 
     DefaultTranslationOf<Base>.Property(p => p.DisplayName)
         .Is(o => "This is the base class implementation"); 
      
  public virtual String DisplayName {
     get {
         return expDisplayName.Evaluate(this);
     }
  }   
}

public class DerivedOne : Base 
{
  private static CompiledExpression<DerivedOne, String> expDisplayName  = 
     DefaultTranslationOf<DerivedOne>.Property(p => p.DisplayName)
         .Is(o => "This is the derived class implementation - for derived one"); 
      
  public override String DisplayName {
     get {
         return expDisplayName.Evaluate(this);
     }
  }   
}

public class DerivedTwo : Base 
{
  private static CompiledExpression<DerivedTwo, String> expDisplayName  = 
     DefaultTranslationOf<DerivedTwo>.Property(p => p.DisplayName)
         .Is(o => "This is the derived class implementation - for derived two"); 
      
  public override String DisplayName {
     get {
         return expDisplayName.Evaluate(this);
     }
  }   
}

public class MyContext : DbContext
{
    public virtual DbSet<Base> Bases { get; set; }
    public virtual DbSet<DerivedOne> DerivedOnes { get; set; }
    public virtual DbSet<DerivedTwo> DerivedTwos { get; set; }
}

public void Main 
{
  using (var context = new MyContext())
  {
    context.Bases.Add(new Base());
    context.Bases.Add(new DerivedOne()); 
    context.Bases.Add(new DerivedTwo());
    context.SaveChanges();
  }
  
  using (var context = new MyContext())
  {
    Console.WriteLine(String.Join("\r\n", context.Bases.Select(b => b.DisplayName).WithTranslations());
    // will print out:
    // This is the base class implementation
    // This is the derived class implementation - for derived one
    // This is the derived class implementation - for derived two
  }  
  
}
```

## References

* [Original blog about the Microsoft.Linq.Transaltions library](http://damieng.com/blog/2009/06/24/client-side-properties-and-any-remote-linq-provider)
* [NuGet resource for this library](https://nuget.org/packages/Microsoft.Linq.Translations)
* [Supported Cannonical Functions in LINQ-to-Entities](http://msdn.microsoft.com/en-us/library/system.data.objects.entityfunctions.aspx)
* [MSDN about Canonical Functions](http://msdn.microsoft.com/en-us/library/bb738626.aspx)
* [Supported and Unsupported LINQ Methods in Linq-to-entities](https://msdn.microsoft.com/en-us/library/vstudio/bb738550(v=vs.110).aspx)
