namespace MCP.Services;

public enum AccessMode
{
    ReadOnly,
    Full
}

public interface IAccessModeProvider
{
    AccessMode GetAccessMode();
}
