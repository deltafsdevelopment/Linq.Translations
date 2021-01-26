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
  public class EvenMoreOverrideTests
  {

    public class A
    {
      [Key] public Int32 PK { get; set; }
      public virtual String Value { get; set; }
      public virtual String Calc => this is C ? "c calculated" : this is B ? "b calculated" : this.Calc2;
      private static CompiledExpression<A, String> expCalc =
        DefaultTranslationOf<A>.Property(p => p.Calc)
          .Is(o => o is C ? "c calculated" : o is B ? "b calculated" : o.Calc2);
      public virtual String Calc2 => "a second calculated";
      private static CompiledExpression<A, String> expCalc2 =
        DefaultTranslationOf<A>.Property(p => p.Calc2)
          .Is(o => "a second calculated");
    }
    public class B : A
    {
    }
    public class C : A
    {
    }

    public class DemoContext : DbContext
    {
      public DemoContext(DbConnection connection)
        : base(connection, true)
      {
      }

      public virtual DbSet<A> As { get; set; }
      public virtual DbSet<B> Bs { get; set; }
      public virtual DbSet<C> Cs { get; set; }

    }

    [Test]
    public void LinqTranslations_CalculatedAttribute_DbIsOfExpressionOnBaseClass()
    {
      var a = new A() { Value = "a" };
      var b = new B() { Value = "b" };
      var c = new C() { Value = "c" };

      using (var connection = Effort.DbConnectionFactory.CreateTransient())
      {
        using (var context = new DemoContext(connection))
        {
          context.As.Add(a);
          context.Bs.Add(b);
          context.Cs.Add(c);
          context.SaveChanges();
        }

        using (var context = new DemoContext(connection))
        {
          var qry = from m in context.Cs
                    orderby m.Value
                    select m.Calc;
          var items = qry.WithTranslations().ToArray();
          Assert.Multiple(() =>
          {
            Assert.AreEqual(c.Calc, items[0], "C override wrong");
          });
        }
      }
    }

  }
}
