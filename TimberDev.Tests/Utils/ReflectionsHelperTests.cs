using System;
using IgorZ.TimberDev.Utils;

namespace TimberDev.Tests;

static class ReflectionsHelperTests {
  public static void GetsCompatibleTypeAndCreatesInstance() {
    var typeId = typeof(DerivedReflectionType).FullName;

    Assert.Equal(typeof(DerivedReflectionType), ReflectionsHelper.GetType(typeId, typeof(BaseReflectionType)));
    Assert.True(ReflectionsHelper.MakeInstance<BaseReflectionType>(typeId) is DerivedReflectionType);
  }

  public static void HandlesMissingAndInvalidTypes() {
    Assert.Equal(null, ReflectionsHelper.GetType("Missing.Type", throwOnError: false));
    Assert.Throws<InvalidOperationException>(
        () => ReflectionsHelper.GetType(typeof(NoDefaultConstructorReflectionType).FullName));
    Assert.Throws<InvalidOperationException>(
        () => ReflectionsHelper.GetType(typeof(UnrelatedReflectionType).FullName, typeof(BaseReflectionType)));
    Assert.Throws<ArgumentNullException>(() => ReflectionsHelper.GetType(""));
  }

  class BaseReflectionType {
  }

  sealed class DerivedReflectionType : BaseReflectionType {
  }

  sealed class NoDefaultConstructorReflectionType : BaseReflectionType {
    public NoDefaultConstructorReflectionType(int value) {
    }
  }

  sealed class UnrelatedReflectionType {
  }
}
