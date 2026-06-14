namespace Timberborn.DuplicationSystem;

public interface IDuplicable<T> {
  void DuplicateFrom(T source);
}
