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
  public class BonusOverrideTests
  {
    public enum EnumType
    {
      TypeB,
      TypeC
    }

    public class A
    {
      [Key] public Int32 PK { get; set; }
      public virtual String Value { get; set; }
      public virtual String Value2 { get; set; }

      //private static CompiledExpression<A, String> expValue2 =
      //  DefaultTranslationOf<A>.Property(p => p.Value2)
      //    .Is(o => o.Value);

    }

    public class B : A
    {
    }

    public class C : A
    {
      public override String Value2
      {
        get
        {
          //return expValue2.Evaluate(this);
          switch (this.Type)
          {
            case EnumType.TypeC:
              return this.Value4;
            default:
              return base.Value2;
          }
        }
        set
        {
          switch (this.Type)
          {
            //case EnumType.TypeB:
            //  return;
            default:
              base.Value2 = value;
              return;
          }
        }
      }

      private static CompiledExpression<C, String> expValue2 =
        DefaultTranslationOf<C>.Property(p => p.Value2)
          .Is(o => o.Type == EnumType.TypeC ? o.Value4 : o.Value2.NoTranslate() );

      public virtual String Value4
      {
        get => expValue4.Evaluate(this);
      }
      private static CompiledExpression<C, String> expValue4 =
        DefaultTranslationOf<C>.Property(p => p.Value4)
          .Is(o => "c value 4 calculated");
      public EnumType Type { get; set; }
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
    public void LinqTranslations_CalculatedAttribute_NoCompiledExpressionOnAbstractBaseClassOverrideWorks()
    {
      var b = new C() { Value = "b", Value2 = "b", Type = EnumType.TypeB };
      var c = new C() { Value = "c", Value2 = "c", Type = EnumType.TypeC };

      using (var connection = Effort.DbConnectionFactory.CreateTransient())
      {
        using (var context = new DemoContext(connection))
        {
          context.As.Add(b);
          context.As.Add(c);
          context.SaveChanges();
        }

        using (var context = new DemoContext(connection))
        {
          var qry = from m in context.As
                    orderby m.Value
                    select  m.Value2;
          var items = qry.WithTranslations().ToArray();
          Assert.Multiple(() =>
          {
            Assert.AreEqual(b.Value2, items[0], "B override wrong");
            Assert.AreEqual(c.Value2, items[1], "C override wrong");
          });
        }
      }
    }

  }
}
