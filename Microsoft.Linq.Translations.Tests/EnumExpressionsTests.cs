using NUnit.Framework;
using System;
using System.ComponentModel.DataAnnotations;
using System.Data.Common;
using System.Data.Entity;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Microsoft.Linq.Translations.Tests
{
  [BaseTranslations]
  public static class BaseTranslations
  {
    public static CompiledExpression<Enum, String> expCalculatedEnumAttribute =
      DefaultTranslationOf<Enum>.Property(p => p.ToString())
        .Is((o) => Formatting.GetCaptionOrName(o));

  }

  [TestFixture]
  public class EnumExpressionsTests
  {
    public enum TestEnum
    {
      NotSpecified = 0,
      Value1 = 1,
      Value2 = 2,
      Value3 = 3
    }

    public enum TestEnum2
    {
      NotSpecified = 0,
      ValueA = 1,
      [Caption("Value B Caption")]
      ValueB = 2,
      ValueC = 3
    }

    public static class TestEnumExpressions
    {
      static TestEnumExpressions()
      {
        Console.WriteLine("TestEnumExpressions ctor");
      }

      public static CompiledExpression<TestEnum, String> expCalculatedEnumAttribute =
        DefaultTranslationOf<TestEnum>.Property(p => p.ToString())
          .Is(o => o == TestEnum.Value1 ? "Override Value 1 Caption" :
                    o == TestEnum.Value2 ? "Override Value 2 Caption" :
                      o == TestEnum.Value3 ? "Override Value 3 Caption" :
                        "Not Specified");
    }

    public class TestEntity
    {
      [Key]
      public Int32 pk { get; set; }
      public TestEnum EnumAttribute { get; set; }
      public TestEnum2 Enum2Attribute { get; set; }

      public String CalculatedEnumAttribute
      {
        get
        {
          return expCalculatedEnumAttribute.Evaluate(this);
        }
      }

      private static CompiledExpression<TestEntity, String> expCalculatedEnumAttribute =
        DefaultTranslationOf<TestEntity>.Property(p => p.CalculatedEnumAttribute)
          .Is(o => o.EnumAttribute.ToString());

      public String CalculatedEnum2Attribute
      {
        get
        {
          return expCalculatedEnum2Attribute.Evaluate(this);
        }
      }

      private static CompiledExpression<TestEntity, String> expCalculatedEnum2Attribute =
        DefaultTranslationOf<TestEntity>.Property(p => p.CalculatedEnum2Attribute)
          .Is(o => o.Enum2Attribute.ToString());
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
      var demo = new TestEntity() { EnumAttribute = TestEnum.NotSpecified, Enum2Attribute = TestEnum2.NotSpecified };
      context.TestEntities.Add(demo);

      var demo1 = new TestEntity() { EnumAttribute = TestEnum.Value1, Enum2Attribute = TestEnum2.ValueA };
      context.TestEntities.Add(demo1);

      var demo2 = new TestEntity() { EnumAttribute = TestEnum.Value2, Enum2Attribute = TestEnum2.ValueB };
      context.TestEntities.Add(demo2);

      var demo3 = new TestEntity() { EnumAttribute = TestEnum.Value3, Enum2Attribute = TestEnum2.ValueC };
      context.TestEntities.Add(demo3);

      context.SaveChanges();
    }

    [Test]
    public void LinqTranslations_CalculatedEnumAttribute_CustomEnumFormattingSuccess()
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
                    orderby b.EnumAttribute 
                    select new
                    {
                      EnumAttribute = b.EnumAttribute,
                      CalculatedEnumAttribute = b.CalculatedEnumAttribute
                    };
          var res = qry.WithTranslations().ToArray();
          Assert.IsNotNull(res);
          Assert.AreEqual(4, res.Length);
          Assert.AreEqual("Not Specified", res[0].CalculatedEnumAttribute);
          Assert.AreEqual("Override Value 1 Caption", res[1].CalculatedEnumAttribute);
          Assert.AreEqual("Override Value 2 Caption", res[2].CalculatedEnumAttribute);
          Assert.AreEqual("Override Value 3 Caption", res[3].CalculatedEnumAttribute);

          foreach(var r in res)
          {
            Console.WriteLine("{0} - {1}", r.EnumAttribute, r.CalculatedEnumAttribute);
          }
        }
      }
    }

    [Test]
    public void LinqTranslations_CalculatedEnum2Attribute_BaseMappingEnumFormattingSuccess()
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
                    orderby b.EnumAttribute 
                    select new
                    {
                      EnumAttribute = b.EnumAttribute,
                      CalculatedEnumAttribute = b.CalculatedEnum2Attribute
                    };
          var res = qry.WithTranslations().ToArray();
          Assert.IsNotNull(res);
          Assert.AreEqual(4, res.Length);
          Assert.AreEqual("Not Specified", res[0].CalculatedEnumAttribute);
          Assert.AreEqual("Value A", res[1].CalculatedEnumAttribute);
          Assert.AreEqual("Value B Caption", res[2].CalculatedEnumAttribute);
          Assert.AreEqual("Value C", res[3].CalculatedEnumAttribute);

          foreach(var r in res)
          {
            Console.WriteLine("{0} - {1}", r.EnumAttribute, r.CalculatedEnumAttribute);
          }
        }
      }
    }


    [Test]
    public void LinqTranslations_CalculatedEnum2Attribute_BaseMappingEnumFormattingInL2OSuccess()
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
                    orderby b.EnumAttribute
                    select b;
          var res = qry.ToArray();
          Assert.IsNotNull(res);
          Assert.AreEqual(4, res.Length);
          Assert.AreEqual("Not Specified", res[0].CalculatedEnum2Attribute);
          Assert.AreEqual("Value A", res[1].CalculatedEnum2Attribute);
          Assert.AreEqual("Value B Caption", res[2].CalculatedEnum2Attribute);
          Assert.AreEqual("Value C", res[3].CalculatedEnum2Attribute);

          foreach (var r in res)
          {
            Console.WriteLine("{0} - {1}", r.EnumAttribute, r.CalculatedEnumAttribute);
          }
        }
      }
    }

  }

  /// <summary>
  ///  Provide a user readable caption for the object this attribute is applied to
  /// </summary>
  /// <remarks>
  ///  Similar to System.ComponentModel.DataAnnotations.DisplayAttribute but can also target Enum members
  /// </remarks>
  public class CaptionAttribute : Attribute
  {
    public String Caption { get; set; }

    public CaptionAttribute()
    {
    }
    public CaptionAttribute(String caption)
    {
      Caption = caption;
    }
  }

  public static class Formatting
  { 
    /// <summary>
    ///  Return the friendly name for an enum member
    /// </summary>
    /// <remarks>If the prop or enum member has a CaptionAttribute provided the value from there will be used
    ///  otherwise our default SpaceOutText on the prop or enum member name will apply</remarks>
    public static String GetCaptionOrName(Object value)
    {
      String caption = GetCaption(value);
      if (caption == null && value != null)
      {
        caption = SpaceOutText(value.ToString());
      }
      return caption;
    }

    /// <summary>
    ///  Return the value of the caption attribute (if it exists) for an enum member
    /// </summary>
    public static String GetCaption(Object value)
    {
      String caption = null;
      CaptionAttribute attr;
      if (value != null)
      {
        Type type = value.GetType();
        MemberInfo[] memInfo = type.GetMember(value.ToString());
        if (memInfo == null || memInfo.Length == 0)
        {
          throw new Exception(String.Format("Get Caption: Cannot find member called {0} on given type {1}", value.ToString(), type.Name));
        }
        attr = memInfo[0].GetCustomAttributes<CaptionAttribute>().FirstOrDefault();
        if (attr != null)
        {
          caption = attr.Caption;
        }
      }
      return caption;
    }

    public static String SpaceOutText(this String value)
    {
      if ((value != null))
      {
        value = Regex.Replace(value, "(_)", " - ", RegexOptions.Compiled);               // Turn underscore into dash
        value = Regex.Replace(value, "([A-Z]+s)([A-Z][a-z])", "$1 $2", RegexOptions.Compiled);
        value = Regex.Replace(value, "([^ ^|])([A-Z][a-z])", "$1 $2", RegexOptions.Compiled);
        value = Regex.Replace(value, "([a-z])([A-Z])", "$1 $2", RegexOptions.Compiled);
        value = Regex.Replace(value, "([a-z])([0-9])", "$1 $2", RegexOptions.Compiled);  // Space out numbers
        value = Regex.Replace(value, "([0-9])([a-z])", "$1 $2", RegexOptions.Compiled);  // Space out numbers
        value = Regex.Replace(value, "BC E", "BCE", RegexOptions.Compiled);
        value = Regex.Replace(value, "PI P", "PIP", RegexOptions.Compiled);
        value = Regex.Replace(value, "CP I", "CPI", RegexOptions.Compiled);
        value = Regex.Replace(value, "AW E", "AWE", RegexOptions.Compiled);
        value = Regex.Replace(value, "PS D", "PSD", RegexOptions.Compiled);
        value = Regex.Replace(value, "EY U", "EYU", RegexOptions.Compiled);
        value = value.Trim();
      }
      return value;
    }
  }
}
