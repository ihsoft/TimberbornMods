namespace Timberborn.InputSystem;

interface IInputProcessor {
  bool ProcessInput();
}

class InputService {
  public IInputProcessor RegisteredProcessor { get; private set; }

  public string HeldKeyId { get; set; }

  public void AddInputProcessor(IInputProcessor inputProcessor) {
    RegisteredProcessor = inputProcessor;
  }

  public bool IsKeyHeld(string keyId) {
    return HeldKeyId == keyId;
  }
}
