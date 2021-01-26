using Effort.DataLoaders;
using NUnit.Framework;
using System;
using System.ComponentModel.DataAnnotations;
using System.Data.Common;
using System.Data.Entity;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Microsoft.Linq.Translations.Tests
{
  /// <summary>
  ///  Microsoft.Linq.Translations is a third party linrary we use to simplify the definition of Linq-to-entities friendly
  ///  calculated fields in entities, we have made some minor enhancements to the library these tests test those enhancements
  /// </summary>
  [TestFixture]
  public class MoreOverrideTests
  {

    public class A
    {
      [Key] public Int32 PK { get; set; }
      public virtual String Value { get; set; }
      public virtual String Calc => this.Calc2;
      private static CompiledExpression<A, String> expCalc =
        DefaultTranslationOf<A>.Property(p => p.Calc)
          .Is(o => o.Calc2);
      public virtual String Calc2 => "a second calculated";
      private static CompiledExpression<A, String> expCalc2 =
        DefaultTranslationOf<A>.Property(p => p.Calc2)
          .Is(o => "a second calculated");

    }
    public class B : A { }
    public class C : A
    {
      public override String Calc => "c calculated";
      private static CompiledExpression<C, String> expCalc =
        DefaultTranslationOf<C>.Property(p => p.Calc)
          .Is(o => "c calculated");
    }
    public class D : B { }
    public class E : C
    {
      public override String Calc => "e calculated";
      private static CompiledExpression<E, String> expCalc =
        DefaultTranslationOf<E>.Property(p => p.Calc)
          .Is(o => "e calculated");
    }
    public class F : E { }
    public class G : F { }

    public class DemoContext : DbContext
    {
      public DemoContext(DbConnection connection)
        : base(connection, true)
      {
      }

      public virtual DbSet<A> As { get; set; }
      public virtual DbSet<B> Bs { get; set; }
      public virtual DbSet<C> Cs { get; set; }
      public virtual DbSet<D> Ds { get; set; }
      public virtual DbSet<E> Es { get; set; }
      public virtual DbSet<F> Fs { get; set; }
      public virtual DbSet<G> Gs { get; set; }
    }

    [Test]
    public void LinqTranslations_CalculatedAttribute_OverrideBehaviourRuntimeType()
    {
      var g = new G() { Value = "g" };
      var f = new F() { Value = "f" };
      var e = new E() { Value = "e" };
      var d = new D() { Value = "d" };
      var c = new C() { Value = "c" };
      var b = new B() { Value = "b" };
      var a = new A() { Value = "a" };

      using (var connection = Effort.DbConnectionFactory.CreateTransient())
      {
        using (var context = new DemoContext(connection))
        {
          context.As.Add(a);
          context.Bs.Add(b);
          context.Cs.Add(c);
          context.Ds.Add(d);
          context.Es.Add(e);
          context.Fs.Add(f);
          context.Gs.Add(g);
          context.SaveChanges();
        }

        using (var context = new DemoContext(connection))
        {
          var qry = from m in context.As
                    orderby m.Value
                    select m.Calc;
          var items = qry.WithTranslations().ToArray();
          Assert.Multiple(() =>
          {
            Assert.AreEqual(a.Calc, items[0], "A override wrong");
            Assert.AreEqual(b.Calc, items[1], "B override wrong");
            Assert.AreEqual(c.Calc, items[2], "C override wrong");
            Assert.AreEqual(d.Calc, items[3], "D override wrong");
            Assert.AreEqual(e.Calc, items[4], "E override wrong");
            Assert.AreEqual(f.Calc, items[5], "F override wrong");
            Assert.AreEqual(g.Calc, items[6], "G override wrong");
          });

        }

      }
    }

    [Test]
    public void LinqTranslations_CalculatedAttribute_OverrideBehaviourRuntimeSubTypeInitialisation()
    {
      // ensure that even when none of the types are explictly initialised the
      // behaviour still works using the implicit initialisation
      var path = Assembly.GetExecutingAssembly().Location;
      path = path.Substring(0, path.IndexOf("bin"));
      TestContext.Out.WriteLine(path);
      using (var connection = Effort.DbConnectionFactory.CreatePersistent("db", new CsvDataLoader(path)))
      {
        using (var context = new DemoContext(connection))
        {
          var qry = from m in context.As
                    orderby m.Value
                    select m.Calc;
          var items = qry.WithTranslations().ToArray();
          Assert.Multiple(() =>
          {
            Assert.AreEqual("a second calculated", items[0], "A override wrong");
            Assert.AreEqual("a second calculated", items[1], "B override wrong");
            Assert.AreEqual("c calculated", items[2], "C override wrong");
            Assert.AreEqual("a second calculated", items[3], "D override wrong");
            Assert.AreEqual("e calculated", items[4], "G override wrong");
          });

        }

      }
    }
  }

}
