namespace Timberborn.InputSystem;

public interface IInputProcessor {
  bool ProcessInput();
}

public class InputService {
  public IInputProcessor RegisteredProcessor { get; private set; }

  public string DownKeyId { get; set; }

  public string HeldKeyId { get; set; }

  public string LongHeldKeyId { get; set; }

  public void AddInputProcessor(IInputProcessor inputProcessor) {
    RegisteredProcessor = inputProcessor;
  }

  public bool IsKeyDown(string keyId) {
    return DownKeyId == keyId;
  }

  public bool IsKeyHeld(string keyId) {
    return HeldKeyId == keyId;
  }

  public bool IsKeyLongHeld(string keyId) {
    return LongHeldKeyId == keyId;
  }
}
