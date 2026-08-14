namespace Timberborn.InputSystem;

interface IInputProcessor {
  bool ProcessInput();
}

class InputService {
  public IInputProcessor RegisteredProcessor { get; private set; }

  public string HeldKeyId { get; set; }
  public string DownKeyId { get; set; }
  public string UpKeyId { get; set; }
  public string ShortHeldKeyId { get; set; }

  public void AddInputProcessor(IInputProcessor inputProcessor) {
    RegisteredProcessor = inputProcessor;
  }

  public bool IsKeyHeld(string keyId) {
    return HeldKeyId == keyId;
  }

  public bool IsKeyDown(string keyId) {
    return DownKeyId == keyId;
  }

  public bool IsKeyUp(string keyId) {
    return UpKeyId == keyId;
  }

  public bool IsKeyUpAfterShortHeld(string keyId) {
    return ShortHeldKeyId == keyId;
  }
}
