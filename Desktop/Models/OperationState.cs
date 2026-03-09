namespace Artemis.Desktop.Models;

///<summary>
/// Represents the current state of an encryption or decryption operation.
/// </summary>

public enum OperationState
{
    Idle,
    Processing,
    Saving,
    Completed,
    Faulted
}