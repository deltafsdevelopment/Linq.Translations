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
  [TestFixture]
  public class MethodTests
  {
    public class TestEntity
    {
      [Key]
      public Int32 pk { get; set; }
      public String NormalAttribute { get; set; }
      public String NormalAttribute2 { get; set; }
      public String NormalAttribute3 { get; set; }


      public String CalculatedAttribute
      {
        get
        {
          return expCalculatedAttribute.Evaluate(this);
        }
      }

      private static CompiledExpression<TestEntity, String> expCalculatedAttribute =
        DefaultTranslationOf<TestEntity>.Property(p => p.CalculatedAttribute)
          .Is(o => o.NormalAttribute);

      public String CalculatedMethodCallingNormalAttribute()
      {
        return expCalculatedMethodCallingNormalAttribute.Evaluate(this);
      }

      private static CompiledExpression<TestEntity, String> expCalculatedMethodCallingNormalAttribute =
        DefaultTranslationOf<TestEntity>.Property(p => p.CalculatedMethodCallingNormalAttribute())
          .Is(o => o.NormalAttribute);

      public String CalculatedMethodCallingCalculatedAttribute()
      {
        return expCalculatedMethodCallingCalculatedAttribute.Evaluate(this);
      }

      private static CompiledExpression<TestEntity, String> expCalculatedMethodCallingCalculatedAttribute =
        DefaultTranslationOf<TestEntity>.Property(p => p.CalculatedMethodCallingCalculatedAttribute())
          .Is(o => o.CalculatedMethodCallingNormalAttribute());


    }

    public class TestContext : DbContext
    {
      public TestContext(DbConnection connection)
        : base(connection, true)
      {
      }

      public virtual DbSet<TestEntity> TestEntities { get; set; }
    }

    private void SeedContext(TestContext context)
    {
      //seed some data in
      var demo = new TestEntity() { pk = 0, NormalAttribute = "TEST0", NormalAttribute2 = "Attr0", NormalAttribute3="AttrA" };
      context.TestEntities.Add(demo);

      var demo1 = new TestEntity() { pk = 1, NormalAttribute = "TEST1", NormalAttribute2 = "Attr1", NormalAttribute3 = "AttrB" };
      context.TestEntities.Add(demo1);

      var demo2 = new TestEntity() { pk = 2, NormalAttribute = "TEST2", NormalAttribute2 = "Attr2", NormalAttribute3 = "AttrC" };
      context.TestEntities.Add(demo2);

      var demo3 = new TestEntity() { pk = 3, NormalAttribute = "TEST3", NormalAttribute2 = "Attr3", NormalAttribute3 = "AttrD" }; ;
      context.TestEntities.Add(demo3);

      context.SaveChanges();
    }


    [Test]
    public void LinqTranslations_CalculatedMethod_CallingNormalAttributeSuccess()
    {
      using (var connection = Effort.DbConnectionFactory.CreateTransient())
      {
        using (var context = new TestContext(connection))
        {
          SeedContext(context);
        }

        using (var context = new TestContext(connection))
        {
          var qry = from b in context.TestEntities 
                    select new { NormalAttribute = b.NormalAttribute, 
                                 CalculatedMethodCallingNormalAttribute = b.CalculatedMethodCallingNormalAttribute() };
          var res = qry.WithTranslations().ToList();
          Assert.IsNotNull(res);
          Assert.AreEqual(4, res.Count());
          Assert.IsTrue(res.All(r => r.CalculatedMethodCallingNormalAttribute == r.NormalAttribute));
        }
      }

    }

    [Test]
    public void LinqTranslations_CalculatedMethod_CallingCalculatedAttributeSuccess()
    {
      using (var connection = Effort.DbConnectionFactory.CreateTransient())
      {
        using (var context = new TestContext(connection))
        {
          SeedContext(context);
        }

        using (var context = new TestContext(connection))
        {
          var qry = from b in context.TestEntities
                    select new
                    {
                      NormalAttribute = b.NormalAttribute,
                      CalculatedMethodCallingCalculatedAttribute = b.CalculatedMethodCallingCalculatedAttribute()
                    };
          var res = qry.WithTranslations().ToList();
          Assert.IsNotNull(res);
          Assert.AreEqual(4, res.Count());
          Assert.IsTrue(res.All(r => r.CalculatedMethodCallingCalculatedAttribute == r.NormalAttribute));
        }
      }

    }

    [Test]
    public void LinqTranslations_StringJoin_Success()
    {
      using (var connection = Effort.DbConnectionFactory.CreateTransient())
      {
        using (var context = new TestContext(connection))
        {
          SeedContext(context);
        }

        using (var context = new TestContext(connection))
        {
          var qry = from t in context.TestEntities
                    orderby t.pk
                    select new {
                      Concat = String.Join(" - ", t.NormalAttribute, t.NormalAttribute2, t.NormalAttribute3, "Constant")
                    };
          var res = qry.WithTranslations().ToArray();
          Assert.IsNotNull(res);
          Assert.AreEqual(4, res.Length);
          Assert.AreEqual("TEST0 - Attr0 - AttrA - Constant", res[0].Concat);
          Assert.AreEqual("TEST1 - Attr1 - AttrB - Constant", res[1].Concat);
          Assert.AreEqual("TEST2 - Attr2 - AttrC - Constant", res[2].Concat);
          Assert.AreEqual("TEST3 - Attr3 - AttrD - Constant", res[3].Concat);
        }
      }

    }

    [Test]
    public void LinqTranslations_QueryableJoin_Success()
    {
      using (var connection = Effort.DbConnectionFactory.CreateTransient())
      {
        using (var context = new TestContext(connection))
        {
          SeedContext(context);
        }

        using (var context = new TestContext(connection))
        {
          var subquery = context.TestEntities.Where(t => t.pk >= 2);
          var qry = from t in context.TestEntities
                    join s in subquery
                    on t.pk equals s.pk
                    select new { t.pk, pk2 = s.pk };
          var res = qry.WithTranslations().ToArray();
          Assert.IsNotNull(res);
        }
      }

    }


    [Test]
    public void LinqTranslations_StringJoin_FirstParameterMustBeConstant()
    {
      using (var connection = Effort.DbConnectionFactory.CreateTransient())
      {
        using (var context = new TestContext(connection))
        {
          SeedContext(context);
        }

        using (var context = new TestContext(connection))
        {
          var qry = from t in context.TestEntities
                    orderby t.pk
                    select new
                    {
                      Concat = String.Join(t.NormalAttribute, t.NormalAttribute2, t.NormalAttribute3, "Constant")
                    };
          Assert.Throws<NotSupportedException>(() => qry.WithTranslations().ToArray());
        }
      }

    }

  }
}
