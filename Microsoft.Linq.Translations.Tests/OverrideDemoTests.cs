using NUnit.Framework;
using System;
using System.ComponentModel.DataAnnotations;
using System.Data.Common;
using System.Data.Entity;
using System.Linq;

// NOTE: Keep unix LF endings in this file as required by test
namespace Microsoft.Linq.Translations.Tests
{
  [TestFixture]
  public class OverrideDemoTests
  {
    [Test]
    public void DemoTest()
    {
      using (var connection = Effort.DbConnectionFactory.CreateTransient())
      {
        using (var context = new MyContext(connection))
        {
          context.Bases.Add(new Base());
          context.Bases.Add(new DerivedOne());
          context.Bases.Add(new DerivedTwo());
          context.Bases.Add(new DerivedThreeNoOverride());
          context.Bases.Add(new DerivedFourFromTwoNoOverride());
          context.SaveChanges();
        }

        using (var context = new MyContext(connection))
        {
          const String expected =
@"This is the base class implementation
This is the derived class implementation - for derived one
This is the derived class implementation - for derived two
This is the base class implementation
This is the derived class implementation - for derived two";
          var output = String.Join("\n", context.Bases.Select(b => b.DisplayName).WithTranslations());
          Console.WriteLine(output);
          Assert.AreEqual(expected, output);
        }
      }
    }

  }

  public class Base
  {
    [Key] public Int32 PK { get; set; }

    private static CompiledExpression<Base, String> expDisplayName =
       DefaultTranslationOf<Base>.Property(p => p.DisplayName)
           .Is(o => "This is the base class implementation");

    public virtual String DisplayName
    {
      get
      {
        return expDisplayName.Evaluate(this);
      }
    }
  }

  public class DerivedOne : Base
  {
    private static CompiledExpression<DerivedOne, String> expDisplayName =
       DefaultTranslationOf<DerivedOne>.Property(p => p.DisplayName)
           .Is(o => "This is the derived class implementation - for derived one");

    public override String DisplayName
    {
      get
      {
        return expDisplayName.Evaluate(this);
      }
    }
  }

  public class DerivedTwo : Base
  {
    private static CompiledExpression<DerivedTwo, String> expDisplayName =
       DefaultTranslationOf<DerivedTwo>.Property(p => p.DisplayName)
           .Is(o => "This is the derived class implementation - for derived two");

    public override String DisplayName
    {
      get
      {
        return expDisplayName.Evaluate(this);
      }
    }
  }

  public class DerivedThreeNoOverride : Base
  {
  }
  
  public class DerivedFourFromTwoNoOverride : DerivedTwo
  {
  }

  public class MyContext : DbContext
  {
    public MyContext(DbConnection connection)
      : base(connection, true)
    {}
    public virtual DbSet<Base> Bases { get; set; }
    public virtual DbSet<DerivedOne> DerivedOnes { get; set; }
    public virtual DbSet<DerivedTwo> DerivedTwos { get; set; }
    public virtual DbSet<DerivedThreeNoOverride> DerivedThrees { get; set; }
    public virtual DbSet<DerivedFourFromTwoNoOverride> DerivedFours { get; set; }
  }

}
