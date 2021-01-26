using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Linq.Translations.Tests
{
  [TestFixture]
  public class TypeSortingTests
  {
    [Test]
    public void TypeSort_Test()
    {
      Type[] types = { typeof(F), typeof(D), typeof(A), typeof(C), typeof(E), typeof(B) };

      Array.Sort(types, (x, y) => x == y ? 0 : (x.IsAssignableFrom(y) ? -1 : (y.IsAssignableFrom(x) ? 1 : -1)));

      Assert.Multiple(() =>
      {
        Assert.AreEqual(typeof(F), types[5]);
        Assert.AreEqual(typeof(D), types[4]);
        Assert.AreEqual(typeof(E), types[3]);
        Assert.AreEqual(typeof(C), types[2]);
        Assert.AreEqual(typeof(B), types[1]);
        Assert.AreEqual(typeof(A), types[0]);
      });
    }
  }

  public class A { }
  public class B : A { }
  public class C : A { }
  public class D : B { }
  public class E : C { }
  public class F : E { }
}
