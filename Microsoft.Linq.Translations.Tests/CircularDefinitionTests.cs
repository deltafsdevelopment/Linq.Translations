using NUnit.Framework;
using System;
using System.ComponentModel.DataAnnotations;
using System.Data.Common;
using System.Data.Entity;
using System.Linq;

namespace Microsoft.Linq.Translations.Tests
{
  /// <summary>
  ///  Microsoft.Linq.Translations is a third party library we use to simplify the definition of Linq-to-entities friendly
  ///  calculated fields in entities, we have made some minor enhancements to the library these tests test those enhancements
  /// </summary>
  [TestFixture]
  public class CircularDefinitionTests
  {

    public class A
    {
      [Key] public Int32 PK { get; set; }
      public virtual String Value { get; set; }
      public virtual String Value2 { get; set; }
      public virtual String Value3 { get; set; }
      public virtual String Value4 { get; set; }
      public virtual String Value5 { get; set; }
      public virtual String Value6 { get; set; }

      private static CompiledExpression<A, String> expValue =
        DefaultTranslationOf<A>.Property(p => p.Value)
          .Is(o => o.Value);
      private static CompiledExpression<A, String> expValue2 =
        DefaultTranslationOf<A>.Property(p => p.Value2)
          .Is(o => o.Value3);
      private static CompiledExpression<A, String> expValue3 =
        DefaultTranslationOf<A>.Property(p => p.Value3)
          .Is(o => o.Value2);
      private static CompiledExpression<A, String> expValue4 =
        DefaultTranslationOf<A>.Property(p => p.Value4)
          .Is(o => o.Value4.NoTranslate());
      private static CompiledExpression<A, String> expValue5 =
        DefaultTranslationOf<A>.Property(p => p.Value5)
          .Is(o => o.Value6.NoTranslate());
      private static CompiledExpression<A, String> expValue6 =
        DefaultTranslationOf<A>.Property(p => p.Value6)
          .Is(o => o.Value5);
    }

    public class DemoContext : DbContext
    {
      public DemoContext(DbConnection connection)
        : base(connection, true)
      {
      }

      public virtual DbSet<A> As { get; set; }

    }

    [Test]
    public void LinqTranslations_CalculatedAttribute_DirectCircularDependencyFails()
    {
      var a = new A() { Value = "a" };

      using (var connection = Effort.DbConnectionFactory.CreateTransient())
      {
        using (var context = new DemoContext(connection))
        {
          context.As.Add(a);
          context.SaveChanges();
        }

        using (var context = new DemoContext(connection))
        {
          var qry = from m in context.As
                    select m.Value;
          Assert.Throws<CircularReferenceException>(() =>
          {
            Assert.AreEqual(a.Value, qry.WithTranslations().First(), "A override wrong");
          });
        }
      }
    }


    [Test]
    public void LinqTranslations_CalculatedAttribute_InDirectCircularDependencyFails()
    {
      var a = new A() { Value = "a" };

      using (var connection = Effort.DbConnectionFactory.CreateTransient())
      {
        using (var context = new DemoContext(connection))
        {
          context.As.Add(a);
          context.SaveChanges();
        }

        using (var context = new DemoContext(connection))
        {
          var qry = from m in context.As
                    select m.Value;
          Assert.Throws<CircularReferenceException>(() =>
          {
            Assert.AreEqual(a.Value3, qry.WithTranslations().First(), "A override wrong");
          });
        }
      }
    }

    [Test]
    public void LinqTranslations_CalculatedAttribute_DirectCircularDependencyUsingNoTranslateSucceeds()
    {
      var a = new A() { Value = "a", Value4 = "underlying value4 for a"};

      using (var connection = Effort.DbConnectionFactory.CreateTransient())
      {
        using (var context = new DemoContext(connection))
        {
          context.As.Add(a);
          context.SaveChanges();
        }

        using (var context = new DemoContext(connection))
        {
          var qry = from m in context.As
                    select m.Value4;
          Assert.Multiple(() =>
          {
            Assert.AreEqual(a.Value4, qry.WithTranslations().First(), "A override wrong");
          });
        }
      }
    }

    [Test]
    public void LinqTranslations_CalculatedAttribute_InDirectCircularDependencyUsingNoTranslateSucceeds()
    {
      var a = new A() { Value = "a", Value6 = "underlying value6 for a" };

      using (var connection = Effort.DbConnectionFactory.CreateTransient())
      {
        using (var context = new DemoContext(connection))
        {
          context.As.Add(a);
          context.SaveChanges();
        }

        using (var context = new DemoContext(connection))
        {
          var qry = from m in context.As
                    select m.Value6;
          Assert.Multiple(() =>
          {
            Assert.AreEqual(a.Value6, qry.WithTranslations().First(), "A override wrong");
          });
        }
      }
    }

  }
}
